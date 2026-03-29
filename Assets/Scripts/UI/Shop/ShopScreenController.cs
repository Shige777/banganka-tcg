using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Economy;
using Banganka.Core.Data;
using Banganka.UI.Common;

namespace Banganka.UI.Shop
{
    /// <summary>
    /// ショップ画面 (MONETIZATION_DESIGN.md, ASSET_SHOP.md)
    /// パック / 通貨 / バンドル / デイリー — 4タブ構成
    /// </summary>
    public class ShopScreenController : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI goldText;
        [SerializeField] TextMeshProUGUI premiumText;

        [Header("Tabs")]
        [SerializeField] Button tabPacks;
        [SerializeField] Button tabCurrency;
        [SerializeField] Button tabBundle;
        [SerializeField] Button tabDaily;

        [Header("Item List")]
        [SerializeField] Transform itemListParent;
        [SerializeField] GameObject shopItemPrefab;

        [Header("Purchase Confirm")]
        [SerializeField] GameObject confirmPanel;
        [SerializeField] TextMeshProUGUI confirmItemName;
        [SerializeField] TextMeshProUGUI confirmCostText;
        [SerializeField] Button confirmBuyButton;

        [Header("Pack Result")]
        [SerializeField] GameObject packResultPanel;
        [SerializeField] Transform packCardParent;
        [SerializeField] GameObject cardViewPrefab;

        [Header("Probability Display (提供割合)")]
        [SerializeField] GameObject probabilityPanel;
        [SerializeField] Button probabilityButton;
        [SerializeField] Button probabilityCloseButton;
        [SerializeField] TextMeshProUGUI probabilityText;

        [Header("Pack Opening Animation")]
        [SerializeField] PackOpeningController packOpeningController;

        readonly List<GameObject> _itemInstances = new();
        readonly List<GameObject> _resultInstances = new();
        string _selectedCategory = "pack";
        ShopItem _pendingPurchase;

        // ── 排出確率定数 (MONETIZATION_DESIGN.md §4.4) ──
        // 通常パック (5枚): 1〜4枠目
        const float NormalSlot1to4_C   = 75f;
        const float NormalSlot1to4_R   = 20f;
        const float NormalSlot1to4_SR  = 4f;
        const float NormalSlot1to4_SSR = 1f;
        // 通常パック: 5枠目 (R以上確定)
        const float NormalSlot5_R   = 80f;
        const float NormalSlot5_SR  = 16f;
        const float NormalSlot5_SSR = 4f;

        // プレミアムパック (10枚): 1〜9枠目
        const float PremiumSlot1to9_C   = 75f;
        const float PremiumSlot1to9_R   = 20f;
        const float PremiumSlot1to9_SR  = 4f;
        const float PremiumSlot1to9_SSR = 1f;
        // プレミアムパック: 10枠目 (SR以上確定)
        const float PremiumSlot10_SR  = 80f;
        const float PremiumSlot10_SSR = 20f;

        // 天井
        const int PityThreshold = 50;

        void OnEnable()
        {
            if (titleText) titleText.text = "ショップ";
            if (confirmPanel) confirmPanel.SetActive(false);
            if (packResultPanel) packResultPanel.SetActive(false);
            if (probabilityPanel) probabilityPanel.SetActive(false);

            if (probabilityButton) probabilityButton.onClick.AddListener(OnProbabilityButton);
            if (probabilityCloseButton) probabilityCloseButton.onClick.AddListener(OnCloseProbability);

            RefreshCurrency();
            ShowCategory("pack");

            CurrencyManager.OnCurrencyChanged += RefreshCurrency;
            ShopSystem.OnShopUpdated += RefreshItems;
            PackSystem.OnPackOpenedEx += ShowPackResultAnimated;
        }

        void OnDisable()
        {
            if (probabilityButton) probabilityButton.onClick.RemoveListener(OnProbabilityButton);
            if (probabilityCloseButton) probabilityCloseButton.onClick.RemoveListener(OnCloseProbability);

            CurrencyManager.OnCurrencyChanged -= RefreshCurrency;
            ShopSystem.OnShopUpdated -= RefreshItems;
            PackSystem.OnPackOpenedEx -= ShowPackResultAnimated;
        }

        void RefreshCurrency()
        {
            if (goldText) goldText.text = $"ゴールド: {CurrencyManager.Gold:#,0}";
            if (premiumText) premiumText.text = $"願晶: {CurrencyManager.Premium:#,0}";
        }

        public void OnTabPacks() => ShowCategory("pack");
        public void OnTabCurrency() => ShowCategory("currency");
        public void OnTabBundle() => ShowCategory("bundle");
        public void OnTabDaily() => ShowCategory("daily");

        void ShowCategory(string category)
        {
            _selectedCategory = category;
            RefreshItems();
        }

        void RefreshItems()
        {
            foreach (var inst in _itemInstances)
                Destroy(inst);
            _itemInstances.Clear();

            IEnumerable<ShopItem> items;
            if (_selectedCategory == "daily")
                items = ShopSystem.DailyRotation;
            else
            {
                var all = ShopSystem.AllItems;
                var filtered = new List<ShopItem>();
                foreach (var item in all)
                    if (item.category == _selectedCategory)
                        filtered.Add(item);
                items = filtered;
            }

            foreach (var item in items)
            {
                if (shopItemPrefab == null || itemListParent == null) continue;
                var obj = Instantiate(shopItemPrefab, itemListParent);
                _itemInstances.Add(obj);

                // Item display
                var texts = obj.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 1) texts[0].text = item.name;
                if (texts.Length >= 2)
                {
                    string cost = item.goldCost > 0
                        ? $"{item.goldCost}ゴールド"
                        : item.premiumCost > 0
                            ? $"{item.premiumCost}願晶"
                            : !string.IsNullOrEmpty(item.iapProductId)
                                ? "購入"
                                : "無料";

                    if (item.isLimited)
                        cost += item.stockRemaining > 0
                            ? $" (残{item.stockRemaining})"
                            : " (売切)";

                    texts[1].text = cost;
                }
                if (texts.Length >= 3) texts[2].text = item.description;

                // Sold out state
                bool soldOut = item.isLimited && item.stockRemaining <= 0;
                bool canAfford = CurrencyManager.CanAfford(item.goldCost, item.premiumCost);

                var btn = obj.GetComponent<Button>();
                if (btn == null) btn = obj.AddComponent<Button>();
                btn.interactable = !soldOut && (canAfford || !string.IsNullOrEmpty(item.iapProductId));
                var captured = item;
                btn.onClick.AddListener(() => ConfirmPurchase(captured));
            }
        }

        void ConfirmPurchase(ShopItem item)
        {
            _pendingPurchase = item;
            if (confirmPanel) confirmPanel.SetActive(true);
            if (confirmItemName) confirmItemName.text = item.name;
            if (confirmCostText)
            {
                confirmCostText.text = item.goldCost > 0
                    ? $"{item.goldCost}ゴールドを消費します"
                    : item.premiumCost > 0
                        ? $"{item.premiumCost}願晶を消費します"
                        : "購入しますか？";
            }
            if (confirmBuyButton)
                confirmBuyButton.interactable = CurrencyManager.CanAfford(item.goldCost, item.premiumCost)
                                                || !string.IsNullOrEmpty(item.iapProductId);
        }

        public void OnConfirmBuy()
        {
            if (_pendingPurchase == null) return;

            bool success = ShopSystem.Purchase(_pendingPurchase.itemId);
            if (confirmPanel) confirmPanel.SetActive(false);

            if (!success)
                Debug.LogWarning($"[Shop] Purchase failed: {_pendingPurchase.itemId}");

            _pendingPurchase = null;
        }

        public void OnCancelPurchase()
        {
            _pendingPurchase = null;
            if (confirmPanel) confirmPanel.SetActive(false);
        }

        void ShowPackResultAnimated(PackOpenResult result)
        {
            // PackOpeningControllerが設定されていれば演出付きで表示
            if (packOpeningController != null)
            {
                packOpeningController.StartPackOpening(result, () =>
                {
                    RefreshCurrency();
                });
                return;
            }

            // フォールバック: 従来のグリッド表示
            if (packResultPanel == null) return;
            packResultPanel.SetActive(true);

            foreach (var inst in _resultInstances)
                Destroy(inst);
            _resultInstances.Clear();

            foreach (var entry in result.cards)
            {
                if (cardViewPrefab == null || packCardParent == null) continue;
                var obj = Instantiate(cardViewPrefab, packCardParent);
                var view = obj.GetComponent<CardView>();
                if (view) view.SetCard(entry.card);
                _resultInstances.Add(obj);
            }
        }

        public void OnClosePackResult()
        {
            if (packResultPanel) packResultPanel.SetActive(false);
            foreach (var inst in _resultInstances)
                Destroy(inst);
            _resultInstances.Clear();
        }

        // ── 提供割合 (確率表示) ──────────────────────────────

        /// <summary>
        /// 「提供割合」ボタン押下 — 排出確率テーブルを表示する。
        /// 特定商取引法 / App Store ガイドライン / Steam 要件に準拠。
        /// </summary>
        public void OnProbabilityButton()
        {
            if (probabilityPanel == null || probabilityText == null) return;

            probabilityText.text = BuildProbabilityTable();
            probabilityPanel.SetActive(true);
        }

        /// <summary>確率パネルを閉じる。</summary>
        public void OnCloseProbability()
        {
            if (probabilityPanel) probabilityPanel.SetActive(false);
        }

        /// <summary>
        /// MONETIZATION_DESIGN.md §4.4 に基づく排出確率テーブル文字列を生成する。
        /// </summary>
        static string BuildProbabilityTable()
        {
            var sb = new System.Text.StringBuilder(1024);

            sb.AppendLine("<b>■ 提供割合</b>");
            sb.AppendLine();

            // ── 通常パック ──
            sb.AppendLine("<b>【通常パック】</b>");
            sb.AppendLine("1パック = カード5枚 (5枠目はR以上確定)");
            sb.AppendLine();
            sb.AppendLine("▼ 1〜4枠目");
            sb.AppendLine($"  C    {NormalSlot1to4_C}%");
            sb.AppendLine($"  R    {NormalSlot1to4_R}%");
            sb.AppendLine($"  SR   {NormalSlot1to4_SR}%");
            sb.AppendLine($"  SSR  {NormalSlot1to4_SSR}%");
            sb.AppendLine();
            sb.AppendLine("▼ 5枠目 (R以上確定)");
            sb.AppendLine($"  R    {NormalSlot5_R}%");
            sb.AppendLine($"  SR   {NormalSlot5_SR}%");
            sb.AppendLine($"  SSR  {NormalSlot5_SSR}%");
            sb.AppendLine();

            // ── プレミアムパック ──
            sb.AppendLine("<b>【プレミアムパック】</b>");
            sb.AppendLine("1パック = カード10枚 (10枠目はSR以上確定)");
            sb.AppendLine();
            sb.AppendLine("▼ 1〜9枠目");
            sb.AppendLine($"  C    {PremiumSlot1to9_C}%");
            sb.AppendLine($"  R    {PremiumSlot1to9_R}%");
            sb.AppendLine($"  SR   {PremiumSlot1to9_SR}%");
            sb.AppendLine($"  SSR  {PremiumSlot1to9_SSR}%");
            sb.AppendLine();
            sb.AppendLine("▼ 10枠目 (SR以上確定)");
            sb.AppendLine($"  SR   {PremiumSlot10_SR}%");
            sb.AppendLine($"  SSR  {PremiumSlot10_SSR}%");
            sb.AppendLine();

            // ── 天井 ──
            sb.AppendLine("<b>【天井システム】</b>");
            sb.AppendLine($"通常パックを{PityThreshold}パック開封してもSSRが");
            sb.AppendLine("出なかった場合、次のパックでSSR1枚確定。");
            sb.AppendLine("※カウントはシリーズ別に独立管理されます。");
            sb.AppendLine();

            // ── 注記 ──
            sb.AppendLine("<size=80%>※ レアリティはカードの入手難易度であり、");
            sb.AppendLine("  カード性能の強さを示すものではありません。");
            sb.AppendLine("※ 表示確率は小数点以下を四捨五入しています。</size>");

            return sb.ToString();
        }
    }
}
