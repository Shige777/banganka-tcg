using System;
using System.Collections.Generic;
using UnityEngine;
#if FIREBASE_ENABLED
using Firebase.Analytics;
#endif

namespace Banganka.Core.Analytics
{
    /// <summary>
    /// Firebase Analytics イベント送信 (ANALYTICS_SPEC.md)
    /// 24コアイベント + バランスKPI + パフォーマンス計測
    /// </summary>
    public static class AnalyticsService
    {
        static bool _initialized;
        static bool _enabled = true;

        // User properties
        static readonly Dictionary<string, object> _userProperties = new();

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _enabled = Config.FirebaseConfig.EnableAnalytics;

            if (!_enabled)
            {
                Debug.Log("[Analytics] Disabled in development environment");
                return;
            }

#if FIREBASE_ENABLED
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
#endif
            Debug.Log("[Analytics] Initialized");
        }

        // ====================================================================
        // Onboarding Events (§3.1)
        // ====================================================================

        public static void LogAppFirstOpen()
            => LogEvent("app_first_open");

        public static void LogTutorialBegin()
            => LogEvent("tutorial_begin");

        public static void LogTutorialStep(int step, string stepName)
            => LogEvent("tutorial_step", new() { { "step", step }, { "step_name", stepName } });

        public static void LogTutorialComplete(float durationSec)
            => LogEvent("tutorial_complete", new() { { "duration_sec", durationSec } });

        public static void LogTutorialSkip(int lastStep)
            => LogEvent("tutorial_skip", new() { { "last_step", lastStep } });

        // ====================================================================
        // Battle Events (§3.2)
        // ====================================================================

        public static void LogMatchStart(string matchId, string mode, string leaderId, string opponentLeaderId)
            => LogEvent("match_start", new()
            {
                { "match_id", matchId },
                { "mode", mode }, // pvp, bot_easy, bot_normal, bot_hard, friend
                { "leader_id", leaderId },
                { "opponent_leader_id", opponentLeaderId },
            });

        public static void LogMatchEnd(string matchId, string result, string reason,
            int totalTurns, int myHp, int opponentHp, float durationSec)
            => LogEvent("match_end", new()
            {
                { "match_id", matchId },
                { "result", result }, // win, lose, draw
                { "reason", reason }, // ko, hp_compare, surrender, timeout, disconnect
                { "total_turns", totalTurns },
                { "my_hp", myHp },
                { "opponent_hp", opponentHp },
                { "duration_sec", durationSec },
            });

        public static void LogCardPlayed(string cardId, string cardType, string aspect, int cpCost, int turn)
            => LogEvent("card_played", new()
            {
                { "card_id", cardId },
                { "card_type", cardType },
                { "aspect", aspect },
                { "cp_cost", cpCost },
                { "turn", turn },
            });

        public static void LogAttackDeclared(string attackerType, string targetType, bool isDirectHit, int turn)
            => LogEvent("attack_declared", new()
            {
                { "attacker_type", attackerType },
                { "target_type", targetType },
                { "is_direct_hit", isDirectHit },
                { "turn", turn },
            });

        public static void LogDirectHit(int hpBefore, int hpAfter, int turn)
            => LogEvent("direct_hit", new()
            {
                { "hp_before", hpBefore },
                { "hp_after", hpAfter },
                { "turn", turn },
            });

        public static void LogSurrender(int turn, int myHp, int opponentHp)
            => LogEvent("surrender", new()
            {
                { "turn", turn },
                { "my_hp", myHp },
                { "opponent_hp", opponentHp },
            });

        public static void LogTimeout(int turn, int consecutiveCount)
            => LogEvent("timeout", new()
            {
                { "turn", turn },
                { "consecutive_count", consecutiveCount },
            });

        public static void LogDisconnect(string matchId, float durationSec)
            => LogEvent("disconnect", new()
            {
                { "match_id", matchId },
                { "duration_sec", durationSec },
            });

        // ====================================================================
        // Shop / IAP Events (§3.3)
        // ====================================================================

        public static void LogShopView(string tab)
            => LogEvent("shop_view", new() { { "tab", tab } });

        public static void LogPackPurchase(string packId, string currency, int cost)
            => LogEvent("pack_purchase", new()
            {
                { "pack_id", packId },
                { "currency", currency },
                { "cost", cost },
            });

        public static void LogPackOpen(string packId, int ssrCount, int srCount, int rCount, int cCount)
            => LogEvent("pack_open", new()
            {
                { "pack_id", packId },
                { "ssr_count", ssrCount },
                { "sr_count", srCount },
                { "r_count", rCount },
                { "c_count", cCount },
            });

        public static void LogIapBegin(string productId, float priceLocal)
            => LogEvent("iap_begin", new()
            {
                { "product_id", productId },
                { "price_local", priceLocal },
            });

        public static void LogIapComplete(string productId, string transactionId)
            => LogEvent("iap_complete", new()
            {
                { "product_id", productId },
                { "transaction_id", transactionId },
            });

        public static void LogIapFail(string productId, string reason)
            => LogEvent("iap_fail", new()
            {
                { "product_id", productId },
                { "reason", reason },
            });

        // ====================================================================
        // Navigation Events (§3.4)
        // ====================================================================

        public static void LogScreenView(string screenName)
            => LogEvent("screen_view", new() { { "screen_name", screenName } });

        public static void LogSessionStart()
            => LogEvent("session_start");

        public static void LogSessionEnd(float durationSec)
            => LogEvent("session_end", new() { { "duration_sec", durationSec } });

        // ====================================================================
        // Story Events (§3.5)
        // ====================================================================

        public static void LogStoryChapterStart(string chapterId)
            => LogEvent("story_chapter_start", new() { { "chapter_id", chapterId } });

        public static void LogStoryChapterComplete(string chapterId, float durationSec)
            => LogEvent("story_chapter_complete", new()
            {
                { "chapter_id", chapterId },
                { "duration_sec", durationSec },
            });

        // ====================================================================
        // Performance Events (§5 — 10% sampling)
        // ====================================================================

        public static void LogFpsDrop(float fps, string screen)
        {
            if (UnityEngine.Random.value > 0.1f) return; // 10% sampling
            LogEvent("perf_fps_drop", new() { { "fps", fps }, { "screen", screen } });
        }

        public static void LogLoadTime(string screen, float seconds)
        {
            if (UnityEngine.Random.value > 0.1f) return;
            LogEvent("perf_load_time", new() { { "screen", screen }, { "seconds", seconds } });
        }

        public static void LogMemoryWarning(float usageMB)
            => LogEvent("perf_memory_warning", new() { { "usage_mb", usageMB } });

        // ====================================================================
        // User Properties
        // ====================================================================

        public static void SetUserProperty(string key, string value)
        {
            _userProperties[key] = value;
            if (!_enabled) return;
#if FIREBASE_ENABLED
            FirebaseAnalytics.SetUserProperty(key, value);
#endif
        }

        public static void UpdateUserProperties(bool tutorialCompleted, int totalMatches, string favoriteAspect)
        {
            SetUserProperty("tutorial_completed", tutorialCompleted.ToString());
            SetUserProperty("total_matches", totalMatches.ToString());
            SetUserProperty("favorite_aspect", favoriteAspect);
        }

        // ====================================================================
        // Core
        // ====================================================================

        static void LogEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (!_initialized) Initialize();
            if (!_enabled)
            {
                if (Config.FirebaseConfig.VerboseLogging)
                    Debug.Log($"[Analytics] {eventName} {FormatParams(parameters)}");
                return;
            }

#if FIREBASE_ENABLED
            if (parameters != null && parameters.Count > 0)
            {
                var fbParams = new Parameter[parameters.Count];
                int idx = 0;
                foreach (var kv in parameters)
                {
                    fbParams[idx++] = kv.Value switch
                    {
                        int i => new Parameter(kv.Key, i),
                        long l => new Parameter(kv.Key, l),
                        float f => new Parameter(kv.Key, f),
                        double d => new Parameter(kv.Key, d),
                        bool b => new Parameter(kv.Key, b ? 1 : 0),
                        _ => new Parameter(kv.Key, kv.Value?.ToString() ?? "")
                    };
                }
                FirebaseAnalytics.LogEvent(eventName, fbParams);
            }
            else
            {
                FirebaseAnalytics.LogEvent(eventName);
            }
#endif
            if (Config.FirebaseConfig.VerboseLogging)
                Debug.Log($"[Analytics] {eventName} {FormatParams(parameters)}");
        }

        static string FormatParams(Dictionary<string, object> p)
        {
            if (p == null || p.Count == 0) return "";
            var parts = new List<string>();
            foreach (var kv in p)
                parts.Add($"{kv.Key}={kv.Value}");
            return "{" + string.Join(", ", parts) + "}";
        }
    }
}
