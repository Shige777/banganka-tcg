using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if FIREBASE_ENABLED
using Firebase.Firestore;
using Firebase.Extensions;
#endif
using Banganka.Core.Data;

namespace Banganka.Core.Network
{
    /// <summary>
    /// Firestore データ同期サービス。
    /// FIREBASE_ENABLED 定義時は Firestore を使用、未定義時は PlayerPrefs/JSON ローカルストレージで動作。
    ///
    /// Collections:
    /// - users/{uid}/              プロフィール
    /// - users/{uid}/cards/{cardId} カード所持数
    /// - users/{uid}/decks/{deckId} デッキデータ
    /// - users/{uid}/stats/         戦績
    /// - matches/{matchId}/         対戦履歴
    /// - rooms/{roomId}/            マッチメイキング部屋
    /// - cardMaster/{cardId}/       カードマスターデータ
    /// </summary>
    public class FirestoreService : MonoBehaviour
    {
        public static FirestoreService Instance { get; private set; }

#if FIREBASE_ENABLED
        FirebaseFirestore _db;
#endif

        // ローカル保存用パス
        string LocalSavePath => Path.Combine(Application.persistentDataPath, "local_data");

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if FIREBASE_ENABLED
            _db = FirebaseFirestore.DefaultInstance;
#else
            // ローカル保存ディレクトリ確保
            if (!Directory.Exists(LocalSavePath))
                Directory.CreateDirectory(LocalSavePath);
#endif
        }

        // ============================================================
        // User Profile
        // ============================================================

        /// <summary>
        /// プレイヤーデータを保存 (users/{uid})
        /// </summary>
        public void SaveUserProfile(PlayerData data)
        {
            if (data == null || string.IsNullOrEmpty(data.uid))
            {
                Debug.LogWarning("[FirestoreService] SaveUserProfile: invalid data");
                return;
            }

#if FIREBASE_ENABLED
            var docRef = _db.Collection("users").Document(data.uid);
            var dict = new Dictionary<string, object>
            {
                { "displayName", data.displayName },
                { "tutorialCompleted", data.tutorialCompleted },
                { "storyChapter", data.storyChapter },
                { "storyScene", data.storyScene },
                { "selectedDeckId", data.selectedDeckId ?? "" },
                { "gold", data.gold },
                { "premium", data.premium },
                { "totalGames", data.totalGames },
                { "wins", data.wins },
                { "losses", data.losses },
                { "draws", data.draws },
                { "rating", data.rating },
                { "winStreak", data.winStreak },
                { "maxWinStreak", data.maxWinStreak },
                { "dailyBotGames", data.dailyBotGames },
                { "lastDailyReset", data.lastDailyReset ?? "" },
                { "battlePassLevel", data.battlePassLevel },
                { "battlePassXp", data.battlePassXp },
                { "battlePassPremium", data.battlePassPremium },
                { "loginStreak", data.loginStreak },
                { "lastLoginDate", data.lastLoginDate ?? "" },
                { "updatedAt", FieldValue.ServerTimestamp }
            };

            docRef.SetAsync(dict, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogError($"[FirestoreService] SaveUserProfile failed: {task.Exception}");
                else
                    Debug.Log("[FirestoreService] User profile saved");
            });
#else
            string json = JsonUtility.ToJson(data, true);
            string path = Path.Combine(LocalSavePath, $"profile_{data.uid}.json");
            File.WriteAllText(path, json);
            Debug.Log($"[FirestoreService] User profile saved (local): {path}");
#endif
        }

        /// <summary>
        /// プレイヤーデータを読み込み (users/{uid})
        /// </summary>
        public void LoadUserProfile(string uid, Action<PlayerData> callback)
        {
            if (string.IsNullOrEmpty(uid))
            {
                Debug.LogWarning("[FirestoreService] LoadUserProfile: uid is empty");
                callback?.Invoke(null);
                return;
            }

#if FIREBASE_ENABLED
            _db.Collection("users").Document(uid).GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || !task.Result.Exists)
                {
                    if (task.IsFaulted)
                        Debug.LogWarning($"[FirestoreService] LoadUserProfile failed: {task.Exception}");
                    callback?.Invoke(null);
                    return;
                }

                var snap = task.Result;
                var data = new PlayerData
                {
                    uid = uid,
                    displayName = snap.ContainsField("displayName") ? snap.GetValue<string>("displayName") : "果求者",
                    tutorialCompleted = snap.ContainsField("tutorialCompleted") && snap.GetValue<bool>("tutorialCompleted"),
                    storyChapter = snap.ContainsField("storyChapter") ? snap.GetValue<int>("storyChapter") : 1,
                    storyScene = snap.ContainsField("storyScene") ? snap.GetValue<int>("storyScene") : 0,
                    selectedDeckId = snap.ContainsField("selectedDeckId") ? snap.GetValue<string>("selectedDeckId") : null,
                    gold = snap.ContainsField("gold") ? snap.GetValue<int>("gold") : 1000,
                    premium = snap.ContainsField("premium") ? snap.GetValue<int>("premium") : 0,
                    totalGames = snap.ContainsField("totalGames") ? snap.GetValue<int>("totalGames") : 0,
                    wins = snap.ContainsField("wins") ? snap.GetValue<int>("wins") : 0,
                    losses = snap.ContainsField("losses") ? snap.GetValue<int>("losses") : 0,
                    draws = snap.ContainsField("draws") ? snap.GetValue<int>("draws") : 0,
                    rating = snap.ContainsField("rating") ? snap.GetValue<int>("rating") : 1000,
                    winStreak = snap.ContainsField("winStreak") ? snap.GetValue<int>("winStreak") : 0,
                    maxWinStreak = snap.ContainsField("maxWinStreak") ? snap.GetValue<int>("maxWinStreak") : 0,
                    dailyBotGames = snap.ContainsField("dailyBotGames") ? snap.GetValue<int>("dailyBotGames") : 0,
                    lastDailyReset = snap.ContainsField("lastDailyReset") ? snap.GetValue<string>("lastDailyReset") : null,
                    battlePassLevel = snap.ContainsField("battlePassLevel") ? snap.GetValue<int>("battlePassLevel") : 0,
                    battlePassXp = snap.ContainsField("battlePassXp") ? snap.GetValue<int>("battlePassXp") : 0,
                    battlePassPremium = snap.ContainsField("battlePassPremium") && snap.GetValue<bool>("battlePassPremium"),
                    loginStreak = snap.ContainsField("loginStreak") ? snap.GetValue<int>("loginStreak") : 0,
                    lastLoginDate = snap.ContainsField("lastLoginDate") ? snap.GetValue<string>("lastLoginDate") : null,
                    starterBundlePurchased = snap.ContainsField("starterBundlePurchased") && snap.GetValue<bool>("starterBundlePurchased"),
                };

                Debug.Log($"[FirestoreService] User profile loaded: {uid}");
                callback?.Invoke(data);
            });
#else
            string path = Path.Combine(LocalSavePath, $"profile_{uid}.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<PlayerData>(json);
                Debug.Log($"[FirestoreService] User profile loaded (local): {uid}");
                callback?.Invoke(data);
            }
            else
            {
                Debug.Log($"[FirestoreService] No local profile found for: {uid}");
                callback?.Invoke(null);
            }
#endif
        }

        // ============================================================
        // Decks
        // ============================================================

        /// <summary>
        /// デッキを保存 (users/{uid}/decks/{deckId})
        /// </summary>
        public void SaveDeck(string uid, DeckData deck)
        {
            if (string.IsNullOrEmpty(uid) || deck == null || string.IsNullOrEmpty(deck.deckId))
            {
                Debug.LogWarning("[FirestoreService] SaveDeck: invalid parameters");
                return;
            }

#if FIREBASE_ENABLED
            var docRef = _db.Collection("users").Document(uid)
                            .Collection("decks").Document(deck.deckId);

            var dict = new Dictionary<string, object>
            {
                { "name", deck.name ?? "" },
                { "leaderId", deck.leaderId ?? "" },
                { "cardIds", deck.cardIds ?? new List<string>() },
                { "isPreset", deck.isPreset },
                { "updatedAt", FieldValue.ServerTimestamp }
            };

            docRef.SetAsync(dict, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogError($"[FirestoreService] SaveDeck failed: {task.Exception}");
                else
                    Debug.Log($"[FirestoreService] Deck saved: {deck.deckId}");
            });
#else
            string json = JsonUtility.ToJson(deck, true);
            string path = Path.Combine(LocalSavePath, $"deck_{uid}_{deck.deckId}.json");
            File.WriteAllText(path, json);
            Debug.Log($"[FirestoreService] Deck saved (local): {deck.deckId}");
#endif
        }

        /// <summary>
        /// 全デッキを読み込み (users/{uid}/decks/)
        /// </summary>
        public void LoadDecks(string uid, Action<List<DeckData>> callback)
        {
            if (string.IsNullOrEmpty(uid))
            {
                callback?.Invoke(new List<DeckData>());
                return;
            }

#if FIREBASE_ENABLED
            _db.Collection("users").Document(uid)
               .Collection("decks")
               .GetSnapshotAsync()
               .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError($"[FirestoreService] LoadDecks failed: {task.Exception}");
                    callback?.Invoke(new List<DeckData>());
                    return;
                }

                var decks = new List<DeckData>();
                foreach (var doc in task.Result.Documents)
                {
                    var deck = new DeckData
                    {
                        deckId = doc.Id,
                        name = doc.GetValue<string>("name"),
                        leaderId = doc.GetValue<string>("leaderId"),
                        cardIds = doc.GetValue<List<string>>("cardIds") ?? new List<string>(),
                        isPreset = doc.GetValue<bool>("isPreset")
                    };
                    decks.Add(deck);
                }

                Debug.Log($"[FirestoreService] Loaded {decks.Count} decks for {uid}");
                callback?.Invoke(decks);
            });
#else
            var decks = new List<DeckData>();
            if (Directory.Exists(LocalSavePath))
            {
                string prefix = $"deck_{uid}_";
                foreach (string file in Directory.GetFiles(LocalSavePath, $"{prefix}*.json"))
                {
                    string json = File.ReadAllText(file);
                    var deck = JsonUtility.FromJson<DeckData>(json);
                    if (deck != null) decks.Add(deck);
                }
            }
            Debug.Log($"[FirestoreService] Loaded {decks.Count} decks (local) for {uid}");
            callback?.Invoke(decks);
#endif
        }

        // ============================================================
        // Card Collection
        // ============================================================

        /// <summary>
        /// カードコレクションを保存 (users/{uid}/cards/)
        /// </summary>
        public void SaveCardCollection(string uid, Dictionary<string, int> cards)
        {
            if (string.IsNullOrEmpty(uid) || cards == null)
            {
                Debug.LogWarning("[FirestoreService] SaveCardCollection: invalid parameters");
                return;
            }

#if FIREBASE_ENABLED
            // Batch write for efficiency
            var batch = _db.StartBatch();
            var cardsCollection = _db.Collection("users").Document(uid).Collection("cards");

            foreach (var kv in cards)
            {
                var docRef = cardsCollection.Document(kv.Key);
                batch.Set(docRef, new Dictionary<string, object>
                {
                    { "count", kv.Value },
                    { "updatedAt", FieldValue.ServerTimestamp }
                }, SetOptions.MergeAll);
            }

            batch.CommitAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogError($"[FirestoreService] SaveCardCollection failed: {task.Exception}");
                else
                    Debug.Log($"[FirestoreService] Card collection saved: {cards.Count} entries");
            });
#else
            // ローカル: JSON一括保存
            string json = JsonUtility.ToJson(new SerializableCardCollection(cards), true);
            string path = Path.Combine(LocalSavePath, $"cards_{uid}.json");
            File.WriteAllText(path, json);
            Debug.Log($"[FirestoreService] Card collection saved (local): {cards.Count} entries");
#endif
        }

        /// <summary>
        /// カードコレクションを読み込み (users/{uid}/cards/)
        /// </summary>
        public void LoadCardCollection(string uid, Action<Dictionary<string, int>> callback)
        {
            if (string.IsNullOrEmpty(uid))
            {
                callback?.Invoke(new Dictionary<string, int>());
                return;
            }

#if FIREBASE_ENABLED
            _db.Collection("users").Document(uid)
               .Collection("cards")
               .GetSnapshotAsync()
               .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError($"[FirestoreService] LoadCardCollection failed: {task.Exception}");
                    callback?.Invoke(new Dictionary<string, int>());
                    return;
                }

                var cards = new Dictionary<string, int>();
                foreach (var doc in task.Result.Documents)
                {
                    int count = doc.GetValue<int>("count");
                    cards[doc.Id] = count;
                }

                Debug.Log($"[FirestoreService] Card collection loaded: {cards.Count} entries");
                callback?.Invoke(cards);
            });
#else
            string path = Path.Combine(LocalSavePath, $"cards_{uid}.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var wrapper = JsonUtility.FromJson<SerializableCardCollection>(json);
                Debug.Log($"[FirestoreService] Card collection loaded (local): {wrapper?.ToDictionary().Count ?? 0} entries");
                callback?.Invoke(wrapper?.ToDictionary() ?? new Dictionary<string, int>());
            }
            else
            {
                callback?.Invoke(new Dictionary<string, int>());
            }
#endif
        }

        // ============================================================
        // Match History
        // ============================================================

        /// <summary>
        /// 対戦結果を保存 (matches/{matchId})
        /// </summary>
        public void SaveMatchResult(MatchHistoryEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.matchId))
            {
                Debug.LogWarning("[FirestoreService] SaveMatchResult: invalid entry");
                return;
            }

#if FIREBASE_ENABLED
            var docRef = _db.Collection("matches").Document(entry.matchId);
            var dict = new Dictionary<string, object>
            {
                { "player1Uid", entry.player1Uid ?? "" },
                { "player2Uid", entry.player2Uid ?? "" },
                { "winnerUid", entry.winnerUid ?? "" },
                { "reason", entry.reason ?? "" },
                { "turnCount", entry.turnCount },
                { "duration", entry.duration },
                { "startedAt", entry.startedAt },
                { "endedAt", entry.endedAt },
                { "createdAt", FieldValue.ServerTimestamp }
            };

            docRef.SetAsync(dict).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogError($"[FirestoreService] SaveMatchResult failed: {task.Exception}");
                else
                    Debug.Log($"[FirestoreService] Match result saved: {entry.matchId}");
            });
#else
            string json = JsonUtility.ToJson(entry, true);
            string path = Path.Combine(LocalSavePath, $"match_{entry.matchId}.json");
            File.WriteAllText(path, json);
            Debug.Log($"[FirestoreService] Match result saved (local): {entry.matchId}");
#endif
        }

        /// <summary>
        /// 対戦履歴を読み込み (matches/ where player1Uid or player2Uid == uid)
        /// </summary>
        public void LoadMatchHistory(string uid, int limit, Action<List<MatchHistoryEntry>> callback)
        {
            if (string.IsNullOrEmpty(uid))
            {
                callback?.Invoke(new List<MatchHistoryEntry>());
                return;
            }

#if FIREBASE_ENABLED
            // Query matches where the player participated (as player1)
            // Note: Firestore requires composite index for OR queries,
            // so we query both sides and merge
            var results = new List<MatchHistoryEntry>();
            int queriesCompleted = 0;

            void CheckComplete()
            {
                queriesCompleted++;
                if (queriesCompleted >= 2)
                {
                    // Sort by endedAt descending and limit
                    results.Sort((a, b) => b.endedAt.CompareTo(a.endedAt));
                    if (results.Count > limit)
                        results.RemoveRange(limit, results.Count - limit);

                    Debug.Log($"[FirestoreService] Match history loaded: {results.Count} entries");
                    callback?.Invoke(results);
                }
            }

            _db.Collection("matches")
               .WhereEqualTo("player1Uid", uid)
               .OrderByDescending("endedAt")
               .Limit(limit)
               .GetSnapshotAsync()
               .ContinueWithOnMainThread(task =>
            {
                if (!task.IsFaulted)
                {
                    foreach (var doc in task.Result.Documents)
                        results.Add(DocToMatchEntry(doc));
                }
                CheckComplete();
            });

            _db.Collection("matches")
               .WhereEqualTo("player2Uid", uid)
               .OrderByDescending("endedAt")
               .Limit(limit)
               .GetSnapshotAsync()
               .ContinueWithOnMainThread(task =>
            {
                if (!task.IsFaulted)
                {
                    foreach (var doc in task.Result.Documents)
                        results.Add(DocToMatchEntry(doc));
                }
                CheckComplete();
            });
#else
            var entries = new List<MatchHistoryEntry>();
            if (Directory.Exists(LocalSavePath))
            {
                foreach (string file in Directory.GetFiles(LocalSavePath, "match_*.json"))
                {
                    string json = File.ReadAllText(file);
                    var entry = JsonUtility.FromJson<MatchHistoryEntry>(json);
                    if (entry != null && (entry.player1Uid == uid || entry.player2Uid == uid))
                        entries.Add(entry);
                }
            }
            entries.Sort((a, b) => b.endedAt.CompareTo(a.endedAt));
            if (entries.Count > limit)
                entries.RemoveRange(limit, entries.Count - limit);
            Debug.Log($"[FirestoreService] Match history loaded (local): {entries.Count} entries");
            callback?.Invoke(entries);
#endif
        }

#if FIREBASE_ENABLED
        MatchHistoryEntry DocToMatchEntry(DocumentSnapshot doc)
        {
            return new MatchHistoryEntry
            {
                matchId = doc.Id,
                player1Uid = doc.GetValue<string>("player1Uid"),
                player2Uid = doc.GetValue<string>("player2Uid"),
                winnerUid = doc.GetValue<string>("winnerUid"),
                reason = doc.GetValue<string>("reason"),
                turnCount = doc.GetValue<int>("turnCount"),
                duration = (float)doc.GetValue<double>("duration"),
                startedAt = doc.GetValue<long>("startedAt"),
                endedAt = doc.GetValue<long>("endedAt")
            };
        }
#endif

        // ============================================================
        // Player Stats
        // ============================================================

        /// <summary>
        /// 対戦結果に基づいてプレイヤー戦績を更新 (users/{uid})
        /// </summary>
        public void UpdatePlayerStats(string uid, MatchResult result)
        {
            if (string.IsNullOrEmpty(uid))
            {
                Debug.LogWarning("[FirestoreService] UpdatePlayerStats: uid is empty");
                return;
            }

#if FIREBASE_ENABLED
            var docRef = _db.Collection("users").Document(uid);

            // Firestore atomic increment
            var updates = new Dictionary<string, object>
            {
                { "totalGames", FieldValue.Increment(1) },
                { "updatedAt", FieldValue.ServerTimestamp }
            };

            switch (result)
            {
                case MatchResult.Win:
                    updates["wins"] = FieldValue.Increment(1);
                    break;
                case MatchResult.Loss:
                    updates["losses"] = FieldValue.Increment(1);
                    break;
                case MatchResult.Draw:
                    updates["draws"] = FieldValue.Increment(1);
                    break;
            }

            docRef.UpdateAsync(updates).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogError($"[FirestoreService] UpdatePlayerStats failed: {task.Exception}");
                else
                    Debug.Log($"[FirestoreService] Player stats updated: {uid} -> {result}");
            });
#else
            // ローカル: PlayerData.Instance 経由で更新 (既に呼び出し側で実施される想定)
            Debug.Log($"[FirestoreService] Player stats updated (local): {uid} -> {result}");
#endif
        }

        // ============================================================
        // Card Master Sync
        // ============================================================

        /// <summary>
        /// カードマスターデータをサーバーからダウンロードし、ローカルキャッシュと同期。
        /// ハッシュ比較で差分がある場合のみ更新する。
        /// callback(true) = 更新あり, callback(false) = 最新
        /// </summary>
        public void SyncCardMaster(Action<bool> callback)
        {
#if FIREBASE_ENABLED
            // まずメタデータのハッシュを確認
            _db.Collection("meta").Document("cardMaster").GetSnapshotAsync()
               .ContinueWithOnMainThread(metaTask =>
            {
                if (metaTask.IsFaulted || !metaTask.Result.Exists)
                {
                    Debug.LogWarning("[FirestoreService] Card master meta not found, skipping sync");
                    callback?.Invoke(false);
                    return;
                }

                string serverHash = metaTask.Result.GetValue<string>("hash");
                string localHash = PlayerPrefs.GetString("cardMasterHash", "");

                if (serverHash == localHash)
                {
                    Debug.Log("[FirestoreService] Card master is up to date");
                    callback?.Invoke(false);
                    return;
                }

                // ハッシュが異なるので全カードマスターをダウンロード
                _db.Collection("cardMaster").GetSnapshotAsync()
                   .ContinueWithOnMainThread(cardTask =>
                {
                    if (cardTask.IsFaulted)
                    {
                        Debug.LogError($"[FirestoreService] Card master download failed: {cardTask.Exception}");
                        callback?.Invoke(false);
                        return;
                    }

                    // StreamingAssets相当のローカルキャッシュに保存
                    var cards = new List<Dictionary<string, object>>();
                    foreach (var doc in cardTask.Result.Documents)
                    {
                        var data = doc.ToDictionary();
                        data["id"] = doc.Id;
                        cards.Add(data);
                    }

                    string cachePath = Path.Combine(Application.persistentDataPath, "cardMaster.json");

                    // Dictionary→JSON: 簡易シリアライズ (Firebase SDK の Dictionary<string,object> を保存)
                    var sb = new System.Text.StringBuilder();
                    sb.Append("[");
                    for (int i = 0; i < cards.Count; i++)
                    {
                        if (i > 0) sb.Append(",");
                        sb.Append("{");
                        int j = 0;
                        foreach (var kv in cards[i])
                        {
                            if (j > 0) sb.Append(",");
                            sb.Append("\"").Append(EscapeJsonString(kv.Key)).Append("\":");
                            AppendJsonValue(sb, kv.Value);
                            j++;
                        }
                        sb.Append("}");
                    }
                    sb.Append("]");
                    File.WriteAllText(cachePath, sb.ToString());

                    PlayerPrefs.SetString("cardMasterHash", serverHash);
                    PlayerPrefs.Save();

                    Debug.Log($"[FirestoreService] Card master synced: {cards.Count} cards");
                    callback?.Invoke(true);
                });
            });
#else
            // ローカル: StreamingAssets から読み込み済みの想定
            Debug.Log("[FirestoreService] Card master sync (local, no-op)");
            callback?.Invoke(false);
#endif
        }

        // ============================================================
        // Helper Types
        // ============================================================

        /// <summary>
        /// Dictionary<string,int> の JsonUtility 対応ラッパー
        /// </summary>
        [Serializable]
        class SerializableCardCollection
        {
            public List<string> keys = new();
            public List<int> values = new();

            public SerializableCardCollection() { }

            public SerializableCardCollection(Dictionary<string, int> dict)
            {
                foreach (var kv in dict)
                {
                    keys.Add(kv.Key);
                    values.Add(kv.Value);
                }
            }

            public Dictionary<string, int> ToDictionary()
            {
                var dict = new Dictionary<string, int>();
                for (int i = 0; i < keys.Count && i < values.Count; i++)
                    dict[keys[i]] = values[i];
                return dict;
            }
        }

#if FIREBASE_ENABLED
        static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        static void AppendJsonValue(System.Text.StringBuilder sb, object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case int or long or float or double:
                    sb.Append(value);
                    break;
                case string s:
                    sb.Append("\"").Append(EscapeJsonString(s)).Append("\"");
                    break;
                case IList<object> list:
                    sb.Append("[");
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append(",");
                        AppendJsonValue(sb, list[i]);
                    }
                    sb.Append("]");
                    break;
                case IDictionary<string, object> dict:
                    sb.Append("{");
                    int k = 0;
                    foreach (var kv in dict)
                    {
                        if (k > 0) sb.Append(",");
                        sb.Append("\"").Append(EscapeJsonString(kv.Key)).Append("\":");
                        AppendJsonValue(sb, kv.Value);
                        k++;
                    }
                    sb.Append("}");
                    break;
                default:
                    sb.Append("\"").Append(EscapeJsonString(value.ToString())).Append("\"");
                    break;
            }
        }
#endif
    }

    // ============================================================
    // Shared Data Types
    // ============================================================

    /// <summary>
    /// 対戦結果列挙
    /// </summary>
    public enum MatchResult
    {
        Win,
        Loss,
        Draw
    }

    /// <summary>
    /// 対戦履歴エントリ (matches/{matchId} スキーマ準拠)
    /// </summary>
    [Serializable]
    public class MatchHistoryEntry
    {
        public string matchId;
        public string player1Uid;
        public string player2Uid;
        public string winnerUid;
        /// <summary>
        /// 勝利理由: "鯱鉾勝利", "塗り勝利", "降参", "タイムアウト"
        /// </summary>
        public string reason;
        public int turnCount;
        public float duration;
        public long startedAt;
        public long endedAt;
    }
}
