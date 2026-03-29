using System;
using UnityEngine;
using Banganka.Core.Battle;
using Banganka.Core.Config;
#if FIREBASE_ENABLED
using Firebase.Database;
#endif

namespace Banganka.Core.Network
{
    /// <summary>
    /// バトル中のリアルタイム同期管理。
    /// RTDB リスナーで GameState を同期する。
    /// Firebase SDK統合前はローカル直接実行。
    /// RTDB paths (NETWORK_SPEC.md §3.1):
    ///   /matches/{matchId}/state/
    ///   /matches/{matchId}/meta/
    ///   /matches/{matchId}/commands/
    ///   /matches/{matchId}/timers/
    ///   /matches/{matchId}/log/
    /// </summary>
    public class MatchSyncManager : MonoBehaviour
    {
        public static MatchSyncManager Instance { get; private set; }

        public string CurrentMatchId { get; private set; }
        public bool IsOnline { get; private set; }
        public PlayerSide LocalSide { get; private set; }

        /// <summary>RTDBから最新のBattleState JSONを受信したとき発火</summary>
#pragma warning disable CS0067 // Firebase統合時に使用予定
        public event Action OnMatchStateUpdated;
        public event Action<string> OnOpponentDisconnected;
        public event Action<string> OnReconnected;
        /// <summary>サーバーからメタ情報(status, winner)が変わったとき発火</summary>
        public event Action<string> OnMetaStatusChanged;
#pragma warning restore CS0067

        float _disconnectTimer;
        const float DisconnectGracePeriod = 60f; // NETWORK_SPEC.md §6.3

        // ターンタイマー (NETWORK_SPEC.md §5)
        const float TurnTimeLimit = 90f; // 秒
        long _turnStartedAtMs;
        bool _turnTimerPaused;

        /// <summary>ローカル計算による残りターン秒数</summary>
        public float RemainingTurnSeconds
        {
            get
            {
                if (_turnTimerPaused || _turnStartedAtMs <= 0) return TurnTimeLimit;
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                float elapsed = (nowMs - _turnStartedAtMs) / 1000f;
                return Mathf.Max(0f, TurnTimeLimit - elapsed);
            }
        }

        /// <summary>最後に受信・パースしたBattleState</summary>
        public BattleState LatestState { get; private set; }

        // プレゼンス状態
        bool _isConnectedToRtdb;
        public bool IsConnectedToRtdb => _isConnectedToRtdb;

#if FIREBASE_ENABLED
        DatabaseReference _rootRef;
        DatabaseReference _stateRef;
        DatabaseReference _metaRef;
        DatabaseReference _timersRef;
        DatabaseReference _logRef;
        DatabaseReference _connectedRef;

        // リスナーハンドル (detach用)
        EventHandler<ValueChangedEventArgs> _stateHandler;
        EventHandler<ValueChangedEventArgs> _metaHandler;
        EventHandler<ValueChangedEventArgs> _timersHandler;
        EventHandler<ValueChangedEventArgs> _connectedHandler;
#endif

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ====================================================================
        // StartListening — RTDBリスナーをアタッチ
        // ====================================================================

        public void StartListening(string matchId, PlayerSide localSide)
        {
            CurrentMatchId = matchId;
            LocalSide = localSide;
            IsOnline = true;
            _turnStartedAtMs = 0;
            _turnTimerPaused = false;
            Debug.Log($"[MatchSyncManager] Listening to match {matchId} as {localSide}");

#if FIREBASE_ENABLED
            AttachRtdbListeners(matchId);
#endif
        }

        // ====================================================================
        // StopListening — 全リスナーをデタッチ
        // ====================================================================

        public void StopListening()
        {
#if FIREBASE_ENABLED
            DetachRtdbListeners();
#endif
            CurrentMatchId = null;
            IsOnline = false;
            LatestState = null;
            _turnStartedAtMs = 0;
        }

        // ====================================================================
        // SendCommand — /commands/ にプッシュ
        // ====================================================================

        public void SendCommand(BattleCommand command)
        {
            if (string.IsNullOrEmpty(CurrentMatchId)) return;

            command.playerUid = AuthService.Instance?.Uid ?? "local";
            command.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

#if FIREBASE_ENABLED
            PushCommandToRtdb(command);
#else
            // ローカルシミュレーション: Cloud Function経由で直接実行
            CloudFunctionClient.Instance?.SendCommand(CurrentMatchId, command);
#endif
        }

        // ====================================================================
        // 切断/再接続ハンドリング (NETWORK_SPEC.md §6)
        // ====================================================================

        public void HandleDisconnect()
        {
            _disconnectTimer = DisconnectGracePeriod;
            _turnTimerPaused = true; // NETWORK_SPEC.md §6.3: ターンタイマー一時停止
            OnOpponentDisconnected?.Invoke("相手の接続を待っています...");
        }

        public void HandleReconnect()
        {
            _disconnectTimer = 0;
            _turnTimerPaused = false;
            OnReconnected?.Invoke("再接続しました");

#if FIREBASE_ENABLED
            // 再接続時: リスナー再アタッチ + disconnectDeadlineクリア
            if (!string.IsNullOrEmpty(CurrentMatchId))
            {
                ReattachAfterReconnect();
            }
#endif
        }

        // ====================================================================
        // Update — 切断タイマー監視
        // ====================================================================

        void Update()
        {
            if (_disconnectTimer > 0)
            {
                _disconnectTimer -= Time.deltaTime;
                if (_disconnectTimer <= 0)
                {
                    Debug.Log("[MatchSyncManager] Disconnect timeout - opponent loses");
                    // タイムアウト敗北はサーバー側で判定 (Cloud Functions checkDisconnect)
                }
            }
        }

        // ====================================================================
        // Firebase RTDB 実装
        // ====================================================================

#if FIREBASE_ENABLED

        void AttachRtdbListeners(string matchId)
        {
            _rootRef = FirebaseDatabase.DefaultInstance.RootReference;
            string basePath = $"matches/{matchId}";

            _stateRef = _rootRef.Child(basePath).Child("state");
            _metaRef = _rootRef.Child(basePath).Child("meta");
            _timersRef = _rootRef.Child(basePath).Child("timers");
            _logRef = _rootRef.Child(basePath).Child("log");
            _connectedRef = FirebaseDatabase.DefaultInstance.GetReference(".info/connected");

            // /state/ リスナー — GameState同期
            _stateHandler = (sender, args) =>
            {
                if (args.DatabaseError != null)
                {
                    Debug.LogError($"[MatchSyncManager] State listener error: {args.DatabaseError.Message}");
                    ErrorHandler.Instance?.HandleError(
                        ErrorHandler.ErrorCategory.Network,
                        $"RTDB state sync error: {args.DatabaseError.Message}");
                    return;
                }
                if (args.Snapshot != null && args.Snapshot.Exists)
                {
                    ParseStateSnapshot(args.Snapshot);
                }
            };
            _stateRef.ValueChanged += _stateHandler;

            // /meta/ リスナー — ステータス変更検知
            _metaHandler = (sender, args) =>
            {
                if (args.DatabaseError != null) return;
                if (args.Snapshot != null && args.Snapshot.Exists)
                {
                    string status = args.Snapshot.Child("status").Value?.ToString();
                    if (!string.IsNullOrEmpty(status))
                    {
                        Debug.Log($"[MatchSyncManager] Meta status: {status}");
                        OnMetaStatusChanged?.Invoke(status);
                    }
                }
            };
            _metaRef.ValueChanged += _metaHandler;

            // /timers/ リスナー — ターンタイマー同期
            _timersHandler = (sender, args) =>
            {
                if (args.DatabaseError != null) return;
                if (args.Snapshot != null && args.Snapshot.Exists)
                {
                    ParseTimersSnapshot(args.Snapshot);
                }
            };
            _timersRef.ValueChanged += _timersHandler;

            // .info/connected — プレゼンスシステム (NETWORK_SPEC.md §6.1)
            _connectedHandler = (sender, args) =>
            {
                if (args.DatabaseError != null) return;
                bool connected = (bool)(args.Snapshot?.Value ?? false);
                bool wasConnected = _isConnectedToRtdb;
                _isConnectedToRtdb = connected;

                Debug.Log($"[MatchSyncManager] RTDB connected: {connected}");

                if (connected && !wasConnected && !string.IsNullOrEmpty(CurrentMatchId))
                {
                    // 接続復帰 → OnDisconnectハンドラー設定
                    SetupOnDisconnectHandler();
                    if (wasConnected == false && LatestState != null)
                    {
                        // 再接続
                        HandleReconnect();
                    }
                }
                else if (!connected && wasConnected)
                {
                    // 切断検知
                    HandleDisconnect();
                }
            };
            _connectedRef.ValueChanged += _connectedHandler;

            // 初回接続時にOnDisconnectハンドラーを設定
            SetupOnDisconnectHandler();

            Debug.Log($"[MatchSyncManager] RTDB listeners attached for {matchId}");
        }

        void DetachRtdbListeners()
        {
            if (_stateRef != null && _stateHandler != null)
                _stateRef.ValueChanged -= _stateHandler;
            if (_metaRef != null && _metaHandler != null)
                _metaRef.ValueChanged -= _metaHandler;
            if (_timersRef != null && _timersHandler != null)
                _timersRef.ValueChanged -= _timersHandler;
            if (_connectedRef != null && _connectedHandler != null)
                _connectedRef.ValueChanged -= _connectedHandler;

            _stateRef = null;
            _metaRef = null;
            _timersRef = null;
            _logRef = null;
            _connectedRef = null;
            _stateHandler = null;
            _metaHandler = null;
            _timersHandler = null;
            _connectedHandler = null;
            _isConnectedToRtdb = false;

            Debug.Log("[MatchSyncManager] RTDB listeners detached");
        }

        /// <summary>
        /// OnDisconnectハンドラー: 切断時にdisconnectDeadlineを自動セット (NETWORK_SPEC.md §6.1)
        /// </summary>
        void SetupOnDisconnectHandler()
        {
            if (_timersRef == null) return;

            long deadlineMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                            + (long)(DisconnectGracePeriod * 1000);
            _timersRef.Child("disconnectDeadline")
                .OnDisconnect()
                .SetValue(deadlineMs);

            Debug.Log("[MatchSyncManager] OnDisconnect handler set");
        }

        /// <summary>
        /// /state/ スナップショットをBattleStateにパース
        /// </summary>
        void ParseStateSnapshot(DataSnapshot snapshot)
        {
            try
            {
                string json = snapshot.GetRawJsonValue();
                if (string.IsNullOrEmpty(json)) return;

                var state = JsonUtility.FromJson<BattleState>(json);
                if (state != null)
                {
                    LatestState = state;
                    Debug.Log($"[MatchSyncManager] State updated: turn={state.turnTotal}, active={state.activePlayer}, gameOver={state.isGameOver}");
                    OnMatchStateUpdated?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchSyncManager] Failed to parse state: {ex.Message}");
                ErrorHandler.Instance?.HandleError(
                    ErrorHandler.ErrorCategory.Data,
                    $"Battle state parse error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// /timers/ スナップショットからターンタイマーを更新
        /// </summary>
        void ParseTimersSnapshot(DataSnapshot snapshot)
        {
            // turnStartedAt
            var turnStartVal = snapshot.Child("turnStartedAt").Value;
            if (turnStartVal != null && long.TryParse(turnStartVal.ToString(), out long ts))
            {
                _turnStartedAtMs = ts;
            }

            // disconnectDeadline
            var deadlineVal = snapshot.Child("disconnectDeadline").Value;
            if (deadlineVal != null && long.TryParse(deadlineVal.ToString(), out long deadline))
            {
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (deadline > nowMs)
                {
                    float remaining = (deadline - nowMs) / 1000f;
                    _disconnectTimer = remaining;
                    Debug.Log($"[MatchSyncManager] Disconnect deadline: {remaining:F1}s remaining");
                }
            }
            else
            {
                // deadlineがnull → 切断なし、タイマークリア
                if (_disconnectTimer > 0)
                {
                    _disconnectTimer = 0;
                    _turnTimerPaused = false;
                }
            }
        }

        /// <summary>
        /// コマンドをRTDB /commands/ にプッシュ (自動生成キー)
        /// </summary>
        void PushCommandToRtdb(BattleCommand command)
        {
            if (_rootRef == null || string.IsNullOrEmpty(CurrentMatchId)) return;

            string json = JsonUtility.ToJson(command);
            var commandsRef = _rootRef.Child($"matches/{CurrentMatchId}/commands");
            var newRef = commandsRef.Push();

            newRef.SetRawJsonValueAsync(json).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError($"[MatchSyncManager] Failed to push command: {task.Exception?.Message}");
                    ErrorHandler.Instance?.HandleError(
                        ErrorHandler.ErrorCategory.Network,
                        $"Command send failed: {task.Exception?.Message}",
                        task.Exception);
                }
                else
                {
                    Debug.Log($"[MatchSyncManager] Command pushed: {command.type} -> {newRef.Key}");
                }
            });
        }

        /// <summary>
        /// 再接続後: リスナー再アタッチ + disconnectDeadlineクリア (NETWORK_SPEC.md §6.2)
        /// </summary>
        void ReattachAfterReconnect()
        {
            // リスナーが外れている場合は再アタッチ
            if (_stateRef == null)
            {
                AttachRtdbListeners(CurrentMatchId);
            }

            // disconnectDeadlineをクリア
            if (_timersRef != null)
            {
                _timersRef.Child("disconnectDeadline").SetValueAsync(null).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Debug.LogError($"[MatchSyncManager] Failed to clear disconnect deadline: {task.Exception?.Message}");
                    }
                    else
                    {
                        Debug.Log("[MatchSyncManager] Disconnect deadline cleared on reconnect");
                    }
                });
            }

            // OnDisconnectハンドラーを再設定
            SetupOnDisconnectHandler();
        }

#endif // FIREBASE_ENABLED
    }
}
