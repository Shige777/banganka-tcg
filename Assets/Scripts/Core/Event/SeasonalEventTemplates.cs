using System;
using System.Collections.Generic;

namespace Banganka.Core.Event
{
    /// <summary>
    /// 季節イベントのテンプレート定義。
    /// Remote Configが未設定の場合のフォールバック用。
    /// 年間スケジュールに合わせた定期イベントを自動生成。
    /// </summary>
    public static class SeasonalEventTemplates
    {
        /// <summary>
        /// 現在の日時に基づいて該当する季節イベントを生成。
        /// Remote Configフォールバック用。
        /// </summary>
        public static List<EventData> GetCurrentSeasonalEvents()
        {
            var events = new List<EventData>();
            var now = DateTime.UtcNow;
            int month = now.Month;

            // 各季節で該当するイベントを追加
            var seasonal = GetSeasonalTemplate(month);
            if (seasonal != null)
                events.Add(seasonal);

            // 月替わりの構築制限イベント（常時1つ開催）
            var restricted = GetMonthlyRestricted(now);
            if (restricted != null)
                events.Add(restricted);

            return events;
        }

        static EventData GetSeasonalTemplate(int month)
        {
            return month switch
            {
                // 正月 (1月)
                1 => CreateEvent("seasonal_newyear", EventType.Seasonal,
                    "新年の果求戦", "新年を祝い、交界に挑め",
                    MonthStart(1), MonthEnd(1, 15),
                    new EventRules { deckSize = 34, specialRules = Array.Empty<string>() },
                    new List<EventReward>
                    {
                        Reward(3, "gold", 500),
                        Reward(5, "pack_ticket", 1),
                        Reward(10, "gold", 1500),
                        Reward(15, "cosmetic", 0, "sleeve_newyear"),
                        Reward(20, "premium", 50),
                    }),

                // 桜祭り (3-4月)
                3 or 4 => CreateEvent("seasonal_sakura", EventType.Seasonal,
                    "桜花の交界", "桜舞う季節の特別対戦",
                    MonthStart(3), MonthEnd(4, 15),
                    new EventRules { deckSize = 34, specialRules = Array.Empty<string>() },
                    new List<EventReward>
                    {
                        Reward(3, "gold", 500),
                        Reward(5, "pack_ticket", 1),
                        Reward(10, "gold", 1500),
                        Reward(15, "cosmetic", 0, "field_sakura"),
                        Reward(25, "premium", 80),
                    }),

                // 夏祭り (7-8月)
                7 or 8 => CreateEvent("seasonal_summer", EventType.Seasonal,
                    "灼熱の闘技場", "真夏の激闘イベント",
                    MonthStart(7), MonthEnd(8, 15),
                    new EventRules { deckSize = 34, specialRules = Array.Empty<string>() },
                    new List<EventReward>
                    {
                        Reward(3, "gold", 500),
                        Reward(5, "pack_ticket", 1),
                        Reward(10, "gold", 2000),
                        Reward(15, "cosmetic", 0, "sleeve_summer"),
                        Reward(20, "cosmetic", 0, "field_volcano"),
                        Reward(30, "premium", 100),
                    }),

                // ハロウィン (10月)
                10 => CreateEvent("seasonal_halloween", EventType.Seasonal,
                    "幻影の果求戦", "妖しき仮面の交界",
                    MonthStart(10), MonthEnd(10, 31),
                    new EventRules { deckSize = 34, specialRules = new[] { "single_aspect" } },
                    new List<EventReward>
                    {
                        Reward(3, "gold", 500),
                        Reward(5, "pack_ticket", 1),
                        Reward(10, "gold", 1500),
                        Reward(15, "cosmetic", 0, "sleeve_halloween"),
                        Reward(20, "premium", 50),
                    }),

                // 年末 (12月)
                12 => CreateEvent("seasonal_winter", EventType.Seasonal,
                    "聖夜の交界", "冬の祝祭対戦イベント",
                    MonthStart(12), MonthEnd(12, 31),
                    new EventRules { deckSize = 34, specialRules = Array.Empty<string>() },
                    new List<EventReward>
                    {
                        Reward(3, "gold", 800),
                        Reward(5, "pack_ticket", 2),
                        Reward(10, "gold", 2000),
                        Reward(15, "cosmetic", 0, "field_celestial"),
                        Reward(20, "cosmetic", 0, "sleeve_winter"),
                        Reward(30, "premium", 120),
                    }),

                _ => null
            };
        }

        /// <summary>月替わり構築制限イベント</summary>
        static EventData GetMonthlyRestricted(DateTime now)
        {
            int month = now.Month;
            string ruleType = (month % 3) switch
            {
                0 => "low_cost_only",    // 低コスト戦
                1 => "single_aspect",    // 単願相杯
                _ => "no_algorithm"      // 界律なし杯
            };
            string title = ruleType switch
            {
                "low_cost_only" => "低コスト戦",
                "single_aspect" => "単願相杯",
                _ => "界律なし杯"
            };

            return CreateEvent($"restricted_{now.Year}_{month:D2}", EventType.RestrictedBattle,
                title, $"{month}月の構築制限イベント",
                MonthStart(month), MonthEnd(month),
                new EventRules { deckSize = 34, specialRules = new[] { ruleType } },
                new List<EventReward>
                {
                    Reward(3, "gold", 300),
                    Reward(5, "pack_ticket", 1),
                    Reward(10, "gold", 1000),
                });
        }

        // ================================================================
        // Helpers
        // ================================================================

        static EventData CreateEvent(string id, EventType type, string title, string subtitle,
            string start, string end, EventRules rules, List<EventReward> rewards)
        {
            return new EventData
            {
                eventId = id,
                type = type,
                title = title,
                subtitle = subtitle,
                startAt = start,
                endAt = end,
                rules = rules,
                rewards = rewards
            };
        }

        static EventReward Reward(int threshold, string type, int amount, string itemId = null)
        {
            return new EventReward
            {
                threshold = threshold,
                rewardType = type,
                amount = amount,
                itemId = itemId
            };
        }

        static string MonthStart(int month)
        {
            int year = DateTime.UtcNow.Year;
            return new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).ToString("o");
        }

        static string MonthEnd(int month, int day = 0)
        {
            int year = DateTime.UtcNow.Year;
            if (day <= 0) day = DateTime.DaysInMonth(year, month);
            return new DateTime(year, month, day, 23, 59, 59, DateTimeKind.Utc).ToString("o");
        }
    }
}
