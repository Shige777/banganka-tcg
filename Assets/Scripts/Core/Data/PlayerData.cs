using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Banganka.Core.Data
{
    /// <summary>
    /// Dictionary&lt;string,int&gt; のJSON変換用ラッパー
    /// (JsonUtilityはDictionaryをシリアライズできないため)
    /// </summary>
    [Serializable]
    public class SerializableCardEntry
    {
        public string cardId;
        public int count;
    }

    /// <summary>
    /// JSON保存用の中間データ構造
    /// </summary>
    [Serializable]
    public class PlayerSaveData
    {
        public string uid;
        public string displayName;
        public bool tutorialCompleted;
        public int storyChapter;
        public int storyScene;
        public string selectedDeckId;
        public int gold;
        public int premium;
        public int totalGames;
        public int wins;
        public int losses;
        public int draws;
        public int rating;
        public int winStreak;
        public int maxWinStreak;
        public List<SerializableCardEntry> cardCollection = new();
        public List<DeckData> decks = new();
        public int dailyBotGames;
        public string lastDailyReset;
        public int battlePassLevel;
        public int battlePassXp;
        public bool battlePassPremium;
        public int loginStreak;
        public string lastLoginDate;
        public int freePackStock;
        public string lastFreePackClaimTime;
        public string equippedSleeve;
        public string equippedField;
        public string equippedFrame;
        public List<string> ownedCosmetics = new();
        public bool starterBundlePurchased;
        public List<SerializableCardEntry> leaderWins = new();
    }

    /// <summary>
    /// プレイヤーデータ (Firestore users/{uid} スキーマ準拠)
    /// ローカル永続化: Application.persistentDataPath/player_save.json
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public string uid;
        public string displayName = "果求者";
        public bool tutorialCompleted;
        public int storyChapter = 1;
        public int storyScene;
        public string selectedDeckId;

        // 通貨
        public int gold = 1000;
        public int premium; // 願晶 (有償)
        public int packTickets; // パックチケット (ドラフト報酬等)

        // 戦績
        public int totalGames;
        public int wins;
        public int losses;
        public int draws;
        public int rating = 1000;
        public int winStreak;
        public int maxWinStreak;

        // コレクション
        public Dictionary<string, int> cardCollection = new(); // cardId -> count (max 3)

        // デッキ
        public List<DeckData> decks = new();

        // ミッション進捗
        public int dailyBotGames; // Bot対戦日次カウント (上限10)
        public string lastDailyReset; // ISO date string

        // バトルパス
        public int battlePassLevel;
        public int battlePassXp;
        public bool battlePassPremium;

        // ログインボーナス
        public int loginStreak;
        public string lastLoginDate;

        // 無料パック
        public int freePackStock;
        public string lastFreePackClaimTime; // ISO 8601

        // コスメティック
        public string equippedSleeve;
        public string equippedField;
        public string equippedFrame;
        public List<string> ownedCosmetics = new();

        // IAP状態 (Firestoreが正典)
        public bool starterBundlePurchased;

        // 願主別勝利数（秘話解放条件用）
        public List<SerializableCardEntry> leaderWins = new();

        static PlayerData _instance;
        static readonly string SaveFileName = "player_save.json";

        public static PlayerData Instance
        {
            get
            {
                if (_instance == null) Load();
                return _instance;
            }
            set => _instance = value;
        }

        static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        /// <summary>
        /// ローカルJSONから読み込み。ファイルがなければ新規作成。
        /// </summary>
        public static void Load()
        {
            string path = SavePath;
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var save = JsonUtility.FromJson<PlayerSaveData>(json);
                    _instance = FromSaveData(save);
                    ValidateLoadedData(_instance);
                    Debug.Log($"[PlayerData] Loaded from {path}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PlayerData] Failed to load save: {e.Message}");
                    _instance = new PlayerData();
                }
            }
            else
            {
                _instance = new PlayerData();
                Debug.Log("[PlayerData] No save file found, created new player data");
            }
        }

        /// <summary>
        /// ローカルJSONに保存。
        /// </summary>
        public static void Save()
        {
            if (_instance == null) return;
            try
            {
                var save = _instance.ToSaveData();
                string json = JsonUtility.ToJson(save, true);
                File.WriteAllText(SavePath, json);
                Debug.Log($"[PlayerData] Saved to {SavePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerData] Failed to save: {e.Message}");
            }
        }

        /// <summary>
        /// セーブデータを削除（デバッグ/アカウント削除用）
        /// </summary>
        public static void DeleteSave()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
                Debug.Log("[PlayerData] Save file deleted");
            }
            _instance = null;
        }

        PlayerSaveData ToSaveData()
        {
            var save = new PlayerSaveData
            {
                uid = uid,
                displayName = displayName,
                tutorialCompleted = tutorialCompleted,
                storyChapter = storyChapter,
                storyScene = storyScene,
                selectedDeckId = selectedDeckId,
                gold = gold,
                premium = premium,
                totalGames = totalGames,
                wins = wins,
                losses = losses,
                draws = draws,
                rating = rating,
                winStreak = winStreak,
                maxWinStreak = maxWinStreak,
                dailyBotGames = dailyBotGames,
                lastDailyReset = lastDailyReset,
                battlePassLevel = battlePassLevel,
                battlePassXp = battlePassXp,
                battlePassPremium = battlePassPremium,
                loginStreak = loginStreak,
                lastLoginDate = lastLoginDate,
                freePackStock = freePackStock,
                lastFreePackClaimTime = lastFreePackClaimTime,
                equippedSleeve = equippedSleeve,
                equippedField = equippedField,
                equippedFrame = equippedFrame,
                ownedCosmetics = new List<string>(ownedCosmetics),
                starterBundlePurchased = starterBundlePurchased,
                leaderWins = new List<SerializableCardEntry>(leaderWins),
                decks = decks
            };

            foreach (var kv in cardCollection)
                save.cardCollection.Add(new SerializableCardEntry { cardId = kv.Key, count = kv.Value });

            return save;
        }

        static PlayerData FromSaveData(PlayerSaveData save)
        {
            var player = new PlayerData
            {
                uid = save.uid,
                displayName = save.displayName,
                tutorialCompleted = save.tutorialCompleted,
                storyChapter = save.storyChapter,
                storyScene = save.storyScene,
                selectedDeckId = save.selectedDeckId,
                gold = save.gold,
                premium = save.premium,
                totalGames = save.totalGames,
                wins = save.wins,
                losses = save.losses,
                draws = save.draws,
                rating = save.rating,
                winStreak = save.winStreak,
                maxWinStreak = save.maxWinStreak,
                dailyBotGames = save.dailyBotGames,
                lastDailyReset = save.lastDailyReset,
                battlePassLevel = save.battlePassLevel,
                battlePassXp = save.battlePassXp,
                battlePassPremium = save.battlePassPremium,
                loginStreak = save.loginStreak,
                lastLoginDate = save.lastLoginDate,
                freePackStock = save.freePackStock,
                lastFreePackClaimTime = save.lastFreePackClaimTime,
                equippedSleeve = save.equippedSleeve,
                equippedField = save.equippedField,
                equippedFrame = save.equippedFrame,
                ownedCosmetics = save.ownedCosmetics ?? new List<string>(),
                starterBundlePurchased = save.starterBundlePurchased,
                leaderWins = save.leaderWins ?? new List<SerializableCardEntry>(),
                decks = save.decks ?? new List<DeckData>()
            };

            player.cardCollection = new Dictionary<string, int>();
            if (save.cardCollection != null)
            {
                foreach (var entry in save.cardCollection)
                    player.cardCollection[entry.cardId] = entry.count;
            }

            return player;
        }

        static void ValidateLoadedData(PlayerData data)
        {
            // Clamp numeric values to valid ranges
            data.gold = Math.Max(0, data.gold);
            data.premium = Math.Max(0, data.premium);
            data.rating = Math.Max(0, data.rating);
            data.totalGames = Math.Max(0, data.totalGames);
            data.wins = Math.Max(0, data.wins);
            data.losses = Math.Max(0, data.losses);
            data.draws = Math.Max(0, data.draws);
            data.winStreak = Math.Max(0, data.winStreak);
            data.maxWinStreak = Math.Max(0, data.maxWinStreak);
            data.storyChapter = Math.Max(1, data.storyChapter);
            data.battlePassLevel = Math.Max(0, data.battlePassLevel);
            data.battlePassXp = Math.Max(0, data.battlePassXp);
            data.dailyBotGames = Math.Clamp(data.dailyBotGames, 0, 10);
            data.freePackStock = Math.Clamp(data.freePackStock, 0, 2);

            // Ensure wins+losses+draws <= totalGames
            if (data.wins + data.losses + data.draws > data.totalGames)
                data.totalGames = data.wins + data.losses + data.draws;

            // Ensure maxWinStreak >= winStreak
            if (data.maxWinStreak < data.winStreak)
                data.maxWinStreak = data.winStreak;

            // Validate decks
            if (data.decks != null)
                data.decks.RemoveAll(d => d == null || string.IsNullOrEmpty(d.deckId));

            // Ensure cardCollection is initialized
            data.cardCollection ??= new Dictionary<string, int>();
        }

        public string RankTitle
        {
            get
            {
                if (rating >= 2000) return "交界の覇者";
                if (rating >= 1500) return "熟達の果求者";
                if (rating >= 1200) return "果求者";
                if (rating >= 1000) return "見習い果求者";
                return "新参の果求者";
            }
        }

        public void RecordWin(bool isPvP = true)
        {
            totalGames++;
            wins++;
            winStreak++;
            if (winStreak > maxWinStreak) maxWinStreak = winStreak;
            gold += isPvP ? 50 : 25;
            if (isPvP) rating += 15;
            Save();
        }

        public void RecordLoss(bool isPvP = true)
        {
            totalGames++;
            losses++;
            winStreak = 0;
            gold += isPvP ? 10 : 5;
            if (isPvP) rating = Math.Max(0, rating - 10);
            Save();
        }

        public void RecordDraw()
        {
            totalGames++;
            draws++;
            Save();
        }

        /// <summary>願主別の勝利数を取得</summary>
        public int GetLeaderWins(string leaderId)
        {
            foreach (var entry in leaderWins)
                if (entry.cardId == leaderId) return entry.count;
            return 0;
        }

        /// <summary>願主別の勝利を記録</summary>
        public void RecordLeaderWin(string leaderId)
        {
            foreach (var entry in leaderWins)
            {
                if (entry.cardId == leaderId) { entry.count++; return; }
            }
            leaderWins.Add(new SerializableCardEntry { cardId = leaderId, count = 1 });
        }

        public bool TrySpend(int goldAmount, int premiumAmount = 0)
        {
            if (gold < goldAmount || premium < premiumAmount) return false;
            gold -= goldAmount;
            premium -= premiumAmount;
            Save();
            return true;
        }

        public bool AddCard(string cardId, int count = 1)
        {
            if (!cardCollection.ContainsKey(cardId))
                cardCollection[cardId] = 0;

            int newCount = cardCollection[cardId] + count;
            if (newCount > 99) newCount = 99; // collection cap
            cardCollection[cardId] = newCount;
            Save();
            return true;
        }

        public int GetCardCount(string cardId)
        {
            return cardCollection.TryGetValue(cardId, out int count) ? count : 0;
        }

        public void InitializeStarterCollection()
        {
            // Grant all starter deck cards × 3
            foreach (var kv in CardDatabase.PresetDecks)
            {
                foreach (var cardId in kv.Value.cardIds)
                    AddCard(cardId, 3);
            }
            // InitializeStarterCollection calls AddCard which saves each time;
            // final state is already persisted
        }
    }

    [Serializable]
    public class DeckData
    {
        public string deckId;
        public string name;
        public string leaderId;
        public List<string> cardIds = new(); // 34 cards
        public bool isPreset;

        public bool IsValid => cardIds.Count == 34;
    }
}
