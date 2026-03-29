using System;

namespace Banganka.Core.Config
{
    /// <summary>
    /// App Store メタデータ + IAP設定 (APPSTORE_CHECKLIST.md, MONETIZATION_DESIGN.md §2.3)
    /// </summary>
    public static class AppStoreConfig
    {
        // ------------------------------------------------------------------
        // App Store Connect メタデータ (APPSTORE_CHECKLIST.md §1.1)
        // ------------------------------------------------------------------

        public const string AppName = "万願果（ばんがんか）";
        public const string Subtitle = "願いをかけて戦う対戦カードゲーム";
        public const string BundleId = "com.banganka.tcg";
        public const string PrimaryCategory = "Games > Card";
        public const string SecondaryCategory = "Games > Strategy";
        public const string Price = "Free"; // 無料 (アプリ内課金あり)
        public const string DefaultLanguage = "ja"; // 日本語 (MVP)

        // 年齢レーティング (APPSTORE_CHECKLIST.md §3.1)
        public const string AgeRating = "9+"; // まれ/軽度のアニメまたはファンタジーバイオレンス

        // プライバシー・サポートURL (APPSTORE_CHECKLIST.md §1.4)
        // リリース前にページ公開を確認すること
        public const string PrivacyPolicyUrl = "https://banngannka.com/privacy";
        public const string SupportUrl = "https://banngannka.com/support";

        // キーワード (APPSTORE_CHECKLIST.md §1.3, 100文字以内)
        public const string Keywords = "TCG,カードゲーム,対戦,願い,ファンタジー,デッキ,戦略,1v1,PvP,カード";

        // ------------------------------------------------------------------
        // IAP Products — 願晶 5段階 (APPSTORE_CHECKLIST.md §4.1, MONETIZATION_DESIGN.md §2.3)
        // ------------------------------------------------------------------

        public static readonly IAPProduct[] Products =
        {
            new()
            {
                productId = "com.banganka.gem60",
                displayName = "願晶 60個",
                priceTierJPY = 160,
                gemAmount = 60,
            },
            new()
            {
                productId = "com.banganka.gem300",
                displayName = "願晶 300個",
                priceTierJPY = 650,
                gemAmount = 300,
            },
            new()
            {
                productId = "com.banganka.gem650",
                displayName = "願晶 650個",
                priceTierJPY = 1300,
                gemAmount = 650,
            },
            new()
            {
                productId = "com.banganka.gem1500",
                displayName = "願晶 1,500個",
                priceTierJPY = 2600,
                gemAmount = 1500,
            },
            new()
            {
                productId = "com.banganka.gem4000",
                displayName = "願晶 4,000個",
                priceTierJPY = 6500,
                gemAmount = 4000,
            },
        };

        // ------------------------------------------------------------------
        // Starter Bundle (MONETIZATION_DESIGN.md §8)
        // ------------------------------------------------------------------

        public static readonly IAPProduct StarterBundle = new()
        {
            productId = "com.banganka.starter",
            displayName = "果求者の始まりセット",
            priceTierJPY = 490,
            gemAmount = 300, // + 通常パック×5 + 限定スリーブ「交界の旅人」
        };

        /// <summary>
        /// productIdからIAPProductを検索する。
        /// </summary>
        public static IAPProduct FindProduct(string productId)
        {
            foreach (var p in Products)
            {
                if (p.productId == productId) return p;
            }
            if (StarterBundle.productId == productId) return StarterBundle;
            return null;
        }
    }

    [Serializable]
    public class IAPProduct
    {
        public string productId;
        public string displayName;
        public int priceTierJPY; // 日本円の価格
        public int gemAmount;    // 付与する願晶数
    }
}
