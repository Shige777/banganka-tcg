using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Banganka.Core.Economy
{
    [Serializable]
    public class ShopItem
    {
        public string itemId;
        public string name;
        public string description;
        public string category; // "pack", "currency", "bundle", "daily", "weekly"
        public int goldCost;
        public int premiumCost;
        public string iapProductId; // for real-money IAP
        public bool isLimited;
        public int stockRemaining;
        public int originalGoldCost;   // 割引前価格 (表示用)
        public int originalPremiumCost; // 割引前価格 (表示用)
    }

    /// <summary>
    /// ショップ管理 (MONETIZATION_DESIGN.md, ASSET_SHOP.md)
    /// パック / 通貨パック / スターターバンドル / デイリーショップ / ウィークリー特集
    /// </summary>
    public static class ShopSystem
    {
        static List<ShopItem> _shopItems;
        static List<ShopItem> _dailyRotation;
        static List<ShopItem> _weeklyFeatured;

        const string DailyShopDateKey = "daily_shop_date";
        const string WeeklyShopWeekKey = "weekly_shop_week";
        const string DailyPurchasedPrefix = "daily_purchased_";
        const string WeeklyPurchasedPrefix = "weekly_purchased_";

        public static IReadOnlyList<ShopItem> AllItems
        {
            get
            {
                if (_shopItems == null) InitShop();
                return _shopItems;
            }
        }

        public static IReadOnlyList<ShopItem> DailyRotation
        {
            get
            {
                if (_dailyRotation == null) RefreshDailyShop();
                return _dailyRotation;
            }
        }

        public static IReadOnlyList<ShopItem> WeeklyFeatured
        {
            get
            {
                if (_weeklyFeatured == null) RefreshWeeklyShop();
                return _weeklyFeatured;
            }
        }

        public static event Action OnShopUpdated;
        public static event Action OnDailyRefresh;
        public static event Action OnWeeklyRefresh;

        static void InitShop()
        {
            _shopItems = new List<ShopItem>
            {
                // Packs
                new() { itemId = "pack_standard", name = "標準パック", description = "カード5枚入り", category = "pack", goldCost = 200 },
                new() { itemId = "pack_contest", name = "曙赤ピックアップ", description = "曙赤カード確率UP", category = "pack", goldCost = 250 },
                new() { itemId = "pack_whisper", name = "空青ピックアップ", description = "空青カード確率UP", category = "pack", goldCost = 250 },
                new() { itemId = "pack_weave", name = "穏緑ピックアップ", description = "穏緑カード確率UP", category = "pack", goldCost = 250 },
                new() { itemId = "pack_verse", name = "妖紫ピックアップ", description = "妖紫カード確率UP", category = "pack", goldCost = 250 },
                new() { itemId = "pack_manifest", name = "遊黄ピックアップ", description = "遊黄カード確率UP", category = "pack", goldCost = 250 },
                new() { itemId = "pack_hush", name = "玄白ピックアップ", description = "玄白カード確率UP", category = "pack", goldCost = 250 },

                // IAP Currency Packs
                new() { itemId = "iap_crystal_small", name = "願晶×100", description = "100願晶", category = "currency", premiumCost = 0, iapProductId = "com.banganka.crystal100" },
                new() { itemId = "iap_crystal_medium", name = "願晶×500+50", description = "550願晶(10%ボーナス)", category = "currency", premiumCost = 0, iapProductId = "com.banganka.crystal500" },
                new() { itemId = "iap_crystal_large", name = "願晶×1000+150", description = "1150願晶(15%ボーナス)", category = "currency", premiumCost = 0, iapProductId = "com.banganka.crystal1000" },
                new() { itemId = "iap_crystal_xl", name = "願晶×2000+400", description = "2400願晶(20%ボーナス)", category = "currency", premiumCost = 0, iapProductId = "com.banganka.crystal2000" },
                new() { itemId = "iap_crystal_xxl", name = "願晶×5000+1500", description = "6500願晶(30%ボーナス)", category = "currency", premiumCost = 0, iapProductId = "com.banganka.crystal5000" },

                // Starter Bundle
                new() { itemId = "bundle_starter", name = "スターターバンドル", description = "パック×10 + 500願晶", category = "bundle", premiumCost = 0, iapProductId = "com.banganka.starter", isLimited = true, stockRemaining = 1 },
            };
        }

        /// <summary>
        /// 起動時やシーン遷移時に呼ぶ。日付/週が変わっていればリフレッシュする。
        /// </summary>
        public static void CheckAndRefreshIfNeeded()
        {
            var now = DateTime.UtcNow;
            string todayStr = now.ToString("yyyy-MM-dd");
            string storedDate = PlayerPrefs.GetString(DailyShopDateKey, "");

            if (storedDate != todayStr)
            {
                RefreshDailyShop();
            }
            else if (_dailyRotation == null)
            {
                // 同日だがメモリにない場合は復元 (アプリ再起動時)
                RefreshDailyShop();
            }

            int currentWeek = GetIsoWeekNumber(now);
            int currentYear = now.Year;
            string weekKey = $"{currentYear}-W{currentWeek:D2}";
            string storedWeek = PlayerPrefs.GetString(WeeklyShopWeekKey, "");

            if (storedWeek != weekKey)
            {
                RefreshWeeklyShop();
            }
            else if (_weeklyFeatured == null)
            {
                RefreshWeeklyShop();
            }
        }

        /// <summary>
        /// 次のデイリーリフレッシュまでの残り時間を返す。
        /// </summary>
        public static TimeSpan GetTimeUntilNextRefresh()
        {
            var now = DateTime.UtcNow;
            var nextMidnight = now.Date.AddDays(1);
            return nextMidnight - now;
        }

        /// <summary>
        /// 次のウィークリーリフレッシュまでの残り時間を返す (次の月曜 00:00 UTC)。
        /// </summary>
        public static TimeSpan GetTimeUntilNextWeeklyRefresh()
        {
            var now = DateTime.UtcNow;
            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7; // 月曜当日は次週
            var nextMonday = now.Date.AddDays(daysUntilMonday);
            return nextMonday - now;
        }

        public static void RefreshDailyShop()
        {
            var now = DateTime.UtcNow;
            string todayStr = now.ToString("yyyy-MM-dd");
            string storedDate = PlayerPrefs.GetString(DailyShopDateKey, "");

            // 同日なら再生成しない (メモリにある場合)
            if (storedDate == todayStr && _dailyRotation != null)
                return;

            // 日付 + 年で決定論的シードを生成
            int seed = now.DayOfYear + now.Year * 1000;
            var rng = new System.Random(seed);
            var allCards = Data.CardDatabase.AllCards.Values.ToList();

            _dailyRotation = new List<ShopItem>();

            // --- Slot 1-2: ランダム単品カード (20%割引) ---
            var usedIndices = new HashSet<int>();
            for (int i = 0; i < 2; i++)
            {
                int idx;
                do { idx = rng.Next(allCards.Count); }
                while (usedIndices.Contains(idx));
                usedIndices.Add(idx);

                var card = allCards[idx];
                int originalCost = CraftSystem.GetCraftCost(card.rarity);
                int discountedCost = originalCost * 80 / 100; // 20% discount
                _dailyRotation.Add(new ShopItem
                {
                    itemId = $"daily_card_{card.id}",
                    name = card.cardName,
                    description = $"[{card.rarity}] デイリー割引 20%OFF",
                    category = "daily",
                    goldCost = discountedCost,
                    originalGoldCost = originalCost,
                    isLimited = true,
                    stockRemaining = 1,
                });
            }

            // --- Slot 3: ランダムパック (15%割引) ---
            string[] packIds = { "pack_standard", "pack_contest", "pack_whisper",
                                 "pack_weave", "pack_verse", "pack_manifest", "pack_hush" };
            string[] packNames = { "標準パック", "曙赤ピックアップ", "空青ピックアップ",
                                   "穏緑ピックアップ", "妖紫ピックアップ", "遊黄ピックアップ", "玄白ピックアップ" };
            int packIdx = rng.Next(packIds.Length);
            int packBasePrice = packIdx == 0 ? 200 : 250;
            int packDiscounted = packBasePrice * 85 / 100; // 15% discount
            _dailyRotation.Add(new ShopItem
            {
                itemId = $"daily_pack_{packIds[packIdx]}",
                name = $"{packNames[packIdx]} (デイリー)",
                description = "デイリー割引 15%OFF",
                category = "daily",
                goldCost = packDiscounted,
                originalGoldCost = packBasePrice,
                isLimited = true,
                stockRemaining = 1,
            });

            // --- Slot 4: ゴールドバンドル (願晶でゴールド購入) ---
            int goldAmount = 500 + rng.Next(4) * 100; // 500 ~ 800
            int bonusGold = goldAmount / 5; // 20% bonus
            _dailyRotation.Add(new ShopItem
            {
                itemId = $"daily_gold_bundle_{goldAmount}",
                name = $"万願金×{goldAmount + bonusGold}",
                description = $"{goldAmount}+{bonusGold}ボーナス ゴールドバンドル",
                category = "daily",
                premiumCost = goldAmount / 5, // 願晶レート: 5ゴールド = 1願晶
                isLimited = true,
                stockRemaining = 1,
            });

            // 購入済みフラグを反映
            RestoreDailyPurchaseState(todayStr);

            // 日付を保存
            PlayerPrefs.SetString(DailyShopDateKey, todayStr);
            PlayerPrefs.Save();

            OnDailyRefresh?.Invoke();
            OnShopUpdated?.Invoke();
        }

        /// <summary>
        /// ウィークリー特集を生成/リフレッシュする。
        /// </summary>
        public static void RefreshWeeklyShop()
        {
            var now = DateTime.UtcNow;
            int weekNum = GetIsoWeekNumber(now);
            int year = now.Year;
            string weekKey = $"{year}-W{weekNum:D2}";
            string storedWeek = PlayerPrefs.GetString(WeeklyShopWeekKey, "");

            // 同週なら再生成しない (メモリにある場合)
            if (storedWeek == weekKey && _weeklyFeatured != null)
                return;

            int seed = weekNum + year * 100;
            var rng = new System.Random(seed);
            var allCards = Data.CardDatabase.AllCards.Values.ToList();

            _weeklyFeatured = new List<ShopItem>();

            // --- Slot 1: SR確定カード (30%割引) ---
            var srCards = allCards.Where(c => c.rarity == "SR").ToList();
            if (srCards.Count > 0)
            {
                var card = srCards[rng.Next(srCards.Count)];
                int originalCost = CraftSystem.GetCraftCost(card.rarity);
                int discountedCost = originalCost * 70 / 100; // 30% discount
                _weeklyFeatured.Add(new ShopItem
                {
                    itemId = $"weekly_sr_{card.id}",
                    name = card.cardName,
                    description = $"[SR] ウィークリー特集 30%OFF",
                    category = "weekly",
                    goldCost = discountedCost,
                    originalGoldCost = originalCost,
                    isLimited = true,
                    stockRemaining = 1,
                });
            }

            // --- Slot 2: スペシャルバンドル (パック×3 + ゴールドのセット) ---
            _weeklyFeatured.Add(new ShopItem
            {
                itemId = $"weekly_bundle_{weekKey}",
                name = "ウィークリーバンドル",
                description = "標準パック×3 + 万願金300",
                category = "weekly",
                premiumCost = 250,
                isLimited = true,
                stockRemaining = 1,
            });

            // 購入済みフラグを反映
            RestoreWeeklyPurchaseState(weekKey);

            // 週を保存
            PlayerPrefs.SetString(WeeklyShopWeekKey, weekKey);
            PlayerPrefs.Save();

            OnWeeklyRefresh?.Invoke();
            OnShopUpdated?.Invoke();
        }

        public static bool Purchase(string itemId)
        {
            var item = _shopItems?.FirstOrDefault(i => i.itemId == itemId)
                ?? _dailyRotation?.FirstOrDefault(i => i.itemId == itemId)
                ?? _weeklyFeatured?.FirstOrDefault(i => i.itemId == itemId);

            if (item == null) return false;
            if (item.isLimited && item.stockRemaining <= 0) return false;

            // IAP items: delegate to IAPManager
            if (!string.IsNullOrEmpty(item.iapProductId))
            {
                if (IAPManager.Instance == null || !IAPManager.Instance.IsInitialized)
                    return false;

                IAPManager.Instance.Purchase(item.iapProductId, success =>
                {
                    if (success && item.isLimited) item.stockRemaining--;
                    OnShopUpdated?.Invoke();
                });
                return true; // purchase initiated (async)
            }

            if (!CurrencyManager.Spend(item.goldCost, item.premiumCost))
                return false;

            if (item.isLimited) item.stockRemaining--;

            // Fulfill purchase
            if (item.category == "pack")
            {
                Data.Aspect? pickup = item.itemId switch
                {
                    "pack_contest" => Data.Aspect.Contest,
                    "pack_whisper" => Data.Aspect.Whisper,
                    "pack_weave" => Data.Aspect.Weave,
                    "pack_verse" => Data.Aspect.Verse,
                    "pack_manifest" => Data.Aspect.Manifest,
                    "pack_hush" => Data.Aspect.Hush,
                    _ => null,
                };
                PackSystem.OpenPack(pickup);
            }
            else if (item.category == "daily")
            {
                FulfillDailyItem(item);
            }
            else if (item.category == "weekly")
            {
                FulfillWeeklyItem(item);
            }

            OnShopUpdated?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        static void FulfillDailyItem(ShopItem item)
        {
            if (item.itemId.StartsWith("daily_card_"))
            {
                string cardId = item.itemId.Substring("daily_card_".Length);
                Data.PlayerData.Instance.AddCard(cardId);
            }
            else if (item.itemId.StartsWith("daily_pack_"))
            {
                string packId = item.itemId.Substring("daily_pack_".Length);
                Data.Aspect? pickup = packId switch
                {
                    "pack_contest" => Data.Aspect.Contest,
                    "pack_whisper" => Data.Aspect.Whisper,
                    "pack_weave" => Data.Aspect.Weave,
                    "pack_verse" => Data.Aspect.Verse,
                    "pack_manifest" => Data.Aspect.Manifest,
                    "pack_hush" => Data.Aspect.Hush,
                    _ => null,
                };
                PackSystem.OpenPack(pickup);
            }
            else if (item.itemId.StartsWith("daily_gold_bundle_"))
            {
                // ゴールドバンドル: item.description からゴールド総量を付与
                string amountStr = item.itemId.Substring("daily_gold_bundle_".Length);
                if (int.TryParse(amountStr, out int baseGold))
                {
                    int bonus = baseGold / 5;
                    CurrencyManager.AddGold(baseGold + bonus);
                }
            }

            // 購入済みフラグ保存
            string todayStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
            PlayerPrefs.SetInt($"{DailyPurchasedPrefix}{todayStr}_{item.itemId}", 1);
            PlayerPrefs.Save();
        }

        static void FulfillWeeklyItem(ShopItem item)
        {
            if (item.itemId.StartsWith("weekly_sr_"))
            {
                string cardId = item.itemId.Substring("weekly_sr_".Length);
                Data.PlayerData.Instance.AddCard(cardId);
            }
            else if (item.itemId.StartsWith("weekly_bundle_"))
            {
                // スペシャルバンドル: パック×3 + ゴールド300
                PackSystem.OpenPack(null);
                PackSystem.OpenPack(null);
                PackSystem.OpenPack(null);
                CurrencyManager.AddGold(300);
            }

            // 購入済みフラグ保存
            var now = DateTime.UtcNow;
            int weekNum = GetIsoWeekNumber(now);
            string weekKey = $"{now.Year}-W{weekNum:D2}";
            PlayerPrefs.SetInt($"{WeeklyPurchasedPrefix}{weekKey}_{item.itemId}", 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// PlayerPrefsから当日の購入済みデイリーアイテムを復元し、stockRemaining=0にする。
        /// </summary>
        static void RestoreDailyPurchaseState(string dateStr)
        {
            if (_dailyRotation == null) return;
            foreach (var item in _dailyRotation)
            {
                if (PlayerPrefs.GetInt($"{DailyPurchasedPrefix}{dateStr}_{item.itemId}", 0) == 1)
                {
                    item.stockRemaining = 0;
                }
            }
        }

        /// <summary>
        /// PlayerPrefsから当週の購入済みウィークリーアイテムを復元し、stockRemaining=0にする。
        /// </summary>
        static void RestoreWeeklyPurchaseState(string weekKey)
        {
            if (_weeklyFeatured == null) return;
            foreach (var item in _weeklyFeatured)
            {
                if (PlayerPrefs.GetInt($"{WeeklyPurchasedPrefix}{weekKey}_{item.itemId}", 0) == 1)
                {
                    item.stockRemaining = 0;
                }
            }
        }

        /// <summary>
        /// ISO 8601 週番号を取得する。
        /// </summary>
        static int GetIsoWeekNumber(DateTime date)
        {
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }
    }
}
