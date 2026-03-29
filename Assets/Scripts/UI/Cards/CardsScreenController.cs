using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Banganka.Core.Data;
using Banganka.Core.Economy;
using Banganka.UI.Common;

namespace Banganka.UI.Cards
{
    /// <summary>
    /// カード/コレクション画面 (COLLECTION_UX_SPEC.md)
    /// フィルタリング、詳細表示、生成/分解
    /// </summary>
    public class CardsScreenController : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI collectionCountText;

        [Header("List")]
        [SerializeField] Transform cardListParent;
        [SerializeField] GameObject cardViewPrefab;

        [Header("Filters")]
        [SerializeField] TMP_Dropdown typeFilter;
        [SerializeField] TMP_Dropdown aspectFilter;
        [SerializeField] TMP_Dropdown sortDropdown;
        [SerializeField] TMP_InputField searchField;
        [SerializeField] Toggle ownedOnlyToggle;

        [Header("Detail")]
        [SerializeField] GameObject detailPanel;
        [SerializeField] CardView detailCardView;
        [SerializeField] TextMeshProUGUI detailOwnedText;
        [SerializeField] TextMeshProUGUI craftCostText;
        [SerializeField] TextMeshProUGUI dismantleCostText;
        [SerializeField] Button craftButton;
        [SerializeField] Button dismantleButton;

        List<CardData> _allCards;
        readonly List<GameObject> _cardInstances = new();
        CardData _selectedCard;

        void OnEnable()
        {
            if (titleText) titleText.text = "カード";
            _allCards = CardDatabase.AllCards.Values.ToList();
            if (detailPanel) detailPanel.SetActive(false);

            UpdateCollectionCount();
            RefreshList();

            CurrencyManager.OnCurrencyChanged += OnCurrencyChanged;
        }

        void OnDisable()
        {
            CurrencyManager.OnCurrencyChanged -= OnCurrencyChanged;
        }

        void OnCurrencyChanged()
        {
            if (_selectedCard != null) UpdateDetailPanel(_selectedCard);
        }

        void UpdateCollectionCount()
        {
            if (collectionCountText == null) return;
            var pd = PlayerData.Instance;
            int uniqueOwned = pd.cardCollection.Count(kv => kv.Value > 0);
            int total = _allCards.Count;
            collectionCountText.text = $"コレクション: {uniqueOwned}/{total} ({uniqueOwned * 100 / Mathf.Max(total, 1)}%)";
        }

        void RefreshList()
        {
            foreach (var inst in _cardInstances)
                Destroy(inst);
            _cardInstances.Clear();

            var filtered = FilterAndSort();

            foreach (var card in filtered)
            {
                if (cardViewPrefab == null || cardListParent == null) continue;
                var obj = Instantiate(cardViewPrefab, cardListParent);
                var view = obj.GetComponent<CardView>();
                if (view) view.SetCard(card);

                // Dim unowned cards
                int owned = PlayerData.Instance.GetCardCount(card.id);
                if (owned <= 0)
                {
                    var cg = obj.GetComponent<CanvasGroup>();
                    if (cg == null) cg = obj.AddComponent<CanvasGroup>();
                    cg.alpha = 0.4f;
                }

                // Owned count badge
                var badge = obj.GetComponentInChildren<TextMeshProUGUI>();
                if (badge != null && owned > 0)
                {
                    // The card view handles its own display; we add count info
                }

                var btn = obj.GetComponent<Button>();
                if (btn == null) btn = obj.AddComponent<Button>();
                var captured = card;
                btn.onClick.AddListener(() => ShowDetail(captured));

                _cardInstances.Add(obj);
            }
        }

        List<CardData> FilterAndSort()
        {
            IEnumerable<CardData> list = _allCards;

            // Type filter
            if (typeFilter != null && typeFilter.value > 0)
            {
                var type = typeFilter.value switch
                {
                    1 => CardType.Manifest,
                    2 => CardType.Spell,
                    3 => CardType.Algorithm,
                    _ => CardType.Manifest,
                };
                list = list.Where(c => c.type == type);
            }

            // Aspect filter
            if (aspectFilter != null && aspectFilter.value > 0)
            {
                var aspect = (Aspect)(aspectFilter.value - 1);
                list = list.Where(c => c.aspect == aspect);
            }

            // Search
            if (searchField != null && !string.IsNullOrEmpty(searchField.text))
            {
                string query = searchField.text.ToLower();
                list = list.Where(c =>
                    c.cardName.ToLower().Contains(query) ||
                    c.id.ToLower().Contains(query) ||
                    (c.flavorText != null && c.flavorText.ToLower().Contains(query)));
            }

            // Owned only
            if (ownedOnlyToggle != null && ownedOnlyToggle.isOn)
                list = list.Where(c => PlayerData.Instance.GetCardCount(c.id) > 0);

            // Sort
            int sortMode = sortDropdown != null ? sortDropdown.value : 0;
            list = sortMode switch
            {
                1 => list.OrderByDescending(c => c.cpCost).ThenBy(c => c.cardName),
                2 => list.OrderBy(c => c.cardName),
                3 => list.OrderBy(c => c.rarity).ThenBy(c => c.cpCost),
                _ => list.OrderBy(c => c.cpCost).ThenBy(c => c.cardName),
            };

            return list.ToList();
        }

        void ShowDetail(CardData card)
        {
            _selectedCard = card;
            if (detailPanel) detailPanel.SetActive(true);
            if (detailCardView) detailCardView.SetCard(card);
            UpdateDetailPanel(card);
        }

        void UpdateDetailPanel(CardData card)
        {
            int owned = PlayerData.Instance.GetCardCount(card.id);
            if (detailOwnedText) detailOwnedText.text = $"所持: {owned}枚";

            int craftGold = CraftSystem.GetCraftCost(card.rarity);
            int dismantleGold = CraftSystem.GetDismantleGain(card.rarity);

            if (craftCostText) craftCostText.text = $"生成: {craftGold}ゴールド";
            if (dismantleCostText) dismantleCostText.text = $"分解: +{dismantleGold}ゴールド";

            if (craftButton)
                craftButton.interactable = owned < 3 && CurrencyManager.Gold >= craftGold;
            if (dismantleButton)
                dismantleButton.interactable = owned > 0;
        }

        public void OnCraft()
        {
            if (_selectedCard == null) return;
            if (CraftSystem.Craft(_selectedCard.id))
            {
                UpdateDetailPanel(_selectedCard);
                UpdateCollectionCount();
            }
        }

        public void OnDismantle()
        {
            if (_selectedCard == null) return;
            if (CraftSystem.Dismantle(_selectedCard.id))
            {
                UpdateDetailPanel(_selectedCard);
                UpdateCollectionCount();
            }
        }

        public void OnCloseDetail()
        {
            if (detailPanel) detailPanel.SetActive(false);
        }

        public void OnFilterChanged()
        {
            RefreshList();
        }

        public void OnSearchChanged()
        {
            RefreshList();
        }
    }
}
