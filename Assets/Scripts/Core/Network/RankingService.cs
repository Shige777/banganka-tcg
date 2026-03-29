using System;
using System.Collections.Generic;
using UnityEngine;
using Banganka.Core.Config;
#if FIREBASE_ENABLED
using Firebase.Functions;
using Firebase.Extensions;
#endif

namespace Banganka.Core.Network
{
    // =========================================================================
    // ランク対戦システム (GAME_DESIGN.md §14)
    // =========================================================================

    /// <summary>ランク情報をサーバーと同期するサービス</summary>
    public class RankingService : MonoBehaviour
    {
        public static RankingService Instance { get; private set; }

        [Serializable]
        public class RankData
        {
            public int rating = 1200;
            public string rank = "bronze_5"; // e.g. "gold_3", "wish_master"
            public int stars = 0;
            public int wins = 0;
            public int losses = 0;
            public int streak = 0;
            public string seasonId = "";
            public int gamesPlayed = 0;
            public int highestRating = 1200;
        }

        [Serializable]
        public class LeaderboardEntry
        {
            public string uid;
            public int rating;
            public string rank;
            public int stars;
            public int wins;
            public int gamesPlayed;
            public string displayName;
        }

        // ----- 状態 -----
        public RankData CurrentRank { get; private set; } = new RankData();
        public List<LeaderboardEntry> Leaderboard { get; private set; } = new();

        // ----- イベント -----
        public event Action<RankData> OnRankChanged;
        public event Action<List<LeaderboardEntry>> OnLeaderboardUpdated;
        public event Action<int> OnSeasonRewardReceived;

        const string PrefKeyRankData = "Rank_CurrentData";

#if FIREBASE_ENABLED
        FirebaseFunctions _functions;
#endif

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            LoadLocal();

#if FIREBASE_ENABLED
            InitializeFirebase();
#endif
        }

#if FIREBASE_ENABLED
        void InitializeFirebase()
        {
            _functions = FirebaseFunctions.GetInstance(FirebaseConfig.FunctionsRegion);
            if (FirebaseConfig.UseEmulator)
            {
                _functions.UseFunctionsEmulator("localhost", 5001);
                Debug.Log("[RankingService] Using Functions emulator");
            }
        }
#endif

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>対戦結果をサーバーに提出してランクを更新</summary>
        public void SubmitMatchResult(
            string matchId, string player1Uid, string player2Uid, string winnerId,
            Action<bool, string> onResult)
        {
#if FIREBASE_ENABLED
            var data = new Dictionary<string, object>
            {
                { "matchId", matchId },
                { "player1Uid", player1Uid },
                { "player2Uid", player2Uid },
                { "winnerId", winnerId }, // "P1", "P2", "Draw"
            };

            CallFunction<Dictionary<string, object>>(
                "submitMatchResult", data,
                result =>
                {
                    if (result != null && result.ContainsKey("player1"))
                    {
                        // クライアント側のランクデータも同期
                        SyncRankDataFromServer();
                        Debug.Log($"[RankingService] Match result submitted: {winnerId}");
                        onResult?.Invoke(true, "Success");
                    }
                    else
                    {
                        onResult?.Invoke(false, "Invalid response");
                    }
                },
                error =>
                {
                    ErrorHandler.Instance?.HandleError(ErrorHandler.ErrorCategory.Network,
                        $"submitMatchResult failed: {error}");
                    onResult?.Invoke(false, error);
                }
            );
#else
            Debug.Log("[RankingService] submitMatchResult: local simulation");
            onResult?.Invoke(true, "LOCAL_MODE");
#endif
        }

        /// <summary>ランキングを取得</summary>
        public void FetchLeaderboard(int limit = 100, Action<bool> onComplete = null)
        {
#if FIREBASE_ENABLED
            var data = new Dictionary<string, object> { { "limit", limit } };

            CallFunction<Dictionary<string, object>>(
                "getRanking", data,
                result =>
                {
                    if (result != null && result.ContainsKey("players"))
                    {
                        ParseLeaderboard(result["players"] as List<object>);
                        OnLeaderboardUpdated?.Invoke(Leaderboard);
                        Debug.Log($"[RankingService] Fetched leaderboard: {Leaderboard.Count} entries");
                        onComplete?.Invoke(true);
                    }
                    else
                    {
                        onComplete?.Invoke(false);
                    }
                },
                error =>
                {
                    ErrorHandler.Instance?.HandleError(ErrorHandler.ErrorCategory.Network,
                        $"getRanking failed: {error}");
                    onComplete?.Invoke(false);
                }
            );
#else
            Debug.Log("[RankingService] FetchLeaderboard: local simulation");
            onComplete?.Invoke(true);
#endif
        }

        /// <summary>プレイヤーのランク情報を取得</summary>
        public void FetchPlayerRank(string uid, Action<bool, RankData> onResult)
        {
#if FIREBASE_ENABLED
            var data = new Dictionary<string, object> { { "uid", uid } };

            CallFunction<Dictionary<string, object>>(
                "getPlayerRank", data,
                result =>
                {
                    if (result != null)
                    {
                        var rankData = ParseRankData(result);
                        if (rankData != null)
                        {
                            onResult?.Invoke(true, rankData);
                            return;
                        }
                    }
                    onResult?.Invoke(false, null);
                },
                error =>
                {
                    ErrorHandler.Instance?.HandleError(ErrorHandler.ErrorCategory.Network,
                        $"getPlayerRank failed: {error}");
                    onResult?.Invoke(false, null);
                }
            );
#else
            Debug.Log("[RankingService] FetchPlayerRank: local simulation");
            onResult?.Invoke(true, CurrentRank);
#endif
        }

        /// <summary>現在のランク情報をローカルから取得</summary>
        public RankData GetCurrentRank()
        {
            return CurrentRank;
        }

        /// <summary>サーバーからランク情報を同期</summary>
        public void SyncRankDataFromServer()
        {
            FetchPlayerRank("", (success, rankData) =>
            {
                if (success && rankData != null)
                {
                    CurrentRank = rankData;
                    SaveLocal();
                    OnRankChanged?.Invoke(CurrentRank);
                    Debug.Log($"[RankingService] Synced rank: {CurrentRank.rank} ({CurrentRank.rating})");
                }
            });
        }

        // =====================================================================
        // Helper
        // =====================================================================

#if FIREBASE_ENABLED
        void CallFunction<T>(string functionName, Dictionary<string, object> data,
            Action<T> onSuccess, Action<string> onError)
        {
            var callable = _functions.GetHttpsCallable(functionName);
            var task = data != null
                ? callable.CallAsync(data)
                : callable.CallAsync();

            task.ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted)
                {
                    string errorMsg = t.Exception?.InnerException?.Message ?? "Unknown error";
                    Debug.LogError($"[RankingService] {functionName} failed: {errorMsg}");
                    onError?.Invoke(errorMsg);
                }
                else if (t.IsCanceled)
                {
                    onError?.Invoke("CANCELLED");
                }
                else
                {
                    try
                    {
                        T result = (T)t.Result.Data;
                        onSuccess?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[RankingService] {functionName} parse error: {ex.Message}");
                        onError?.Invoke($"PARSE_ERROR: {ex.Message}");
                    }
                }
            });
        }
#endif

        RankData ParseRankData(Dictionary<string, object> data)
        {
            if (data == null) return null;

            try
            {
                return new RankData
                {
                    rating = data.ContainsKey("rating") ? Convert.ToInt32(data["rating"]) : 1200,
                    rank = data.ContainsKey("rank") ? (string)data["rank"] : "bronze_5",
                    stars = data.ContainsKey("stars") ? Convert.ToInt32(data["stars"]) : 0,
                    wins = data.ContainsKey("wins") ? Convert.ToInt32(data["wins"]) : 0,
                    losses = data.ContainsKey("losses") ? Convert.ToInt32(data["losses"]) : 0,
                    streak = data.ContainsKey("streak") ? Convert.ToInt32(data["streak"]) : 0,
                    seasonId = data.ContainsKey("seasonId") ? (string)data["seasonId"] : "",
                    gamesPlayed = data.ContainsKey("gamesPlayed") ? Convert.ToInt32(data["gamesPlayed"]) : 0,
                    highestRating = data.ContainsKey("highestRating") ? Convert.ToInt32(data["highestRating"]) : 1200,
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RankingService] Failed to parse RankData: {ex.Message}");
                return null;
            }
        }

        void ParseLeaderboard(List<object> players)
        {
            Leaderboard.Clear();
            if (players == null) return;

            foreach (var item in players)
            {
                if (item is Dictionary<string, object> dict)
                {
                    try
                    {
                        var entry = new LeaderboardEntry
                        {
                            uid = dict.ContainsKey("uid") ? (string)dict["uid"] : "",
                            rating = dict.ContainsKey("rating") ? Convert.ToInt32(dict["rating"]) : 0,
                            rank = dict.ContainsKey("rank") ? (string)dict["rank"] : "",
                            stars = dict.ContainsKey("stars") ? Convert.ToInt32(dict["stars"]) : 0,
                            wins = dict.ContainsKey("wins") ? Convert.ToInt32(dict["wins"]) : 0,
                            gamesPlayed = dict.ContainsKey("gamesPlayed") ? Convert.ToInt32(dict["gamesPlayed"]) : 0,
                            displayName = dict.ContainsKey("displayName") ? (string)dict["displayName"] : "Unknown",
                        };
                        Leaderboard.Add(entry);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[RankingService] Failed to parse leaderboard entry: {ex.Message}");
                    }
                }
            }
        }

        // =====================================================================
        // Local Persistence
        // =====================================================================

        void SaveLocal()
        {
            string json = JsonUtility.ToJson(CurrentRank);
            PlayerPrefs.SetString(PrefKeyRankData, json);
            PlayerPrefs.Save();
        }

        void LoadLocal()
        {
            string json = PlayerPrefs.GetString(PrefKeyRankData, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    CurrentRank = JsonUtility.FromJson<RankData>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RankingService] Failed to load local rank data: {ex.Message}");
                }
            }
        }
    }
}
