using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Data;
using Banganka.UI.Tween;
using DG.Tweening;

namespace Banganka.UI.Cards
{
    public class CardListController : MonoBehaviour
    {
        Transform _content;
        CardType? _typeFilter;
        Aspect? _aspectFilter;
        string _searchQuery = "";

        // Detail panel
        GameObject _detailPanel;
        TextMeshProUGUI _detailName;
        TextMeshProUGUI _detailType;
        TextMeshProUGUI _detailStats;
        TextMeshProUGUI _detailEffect;
        TextMeshProUGUI _detailFlavor;
        Image _detailAccent;

        public enum SortMode { Default, CostAsc, CostDesc, NameAsc }
        SortMode _sortMode = SortMode.Default;

        public void Init(Transform content)
        {
            _content = content;
        }

        public void InitDetailPanel(GameObject panel, TextMeshProUGUI nameT, TextMeshProUGUI typeT,
            TextMeshProUGUI statsT, TextMeshProUGUI effectT, TextMeshProUGUI flavorT, Image accent)
        {
            _detailPanel = panel;
            _detailName = nameT;
            _detailType = typeT;
            _detailStats = statsT;
            _detailEffect = effectT;
            _detailFlavor = flavorT;
            _detailAccent = accent;
        }

        public void SetTypeFilter(CardType? type)
        {
            _typeFilter = type;
            ApplyFilters();
        }

        public void SetAspectFilter(Aspect? aspect)
        {
            _aspectFilter = aspect;
            ApplyFilters();
        }

        public void SetSearchQuery(string query)
        {
            _searchQuery = query?.ToLower() ?? "";
            ApplyFilters();
        }

        public void SetSort(SortMode mode)
        {
            _sortMode = mode;
            ApplySort();
            ApplyFilters();
        }

        public void ShowDetail(string cardId)
        {
            if (_detailPanel == null) return;
            if (!CardDatabase.AllCards.TryGetValue(cardId, out var card)) return;

            _detailPanel.SetActive(true);
            UITweenAnimations.PanelPopIn(_detailPanel.transform);

            if (_detailName) _detailName.text = card.cardName;

            string typeStr = card.type switch
            {
                CardType.Manifest => "顕現",
                CardType.Spell => "詠術",
                CardType.Algorithm => "界律",
                _ => ""
            };
            if (_detailType)
                _detailType.text = $"{typeStr}  [{AspectColors.GetDisplayName(card.aspect)}]  CP {card.cpCost}";

            if (_detailAccent)
                _detailAccent.color = AspectColors.GetColor(card.aspect);

            // Stats
            string stats = "";
            if (card.type == CardType.Manifest)
            {
                stats = $"戦力 {card.battlePower}    願撃 {card.wishDamage}";
                if (card.keywords != null && card.keywords.Length > 0)
                    stats += $"\nキーワード: {string.Join(", ", card.keywords)}";
            }
            if (_detailStats) _detailStats.text = stats;

            // Effect
            string effect = "";
            if (card.type == CardType.Spell)
            {
                effect = card.effectKey switch
                {
                    "SPELL_PUSH_SMALL" => $"願力を {card.baseGaugeDelta} 自分側へ押し込む",
                    "SPELL_PUSH_MEDIUM" => $"願力を {card.baseGaugeDelta} 自分側へ押し込む",
                    "SPELL_POWER_PLUS" => $"味方顕現1体の戦力を +{card.powerDelta}",
                    "SPELL_WISHDMG_PLUS" => $"味方顕現1体の願撃を +{card.wishDamageDelta}",
                    "SPELL_REST" => $"相手顕現を {card.restTargets} 体消耗状態にする",
                    "SPELL_REMOVE_DAMAGED" => $"条件を満たす相手顕現を除去（{card.removeCondition}）",
                    _ => card.effectKey
                };
            }
            else if (card.type == CardType.Algorithm)
            {
                if (card.globalRule != null)
                    effect += $"全体効果: {DescribeRule(card.globalRule)}";
                if (card.ownerBonus != null)
                    effect += $"\n設置者ボーナス: {DescribeRule(card.ownerBonus)}";
            }
            if (_detailEffect) _detailEffect.text = effect;

            if (_detailFlavor) _detailFlavor.text = card.flavorText ?? "";
        }

        static string DescribeRule(AlgorithmRule rule)
        {
            string desc = rule.kind switch
            {
                "spell_gauge_plus" => $"詠術の願力変動 +{rule.value}",
                "power_plus" => $"戦力 +{rule.value}",
                "grant_rush" => "Rush付与",
                "wish_damage_plus" => $"願撃 +{rule.value}",
                "direct_hit_plus" => $"直撃願撃 +{rule.value}",
                _ => rule.kind
            };
            if (!string.IsNullOrEmpty(rule.condition))
                desc += $"（条件: {rule.condition}）";
            return desc;
        }

        public void HideDetail()
        {
            if (_detailPanel)
                UITweenAnimations.PanelPopOut(_detailPanel.transform);
        }

        void ApplySort()
        {
            if (_content == null) return;

            var children = new List<Transform>();
            for (int i = 0; i < _content.childCount; i++)
                children.Add(_content.GetChild(i));

            children.Sort((a, b) =>
            {
                CardDatabase.AllCards.TryGetValue(a.name, out var cardA);
                CardDatabase.AllCards.TryGetValue(b.name, out var cardB);
                if (cardA == null || cardB == null) return 0;

                return _sortMode switch
                {
                    SortMode.CostAsc => cardA.cpCost.CompareTo(cardB.cpCost),
                    SortMode.CostDesc => cardB.cpCost.CompareTo(cardA.cpCost),
                    SortMode.NameAsc => string.Compare(cardA.cardName, cardB.cardName, StringComparison.Ordinal),
                    _ => 0
                };
            });

            for (int i = 0; i < children.Count; i++)
                children[i].SetSiblingIndex(i);
        }

        void ApplyFilters()
        {
            if (_content == null) return;

            for (int i = 0; i < _content.childCount; i++)
            {
                var child = _content.GetChild(i);
                if (CardDatabase.AllCards.TryGetValue(child.name, out var card))
                {
                    bool show = true;
                    if (_typeFilter.HasValue && card.type != _typeFilter.Value) show = false;
                    if (_aspectFilter.HasValue && card.aspect != _aspectFilter.Value) show = false;
                    if (!string.IsNullOrEmpty(_searchQuery) && !card.cardName.ToLower().Contains(_searchQuery))
                        show = false;
                    child.gameObject.SetActive(show);
                }
            }
        }
    }
}
