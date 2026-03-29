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
    /// <summary>
    /// Cloud Functions呼び出しクライアント。
    /// Firebase SDK統合前はローカルシミュレーションで動作。
    /// BACKEND_DESIGN.md §4.1 の全関数をカバー。
    /// </summary>
    public class CloudFunctionClient : MonoBehaviour
    {
        public static CloudFunctionClient Instance { get; private set; }

        // ====================================================================
        // レートリミット設定 (関数ごとの呼び出し間隔制限)
        // ====================================================================

        readonly Dictionary<string, float> _lastCallTime = new();
        readonly Dictionary<string, int> _callCounts = new();

        static readonly Dictionary<string, float> CooldownSeconds = new()
        {
            { "createRoom",      2f },
            { "joinRoom",        2f },
            { "startMatch",      3f },
            { "processCommand",  0.3f }, // バトル中は高頻度許可
            { "purchaseItem",    5f },
            { "verifyReceipt",   10f },
            { "deleteAccount",   30f },
            { "claimEventReward", 3f },
        };

        const int MaxRetries = 3;
        const float BaseRetryDelay = 1f;

#if FIREBASE_ENABLED
        FirebaseFunctions _functions;
#endif

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

#if FIREBASE_ENABLED
            InitializeFirebaseFunctions();
#endif
        }

#if FIREBASE_ENABLED
        void InitializeFirebaseFunctions()
        {
            _functions = FirebaseFunctions.GetInstance(FirebaseConfig.FunctionsRegion);
            if (FirebaseConfig.UseEmulator)
            {
                // エミュレータ使用時はローカルURL
                _functions.UseFunctionsEmulator("localhost", 5001);
                Debug.Log("[CloudFunctionClient] Using Functions emulator");
            }
            Debug.Log($"[CloudFunctionClient] Initialized (region={FirebaseConfig.FunctionsRegion}, env={FirebaseConfig.CurrentEnv})");
        }
#endif

        // ====================================================================
        // レートリミットチェック
        // ====================================================================

        bool CheckRateLimit(string functionName)
        {
            if (!CooldownSeconds.TryGetValue(functionName, out float cooldown))
                cooldown = 1f;

            if (_lastCallTime.TryGetValue(functionName, out float lastTime))
            {
                float elapsed = Time.realtimeSinceStartup - lastTime;
                if (elapsed < cooldown)
                {
                    Debug.LogWarning($"[CloudFunctionClient] Rate limited: {functionName} (wait {cooldown - elapsed:F1}s)");
                    return false;
                }
            }

            _lastCallTime[functionName] = Time.realtimeSinceStartup;
            _callCounts[functionName] = _callCounts.GetValueOrDefault(functionName, 0) + 1;
            return true;
        }

        // ====================================================================
        // createRoom — ルーム作成 (6桁ID返却)
        // BACKEND_DESIGN.md §4.1 / NETWORK_SPEC.md §2.1
        // ====================================================================

        public void CreateRoom(Action<string> onSuccess, Action<string> onError = null)
        {
            if (!CheckRateLimit("createRoom"))
            {
                onError?.Invoke("RATE_LIMITED");
                return;
            }

#if FIREBASE_ENABLED
            CallFunction<Dictionary<string, object>>("createRoom", null, result =>
            {
                string roomId = result?["roomId"]?.ToString();
                if (!string.IsNullOrEmpty(roomId))
                {
                    Debug.Log($"[CloudFunctionClient] Room created: {roomId}");
                    onSuccess?.Invoke(roomId);
                }
                else
                {
                    onError?.Invoke("INVALID_RESPONSE");
                }
            }, error =>
            {
                ErrorHandler.Instance?.HandleError(ErrorHandler.ErrorCategory.Network,
                    $"createRoom failed: {error}");
                onError?.Invoke(error);
            });
#else
            // ローカルシミュレーション
            RoomManager.Instance?.CreateRoom();
            onSuccess?.Invoke(RoomManager.Instance?.RoomId ?? "LOCAL");
#endif
        }

        // ====================================================================
        // joinRoom — ルーム参加
        // BACKEND_DESIGN.md §4.1 / NETWORK_SPEC.md §2.1
        // ====================================================================

        public void JoinRoom(string roomId, Action<bool> onResult)
        {
            if (!CheckRateLimit("joinRoom"))
            {
                onResult?.Invoke(false);
                return;
            }

#if FIREBASE_ENABLED
            var data = new Dictionary<string, object> { { "roomId", roomId } };
            CallFunction<Dictionary<string, object>>("joinRoom", data, result =>
            {
                bool success = result != null && result.ContainsKey("success")
                    && (bool)result["success"];
                Debug.Log($"[CloudFunctionClient] joinRoom: {(success ? "success" : "failed")}");
                onResult?.Invoke(success);
            }, error =>
            {
                ErrorHandler.Instance?.HandleError(ErrorHandler.ErrorCategory.Network,
                    $"joinRoom failed: {error}");
                onResult?.Invoke(false);
            });
#else
            // ローカルシミュレーション
            RoomManager.Instance?.JoinRoom(roomId);
            onResult?.Invoke(true);
#endif
        }

        // ====================================================================
        // startMatch — 対戦初期化 (matchId + 初期GameState返却)
        // BACKEND_DESIGN.md §4.1
        // ====================================================================

        public void StartMatch(string roomId, Action<string> onMatchId)
        {
            if (!CheckRateLimit("startMatch"))
            {
                onMatchId?.Invoke(null);
                return;
            }

#if FIREBASE_ENABLED
            var data = new Dictionary<string, object> { { "roomId", roomId } };
            CallFunction<Dictionary<string, object>>("startMatch", data, result =>
            {
                string matchId = result?["matchId"]?.ToString();
                Debug.Log($"[CloudFunctionClient] Match started: {matchId}");
                onMatchId?.Invoke(matchId);
            }, error =>
            {
                ErrorHandler.Instance?.HandleError(ErrorHandler.ErrorCategory.Network,
                    $"startMatch failed: {error}");
                onMatchId?.Invoke(null);
            });
#else
            // ローカル: generate match ID
            var matchId = $"match_{DateTime.UtcNow.Ticks}";
            onMatchId?.Invoke(matchId);
#endif
        }

        // ====================================================================
        // CreateBotMatch — Bot対戦開始
        // ====================================================================

        public void CreateBotMatch(string difficulty, string deckId, Action<string> onMatchId)
        {
#if FIREBASE_ENABLED
            var data = new Dictionary<string, object>
            {
                { "difficulty", difficulty },
                { "deckId", deckId }
            };
            CallFunction<Dictionary<string, object>>("createBotMatch", data, result =>
            {
                string matchId = result?["matchId"]?.ToString();
                Debug.Log($"[CloudFunctionClient] Bot match started: {matchId}");
                onMatchId?.Invoke(matchId);
            }, error =>
            {
                ErrorHandler.Instance?.HandleError(ErrorHandler.ErrorCategory.Battle,
                    $"createBotMatch failed: {error}");
                onMatchId?.Invoke(null);
            });
#else
            var matchId = $"bot_{difficulty}_{DateTime.UtcNow.Ticks}";
            onMatchId?.Invoke(matchId);
#endif
        }

        /// <summary>マッチング範囲を拡大通知 (§14.2: ±tierRange に拡大)</summary>
        public void ExpandMatchRange(string deckId, int tierRange)
        {
#if FIREBASE_ENABLED
            var data = new Dictionary<string, object>
            {
                { "deckId", deckId },
                { "tierRange", tierRange }
            };
            CallFunction<Dictionary<string, object>>("expandMatchRange", data, _ =>
            {
                Debug.Log($"[CloudFunctionClient] Match range expanded to ±{tierRange}");
            }, error =>
            {
                Debug.LogWarning($"[CloudFunctionClient] expandMatchRange failed: {error}");
            });
#else
            Debug.Log($"[CloudFunctionClient] (local) Match range expanded to ±{tierRange}");
#endif
        }

        // ====================================================================
        // processCommand (SendCommand) — コマンド送信→検証済みState返却
        // BACKEND_DESIGN.md §4.2
        // ====================================================================

        public void SendCommand(string matchId, BattleCommand command, Action<bool> onResult = null)
        {
            if (!CheckRateLimit("processCommand"))
            {
                onResult?.Invoke(false);
                return;
            }

#if FIREBASE_ENABLED
            var data = new Dictionary<string, object>
            {
                { "matchId", matchId },
                { "command", JsonUtility.ToJson(command) }
            };
            CallFunction<Dictionary<string, object>>("processCommand", data, result =>
            {
                bool valid = result != null && !result.ContainsKey("error");
                if (!valid)
                {
                    string reason = result?.ContainsKey("reason") == true
                        ? result["reason"].ToString() : "unknown";
                    Debug.LogWarning($"[CloudFunctionClient] Command rejected: {reason}");
                }
                onResult?.Invoke(valid);
            }, error =>
            {
                ErrorHandler.Instance?.HandleError(ErrorHandler.ErrorCategory.Battle,
                    $"processCommand failed: {error}");
                onResult?.Invoke(false);
            });
#else
            // ローカル: direct execution
            Debug.Log($"[CloudFunctionClient] Command: {command.type} for match {matchId}");
            onResult?.Invoke(true);
#endif
        }

        // ====================================================================
        // purchaseItem — ショップ購入
        // BACKEND_DESIGN.md §4.1
        // ====================================================================

        public void PurchaseItem(string itemId, int quantity, Action<bool, Dictionary<string, int>> onResult)
        {
            if (!CheckRateLimit("purchaseItem"))
            {
                onResult?.Invoke(false, null);
                return;
            }

#if FIREBASE_ENABLED
            var data = new Dictionary<string, object>
            {
                { "itemId", itemId },
                { "quantity", quantity }
            };
            CallFunction<Dictionary<string, object>>("purchaseItem", data, result =>
            {
                if (result != null && result.ContainsKey("rewards"))
                {
                    var rewards = new Dictionary<string, int>();
                    // サーバーからrewards mapをパース
                    if (result["rewards"] is Dictionary<string, object> rewardMap)
                    {
                        foreach (var kv in rewardMap)
                        {
                            if (int.TryParse(kv.Value.ToString(), out int count))
                                rewards[kv.Key] = count;
                        }
                    }
                    Debug.Log($"[CloudFunctionClient] Purchase success: {itemId} x{quantity}");
                    onResult?.Invoke(true, rewards);
                }
                else
                {
                    string reason = result?.ContainsKey("error") == true
                        ? result["error"].ToString() : "purchase_failed";
                    Debug.LogWarning($"[CloudFunctionClient] Purchase failed: {reason}");
                    onResult?.Invoke(false, null);
                }
            }, error =>
            {
                ErrorHandler.Instance?.HandleError(ErrorHandler.ErrorCategory.Purchase,
                    $"purchaseItem failed: {error}");
                onResult?.Invoke(false, null);
            });
#else
            // ローカルシミュレーション: 通貨消費なしで成功扱い (開発用)
            Debug.Log($"[CloudFunctionClient] PurchaseItem local simulation: {itemId} x{quantity}");
            var localRewards = new Dictionary<string, int> { { itemId, quantity } };
            onResult?.Invoke(true, localRewards);
#endif
        }

        // ====================================================================
        // verifyReceipt — App Storeレシート検証
        // BACKEND_DESIGN.md §4.1
        // ====================================================================

        public void VerifyReceipt(string receiptData, string productId, Action<bool, string> onResult)
        {
            if (!CheckRateLimit("verifyReceipt"))
            {
                onResult?.Invoke(false, "RATE_LIMITED");
                return;
            }

#if FIREBASE_ENABLED
            var data = new Dictionary<string, object>
            {
                { "receiptData", receiptData },
                { "productId", productId },
            };
            CallFunction<Dictionary<string, object>>("verifyReceipt", data, result =>
            {
                bool valid = result != null && result.ContainsKey("valid")
                    && (bool)result["valid"];
                string status = result?.ContainsKey("status") == true
                    ? result["status"].ToString() : "";
                Debug.Log($"[CloudFunctionClient] Receipt verification: valid={valid}, status={status}");
                onResult?.Invoke(valid, status);
            }, error =>
            {
                ErrorHandler.Instance?.HandleError(ErrorHandler.ErrorCategory.Purchase,
                    $"verifyReceipt failed: {error}");
                onResult?.Invoke(false, error);
            });
#else
            // ローカルシミュレーション: レシートは常に有効扱い (開発用)
            Debug.Log($"[CloudFunctionClient] verifyReceipt local simulation: auto-approve");
            onResult?.Invoke(true, "LOCAL_APPROVED");
#endif
        }

        // ====================================================================
        // deleteAccount — アカウント完全削除 (App Store審査要件)
        // BACKEND_DESIGN.md §2.3 / §4.1
        // ====================================================================

        public void DeleteAccount(Action<bool> onResult)
        {
            if (!CheckRateLimit("deleteAccount"))
            {
                onResult?.Invoke(false);
                return;
            }

#if FIREBASE_ENABLED
            CallFunction<Dictionary<string, object>>("deleteAccount", null, result =>
            {
                bool success = result != null && result.ContainsKey("success")
                    && (bool)result["success"];
                Debug.Log($"[CloudFunctionClient] Account deletion: {(success ? "success" : "failed")}");
                if (success)
                {
                    AuthService.Instance?.SignOut();
                }
                onResult?.Invoke(success);
            }, error =>
            {
                ErrorHandler.Instance?.HandleError(ErrorHandler.ErrorCategory.Auth,
                    $"deleteAccount failed: {error}");
                onResult?.Invoke(false);
            });
#else
            Debug.LogWarning("[CloudFunctionClient] deleteAccount: local simulation");
            AuthService.Instance?.SignOut();
            onResult?.Invoke(true);
#endif
        }

        // ====================================================================
        // claimEventReward — イベント報酬受取
        // EVENT_SYSTEM_SPEC.md
        // ====================================================================

        public void ClaimEventReward(string eventId, int threshold, Action<bool, Dictionary<string, int>> onResult)
        {
            if (!CheckRateLimit("claimEventReward"))
            {
                onResult?.Invoke(false, null);
                return;
            }

#if FIREBASE_ENABLED
            var data = new Dictionary<string, object>
            {
                { "eventId", eventId },
                { "threshold", threshold }
            };
            CallFunction<Dictionary<string, object>>("claimEventReward", data, result =>
            {
                if (result != null && result.ContainsKey("rewards"))
                {
                    var rewards = new Dictionary<string, int>();
                    if (result["rewards"] is Dictionary<string, object> rewardMap)
                    {
                        foreach (var kv in rewardMap)
                        {
                            if (int.TryParse(kv.Value.ToString(), out int count))
                                rewards[kv.Key] = count;
                        }
                    }
                    Debug.Log($"[CloudFunctionClient] Event reward claimed: {eventId}");
                    onResult?.Invoke(true, rewards);
                }
                else
                {
                    onResult?.Invoke(false, null);
                }
            }, error =>
            {
                ErrorHandler.Instance?.HandleError(ErrorHandler.ErrorCategory.Network,
                    $"claimEventReward failed: {error}");
                onResult?.Invoke(false, null);
            });
#else
            // ローカルシミュレーション: イベント報酬付与 (開発用)
            Debug.Log($"[CloudFunctionClient] claimEventReward local simulation: {eventId} threshold={threshold}");
            var localRewards = new Dictionary<string, int> { { "gems", 100 } };
            onResult?.Invoke(true, localRewards);
#endif
        }

        // ====================================================================
        // 共通: Firebase Callable ラッパー (リトライ付き)
        // ====================================================================

#if FIREBASE_ENABLED
        void CallFunction<T>(string functionName, Dictionary<string, object> data,
            Action<T> onSuccess, Action<string> onError, int retryCount = 0)
        {
            var callable = _functions.GetHttpsCallable(functionName);
            var task = data != null
                ? callable.CallAsync(data)
                : callable.CallAsync();

            task.ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted)
                {
                    string errorMsg = t.Exception?.InnerException?.Message ?? t.Exception?.Message ?? "Unknown error";

                    // リトライ可能なエラーか判定
                    bool isRetryable = IsRetryableError(errorMsg);
                    if (isRetryable && retryCount < MaxRetries)
                    {
                        float delay = BaseRetryDelay * Mathf.Pow(2, retryCount);
                        Debug.LogWarning($"[CloudFunctionClient] {functionName} retry {retryCount + 1}/{MaxRetries} in {delay}s: {errorMsg}");
                        StartCoroutine(RetryCoroutine(delay, () =>
                        {
                            CallFunction(functionName, data, onSuccess, onError, retryCount + 1);
                        }));
                        return;
                    }

                    Debug.LogError($"[CloudFunctionClient] {functionName} failed: {errorMsg}");
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
                        Debug.LogError($"[CloudFunctionClient] {functionName} response parse error: {ex.Message}");
                        onError?.Invoke($"PARSE_ERROR: {ex.Message}");
                    }
                }
            });
        }

        static bool IsRetryableError(string errorMsg)
        {
            // UNAVAILABLE, DEADLINE_EXCEEDED, INTERNAL は再試行可能
            return errorMsg.Contains("UNAVAILABLE")
                || errorMsg.Contains("DEADLINE_EXCEEDED")
                || errorMsg.Contains("INTERNAL")
                || errorMsg.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        }

        System.Collections.IEnumerator RetryCoroutine(float delay, Action action)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }
#endif

        // ====================================================================
        // デバッグ: 呼び出し統計
        // ====================================================================

        /// <summary>各関数の呼び出し回数を返す</summary>
        public Dictionary<string, int> GetCallStats() => new(_callCounts);

        /// <summary>レートリミット状態をリセット (テスト用)</summary>
        public void ResetRateLimits()
        {
            _lastCallTime.Clear();
            _callCounts.Clear();
        }
    }

    [Serializable]
    public class BattleCommand
    {
        public string type; // "Mulligan","PlayManifest","PlaySpell","PlayAlgorithm","DeclareAttack","DeclareBlock","EndTurn","UseSkill","Ambush"
        public string playerUid;
        public long timestamp;

        // Mulligan
        public int[] mulliganIndices;

        // PlayManifest
        public string cardId;

        // DeclareAttack
        public string attackerId;
        public string targetId;
        public string attackerType; // "Leader" or "Unit"
        public string targetType; // "Leader" or "Unit"

        // DeclareBlock
        public string blockerId;

        // UseSkill
        public int skillLevel;
    }
}
