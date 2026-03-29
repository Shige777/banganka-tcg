using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Banganka.Core.Battle;
using Banganka.Core.Config;
using Banganka.Core.Data;
using Banganka.UI.Tutorial;

namespace Banganka.Game
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public BattleEngine BattleEngine { get; private set; }
        public SimpleAI BotAI { get; private set; }
        public GameScreen CurrentScreen { get; private set; }
        public BotDifficulty SelectedDifficulty { get; set; } = BotDifficulty.Normal;

        public enum GameScreen
        {
            Home,
            Battle,
            Cards,
            Story,
            Shop,
            Match
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            EnsurePlayerInitialized();
            CheckTutorial();
        }

        void CheckTutorial()
        {
            if (PlayerData.Instance.tutorialCompleted) return;

            var tc = Object.FindFirstObjectByType<TutorialController>();
            if (tc != null)
            {
                tc.OnTutorialCompleted += () => Debug.Log("[GameManager] Tutorial completed");
                tc.OnTutorialSkipped += () => Debug.Log("[GameManager] Tutorial skipped");
                tc.StartTutorial();
            }
            else
            {
                Debug.Log("[GameManager] TutorialController not found — skipping tutorial");
            }
        }

        /// <summary>
        /// 新規プレイヤーの場合、初期カードコレクションとデフォルトデッキを付与する。
        /// </summary>
        void EnsurePlayerInitialized()
        {
            var pd = PlayerData.Instance;
            if (pd.cardCollection.Count > 0) return;

            Debug.Log("[GameManager] New player detected — initializing starter collection");
            pd.InitializeStarterCollection();

            // Create default decks from all preset decks
            foreach (var kv in CardDatabase.PresetDecks)
            {
                var preset = kv.Value;
                pd.decks.Add(new DeckData
                {
                    deckId = kv.Key,
                    name = preset.name,
                    leaderId = preset.leaderId,
                    cardIds = new List<string>(preset.cardIds),
                    isPreset = true,
                });
            }

            // Select first deck
            if (pd.decks.Count > 0)
                pd.selectedDeckId = pd.decks[0].deckId;

            PlayerData.Save();
            Debug.Log($"[GameManager] Starter collection: {pd.cardCollection.Count} unique cards, {pd.decks.Count} decks");
        }

        /// <summary>
        /// プレイヤーの選択デッキでAI対戦を開始する。
        /// </summary>
        public void StartNewMatch()
        {
            EnsurePlayerInitialized();
            var pd = PlayerData.Instance;

            // Player deck
            var playerDeck = GetPlayerDeck(pd);
            var playerLeader = CardDatabase.GetLeader(GetPlayerLeaderId(pd));

            // Bot deck — random preset deck (different from player's if possible)
            var botDeckEntry = PickBotDeck(pd.selectedDeckId);
            var botDeck = CardDatabase.BuildDeck(botDeckEntry.cardIds);
            var botLeader = CardDatabase.GetLeader(botDeckEntry.leaderId);

            BattleEngine = new BattleEngine();
            BattleEngine.InitMatch(playerLeader, botLeader, playerDeck, botDeck, MatchModeConfig.CurrentMode);

            BotAI = new SimpleAI(BattleEngine, PlayerSide.Player2, SelectedDifficulty);

            SetScreen(GameScreen.Match);
        }

        List<CardData> GetPlayerDeck(PlayerData pd)
        {
            // Try selected deck
            var selected = pd.decks.Find(d => d.deckId == pd.selectedDeckId);
            if (selected != null && selected.IsValid)
                return CardDatabase.BuildDeck(selected.cardIds);

            // Try any valid deck
            var any = pd.decks.Find(d => d.IsValid);
            if (any != null)
                return CardDatabase.BuildDeck(any.cardIds);

            // Fallback to starter deck
            return CardDatabase.BuildDeck(CardDatabase.StarterDeckIds);
        }

        string GetPlayerLeaderId(PlayerData pd)
        {
            var selected = pd.decks.Find(d => d.deckId == pd.selectedDeckId);
            if (selected != null && !string.IsNullOrEmpty(selected.leaderId))
                return selected.leaderId;
            return CardDatabase.DefaultLeader.id;
        }

        PresetDeckData PickBotDeck(string avoidDeckId)
        {
            var presets = CardDatabase.PresetDecks;
            // Try to pick a different deck than the player
            foreach (var kv in presets)
            {
                if (kv.Key != avoidDeckId)
                    return kv.Value;
            }
            // Fallback: any deck
            return presets.Values.First();
        }

        public void SetScreen(GameScreen screen)
        {
            CurrentScreen = screen;
        }
    }
}
