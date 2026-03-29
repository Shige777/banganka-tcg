using System;
using System.Collections.Generic;
using Banganka.Core.Data;

namespace Banganka.Core.Economy
{
    /// <summary>
    /// パック開封システム (MONETIZATION_DESIGN.md §4)
    /// スロット1-4: C=75%, R=20%, SR=4%, SSR=1%
    /// スロット5:   R=80%, SR=16%, SSR=4% (R以上確定)
    /// 天井: 50パックでSSR確定
    /// 無料パック: 24時間ごとに1パック補充、最大2ストック
    /// </summary>
    public static class PackSystem
    {
        // 無料パック設定
        public const int FreePackMaxStock = 2;
        public const double FreePackIntervalHours = 24.0;

        static readonly System.Random _rng = new();

        public const int CardsPerPack = 5;
        public const int PackGoldCost = 500;
        public const int PackPremiumCost = 50;

        // 旧イベント（後方互換）
        public static event Action<List<CardData>> OnPackOpened;
        // 新イベント（演出用メタデータ付き）
        public static event Action<PackOpenResult> OnPackOpenedEx;

        // スロット1-4 レアリティ確率 (MONETIZATION_DESIGN.md §4.4)
        static readonly (string rarity, float weight)[] NormalWeights =
        {
            ("C",   0.75f),
            ("R",   0.20f),
            ("SR",  0.04f),
            ("SSR", 0.01f),
        };

        // スロット5 レアリティ確率 (R以上確定)
        static readonly (string rarity, float weight)[] GuaranteedWeights =
        {
            ("R",   0.80f),
            ("SR",  0.16f),
            ("SSR", 0.04f),
        };

        // 重複変換ゴールド (MONETIZATION_DESIGN.md §4.6)
        static readonly Dictionary<string, int> DuplicateGold = new()
        {
            { "C", 30 },
            { "R", 80 },
            { "SR", 200 },
            { "SSR", 500 },
        };

        // 天井: 50パック (MONETIZATION_DESIGN.md §4.5)
        const int PityThreshold = 50;
        static int _packsSinceSSR;

        /// <summary>パック開封（テスト用にRNGシード指定可）</summary>
        public static PackOpenResult OpenPack(Aspect? aspectPickup = null, System.Random rng = null)
        {
            var r = rng ?? _rng;
            var result = new PackOpenResult();
            _packsSinceSSR++;

            for (int i = 0; i < CardsPerPack; i++)
            {
                bool isLastSlot = (i == CardsPerPack - 1);
                bool forcePity = isLastSlot && _packsSinceSSR >= PityThreshold;

                string rarity;
                if (forcePity)
                {
                    rarity = "SSR";
                    result.hasPityTriggered = true;
                }
                else
                {
                    rarity = RollRarity(isLastSlot ? GuaranteedWeights : NormalWeights, r);
                }

                if (rarity == "SSR") _packsSinceSSR = 0;

                var card = PickCardOfRarity(rarity, aspectPickup, r);
                if (card == null) continue;

                var entry = new PackCardEntry
                {
                    card = card,
                    rarity = rarity,
                    slotIndex = i,
                };

                int owned = PlayerData.Instance.GetCardCount(card.id);
                entry.isNew = (owned == 0);

                if (owned >= 3)
                {
                    // 3枚超 → ゴールド変換 (カードは追加しない)
                    entry.isDuplicate = true;
                    int gold = DuplicateGold.TryGetValue(rarity, out int g) ? g : 0;
                    entry.goldConverted = gold;
                    result.totalGoldConverted += gold;
                    PlayerData.Instance.gold += gold;
                }
                else
                {
                    PlayerData.Instance.AddCard(card.id);
                }

                result.cards.Add(entry);
            }

            // 両イベントを発火（後方互換 + 新演出システム）
            var legacyList = new List<CardData>();
            foreach (var e in result.cards) legacyList.Add(e.card);
            OnPackOpened?.Invoke(legacyList);
            OnPackOpenedEx?.Invoke(result);

            return result;
        }

        /// <summary>天井カウンターの現在値</summary>
        public static int PacksSinceSSR => _packsSinceSSR;

        /// <summary>天井までの残りパック数</summary>
        public static int PacksUntilPity => Math.Max(0, PityThreshold - _packsSinceSSR);

        /// <summary>テスト用: 天井カウンターをリセット</summary>
        public static void ResetPityCounter() => _packsSinceSSR = 0;

        static string RollRarity((string rarity, float weight)[] weights, System.Random r)
        {
            float roll = (float)r.NextDouble();
            float cumulative = 0;
            foreach (var (rarity, weight) in weights)
            {
                cumulative += weight;
                if (roll <= cumulative) return rarity;
            }
            return weights[0].rarity;
        }

        // ================================================================
        // 無料パックタイマー
        // ================================================================

        /// <summary>未受取の無料パックストックを更新して返す</summary>
        public static int RefreshFreePackStock()
        {
            var pd = PlayerData.Instance;
            if (pd.freePackStock >= FreePackMaxStock) return pd.freePackStock;

            if (string.IsNullOrEmpty(pd.lastFreePackClaimTime))
            {
                // 初回: 即1パック付与
                pd.freePackStock = 1;
                pd.lastFreePackClaimTime = DateTime.UtcNow.ToString("o");
                PlayerData.Save();
                return pd.freePackStock;
            }

            if (!DateTime.TryParse(pd.lastFreePackClaimTime, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime lastClaim))
                return pd.freePackStock;

            double hoursSince = (DateTime.UtcNow - lastClaim).TotalHours;
            int newPacks = (int)(hoursSince / FreePackIntervalHours);
            if (newPacks <= 0) return pd.freePackStock;

            pd.freePackStock = Math.Min(pd.freePackStock + newPacks, FreePackMaxStock);
            // タイムスタンプを消費分だけ進める（端数を保持）
            pd.lastFreePackClaimTime = lastClaim.AddHours(newPacks * FreePackIntervalHours).ToString("o");
            PlayerData.Save();
            return pd.freePackStock;
        }

        /// <summary>無料パックを受け取れるか</summary>
        public static bool CanClaimFreePack() => RefreshFreePackStock() > 0;

        /// <summary>無料パックを開封</summary>
        public static PackOpenResult ClaimFreePack(Aspect? aspectPickup = null)
        {
            RefreshFreePackStock();
            var pd = PlayerData.Instance;
            if (pd.freePackStock <= 0) return null;

            pd.freePackStock--;
            if (pd.freePackStock < FreePackMaxStock)
                pd.lastFreePackClaimTime = DateTime.UtcNow.ToString("o");
            PlayerData.Save();

            return OpenPack(aspectPickup);
        }

        /// <summary>次の無料パックまでの残り時間。ストック満タンならTimeSpan.Zero</summary>
        public static TimeSpan GetTimeUntilNextFreePack()
        {
            var pd = PlayerData.Instance;
            if (pd.freePackStock >= FreePackMaxStock) return TimeSpan.Zero;

            if (string.IsNullOrEmpty(pd.lastFreePackClaimTime))
                return TimeSpan.Zero; // RefreshFreePackStockで初期化される

            if (!DateTime.TryParse(pd.lastFreePackClaimTime, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime lastClaim))
                return TimeSpan.FromHours(FreePackIntervalHours);

            DateTime nextPack = lastClaim.AddHours(FreePackIntervalHours);
            TimeSpan remaining = nextPack - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        static CardData PickCardOfRarity(string rarity, Aspect? aspectPickup, System.Random r)
        {
            var candidates = new List<CardData>();
            foreach (var kv in CardDatabase.AllCards)
            {
                if (kv.Value.rarity != rarity) continue;

                if (aspectPickup.HasValue && kv.Value.aspect != aspectPickup.Value)
                {
                    // ピックアップ外のカードは50%の確率で候補に含める
                    if (r.NextDouble() < 0.5) continue;
                }
                candidates.Add(kv.Value);
            }

            if (candidates.Count == 0)
            {
                // フォールバック: レアリティのみでフィルタ
                foreach (var kv in CardDatabase.AllCards)
                    if (kv.Value.rarity == rarity) candidates.Add(kv.Value);
            }

            if (candidates.Count == 0) return null;
            return candidates[r.Next(candidates.Count)];
        }
    }
}
