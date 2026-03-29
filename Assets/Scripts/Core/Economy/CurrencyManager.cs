using System;
using UnityEngine;
using Banganka.Core.Data;

namespace Banganka.Core.Economy
{
    /// <summary>
    /// 通貨管理: ゴールド + 願晶(有償) (MONETIZATION_DESIGN.md)
    /// </summary>
    public static class CurrencyManager
    {
        public static int Gold => PlayerData.Instance.gold;
        public static int Premium => PlayerData.Instance.premium;

        public static event Action OnCurrencyChanged;

        public static bool CanAfford(int goldCost, int premiumCost = 0)
        {
            return PlayerData.Instance.gold >= goldCost && PlayerData.Instance.premium >= premiumCost;
        }

        public static bool Spend(int goldCost, int premiumCost = 0)
        {
            if (!CanAfford(goldCost, premiumCost)) return false;
            PlayerData.Instance.gold -= goldCost;
            PlayerData.Instance.premium -= premiumCost;
            OnCurrencyChanged?.Invoke();
            return true;
        }

        public static void AddGold(int amount)
        {
            PlayerData.Instance.gold += amount;
            OnCurrencyChanged?.Invoke();
        }

        public static void AddPremium(int amount)
        {
            PlayerData.Instance.premium += amount;
            OnCurrencyChanged?.Invoke();
        }
    }
}
