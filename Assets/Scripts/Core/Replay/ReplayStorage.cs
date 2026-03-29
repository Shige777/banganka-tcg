using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Banganka.Core.Replay
{
    /// <summary>
    /// リプレイの保存・読み込み・管理 (REPLAY_SPEC.md 1.4 準拠)
    /// ローカルストレージ: Application.persistentDataPath/replays/
    /// 自動保存: 直近20試合、お気に入り: 上限50件
    /// </summary>
    public static class ReplayStorage
    {
        const int MaxAutoSave = 20;
        const int MaxFavorites = 50;
        const string ReplayDir = "replays";
        const string FavoritesFile = "replay_favorites.json";

        static string BasePath => Path.Combine(Application.persistentDataPath, ReplayDir);
        static string FavoritesPath => Path.Combine(Application.persistentDataPath, FavoritesFile);

        /// <summary>
        /// リプレイを保存する
        /// </summary>
        public static bool SaveReplay(ReplayData data, bool isFavorite = false)
        {
            if (data == null || string.IsNullOrEmpty(data.replayId)) return false;

            try
            {
                EnsureDirectory();

                string filePath = GetReplayPath(data.replayId);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(filePath, json);

                if (isFavorite)
                {
                    SetFavorite(data.replayId, true);
                }

                // 自動保存制限の適用
                PruneOldReplays();

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReplayStorage] SaveReplay failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// リプレイを読み込む
        /// </summary>
        public static ReplayData LoadReplay(string replayId)
        {
            if (string.IsNullOrEmpty(replayId)) return null;

            try
            {
                string filePath = GetReplayPath(replayId);
                if (!File.Exists(filePath)) return null;

                string json = File.ReadAllText(filePath);
                return JsonUtility.FromJson<ReplayData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReplayStorage] LoadReplay failed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 保存済みリプレイ一覧を取得（新しい順）
        /// </summary>
        public static List<ReplaySummary> GetReplayList()
        {
            var summaries = new List<ReplaySummary>();

            try
            {
                EnsureDirectory();
                var favorites = LoadFavorites();
                var files = Directory.GetFiles(BasePath, "*.json");

                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var data = JsonUtility.FromJson<ReplayData>(json);
                        if (data == null) continue;

                        summaries.Add(new ReplaySummary
                        {
                            replayId = data.replayId,
                            matchId = data.matchId,
                            player1Name = data.players?.Length > 0 ? data.players[0].name : "?",
                            player2Name = data.players?.Length > 1 ? data.players[1].name : "?",
                            player1WishMaster = data.players?.Length > 0 ? data.players[0].wishMasterId : "",
                            player2WishMaster = data.players?.Length > 1 ? data.players[1].wishMasterId : "",
                            winner = data.result?.winner ?? 0,
                            reason = data.result?.reason ?? "",
                            totalTurns = data.result?.totalTurns ?? 0,
                            createdAt = data.createdAt,
                            isFavorite = favorites.Contains(data.replayId)
                        });
                    }
                    catch
                    {
                        // 壊れたファイルはスキップ
                    }
                }

                // 新しい順にソート
                summaries.Sort((a, b) => string.Compare(b.createdAt, a.createdAt, StringComparison.Ordinal));
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReplayStorage] GetReplayList failed: {e.Message}");
            }

            return summaries;
        }

        /// <summary>
        /// リプレイを削除する
        /// </summary>
        public static bool DeleteReplay(string replayId)
        {
            if (string.IsNullOrEmpty(replayId)) return false;

            try
            {
                string filePath = GetReplayPath(replayId);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // お気に入りからも除去
                SetFavorite(replayId, false);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReplayStorage] DeleteReplay failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// お気に入り状態をトグル
        /// </summary>
        public static bool ToggleFavorite(string replayId)
        {
            var favorites = LoadFavorites();
            bool isFav = favorites.Contains(replayId);

            if (isFav)
            {
                SetFavorite(replayId, false);
                return false; // now not favorite
            }
            else
            {
                if (favorites.Count >= MaxFavorites)
                {
                    Debug.LogWarning($"[ReplayStorage] お気に入り上限({MaxFavorites}件)に達しています");
                    return false;
                }

                SetFavorite(replayId, true);
                return true; // now favorite
            }
        }

        /// <summary>
        /// 指定リプレイがお気に入りかどうか
        /// </summary>
        public static bool IsFavorite(string replayId)
        {
            var favorites = LoadFavorites();
            return favorites.Contains(replayId);
        }

        /// <summary>
        /// 古いリプレイを削除（お気に入り以外の自動保存を直近20件に制限）
        /// </summary>
        public static void PruneOldReplays()
        {
            try
            {
                var favorites = LoadFavorites();
                var allReplays = GetReplayList();

                // お気に入り以外を抽出（新しい順）
                var nonFavorites = allReplays
                    .Where(r => !favorites.Contains(r.replayId))
                    .ToList();

                // MaxAutoSave を超えた分を削除
                if (nonFavorites.Count > MaxAutoSave)
                {
                    var toDelete = nonFavorites.Skip(MaxAutoSave).ToList();
                    foreach (var replay in toDelete)
                    {
                        string path = GetReplayPath(replay.replayId);
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReplayStorage] PruneOldReplays failed: {e.Message}");
            }
        }

        // ====================================================================
        // Private helpers
        // ====================================================================

        static void EnsureDirectory()
        {
            if (!Directory.Exists(BasePath))
            {
                Directory.CreateDirectory(BasePath);
            }
        }

        static string GetReplayPath(string replayId)
        {
            return Path.Combine(BasePath, $"{replayId}.json");
        }

        static void SetFavorite(string replayId, bool isFavorite)
        {
            var favorites = LoadFavorites();

            if (isFavorite && !favorites.Contains(replayId))
            {
                favorites.Add(replayId);
            }
            else if (!isFavorite)
            {
                favorites.Remove(replayId);
            }

            SaveFavorites(favorites);
        }

        static HashSet<string> LoadFavorites()
        {
            try
            {
                if (!File.Exists(FavoritesPath))
                    return new HashSet<string>();

                string json = File.ReadAllText(FavoritesPath);
                var wrapper = JsonUtility.FromJson<FavoritesWrapper>(json);
                return wrapper?.ids != null
                    ? new HashSet<string>(wrapper.ids)
                    : new HashSet<string>();
            }
            catch
            {
                return new HashSet<string>();
            }
        }

        static void SaveFavorites(HashSet<string> favorites)
        {
            try
            {
                var wrapper = new FavoritesWrapper { ids = favorites.ToList() };
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(FavoritesPath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReplayStorage] SaveFavorites failed: {e.Message}");
            }
        }

        [Serializable]
        class FavoritesWrapper
        {
            public List<string> ids = new();
        }
    }

    /// <summary>
    /// リプレイ一覧表示用のサマリーデータ
    /// </summary>
    [Serializable]
    public class ReplaySummary
    {
        public string replayId;
        public string matchId;
        public string player1Name;
        public string player2Name;
        public string player1WishMaster;
        public string player2WishMaster;
        public int winner;
        public string reason;
        public int totalTurns;
        public string createdAt;
        public bool isFavorite;
    }
}
