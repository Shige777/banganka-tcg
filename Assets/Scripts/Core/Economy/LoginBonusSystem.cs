using System;
using Banganka.Core.Data;

namespace Banganka.Core.Economy
{
    /// <summary>
    /// ログインボーナス (MONETIZATION_DESIGN.md)
    /// 7日サイクル。連続ログインでエスカレート。
    /// </summary>
    public static class LoginBonusSystem
    {
        static readonly int[] GoldRewards = { 50, 75, 100, 125, 150, 200, 300 };

        public static event Action<int, int> OnBonusClaimed; // (day, gold)

        public static bool CheckAndClaim()
        {
            var pd = PlayerData.Instance;
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            if (pd.lastLoginDate == today) return false; // Already claimed today

            // Check consecutive
            if (pd.lastLoginDate != null)
            {
                var lastDate = DateTime.Parse(pd.lastLoginDate);
                var diff = (DateTime.UtcNow.Date - lastDate.Date).Days;
                if (diff == 1)
                    pd.loginStreak++;
                else if (diff > 1)
                    pd.loginStreak = 0; // Reset streak
            }

            pd.lastLoginDate = today;

            int day = pd.loginStreak % 7; // 0-6 cycle
            int gold = GoldRewards[day];
            CurrencyManager.AddGold(gold);

            // Battle Pass XP for login
            BattlePassSystem.AddXp(10);

            OnBonusClaimed?.Invoke(day + 1, gold);
            return true;
        }

        public static int CurrentStreak => PlayerData.Instance.loginStreak;
        public static int NextReward
        {
            get
            {
                int day = PlayerData.Instance.loginStreak % 7;
                return GoldRewards[day];
            }
        }
    }
}
