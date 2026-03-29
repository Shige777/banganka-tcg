using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Data;
using Banganka.Audio;

namespace Banganka.UI.Cards
{
    /// <summary>
    /// カード展示バインダー。
    /// 全カードをアスペクト別ページで表示し、コレクション進捗を確認。
    /// 所持カードは彩色、未所持はシルエット表示。
    /// </summary>
    public class CardBinderController : MonoBehaviour
    {
        Canvas _canvas;
        GameObject _root;
        ScrollRect _scrollRect;
        GameObject _gridContent;
        TextMeshProUGUI _progressText;
        TextMeshProUGUI _pageTitle;
        readonly List<GameObject> _cardInstances = new();

        // ページ管理
        int _currentPage; // 0=All, 1-6=各アスペクト
        static readonly string[] PageNames = { "全カード", "曙", "空", "穏", "妖", "遊", "玄" };
        static readonly Aspect[] PageAspects = { 0, Aspect.Contest, Aspect.Whisper, Aspect.Weave, Aspect.Verse, Aspect.Manifest, Aspect.Hush };

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Show()
        {
            EnsureUI();
            _currentPage = 0;
            RefreshPage();
            _root.SetActive(true);
            SoundManager.Instance?.PlayUISE("se_screen_open");
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root);
        }

        void EnsureUI()
        {
            if (_root != null) return;

            // Canvas
            if (_canvas == null)
            {
                var cObj = new GameObject("BinderCanvas");
                cObj.transform.SetParent(transform, false);
                _canvas = cObj.AddComponent<Canvas>();
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = 85;
                cObj.AddComponent<GraphicRaycaster>();
                var scaler = cObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
            }

            _root = new GameObject("BinderRoot");
            _root.transform.SetParent(_canvas.transform, false);
            var rootRt = _root.AddComponent<RectTransform>();
            StretchFull(rootRt);

            // Background
            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.06f, 0.10f, 0.97f);

            // Header bar
            var headerObj = CreateChild(_root.transform, "Header");
            var headerRt = headerObj.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0, 0.92f);
            headerRt.anchorMax = Vector2.one;
            headerRt.offsetMin = Vector2.zero;
            headerRt.offsetMax = Vector2.zero;
            headerObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f);

            _pageTitle = CreateChild(headerObj.transform, "Title").AddComponent<TextMeshProUGUI>();
            var titleRt = _pageTitle.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.15f, 0);
            titleRt.anchorMax = new Vector2(0.85f, 1);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            _pageTitle.fontSize = 30;
            _pageTitle.fontStyle = FontStyles.Bold;
            _pageTitle.color = Color.white;
            _pageTitle.alignment = TextAlignmentOptions.Center;

            // Close button
            var closeObj = CreateChild(headerObj.transform, "Close");
            var closeRt = closeObj.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.88f, 0.1f);
            closeRt.anchorMax = new Vector2(0.98f, 0.9f);
            closeRt.offsetMin = Vector2.zero;
            closeRt.offsetMax = Vector2.zero;
            closeObj.AddComponent<Image>().color = new Color(0.3f, 0.15f, 0.15f);
            closeObj.AddComponent<Button>().onClick.AddListener(Hide);
            var closeTmp = closeObj.AddComponent<TextMeshProUGUI>();
            closeTmp.text = "X";
            closeTmp.fontSize = 22;
            closeTmp.alignment = TextAlignmentOptions.Center;
            closeTmp.color = Color.white;

            // Page nav buttons
            var prevObj = CreateChild(headerObj.transform, "Prev");
            SetAnchored(prevObj.GetComponent<RectTransform>(), 0.02f, 0.1f, 0.12f, 0.9f);
            prevObj.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.28f);
            prevObj.AddComponent<Button>().onClick.AddListener(() => ChangePage(-1));
            var prevTmp = prevObj.AddComponent<TextMeshProUGUI>();
            prevTmp.text = "<";
            prevTmp.fontSize = 24;
            prevTmp.alignment = TextAlignmentOptions.Center;
            prevTmp.color = Color.white;

            var nextObj = CreateChild(headerObj.transform, "Next");
            SetAnchored(nextObj.GetComponent<RectTransform>(), 0.88f - 0.13f, 0.1f, 0.88f - 0.01f, 0.9f);
            nextObj.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.28f);
            nextObj.AddComponent<Button>().onClick.AddListener(() => ChangePage(1));
            var nextTmp = nextObj.AddComponent<TextMeshProUGUI>();
            nextTmp.text = ">";
            nextTmp.fontSize = 24;
            nextTmp.alignment = TextAlignmentOptions.Center;
            nextTmp.color = Color.white;

            // Progress bar
            var progObj = CreateChild(_root.transform, "Progress");
            SetAnchored(progObj.GetComponent<RectTransform>(), 0.05f, 0.88f, 0.95f, 0.92f);
            _progressText = progObj.AddComponent<TextMeshProUGUI>();
            _progressText.fontSize = 16;
            _progressText.color = new Color(0.7f, 0.7f, 0.75f);
            _progressText.alignment = TextAlignmentOptions.Center;

            // Scroll area
            var scrollObj = CreateChild(_root.transform, "Scroll");
            SetAnchored(scrollObj.GetComponent<RectTransform>(), 0.02f, 0.02f, 0.98f, 0.87f);
            _scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            scrollObj.AddComponent<Mask>().showMaskGraphic = false;

            // Grid content
            _gridContent = new GameObject("Grid");
            _gridContent.transform.SetParent(scrollObj.transform, false);
            var gridRt = _gridContent.AddComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0, 1);
            gridRt.anchorMax = Vector2.one;
            gridRt.pivot = new Vector2(0.5f, 1);
            gridRt.sizeDelta = Vector2.zero;

            var glg = _gridContent.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(180, 250);
            glg.spacing = new Vector2(12, 12);
            glg.padding = new RectOffset(12, 12, 12, 12);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 5;

            var csf = _gridContent.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect.content = gridRt;
            _scrollRect.vertical = true;
            _scrollRect.horizontal = false;
        }

        void ChangePage(int delta)
        {
            _currentPage = (_currentPage + delta + PageNames.Length) % PageNames.Length;
            RefreshPage();
            SoundManager.Instance?.PlayUISE("se_page_turn");
        }

        void RefreshPage()
        {
            foreach (var inst in _cardInstances)
                if (inst != null) Destroy(inst);
            _cardInstances.Clear();

            _pageTitle.text = $"カード図鑑 - {PageNames[_currentPage]}";

            var allCards = CardDatabase.AllCards.Values
                .OrderBy(c => c.aspect)
                .ThenBy(c => c.cpCost)
                .ThenBy(c => c.cardName)
                .ToList();

            if (_currentPage > 0)
            {
                var filterAspect = PageAspects[_currentPage];
                allCards = allCards.Where(c => c.aspect == filterAspect).ToList();
            }

            var owned = PlayerData.Instance.cardCollection;
            int ownedCount = 0;

            foreach (var card in allCards)
            {
                int count = owned.TryGetValue(card.id, out int c) ? c : 0;
                bool isOwned = count > 0;
                if (isOwned) ownedCount++;

                CreateBinderCard(card, count, isOwned);
            }

            // Progress
            int total = allCards.Count;
            float pct = total > 0 ? (float)ownedCount / total * 100f : 0;
            _progressText.text = $"コレクション: {ownedCount}/{total} ({pct:F1}%)";

            // Scroll to top
            if (_scrollRect != null)
                _scrollRect.normalizedPosition = new Vector2(0, 1);
        }

        void CreateBinderCard(CardData card, int ownedCount, bool isOwned)
        {
            var obj = new GameObject(card.id);
            obj.transform.SetParent(_gridContent.transform, false);

            // Background
            var bg = obj.AddComponent<Image>();
            if (isOwned)
            {
                Color ac = AspectColors.GetColor(card.aspect);
                bg.color = new Color(ac.r * 0.4f, ac.g * 0.4f, ac.b * 0.4f, 0.9f);
            }
            else
            {
                bg.color = new Color(0.08f, 0.08f, 0.10f, 0.7f); // Silhouette
            }

            // Card name
            var nameObj = CreateChild(obj.transform, "Name");
            SetAnchored(nameObj.GetComponent<RectTransform>(), 0.05f, 0.7f, 0.95f, 0.9f);
            var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = isOwned ? card.cardName : "???";
            nameTmp.fontSize = 14;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = isOwned ? Color.white : new Color(0.3f, 0.3f, 0.35f);
            nameTmp.alignment = TextAlignmentOptions.Center;

            // Rarity indicator
            var rarObj = CreateChild(obj.transform, "Rarity");
            SetAnchored(rarObj.GetComponent<RectTransform>(), 0.1f, 0.55f, 0.9f, 0.7f);
            var rarTmp = rarObj.AddComponent<TextMeshProUGUI>();
            rarTmp.text = isOwned ? card.rarity : "";
            rarTmp.fontSize = 18;
            rarTmp.fontStyle = FontStyles.Bold;
            rarTmp.color = isOwned ? GetRarityColor(card.rarity) : Color.clear;
            rarTmp.alignment = TextAlignmentOptions.Center;

            // Type + Cost
            var infoObj = CreateChild(obj.transform, "Info");
            SetAnchored(infoObj.GetComponent<RectTransform>(), 0.05f, 0.35f, 0.95f, 0.55f);
            var infoTmp = infoObj.AddComponent<TextMeshProUGUI>();
            if (isOwned)
            {
                string typeName = card.type switch
                {
                    CardType.Manifest => "顕現",
                    CardType.Spell => "詠術",
                    CardType.Algorithm => "界律",
                    _ => ""
                };
                infoTmp.text = $"CP:{card.cpCost}  {typeName}";
            }
            else
            {
                infoTmp.text = "";
            }
            infoTmp.fontSize = 12;
            infoTmp.color = new Color(0.7f, 0.7f, 0.75f);
            infoTmp.alignment = TextAlignmentOptions.Center;

            // Owned count
            var countObj = CreateChild(obj.transform, "Count");
            SetAnchored(countObj.GetComponent<RectTransform>(), 0.6f, 0.02f, 0.95f, 0.15f);
            var countTmp = countObj.AddComponent<TextMeshProUGUI>();
            countTmp.text = isOwned ? $"x{ownedCount}" : "";
            countTmp.fontSize = 14;
            countTmp.fontStyle = FontStyles.Bold;
            countTmp.color = new Color(0.8f, 0.8f, 0.85f);
            countTmp.alignment = TextAlignmentOptions.BottomRight;

            // Aspect icon (color dot)
            var dotObj = CreateChild(obj.transform, "Dot");
            SetAnchored(dotObj.GetComponent<RectTransform>(), 0.05f, 0.02f, 0.15f, 0.12f);
            var dotImg = dotObj.AddComponent<Image>();
            dotImg.color = isOwned
                ? AspectColors.GetColor(card.aspect)
                : new Color(0.2f, 0.2f, 0.25f);

            _cardInstances.Add(obj);
        }

        static Color GetRarityColor(string rarity) => rarity switch
        {
            "SSR" => new Color(1f, 0.85f, 0.2f),
            "SR" => new Color(0.83f, 0.66f, 0.26f),
            "R" => new Color(0.3f, 0.64f, 1f),
            _ => Color.white
        };

        static GameObject CreateChild(Transform parent, string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            return obj;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void SetAnchored(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
