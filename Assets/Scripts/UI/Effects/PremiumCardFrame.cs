using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Data;

namespace Banganka.UI.Effects
{
    /// <summary>
    /// Builds a high-quality card visual with layered frame, glow, cost badge, and stat display.
    /// Designed to match competitor TCG card quality (Snap/Shadowverse level).
    /// </summary>
    public static class PremiumCardFrame
    {
        public static GameObject Create(Transform parent, CardData card, float width = 200, float height = 280)
        {
            Color aspect = AspectColors.GetColor(card.aspect);
            Color aspectDark = new(aspect.r * 0.3f, aspect.g * 0.3f, aspect.b * 0.3f, 1f);
            Color aspectMid = new(aspect.r * 0.5f, aspect.g * 0.5f, aspect.b * 0.5f, 1f);

            // Root
            var root = new GameObject(card.id);
            root.transform.SetParent(parent, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(width, height);

            // Outer glow (slightly larger, semi-transparent aspect color)
            var glow = MakeChild(root, "OuterGlow", -2, -2, width + 4, height + 4);
            var glowImg = glow.AddComponent<Image>();
            glowImg.color = new Color(aspect.r, aspect.g, aspect.b, 0.25f);
            glowImg.raycastTarget = false;

            // Card background
            var bg = MakeChild(root, "CardBg", 0, 0, width, height);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.06f, 0.10f, 0.97f);

            // Top accent bar (aspect color)
            var topBar = MakeChild(root, "TopBar", 0, height * 0.5f - 3f, width, 6f);
            var topBarImg = topBar.AddComponent<Image>();
            topBarImg.color = aspect;
            topBarImg.raycastTarget = false;

            // Left accent strip
            var leftStrip = MakeChild(root, "LeftStrip", -width * 0.5f + 1.5f, 0, 3f, height);
            var lsImg = leftStrip.AddComponent<Image>();
            lsImg.color = new Color(aspect.r, aspect.g, aspect.b, 0.5f);
            lsImg.raycastTarget = false;

            // Art area (placeholder gradient)
            float artHeight = height * 0.35f;
            float artY = height * 0.15f;
            var artArea = MakeChild(root, "ArtArea", 0, artY, width - 12, artHeight);
            var artBg = artArea.AddComponent<Image>();
            artBg.color = aspectDark;
            artBg.raycastTarget = false;

            // Art area inner glow
            var artGlow = MakeChild(artArea, "ArtGlow", 0, 0, width - 16, artHeight - 4);
            var agImg = artGlow.AddComponent<Image>();
            agImg.color = new Color(aspect.r * 0.15f, aspect.g * 0.15f, aspect.b * 0.15f, 0.6f);
            agImg.raycastTarget = false;

            // Type icon area (top-left of art)
            string typeStr = card.type switch
            {
                CardType.Manifest => "顕",
                CardType.Spell => "詠",
                CardType.Algorithm => "律",
                _ => "?"
            };
            var typeIcon = MakeChild(artArea, "TypeIcon",
                -width * 0.5f + 24, artHeight * 0.5f - 14, 24, 24);
            var tiImg = typeIcon.AddComponent<Image>();
            tiImg.color = new Color(0, 0, 0, 0.6f);
            var tiText = MakeTextChild(typeIcon, typeStr, 12, FontStyles.Bold, Color.white);

            // Cost badge (top-left, prominent)
            float badgeSize = Mathf.Min(width, height) * 0.18f;
            var costBadge = MakeChild(root, "CostBadge",
                -width * 0.5f + badgeSize * 0.5f + 4,
                height * 0.5f - badgeSize * 0.5f - 4,
                badgeSize, badgeSize);
            var cbImg = costBadge.AddComponent<Image>();
            cbImg.color = aspect;

            // Cost outline
            var costOutline = MakeChild(costBadge, "Outline", 0, 0, badgeSize + 3, badgeSize + 3);
            var coImg = costOutline.AddComponent<Image>();
            coImg.color = new Color(0, 0, 0, 0.5f);
            coImg.raycastTarget = false;
            costOutline.transform.SetAsFirstSibling();

            var costText = MakeTextChild(costBadge, card.cpCost.ToString(), (int)(badgeSize * 0.6f),
                FontStyles.Bold, Color.white);
            costText.outlineWidth = 0.25f;
            costText.outlineColor = new Color32(0, 0, 0, 200);

            // Card name area
            float nameY = height * 0.5f - 16f;
            var nameBar = MakeChild(root, "NameBar", 0, nameY - 14, width - 8, 28);
            var nbImg = nameBar.AddComponent<Image>();
            nbImg.color = new Color(0.04f, 0.04f, 0.07f, 0.9f);
            nbImg.raycastTarget = false;

            var nameAccent = MakeChild(nameBar, "Accent", -width * 0.5f + 5.5f, 0, 3, 24);
            var naImg = nameAccent.AddComponent<Image>();
            naImg.color = aspect;
            naImg.raycastTarget = false;

            var nameTmp = MakeTextChild(nameBar, card.cardName, 16, FontStyles.Bold, Color.white,
                TextAlignmentOptions.MidlineLeft, new Vector2(12, 0));

            // Aspect chip (below name)
            string aspectStr = AspectColors.GetDisplayName(card.aspect);
            string typeFullStr = card.type switch
            {
                CardType.Manifest => "顕現",
                CardType.Spell => "詠術",
                CardType.Algorithm => "界律",
                _ => ""
            };
            float chipY = nameY - 38;
            var chipBar = MakeChild(root, "ChipBar", 0, chipY, width - 16, 18);
            var chipTmp = MakeTextChild(chipBar, $"{typeFullStr}  [{aspectStr}]", 11, FontStyles.Normal,
                new Color(0.6f, 0.6f, 0.7f), TextAlignmentOptions.MidlineLeft, new Vector2(4, 0));

            // Stats area (bottom)
            if (card.type == CardType.Manifest)
            {
                // Power badge (bottom-left)
                var pwrArea = MakeChild(root, "PowerArea",
                    -width * 0.5f + 40, -height * 0.5f + 20, 70, 30);
                var pwrBg = pwrArea.AddComponent<Image>();
                pwrBg.color = new Color(0.15f, 0.1f, 0.05f, 0.9f);
                var pwrTmp = MakeTextChild(pwrArea, $"⚔{card.battlePower}", 14, FontStyles.Bold,
                    new Color(1f, 0.85f, 0.4f));

                // Wish damage badge (bottom-right)
                var wdArea = MakeChild(root, "WishDmgArea",
                    width * 0.5f - 40, -height * 0.5f + 20, 50, 30);
                var wdBg = wdArea.AddComponent<Image>();
                wdBg.color = new Color(0.2f, 0.05f, 0.05f, 0.9f);
                var wdTmp = MakeTextChild(wdArea, $"♦{card.wishDamage}", 14, FontStyles.Bold,
                    new Color(1f, 0.4f, 0.4f));

                // Keywords
                if (card.keywords != null && card.keywords.Length > 0)
                {
                    float kwY = -height * 0.5f + 50;
                    var kwArea = MakeChild(root, "Keywords", 0, kwY, width - 16, 20);
                    var kwTmp = MakeTextChild(kwArea, string.Join("  ", card.keywords), 11,
                        FontStyles.Italic, new Color(0.8f, 0.7f, 1f));
                }
            }
            else if (card.type == CardType.Spell)
            {
                string effectDesc = DescribeSpellEffect(card);
                float effY = -height * 0.5f + 40;
                var effArea = MakeChild(root, "Effect", 0, effY, width - 16, 50);
                var effTmp = MakeTextChild(effArea, effectDesc, 12, FontStyles.Normal,
                    new Color(0.7f, 0.8f, 1f), TextAlignmentOptions.Center);
                effTmp.textWrappingMode = TextWrappingModes.Normal;
            }
            else if (card.type == CardType.Algorithm)
            {
                string algoDesc = "";
                if (card.globalRule != null)
                    algoDesc += $"全体: {DescribeAlgoRule(card.globalRule)}";
                if (card.ownerBonus != null)
                    algoDesc += $"\n設置者: {DescribeAlgoRule(card.ownerBonus)}";
                float algoY = -height * 0.5f + 40;
                var algoArea = MakeChild(root, "AlgoEffect", 0, algoY, width - 16, 50);
                var algoTmp = MakeTextChild(algoArea, algoDesc, 11, FontStyles.Normal,
                    new Color(1f, 0.8f, 0.6f), TextAlignmentOptions.Center);
                algoTmp.textWrappingMode = TextWrappingModes.Normal;
            }

            // Flavor text
            if (!string.IsNullOrEmpty(card.flavorText))
            {
                float flvY = -height * 0.5f + 12;
                var flvArea = MakeChild(root, "Flavor", 0, flvY, width - 20, 18);
                var flvTmp = MakeTextChild(flvArea, card.flavorText, 9, FontStyles.Italic,
                    new Color(0.45f, 0.45f, 0.5f), TextAlignmentOptions.Center);
                flvTmp.textWrappingMode = TextWrappingModes.Normal;
                flvTmp.overflowMode = TextOverflowModes.Ellipsis;
            }

            // Bottom bar
            var bottomBar = MakeChild(root, "BottomBar", 0, -height * 0.5f + 1.5f, width, 3);
            var bbImg = bottomBar.AddComponent<Image>();
            bbImg.color = aspectMid;
            bbImg.raycastTarget = false;

            return root;
        }

        // Compact card for hand / list (smaller, essential info only)
        public static GameObject CreateCompact(Transform parent, CardData card, float width = 160, float height = 100)
        {
            Color aspect = AspectColors.GetColor(card.aspect);

            var root = new GameObject(card.id);
            root.transform.SetParent(parent, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(width, height);

            // Background
            var bg = MakeChild(root, "Bg", 0, 0, width, height);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(aspect.r * 0.2f, aspect.g * 0.2f, aspect.b * 0.2f, 0.95f);

            // Left accent
            var acc = MakeChild(root, "Accent", -width * 0.5f + 1.5f, 0, 3, height);
            var accImg = acc.AddComponent<Image>();
            accImg.color = aspect;
            accImg.raycastTarget = false;

            // Cost
            var cost = MakeChild(root, "Cost", -width * 0.5f + 16, height * 0.5f - 12, 22, 22);
            var cImg = cost.AddComponent<Image>();
            cImg.color = aspect;
            var cTmp = MakeTextChild(cost, card.cpCost.ToString(), 13, FontStyles.Bold, Color.white);
            cTmp.outlineWidth = 0.2f;
            cTmp.outlineColor = new Color32(0, 0, 0, 200);

            // Name
            var name = MakeChild(root, "Name", 12, height * 0.5f - 12, width - 40, 20);
            MakeTextChild(name, card.cardName, 13, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);

            // Type line
            string typeStr = card.type switch
            {
                CardType.Manifest => "顕現",
                CardType.Spell => "詠術",
                CardType.Algorithm => "界律",
                _ => ""
            };
            var typeLine = MakeChild(root, "Type", 0, height * 0.5f - 32, width - 12, 14);
            MakeTextChild(typeLine, $"{typeStr} [{AspectColors.GetDisplayName(card.aspect)}]",
                10, FontStyles.Normal, new Color(0.55f, 0.55f, 0.65f), TextAlignmentOptions.MidlineLeft, new Vector2(6, 0));

            // Stats
            if (card.type == CardType.Manifest)
            {
                var stats = MakeChild(root, "Stats", 0, -height * 0.5f + 14, width - 12, 18);
                string kwStr = card.keywords != null && card.keywords.Length > 0
                    ? "  " + string.Join(" ", card.keywords) : "";
                MakeTextChild(stats, $"⚔{card.battlePower} ♦{card.wishDamage}{kwStr}",
                    11, FontStyles.Bold, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.MidlineLeft, new Vector2(6, 0));
            }

            return root;
        }

        // ---- Helpers ----

        static GameObject MakeChild(GameObject parent, string name, float x, float y, float w, float h)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return obj;
        }

        static TextMeshProUGUI MakeTextChild(GameObject parent, string text, int size,
            FontStyles style, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center,
            Vector2? offset = null)
        {
            var obj = new GameObject("Text");
            obj.transform.SetParent(parent.transform, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offset ?? Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            var jpFont = Banganka.Game.AutoBootstrap.JapaneseFont;
            if (jpFont != null) tmp.font = jpFont;
            return tmp;
        }

        static string DescribeSpellEffect(CardData card)
        {
            return card.effectKey switch
            {
                "SPELL_PUSH_SMALL" => $"願力を {card.baseGaugeDelta} 押し込む",
                "SPELL_PUSH_MEDIUM" => $"願力を {card.baseGaugeDelta} 押し込む",
                "SPELL_POWER_PLUS" => $"味方の戦力 +{card.powerDelta}",
                "SPELL_WISHDMG_PLUS" => $"味方の願撃 +{card.wishDamageDelta}",
                "SPELL_REST" => $"相手{card.restTargets}体を消耗",
                "SPELL_REMOVE_DAMAGED" => $"条件除去({card.removeCondition})",
                _ => card.effectKey ?? ""
            };
        }

        static string DescribeAlgoRule(AlgorithmRule rule)
        {
            return rule.kind switch
            {
                "spell_gauge_plus" => $"詠術願力+{rule.value}",
                "power_plus" => $"戦力+{rule.value}",
                "grant_rush" => "Rush付与",
                "wish_damage_plus" => $"願撃+{rule.value}",
                "direct_hit_plus" => $"直撃+{rule.value}",
                _ => rule.kind
            } + (string.IsNullOrEmpty(rule.condition) ? "" : $" ({rule.condition})");
        }
    }
}
