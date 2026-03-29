using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Banganka.Core.Config
{
    /// <summary>
    /// セキュリティ設定 (SECURITY_SPEC.md)
    /// レート制限、チート検知、BAN管理、コマンド整合性検証。
    /// </summary>
    public static class SecurityConfig
    {
        // ------------------------------------------------------------------
        // BAN レベル (SECURITY_SPEC.md §6.1)
        // ------------------------------------------------------------------

        public enum BanLevel
        {
            None,
            Warning,  // ログ記録のみ (スピードハック検知1回、軽微なクライアント不整合)
            SoftBan,  // 24時間対戦停止 + 警告 (スピードハック3回以上、レシート不正1回)
            HardBan,  // アカウント永久停止 (レシート偽造確認、大規模不正、RMT確認)
        }

        // ------------------------------------------------------------------
        // チート検知閾値 (SECURITY_SPEC.md §2.2)
        // ------------------------------------------------------------------

        /// <summary>スピードハック検知: サーバー時刻との乖離許容範囲 (秒)</summary>
        public const float SpeedHackTimeThreshold = 5f;

        /// <summary>スピードハック連続検知回数 → SoftBAN移行</summary>
        public const int SpeedHackWarningToSoftBan = 3;

        /// <summary>SoftBAN期間 (時間)</summary>
        public const int SoftBanDurationHours = 24;

        // ------------------------------------------------------------------
        // レート制限 (SECURITY_SPEC.md §5.1)
        // ------------------------------------------------------------------

        public static readonly Dictionary<string, RateLimitRule> RateLimits = new()
        {
            { "createRoom",     new RateLimitRule { maxCalls = 5,  windowSeconds = 60 } },
            { "joinRoom",       new RateLimitRule { maxCalls = 10, windowSeconds = 60 } },
            { "processCommand", new RateLimitRule { maxCalls = 30, windowSeconds = 60 } },
            { "purchaseItem",   new RateLimitRule { maxCalls = 3,  windowSeconds = 60 } },
            { "verifyReceipt",  new RateLimitRule { maxCalls = 3,  windowSeconds = 60 } },
            // Firestore (SECURITY_SPEC.md §5.2)
            { "saveDeck",         new RateLimitRule { maxCalls = 10, windowSeconds = 60 } },
            { "updateProfile",    new RateLimitRule { maxCalls = 5,  windowSeconds = 60 } },
            // RTDB (SECURITY_SPEC.md §9.1)
            { "rtdbCommand",      new RateLimitRule { maxCalls = 1,  windowSeconds = 1  } },
        };

        // ------------------------------------------------------------------
        // Jailbreak検知パス (SECURITY_SPEC.md §2.3, iOS)
        // ------------------------------------------------------------------

        public static readonly string[] JailbreakPaths =
        {
            "/Applications/Cydia.app",
            "/Library/MobileSubstrate/MobileSubstrate.dylib",
            "/bin/bash",
            "/usr/sbin/sshd",
            "/etc/apt",
            "/private/var/lib/apt/",
            "/usr/bin/ssh",
        };

        /// <summary>
        /// Jailbreak検知を実行する (SECURITY_SPEC.md §2.3)
        /// 検知しても警告表示のみ、BAN対象外。
        /// </summary>
        public static bool DetectJailbreak()
        {
#if UNITY_IOS && !UNITY_EDITOR
            foreach (var path in JailbreakPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    Debug.LogWarning($"[Security] Jailbreak indicator detected: {path}");
                    return true;
                }
            }
#endif
            return false;
        }
    }

    // ------------------------------------------------------------------
    // Rate Limit (SECURITY_SPEC.md §5)
    // ------------------------------------------------------------------

    [Serializable]
    public class RateLimitRule
    {
        public int maxCalls;
        public int windowSeconds;
    }

    /// <summary>
    /// クライアントサイドのレート制限トラッカー。
    /// サーバー側のレート制限 (Cloud Functions) を補完する。
    /// </summary>
    public class RateLimiter
    {
        readonly Dictionary<string, List<float>> _callHistory = new();

        /// <summary>
        /// 呼び出しが許可されるかを判定し、許可なら記録する。
        /// </summary>
        public bool TryCall(string functionName)
        {
            if (!SecurityConfig.RateLimits.TryGetValue(functionName, out var rule))
                return true; // ルールなしなら許可

            if (!_callHistory.TryGetValue(functionName, out var history))
            {
                history = new List<float>();
                _callHistory[functionName] = history;
            }

            float now = Time.realtimeSinceStartup;
            float windowStart = now - rule.windowSeconds;

            // ウィンドウ外の古いエントリを削除
            history.RemoveAll(t => t < windowStart);

            if (history.Count >= rule.maxCalls)
            {
                Debug.LogWarning($"[RateLimiter] Rate limit exceeded: {functionName} ({history.Count}/{rule.maxCalls} in {rule.windowSeconds}s)");
                return false;
            }

            history.Add(now);
            return true;
        }

        /// <summary>
        /// 次に呼び出し可能になるまでの待機時間 (秒) を返す。
        /// </summary>
        public float GetCooldown(string functionName)
        {
            if (!SecurityConfig.RateLimits.TryGetValue(functionName, out var rule))
                return 0f;

            if (!_callHistory.TryGetValue(functionName, out var history))
                return 0f;

            float now = Time.realtimeSinceStartup;
            float windowStart = now - rule.windowSeconds;
            history.RemoveAll(t => t < windowStart);

            if (history.Count < rule.maxCalls) return 0f;

            // 最も古い呼び出しがウィンドウから外れるまでの時間
            float oldestInWindow = history[0];
            return (oldestInWindow + rule.windowSeconds) - now;
        }

        /// <summary>
        /// 全履歴をクリアする。
        /// </summary>
        public void Reset()
        {
            _callHistory.Clear();
        }
    }

    // ------------------------------------------------------------------
    // Command Integrity (SECURITY_SPEC.md §2.2)
    // ------------------------------------------------------------------

    /// <summary>
    /// コマンド整合性検証: HMAC署名 + シーケンスナンバー (SECURITY_SPEC.md §2.2)
    /// パケット改竄防止 + リプレイ攻撃防止。
    /// </summary>
    public class CommandIntegrity
    {
        int _sequenceNumber;
        byte[] _hmacKey;

        public int CurrentSequence => _sequenceNumber;

        /// <summary>
        /// セッション開始時にHMACキーを設定する。
        /// サーバーとの認証完了後に共有キーを受け取る想定。
        /// </summary>
        public void Initialize(byte[] sharedKey)
        {
            _hmacKey = sharedKey;
            _sequenceNumber = 0;
        }

        /// <summary>
        /// コマンドに署名を付与する。シーケンスナンバーも自動インクリメント。
        /// </summary>
        public (string signature, int sequence) SignCommand(string commandJson)
        {
            _sequenceNumber++;

            string payload = $"{_sequenceNumber}:{commandJson}";
            string signature = ComputeHmac(payload);

            return (signature, _sequenceNumber);
        }

        /// <summary>
        /// サーバーから受信したコマンドの署名を検証する。
        /// </summary>
        public bool VerifySignature(string commandJson, int sequence, string signature)
        {
            string payload = $"{sequence}:{commandJson}";
            string expected = ComputeHmac(payload);
            return signature == expected;
        }

        /// <summary>
        /// シーケンスナンバーが単調増加であることを検証する (リプレイ攻撃防止)。
        /// </summary>
        public bool ValidateSequence(int receivedSequence)
        {
            return receivedSequence > _sequenceNumber;
        }

        string ComputeHmac(string payload)
        {
            if (_hmacKey == null || _hmacKey.Length == 0)
            {
                Debug.LogWarning("[CommandIntegrity] HMAC key not initialized");
                return "";
            }

            using var hmac = new HMACSHA256(_hmacKey);
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToBase64String(hash);
        }
    }

    // ------------------------------------------------------------------
    // Client-Side Checksum (SECURITY_SPEC.md §2.2 — メモリ改竄検知)
    // ------------------------------------------------------------------

    /// <summary>
    /// 重要値のチェックサム検証 (SECURITY_SPEC.md §2.2)
    /// CP、手札枚数等の改竄を検知する補助防御。
    /// </summary>
    public class IntegrityChecksum
    {
        readonly Dictionary<string, int> _checksums = new();

        /// <summary>
        /// 値を登録し、チェックサムを記録する。
        /// </summary>
        public void Register(string key, int value)
        {
            _checksums[key] = ComputeChecksum(key, value);
        }

        /// <summary>
        /// 値が改竄されていないかを検証する。
        /// </summary>
        public bool Verify(string key, int currentValue)
        {
            if (!_checksums.TryGetValue(key, out int storedChecksum))
                return true; // 未登録の場合はスキップ

            int expected = ComputeChecksum(key, currentValue);
            if (expected != storedChecksum)
            {
                Debug.LogWarning($"[IntegrityChecksum] Tampering detected for '{key}': expected={storedChecksum}, actual={expected}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 全登録値をクリアする。
        /// </summary>
        public void Clear()
        {
            _checksums.Clear();
        }

        static int ComputeChecksum(string key, int value)
        {
            // 単純なハッシュ (本番ではより堅牢なアルゴリズムを検討)
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + key.GetHashCode();
                hash = hash * 31 + value;
                hash = hash * 31 + 0x5A5A5A5A; // ソルト
                return hash;
            }
        }
    }
}
