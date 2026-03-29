using System;
using System.Collections.Generic;
using Banganka.Core.Data;

namespace Banganka.Core.Economy
{
    /// <summary>
    /// 願道パス (バトルパス): 30レベル (MONETIZATION_DESIGN.md)
    /// </summary>
    public static class BattlePassSystem
    {
        public const int MaxLevel = 30;
        public const int XpPerLevel = 100;
        public const int PremiumPassCost = 980; // 願晶

        public static int Level => PlayerData.Instance.battlePassLevel;
        public static int Xp => PlayerData.Instance.battlePassXp;
        public static bool IsPremium => PlayerData.Instance.battlePassPremium;

        public static event Action<int> OnLevelUp;

        public static void AddXp(int amount)
        {
            var pd = PlayerData.Instance;
            pd.battlePassXp += amount;

            while (pd.battlePassXp >= XpPerLevel && pd.battlePassLevel < MaxLevel)
            {
                pd.battlePassXp -= XpPerLevel;
                pd.battlePassLevel++;
                OnLevelUp?.Invoke(pd.battlePassLevel);

                // Grant rewards
                GrantLevelReward(pd.battlePassLevel);
            }
        }

        public static bool UpgradeToPremium()
        {
            if (PlayerData.Instance.battlePassPremium) return false;
            if (!CurrencyManager.Spend(0, PremiumPassCost)) return false;
            PlayerData.Instance.battlePassPremium = true;

            // Grant all previously missed premium rewards
            for (int i = 1; i <= Level; i++)
                GrantPremiumReward(i);

            return true;
        }

        static void GrantLevelReward(int level)
        {
            // Free track rewards every level
            int goldReward = level switch
            {
                <= 10 => 50,
                <= 20 => 75,
                _ => 100,
            };
            CurrencyManager.AddGold(goldReward);

            // Pack ticket every 5 levels
            if (level % 5 == 0)
            {
                // Grant a free pack
                PackSystem.OpenPack();
            }

            if (IsPremium)
                GrantPremiumReward(level);
        }

        static void GrantPremiumReward(int level)
        {
            // Premium track: extra gold + premium currency
            CurrencyManager.AddGold(25);
            if (level % 3 == 0) CurrencyManager.AddPremium(10);
        }

        public static List<(int level, string freeReward, string premiumReward)> GetRewardTable()
        {
            var table = new List<(int, string, string)>();
            for (int i = 1; i <= MaxLevel; i++)
            {
                string free = i % 5 == 0 ? $"ゴールド + パック" : $"ゴールド {(i <= 10 ? 50 : i <= 20 ? 75 : 100)}";
                string premium = i % 3 == 0 ? "ゴールド25 + 願晶10" : "ゴールド25";
                table.Add((i, free, premium));
            }
            return table;
        }
    }
}
