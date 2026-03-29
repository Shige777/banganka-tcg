using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Banganka.Core.Data
{
    /// <summary>
    /// プリセットデッキ: 願主ID + カードID一覧
    /// </summary>
    [Serializable]
    public class PresetDeckData
    {
        public string name;
        public string leaderId;
        public List<string> cardIds;
    }

    /// <summary>
    /// カードデータベース — StreamingAssets JSONからロード
    /// 正典: StreamingAssets/Cards/*.json, Leaders/*.json, Decks/*.json
    /// 将来: Firestore cardMaster → StreamingAssets ハッシュ同期
    /// </summary>
    public static class CardDatabase
    {
        static Dictionary<string, CardData> _cards;
        static Dictionary<string, LeaderData> _leaders;
        static Dictionary<string, PresetDeckData> _presetDecks;
        static LeaderData _defaultLeader;
        static List<string> _starterDeckIds;

        public static IReadOnlyDictionary<string, CardData> AllCards
        {
            get { if (_cards == null) Init(); return _cards; }
        }

        public static LeaderData DefaultLeader
        {
            get { if (_defaultLeader == null) Init(); return _defaultLeader; }
        }

        public static List<string> StarterDeckIds
        {
            get { if (_starterDeckIds == null) Init(); return _starterDeckIds; }
        }

        public static IReadOnlyDictionary<string, LeaderData> AllLeaders
        {
            get { if (_leaders == null) Init(); return _leaders; }
        }

        public static IReadOnlyDictionary<string, PresetDeckData> PresetDecks
        {
            get { if (_presetDecks == null) Init(); return _presetDecks; }
        }

        /// <summary>テスト用: カードを直接登録する</summary>
        public static void RegisterForTest(CardData card)
        {
            if (_cards == null) _cards = new Dictionary<string, CardData>();
            _cards[card.id] = card;
        }

        /// <summary>テスト用: 全データをクリアする</summary>
        public static void ClearForTest()
        {
            _cards = new Dictionary<string, CardData>();
            _leaders = new Dictionary<string, LeaderData>();
            _presetDecks = new Dictionary<string, PresetDeckData>();
            _defaultLeader = null;
            _starterDeckIds = null;
        }

        /// <summary>
        /// 全データを再読み込み（ホットリロード用）
        /// </summary>
        public static void Reload()
        {
            _cards = null;
            _leaders = null;
            _presetDecks = null;
            _defaultLeader = null;
            _starterDeckIds = null;
            Init();
        }

        static void Init()
        {
            _cards = new Dictionary<string, CardData>();
            _leaders = new Dictionary<string, LeaderData>();
            _presetDecks = new Dictionary<string, PresetDeckData>();

            LoadCards();
            LoadLeaders();
            LoadPresetDecks();
        }

        static void LoadCards()
        {
            string cardsDir = Path.Combine(Application.streamingAssetsPath, "Cards");
            if (!Directory.Exists(cardsDir))
            {
                Debug.LogError($"[CardDatabase] Cards directory not found: {cardsDir}");
                return;
            }

            foreach (var filePath in Directory.GetFiles(cardsDir, "*.json"))
            {
                string json = File.ReadAllText(filePath);
                var card = JsonUtility.FromJson<CardData>(json);
                if (card != null && !string.IsNullOrEmpty(card.id))
                {
                    _cards[card.id] = card;
                }
                else
                {
                    Debug.LogWarning($"[CardDatabase] Failed to parse card: {filePath}");
                }
            }

            Debug.Log($"[CardDatabase] Loaded {_cards.Count} cards from StreamingAssets/Cards/");
        }

        static void LoadLeaders()
        {
            string leadersDir = Path.Combine(Application.streamingAssetsPath, "Leaders");
            if (!Directory.Exists(leadersDir))
            {
                Debug.LogError($"[CardDatabase] Leaders directory not found: {leadersDir}");
                return;
            }

            foreach (var filePath in Directory.GetFiles(leadersDir, "*.json"))
            {
                string json = File.ReadAllText(filePath);
                var leader = JsonUtility.FromJson<LeaderData>(json);
                if (leader != null && !string.IsNullOrEmpty(leader.id))
                {
                    _leaders[leader.id] = leader;
                    if (leader.id == "LDR_CON_01")
                        _defaultLeader = leader;
                }
                else
                {
                    Debug.LogWarning($"[CardDatabase] Failed to parse leader: {filePath}");
                }
            }

            if (_defaultLeader == null && _leaders.Count > 0)
            {
                foreach (var leader in _leaders.Values)
                {
                    _defaultLeader = leader;
                    break;
                }
            }

            Debug.Log($"[CardDatabase] Loaded {_leaders.Count} leaders from StreamingAssets/Leaders/");
        }

        static void LoadPresetDecks()
        {
            string decksDir = Path.Combine(Application.streamingAssetsPath, "Decks");
            if (!Directory.Exists(decksDir))
            {
                Debug.LogWarning($"[CardDatabase] Decks directory not found: {decksDir}");
                _starterDeckIds = new List<string>();
                return;
            }

            foreach (var filePath in Directory.GetFiles(decksDir, "*.json"))
            {
                string json = File.ReadAllText(filePath);
                var deck = JsonUtility.FromJson<PresetDeckData>(json);
                if (deck != null && !string.IsNullOrEmpty(deck.name))
                {
                    string key = Path.GetFileNameWithoutExtension(filePath);
                    _presetDecks[key] = deck;
                }
                else
                {
                    Debug.LogWarning($"[CardDatabase] Failed to parse deck: {filePath}");
                }
            }

            // Default starter = contest_aggro
            if (_presetDecks.TryGetValue("contest_aggro", out var starterDeck))
                _starterDeckIds = starterDeck.cardIds;
            else if (_presetDecks.Count > 0)
            {
                foreach (var deck in _presetDecks.Values)
                {
                    _starterDeckIds = deck.cardIds;
                    break;
                }
            }
            else
                _starterDeckIds = new List<string>();

            Debug.Log($"[CardDatabase] Loaded {_presetDecks.Count} preset decks from StreamingAssets/Decks/");
        }

        public static List<CardData> BuildDeck(List<string> cardIds)
        {
            var deck = new List<CardData>();
            if (_cards == null) Init();
            foreach (var id in cardIds)
            {
                if (_cards.TryGetValue(id, out var card))
                    deck.Add(card);
                else
                    throw new System.InvalidOperationException($"Unknown card ID: {id}");
            }
            return deck;
        }

        public static CardData GetCard(string cardId)
        {
            if (_cards == null) Init();
            return _cards.TryGetValue(cardId, out var card) ? card : null;
        }

        public static LeaderData GetLeader(string leaderId)
        {
            if (_leaders == null) Init();
            return _leaders.TryGetValue(leaderId, out var leader) ? leader : _defaultLeader;
        }
    }
}
