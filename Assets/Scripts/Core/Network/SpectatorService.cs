using System;
using System.Collections.Generic;
using UnityEngine;
using Banganka.Core.Battle;
#if FIREBASE_ENABLED
using Firebase.Database;
#endif

namespace Banganka.Core.Network
{
    /// <summary>
    /// 観戦モードサービス。
    /// RTDBの試合データを読み取り専用でリスニングし、
    /// 観戦UIへBattleState更新をブロードキャスト。
    /// </summary>
    public class SpectatorService : MonoBehaviour
    {
        public static SpectatorService Instance { get; private set; }

        public bool IsSpectating { get; private set; }
        public string SpectatingMatchId { get; private set; }
        public BattleState LatestState { get; private set; }

        /// <summary>観戦中の試合状態が更新された</summary>
        public event Action<BattleState> OnStateUpdated;
        /// <summary>試合が終了した</summary>
        public event Action<string> OnMatchEnded; // reason

        // アクティブな観戦可能試合リスト
        public event Action<List<SpectateMatch>> OnMatchListUpdated;

#if FIREBASE_ENABLED
        DatabaseReference _stateRef;
        DatabaseReference _metaRef;
#endif

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>観戦可能な試合一覧を取得</summary>
        public void FetchSpectateList()
        {
#if FIREBASE_ENABLED
            var db = FirebaseDatabase.DefaultInstance;
            db.GetReference("matches")
                .OrderByChild("meta/status")
                .EqualTo("active")
                .LimitToLast(20)
                .GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (!task.IsCompleted || task.IsFaulted) return;

                    var matches = new List<SpectateMatch>();
                    foreach (var child in task.Result.Children)
                    {
                        var meta = child.Child("meta");
                        matches.Add(new SpectateMatch
                        {
                            matchId = child.Key,
                            player1Name = meta.Child("p1Name").Value?.ToString() ?? "?",
                            player2Name = meta.Child("p2Name").Value?.ToString() ?? "?",
                            player1Rating = int.TryParse(meta.Child("p1Rating").Value?.ToString(), out int r1) ? r1 : 0,
                            player2Rating = int.TryParse(meta.Child("p2Rating").Value?.ToString(), out int r2) ? r2 : 0,
                            currentTurn = int.TryParse(meta.Child("turn").Value?.ToString(), out int t) ? t : 0,
                            spectatorCount = int.TryParse(meta.Child("spectators").Value?.ToString(), out int s) ? s : 0,
                        });
                    }

                    // レーティング降順（高レートの試合を上に）
                    matches.Sort((a, b) =>
                        (b.player1Rating + b.player2Rating).CompareTo(a.player1Rating + a.player2Rating));

                    OnMatchListUpdated?.Invoke(matches);
                });
#else
            // Firebaseなし: 空リスト返却
            OnMatchListUpdated?.Invoke(new List<SpectateMatch>());
#endif
        }

        /// <summary>指定試合の観戦を開始</summary>
        public void StartSpectating(string matchId)
        {
            if (IsSpectating) StopSpectating();

            SpectatingMatchId = matchId;
            IsSpectating = true;
            Debug.Log($"[Spectator] Started spectating: {matchId}");

#if FIREBASE_ENABLED
            var db = FirebaseDatabase.DefaultInstance;

            // State listener (read-only)
            _stateRef = db.GetReference($"matches/{matchId}/state");
            _stateRef.ValueChanged += OnRtdbStateChanged;

            // Meta listener (to detect match end)
            _metaRef = db.GetReference($"matches/{matchId}/meta/status");
            _metaRef.ValueChanged += OnRtdbMetaChanged;

            // Increment spectator count
            var specCountRef = db.GetReference($"matches/{matchId}/meta/spectators");
            specCountRef.RunTransaction(data =>
            {
                int current = data.Value != null ? int.Parse(data.Value.ToString()) : 0;
                data.Value = current + 1;
                return TransactionResult.Success(data);
            });
#endif
        }

        /// <summary>観戦を停止</summary>
        public void StopSpectating()
        {
            if (!IsSpectating) return;

#if FIREBASE_ENABLED
            if (_stateRef != null)
            {
                _stateRef.ValueChanged -= OnRtdbStateChanged;
                _stateRef = null;
            }
            if (_metaRef != null)
            {
                _metaRef.ValueChanged -= OnRtdbMetaChanged;
                _metaRef = null;
            }

            // Decrement spectator count
            if (!string.IsNullOrEmpty(SpectatingMatchId))
            {
                var db = FirebaseDatabase.DefaultInstance;
                var specCountRef = db.GetReference($"matches/{SpectatingMatchId}/meta/spectators");
                specCountRef.RunTransaction(data =>
                {
                    int current = data.Value != null ? int.Parse(data.Value.ToString()) : 0;
                    data.Value = Math.Max(0, current - 1);
                    return TransactionResult.Success(data);
                });
            }
#endif

            IsSpectating = false;
            SpectatingMatchId = null;
            LatestState = null;
            Debug.Log("[Spectator] Stopped spectating");
        }

#if FIREBASE_ENABLED
        void OnRtdbStateChanged(object sender, ValueChangedEventArgs e)
        {
            if (e.DatabaseError != null)
            {
                Debug.LogWarning($"[Spectator] State read error: {e.DatabaseError.Message}");
                return;
            }

            if (e.Snapshot?.RawJsonValue == null) return;

            try
            {
                LatestState = JsonUtility.FromJson<BattleState>(e.Snapshot.RawJsonValue);
                OnStateUpdated?.Invoke(LatestState);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Spectator] State parse error: {ex.Message}");
            }
        }

        void OnRtdbMetaChanged(object sender, ValueChangedEventArgs e)
        {
            if (e.Snapshot?.Value == null) return;
            string status = e.Snapshot.Value.ToString();
            if (status == "finished" || status == "aborted")
            {
                OnMatchEnded?.Invoke(status);
                StopSpectating();
            }
        }
#endif

        void OnDestroy()
        {
            StopSpectating();
        }
    }

    /// <summary>観戦可能試合のサマリー</summary>
    [Serializable]
    public class SpectateMatch
    {
        public string matchId;
        public string player1Name;
        public string player2Name;
        public int player1Rating;
        public int player2Rating;
        public int currentTurn;
        public int spectatorCount;
    }
}
