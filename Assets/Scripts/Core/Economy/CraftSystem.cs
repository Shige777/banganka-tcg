using System;
using System.Collections.Generic;
using Banganka.Core.Data;

namespace Banganka.Core.Economy
{
    /// <summary>
    /// カード生成・分解システム (CARD_SCHEMA.md §6.1)
    /// </summary>
    public static class CraftSystem
    {
        // 生成コスト (ゴールド)
        static readonly Dictionary<string, int> CraftCost = new()
        {
            { "C", 100 },
            { "R", 300 },
            { "SR", 800 },
            { "SSR", 2400 },
        };

        // 分解で得られるゴールド
        static readonly Dictionary<string, int> DismantleGain = new()
        {
            { "C", 25 },
            { "R", 75 },
            { "SR", 200 },
            { "SSR", 600 },
        };

        public static int GetCraftCost(string rarity)
        {
            return CraftCost.TryGetValue(rarity, out int cost) ? cost : 9999;
        }

        public static int GetDismantleGain(string rarity)
        {
            return DismantleGain.TryGetValue(rarity, out int gain) ? gain : 0;
        }

        public static bool CanCraft(string cardId)
        {
            if (!CardDatabase.AllCards.TryGetValue(cardId, out var card)) return false;
            int owned = PlayerData.Instance.GetCardCount(cardId);
            if (owned >= 3) return false; // max copies
            int cost = GetCraftCost(card.rarity);
            return CurrencyManager.CanAfford(cost);
        }

        public static bool Craft(string cardId)
        {
            if (!CanCraft(cardId)) return false;
            var card = CardDatabase.AllCards[cardId];
            int cost = GetCraftCost(card.rarity);
            if (!CurrencyManager.Spend(cost)) return false;
            PlayerData.Instance.AddCard(cardId);
            return true;
        }

        public static bool CanDismantle(string cardId)
        {
            return PlayerData.Instance.GetCardCount(cardId) > 0;
        }

        public static bool Dismantle(string cardId)
        {
            if (!CanDismantle(cardId)) return false;
            var card = CardDatabase.AllCards[cardId];
            int gain = GetDismantleGain(card.rarity);

            // Remove one copy
            PlayerData.Instance.cardCollection[cardId]--;
            if (PlayerData.Instance.cardCollection[cardId] <= 0)
                PlayerData.Instance.cardCollection.Remove(cardId);

            CurrencyManager.AddGold(gain);
            return true;
        }

        public static int BulkDismantle(List<string> cardIds)
        {
            int totalGain = 0;
            foreach (var id in cardIds)
            {
                if (Dismantle(id))
                {
                    var card = CardDatabase.AllCards[id];
                    totalGain += GetDismantleGain(card.rarity);
                }
            }
            return totalGain;
        }
    }
}
