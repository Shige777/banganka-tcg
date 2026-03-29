using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Banganka.Core.Data;

namespace Banganka.UI.Cards
{
    /// <summary>
    /// 仮想スクロール (COLLECTION_UX_SPEC §1.3)
    /// ビューポート + バッファ行分のセルのみ生成し、スクロール時にリサイクルする
    /// </summary>
    public class VirtualScrollRect : MonoBehaviour
    {
        // ====================================================================
        // Configuration
        // ====================================================================

        [SerializeField] ScrollRect scrollRect;
        [SerializeField] RectTransform viewport;
        [SerializeField] RectTransform content;
        [SerializeField] GameObject cellPrefab;
        [SerializeField] int columns = 4;
        [SerializeField] float cellWidth = 100f;
        [SerializeField] float cellHeight = 140f;
        [SerializeField] float spacing = 8f;
        [SerializeField] int bufferRows = 2;

        // ====================================================================
        // Data source
        // ====================================================================

        List<string> _cardIds = new();

        // ====================================================================
        // Pool & active tracking
        // ====================================================================

        readonly Queue<GameObject> _pool = new();
        readonly Dictionary<int, GameObject> _activeCells = new();

        int _prevFirstRow = -1;
        int _prevLastRow = -1;

        // ====================================================================
        // Events
        // ====================================================================

        public event Action<string> OnCellClicked;

        // ====================================================================
        // Properties
        // ====================================================================

        float RowHeight => cellHeight + spacing;
        int TotalRows => _cardIds.Count > 0 ? Mathf.CeilToInt((float)_cardIds.Count / columns) : 0;
        float ViewportHeight => viewport != null ? viewport.rect.height : 0f;

        // ====================================================================
        // Lifecycle
        // ====================================================================

        void OnEnable()
        {
            if (scrollRect != null)
                scrollRect.onValueChanged.AddListener(OnScroll);
        }

        void OnDisable()
        {
            if (scrollRect != null)
                scrollRect.onValueChanged.RemoveListener(OnScroll);
        }

        // ====================================================================
        // Public API
        // ====================================================================

        /// <summary>
        /// データソースを設定し、コンテンツ高さを再計算してスクロール位置をリセットする
        /// </summary>
        public void SetData(List<string> cardIds)
        {
            _cardIds = cardIds ?? new List<string>();

            // Return all active cells to pool
            var indices = new List<int>(_activeCells.Keys);
            foreach (int idx in indices)
                DeactivateCell(idx);

            _prevFirstRow = -1;
            _prevLastRow = -1;

            // Recalculate content height
            float totalHeight = TotalRows * RowHeight - spacing;
            if (totalHeight < 0f) totalHeight = 0f;
            content.sizeDelta = new Vector2(content.sizeDelta.x, totalHeight);

            // Reset scroll position to top
            content.anchoredPosition = Vector2.zero;

            // Initial fill
            RefreshVisibleCells();
        }

        /// <summary>
        /// 現在表示中のセルを強制的に再バインドする（フィルタ変更後等）
        /// </summary>
        public void RefreshAllCells()
        {
            foreach (var kvp in _activeCells)
            {
                if (kvp.Key < _cardIds.Count)
                    BindCell(kvp.Value, _cardIds[kvp.Key]);
            }
        }

        // ====================================================================
        // Scroll handling
        // ====================================================================

        void OnScroll(Vector2 normalizedPos)
        {
            RefreshVisibleCells();
        }

        void RefreshVisibleCells()
        {
            if (_cardIds.Count == 0) return;

            GetVisibleRange(out int firstRow, out int lastRow);

            // Clamp to valid range
            firstRow = Mathf.Max(0, firstRow - bufferRows);
            lastRow = Mathf.Min(TotalRows - 1, lastRow + bufferRows);

            if (firstRow == _prevFirstRow && lastRow == _prevLastRow)
                return;

            // Deactivate cells outside new range
            var toDeactivate = new List<int>();
            foreach (var kvp in _activeCells)
            {
                int row = kvp.Key / columns;
                if (row < firstRow || row > lastRow)
                    toDeactivate.Add(kvp.Key);
            }
            foreach (int idx in toDeactivate)
                DeactivateCell(idx);

            // Activate cells in new range
            int firstIndex = firstRow * columns;
            int lastIndex = Mathf.Min((lastRow + 1) * columns, _cardIds.Count);
            for (int i = firstIndex; i < lastIndex; i++)
            {
                if (!_activeCells.ContainsKey(i))
                    ActivateCell(i);
            }

            _prevFirstRow = firstRow;
            _prevLastRow = lastRow;
        }

        /// <summary>
        /// スクロール位置からビューポート内に見える最初と最後の行を計算する
        /// </summary>
        void GetVisibleRange(out int firstRow, out int lastRow)
        {
            float scrollY = content.anchoredPosition.y;
            scrollY = Mathf.Max(0f, scrollY);

            firstRow = Mathf.FloorToInt(scrollY / RowHeight);
            lastRow = Mathf.FloorToInt((scrollY + ViewportHeight) / RowHeight);
            lastRow = Mathf.Min(lastRow, TotalRows - 1);
        }

        // ====================================================================
        // Cell management
        // ====================================================================

        void ActivateCell(int index)
        {
            if (index < 0 || index >= _cardIds.Count) return;
            if (_activeCells.ContainsKey(index)) return;

            GameObject cell = GetFromPool();
            cell.SetActive(true);

            // Position cell within content
            int row = index / columns;
            int col = index % columns;
            var rt = cell.GetComponent<RectTransform>();
            if (rt != null)
            {
                float x = col * (cellWidth + spacing);
                float y = -(row * RowHeight);
                rt.anchoredPosition = new Vector2(x, y);
                rt.sizeDelta = new Vector2(cellWidth, cellHeight);
            }

            BindCell(cell, _cardIds[index]);

            // Set up click handler
            var button = cell.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                string cardId = _cardIds[index];
                button.onClick.AddListener(() => OnCellClicked?.Invoke(cardId));
            }

            _activeCells[index] = cell;
        }

        void DeactivateCell(int index)
        {
            if (!_activeCells.TryGetValue(index, out var cell)) return;

            var button = cell.GetComponent<Button>();
            if (button != null)
                button.onClick.RemoveAllListeners();

            cell.SetActive(false);
            cell.transform.SetParent(content, false);
            _pool.Enqueue(cell);
            _activeCells.Remove(index);
        }

        GameObject GetFromPool()
        {
            if (_pool.Count > 0)
                return _pool.Dequeue();

            var obj = Instantiate(cellPrefab, content);
            obj.SetActive(false);
            return obj;
        }

        /// <summary>
        /// セルにカードデータを表示する
        /// </summary>
        void BindCell(GameObject cell, string cardId)
        {
            if (!CardDatabase.AllCards.TryGetValue(cardId, out var card))
                return;

            cell.name = cardId;

            // CardView がアタッチされていればそちらに委譲
            var cardView = cell.GetComponent<Common.CardView>();
            if (cardView != null)
            {
                cardView.SetCard(card);
                return;
            }

            // フォールバック: TMPro テキストがあればカード名を表示
            var tmp = cell.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null)
                tmp.text = card.cardName;

            // アスペクトカラーの反映
            var img = cell.GetComponent<Image>();
            if (img != null)
            {
                var color = AspectColors.GetColor(card.aspect);
                color.a = 0.15f;
                img.color = color;
            }
        }
    }
}
