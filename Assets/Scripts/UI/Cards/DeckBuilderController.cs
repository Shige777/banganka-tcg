using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Data;
using Banganka.Core.Config;

namespace Banganka.UI.Cards
{
    /// <summary>
    /// デッキビルダーUI (DECK_BUILDER_SPEC.md 準拠)
    /// 34枚スロット、フィルタ、コストカーブ、デッキコード
    /// </summary>
    public class DeckBuilderController : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TMP_InputField deckNameInput;
        [SerializeField] TextMeshProUGUI cardCountText;
        [SerializeField] TextMeshProUGUI typeBreakdownText;
        [SerializeField] Transform deckSlotParent;
        [SerializeField] Transform cardGridParent;
        [SerializeField] Button saveButton;
        [SerializeField] Button backButton;
        [SerializeField] TextMeshProUGUI deckCodeText;
        [SerializeField] Button autoBuildButton;

        // Filters
        [SerializeField] TMP_Dropdown aspectDropdown;
        [SerializeField] TMP_Dropdown typeDropdown;
        [SerializeField] TMP_Dropdown costDropdown;
        [SerializeField] TMP_Dropdown sortDropdown;
        [SerializeField] TMP_InputField searchInput;

        // Cost curve bars
        [SerializeField] Transform costCurveParent;

        string _editingDeckId;
        string _leaderId;
        readonly List<string> _deckCards = new();
        readonly List<GameObject> _slotInstances = new();
        readonly List<GameObject> _gridInstances = new();

        // Filters state
        Aspect? _aspectFilter;
        CardType? _typeFilter;
        int? _costFilter;
        string _searchQuery = "";

        public enum SortMode { CostAsc, CostDesc, Name, Rarity }
        SortMode _sortMode = SortMode.CostAsc;

        void OnEnable()
        {
            if (titleText) titleText.text = "デッキ編集";
            if (autoBuildButton) autoBuildButton.onClick.AddListener(OnAutoBuild);
            RefreshAll();
        }

        void OnDisable()
        {
            if (autoBuildButton) autoBuildButton.onClick.RemoveListener(OnAutoBuild);
        }

        public void LoadDeck(DeckData deck)
        {
            _editingDeckId = deck.deckId;
            _leaderId = deck.leaderId;
            _deckCards.Clear();
            _deckCards.AddRange(deck.cardIds);
            if (deckNameInput) deckNameInput.text = deck.name;
            RefreshAll();
        }

        public void NewDeck()
        {
            _editingDeckId = Guid.NewGuid().ToString("N")[..8];
            _leaderId = CardDatabase.DefaultLeader.id;
            _deckCards.Clear();
            if (deckNameInput) deckNameInput.text = "新しいデッキ";
            RefreshAll();
        }

        void RefreshAll()
        {
            RefreshDeckSlots();
            RefreshCardGrid();
            RefreshCounters();
            RefreshCostCurve();
        }

        void RefreshCounters()
        {
            int count = _deckCards.Count;
            bool complete = count == BalanceConfig.DeckSize;

            if (cardCountText)
            {
                cardCountText.text = $"{count}/{BalanceConfig.DeckSize}";
                cardCountText.color = complete ? Color.green : Color.red;
            }

            if (typeBreakdownText)
            {
                int manifest = _deckCards.Count(id => CardDatabase.AllCards.TryGetValue(id, out var c) && c.type == CardType.Manifest);
                int spell = _deckCards.Count(id => CardDatabase.AllCards.TryGetValue(id, out var c) && c.type == CardType.Spell);
                int algo = _deckCards.Count(id => CardDatabase.AllCards.TryGetValue(id, out var c) && c.type == CardType.Algorithm);
                typeBreakdownText.text = $"顕現:{manifest} 詠術:{spell} 界律:{algo}";
            }

            if (saveButton) saveButton.interactable = complete;
        }

        void RefreshDeckSlots()
        {
            foreach (var inst in _slotInstances) if (inst) Destroy(inst);
            _slotInstances.Clear();

            if (deckSlotParent == null) return;

            for (int i = 0; i < _deckCards.Count; i++)
            {
                var cardId = _deckCards[i];
                if (!CardDatabase.AllCards.TryGetValue(cardId, out var card)) continue;

                var obj = CreateMiniCardSlot(deckSlotParent, card, i);
                _slotInstances.Add(obj);
            }
        }

        GameObject CreateMiniCardSlot(Transform parent, CardData card, int index)
        {
            var obj = new GameObject(card.id);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(50, 70);
            var bg = obj.AddComponent<Image>();
            bg.color = new Color(AspectColors.GetColor(card.aspect).r,
                AspectColors.GetColor(card.aspect).g,
                AspectColors.GetColor(card.aspect).b, 0.7f);

            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(obj.transform, false);
            var nrt = nameObj.AddComponent<RectTransform>();
            nrt.anchorMin = Vector2.zero;
            nrt.anchorMax = Vector2.one;
            nrt.offsetMin = Vector2.zero;
            nrt.offsetMax = Vector2.zero;
            var tmp = nameObj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"{card.cpCost}\n{card.cardName}";
            tmp.fontSize = 8;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            var btn = obj.AddComponent<Button>();
            int idx = index;
            btn.onClick.AddListener(() => RemoveCard(idx));

            return obj;
        }

        void RefreshCardGrid()
        {
            foreach (var inst in _gridInstances) if (inst) Destroy(inst);
            _gridInstances.Clear();

            if (cardGridParent == null) return;

            var cards = GetFilteredCards();
            foreach (var card in cards)
            {
                var obj = CreateGridCard(cardGridParent, card);
                _gridInstances.Add(obj);
            }
        }

        List<CardData> GetFilteredCards()
        {
            var cards = CardDatabase.AllCards.Values.AsEnumerable();

            if (_aspectFilter.HasValue)
                cards = cards.Where(c => c.aspect == _aspectFilter.Value);
            if (_typeFilter.HasValue)
                cards = cards.Where(c => c.type == _typeFilter.Value);
            if (_costFilter.HasValue)
                cards = cards.Where(c => _costFilter.Value >= 10 ? c.cpCost >= 10 : c.cpCost == _costFilter.Value);
            if (!string.IsNullOrEmpty(_searchQuery))
                cards = cards.Where(c => c.cardName.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));

            cards = _sortMode switch
            {
                SortMode.CostAsc => cards.OrderBy(c => c.cpCost).ThenBy(c => c.cardName),
                SortMode.CostDesc => cards.OrderByDescending(c => c.cpCost).ThenBy(c => c.cardName),
                SortMode.Name => cards.OrderBy(c => c.cardName),
                SortMode.Rarity => cards.OrderByDescending(c => RarityOrder(c.rarity)).ThenBy(c => c.cpCost),
                _ => cards.OrderBy(c => c.cpCost),
            };

            return cards.ToList();
        }

        static int RarityOrder(string rarity) => rarity switch
        {
            "SSR" => 4, "SR" => 3, "R" => 2, "C" => 1, _ => 0
        };

        GameObject CreateGridCard(Transform parent, CardData card)
        {
            var obj = new GameObject(card.id);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 140);
            var bg = obj.AddComponent<Image>();

            int inDeck = _deckCards.Count(id => id == card.id);
            bool canAdd = inDeck < BalanceConfig.SameNameLimit && _deckCards.Count < BalanceConfig.DeckSize;
            bg.color = canAdd ? new Color(0.15f, 0.15f, 0.2f, 0.9f) : new Color(0.1f, 0.1f, 0.1f, 0.5f);

            // Card info
            var nameObj = new GameObject("Info");
            nameObj.transform.SetParent(obj.transform, false);
            var nrt = nameObj.AddComponent<RectTransform>();
            nrt.anchorMin = Vector2.zero;
            nrt.anchorMax = Vector2.one;
            nrt.offsetMin = new Vector2(4, 4);
            nrt.offsetMax = new Vector2(-4, -4);
            var tmp = nameObj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"<b>{card.cpCost}</b> {card.cardName}\n{inDeck}/{BalanceConfig.SameNameLimit}";
            tmp.fontSize = 10;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            if (canAdd)
            {
                var btn = obj.AddComponent<Button>();
                var captured = card;
                btn.onClick.AddListener(() => AddCard(captured.id));
            }

            return obj;
        }

        void RefreshCostCurve()
        {
            if (costCurveParent == null) return;

            // Count cards per cost
            var costCounts = new int[11]; // 0-9 + 10+
            foreach (var id in _deckCards)
            {
                if (CardDatabase.AllCards.TryGetValue(id, out var card))
                {
                    int bucket = Math.Min(card.cpCost, 10);
                    costCounts[bucket]++;
                }
            }

            int maxCount = costCounts.Max();
            if (maxCount == 0) maxCount = 1;

            // Clear existing
            for (int i = costCurveParent.childCount - 1; i >= 0; i--)
                Destroy(costCurveParent.GetChild(i).gameObject);

            // Create bars
            for (int cost = 0; cost <= 10; cost++)
            {
                var bar = new GameObject($"Cost{cost}");
                bar.transform.SetParent(costCurveParent, false);
                var brt = bar.AddComponent<RectTransform>();
                brt.sizeDelta = new Vector2(20, costCounts[cost] * 30f / maxCount + 5);
                var bimg = bar.AddComponent<Image>();
                bimg.color = costCounts[cost] > 0 ? new Color(0.3f, 0.5f, 1f) : new Color(0.2f, 0.2f, 0.3f);
            }
        }

        // ====================================================================
        // Auto Build
        // ====================================================================

        public void OnAutoBuild()
        {
            // リーダーのアスペクトで自動構築
            var result = AutoDeckBuilder.BuildForLeader(_leaderId);
            if (result == null)
            {
                Debug.LogWarning("[DeckBuilder] Auto build failed — not enough owned cards");
                return;
            }

            _deckCards.Clear();
            _deckCards.AddRange(result);
            RefreshAll();
            Debug.Log($"[DeckBuilder] Auto build complete: {_deckCards.Count} cards");
        }

        // ====================================================================
        // Actions
        // ====================================================================

        public void AddCard(string cardId)
        {
            int inDeck = _deckCards.Count(id => id == cardId);
            if (inDeck >= BalanceConfig.SameNameLimit) return;
            if (_deckCards.Count >= BalanceConfig.DeckSize) return;

            _deckCards.Add(cardId);
            RefreshAll();
        }

        public void RemoveCard(int index)
        {
            if (index < 0 || index >= _deckCards.Count) return;
            _deckCards.RemoveAt(index);
            RefreshAll();
        }

        public void OnSave()
        {
            if (_deckCards.Count != BalanceConfig.DeckSize) return;

            var deck = new DeckData
            {
                deckId = _editingDeckId,
                name = deckNameInput?.text ?? "デッキ",
                leaderId = _leaderId,
                cardIds = new List<string>(_deckCards),
            };

            // Save to player data
            var existing = PlayerData.Instance.decks.FindIndex(d => d.deckId == deck.deckId);
            if (existing >= 0)
                PlayerData.Instance.decks[existing] = deck;
            else
                PlayerData.Instance.decks.Add(deck);

            Debug.Log($"[DeckBuilder] Saved deck: {deck.name} ({deck.cardIds.Count} cards)");
        }

        // ====================================================================
        // Filter callbacks
        // ====================================================================

        public void OnAspectFilterChanged()
        {
            if (aspectDropdown == null) return;
            _aspectFilter = aspectDropdown.value > 0 ? (Aspect)(aspectDropdown.value - 1) : null;
            RefreshCardGrid();
        }

        public void OnTypeFilterChanged()
        {
            if (typeDropdown == null) return;
            _typeFilter = typeDropdown.value switch
            {
                1 => CardType.Manifest,
                2 => CardType.Spell,
                3 => CardType.Algorithm,
                _ => null
            };
            RefreshCardGrid();
        }

        public void OnCostFilterChanged()
        {
            if (costDropdown == null) return;
            _costFilter = costDropdown.value > 0 ? costDropdown.value - 1 : null;
            RefreshCardGrid();
        }

        public void OnSortChanged()
        {
            if (sortDropdown == null) return;
            _sortMode = sortDropdown.value switch
            {
                0 => SortMode.CostAsc,
                1 => SortMode.CostDesc,
                2 => SortMode.Name,
                3 => SortMode.Rarity,
                _ => SortMode.CostAsc,
            };
            RefreshCardGrid();
        }

        public void OnSearchChanged()
        {
            _searchQuery = searchInput?.text ?? "";
            RefreshCardGrid();
        }

        // ====================================================================
        // Deck Code (BNG1:{base64}:{CRC8})
        // ====================================================================

        public string ExportDeckCode()
        {
            var code = DeckCodec.Encode(_leaderId, _deckCards);
            if (deckCodeText) deckCodeText.text = code;
            return code;
        }

        public bool ImportDeckCode(string code)
        {
            if (!DeckCodec.Decode(code, out string leaderId, out List<string> cards))
                return false;

            _leaderId = leaderId;
            _deckCards.Clear();
            _deckCards.AddRange(cards);
            RefreshAll();
            return true;
        }
    }

    /// <summary>
    /// デッキコード: BNG1:{base64}:{CRC8} (DECK_BUILDER_SPEC.md)
    /// </summary>
    public static class DeckCodec
    {
        public static string Encode(string leaderId, List<string> cardIds)
        {
            // Group cards by id with count
            var counts = new Dictionary<string, int>();
            foreach (var id in cardIds)
            {
                if (!counts.ContainsKey(id)) counts[id] = 0;
                counts[id]++;
            }

            // Simple JSON: {"w":"leaderId","c":[["cardId",count],...]}
            var sb = new StringBuilder();
            sb.Append("{\"w\":\"").Append(leaderId).Append("\",\"c\":[");
            bool first = true;
            foreach (var kv in counts)
            {
                if (!first) sb.Append(",");
                sb.Append("[\"").Append(kv.Key).Append("\",").Append(kv.Value).Append("]");
                first = false;
            }
            sb.Append("]}");

            var json = sb.ToString();
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            var crc = ComputeCRC8(Encoding.UTF8.GetBytes(json));

            return $"BNG1:{base64}:{crc:X2}";
        }

        public static bool Decode(string code, out string leaderId, out List<string> cards)
        {
            leaderId = null;
            cards = null;

            if (string.IsNullOrEmpty(code) || !code.StartsWith("BNG1:"))
                return false;

            var parts = code.Split(':');
            if (parts.Length < 2) return false;

            string base64 = parts[1];

            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));

                // Validate CRC if present
                if (parts.Length >= 3)
                {
                    var expectedCrc = Convert.ToByte(parts[2], 16);
                    var actualCrc = ComputeCRC8(Encoding.UTF8.GetBytes(json));
                    if (expectedCrc != actualCrc) return false;
                }

                // Simple JSON parse (avoid dependency on JsonUtility for Dictionary)
                leaderId = ExtractJsonString(json, "w");
                cards = new List<string>();

                // Parse card array manually
                int arrStart = json.IndexOf("\"c\":[", StringComparison.Ordinal);
                if (arrStart < 0) return false;
                arrStart = json.IndexOf('[', arrStart + 4);

                string arrStr = json.Substring(arrStart);
                // Parse [[id,count],...]
                int pos = 1; // skip opening [
                while (pos < arrStr.Length)
                {
                    int entryStart = arrStr.IndexOf('[', pos);
                    if (entryStart < 0) break;
                    int entryEnd = arrStr.IndexOf(']', entryStart);
                    if (entryEnd < 0) break;

                    string entry = arrStr.Substring(entryStart + 1, entryEnd - entryStart - 1);
                    var entryParts = entry.Split(',');
                    if (entryParts.Length >= 2)
                    {
                        string cardId = entryParts[0].Trim().Trim('"');
                        int count = int.Parse(entryParts[1].Trim());
                        for (int i = 0; i < count; i++)
                            cards.Add(cardId);
                    }

                    pos = entryEnd + 1;
                }

                return leaderId != null && cards.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        static string ExtractJsonString(string json, string key)
        {
            string search = $"\"{key}\":\"";
            int start = json.IndexOf(search, StringComparison.Ordinal);
            if (start < 0) return null;
            start += search.Length;
            int end = json.IndexOf('"', start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        static byte ComputeCRC8(byte[] data)
        {
            byte crc = 0;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x80) != 0)
                        crc = (byte)((crc << 1) ^ 0x07);
                    else
                        crc <<= 1;
                }
            }
            return crc;
        }
    }
}
