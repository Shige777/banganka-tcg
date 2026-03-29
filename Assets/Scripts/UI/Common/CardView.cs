using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Data;
using Banganka.Core.Config;
using Banganka.UI.Effects;

namespace Banganka.UI.Common
{
    public class CardView : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI cardNameText;
        [SerializeField] TextMeshProUGUI costText;
        [SerializeField] Image costBackground;
        [SerializeField] TextMeshProUGUI typeText;
        [SerializeField] TextMeshProUGUI powerText;
        [SerializeField] TextMeshProUGUI wishDamageText;
        [SerializeField] TextMeshProUGUI keywordsText;
        [SerializeField] TextMeshProUGUI effectText;
        [SerializeField] Image cardIllustration;
        [SerializeField] Image aspectGlow;
        [SerializeField] Image cardFrame;
        [SerializeField] Image aspectChip;

        CardShaderController _shaderController;
        CardData _data;
        bool _uiBuilt;

        public CardData Data => _data;

        void OnEnable()
        {
            AccessibilitySettings.OnSettingsChanged += OnAccessibilityChanged;
        }

        void OnDisable()
        {
            AccessibilitySettings.OnSettingsChanged -= OnAccessibilityChanged;
        }

        void OnAccessibilityChanged()
        {
            if (_data != null) SetCard(_data);
        }

        /// <summary>
        /// SerializeFieldが未接続の場合、UIを自動構築する。
        /// Cards/CardView.prefab等、ベアプレハブで使われるケース向け。
        /// </summary>
        void EnsureUI()
        {
            if (_uiBuilt) return;
            if (cardNameText != null) { _uiBuilt = true; return; } // Prefab has wired refs

            _uiBuilt = true;

            // Ensure RectTransform on root
            var rootRt = GetComponent<RectTransform>();
            if (rootRt == null)
                rootRt = gameObject.AddComponent<RectTransform>();

            // CanvasRenderer + background Image
            if (GetComponent<CanvasRenderer>() == null)
                gameObject.AddComponent<CanvasRenderer>();
            var bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

            var jpFont = Banganka.Game.AutoBootstrap.JapaneseFont;

            // Aspect Glow (behind everything)
            var glowObj = MakeChild("Glow", 0, 0, 1, 1);
            glowObj.transform.SetAsFirstSibling();
            aspectGlow = glowObj.AddComponent<Image>();
            aspectGlow.color = new Color(1, 1, 1, 0);
            aspectGlow.raycastTarget = false;

            // Card Illustration
            var illObj = MakeChild("Illustration", 0, 0.28f, 1, 0.84f);
            illObj.transform.SetSiblingIndex(1);
            cardIllustration = illObj.AddComponent<Image>();
            cardIllustration.color = Color.white;
            cardIllustration.preserveAspect = true;
            cardIllustration.raycastTarget = false;
            cardIllustration.enabled = false;

            // Cost BG
            var costBgObj = MakeChild("CostBg", 0, 0.84f, 0.22f, 1);
            costBackground = costBgObj.AddComponent<Image>();
            costBackground.color = Color.gray;

            // Cost Text
            costText = MakeTMPChild(costBgObj, "CostVal", "", 16, Color.white, true, jpFont);

            // Card Name
            cardNameText = MakeTMPChild("CardName", "", 13, Color.white, false, jpFont,
                0.24f, 0.84f, 1, 1, TextAlignmentOptions.MidlineLeft);

            // Type
            typeText = MakeTMPChild("Type", "", 10, new Color(0.7f, 0.7f, 0.8f), false, jpFont,
                0.02f, 0.72f, 0.98f, 0.84f);

            // Power
            powerText = MakeTMPChild("Power", "", 12, Color.white, false, jpFont,
                0.02f, 0.52f, 0.5f, 0.72f);

            // Wish Damage
            wishDamageText = MakeTMPChild("WishDmg", "", 12, new Color(1f, 0.85f, 0.4f), false, jpFont,
                0.5f, 0.52f, 0.98f, 0.72f);

            // Keywords
            keywordsText = MakeTMPChild("Keywords", "", 10, new Color(0.4f, 0.8f, 1f), false, jpFont,
                0.02f, 0.36f, 0.98f, 0.52f);

            // Effect
            effectText = MakeTMPChild("Effect", "", 10, new Color(0.8f, 0.8f, 0.9f), false, jpFont,
                0.02f, 0.02f, 0.98f, 0.36f);
        }

        GameObject MakeChild(string name, float xMin, float yMin, float xMax, float yMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return obj;
        }

        TextMeshProUGUI MakeTMPChild(string name, string text, int size, Color color,
            bool bold, TMP_FontAsset font,
            float xMin = 0, float yMin = 0, float xMax = 1, float yMax = 1,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var obj = MakeChild(name, xMin, yMin, xMax, yMax);
            return SetupTMP(obj, text, size, color, bold, font, align);
        }

        TextMeshProUGUI MakeTMPChild(GameObject parent, string name, string text, int size,
            Color color, bool bold, TMP_FontAsset font,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return SetupTMP(obj, text, size, color, bold, font, align);
        }

        static TextMeshProUGUI SetupTMP(GameObject obj, string text, int size, Color color,
            bool bold, TMP_FontAsset font, TextAlignmentOptions align)
        {
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            if (font != null) tmp.font = font;
            return tmp;
        }

        public void SetCard(CardData data)
        {
            _data = data;
            if (data == null) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);
            EnsureUI();

            // カードイラスト読み込み
            if (cardIllustration)
            {
                var sprite = LoadIllustration(data.illustrationId);
                if (sprite != null)
                {
                    cardIllustration.sprite = sprite;
                    cardIllustration.enabled = true;
                    cardIllustration.preserveAspect = false;
                    cardIllustration.type = Image.Type.Simple;
                }
                else
                {
                    cardIllustration.enabled = false;
                }
            }

            // 色覚モードに応じたアスペクト色を使用
            Color aspectColor = AccessibilitySettings.GetAspectColor(data.aspect);
            string aspectName = AspectColors.GetDisplayName(data.aspect);
            string aspectIcon = AccessibilitySettings.AspectIcons.TryGetValue(data.aspect, out var icon) ? icon + " " : "";

            if (cardNameText)
            {
                cardNameText.text = data.cardName;
                cardNameText.fontSize = AccessibilitySettings.CardNameFontSize;
            }
            if (costText) costText.text = data.cpCost.ToString();
            if (costBackground) costBackground.color = aspectColor;
            if (aspectChip) aspectChip.color = aspectColor;

            if (aspectGlow)
            {
                var glowColor = aspectColor;
                glowColor.a = 0.3f;
                aspectGlow.color = glowColor;
            }

            // テキストサイズ反映
            float effectFontSize = AccessibilitySettings.EffectTextFontSize;
            float bodyFontSize = AccessibilitySettings.BodyFontSize;

            // レアリティ別シェーダー適用
            ApplyRarityShader(data.rarity, data.aspect);

            switch (data.type)
            {
                case CardType.Manifest:
                    if (typeText) typeText.text = $"{aspectIcon}顕現 [{aspectName}]";
                    if (powerText) { powerText.gameObject.SetActive(true); powerText.text = $"戦力 {data.battlePower}"; powerText.fontSize = bodyFontSize; }
                    if (wishDamageText) { wishDamageText.gameObject.SetActive(true); wishDamageText.text = $"願撃 {data.wishDamage}"; wishDamageText.fontSize = bodyFontSize; }
                    if (keywordsText) { keywordsText.text = data.keywords != null && data.keywords.Length > 0 ? string.Join(" ", data.keywords) : ""; keywordsText.fontSize = effectFontSize; }
                    if (effectText) effectText.text = "";
                    break;

                case CardType.Spell:
                    if (typeText) typeText.text = $"{aspectIcon}詠術 [{aspectName}]";
                    if (powerText) powerText.gameObject.SetActive(false);
                    if (wishDamageText) wishDamageText.gameObject.SetActive(false);
                    if (keywordsText) keywordsText.text = "";
                    if (effectText) { effectText.text = GetSpellDescription(data); effectText.fontSize = effectFontSize; }
                    break;

                case CardType.Algorithm:
                    if (typeText) typeText.text = $"{aspectIcon}界律 [{aspectName}]";
                    if (powerText) powerText.gameObject.SetActive(false);
                    if (wishDamageText) wishDamageText.gameObject.SetActive(false);
                    if (keywordsText) keywordsText.text = "";
                    if (effectText) { effectText.text = GetAlgorithmDescription(data); effectText.fontSize = effectFontSize; }
                    break;
            }
        }

        public void SetLeader(LeaderData leader)
        {
            if (leader == null) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);
            EnsureUI();

            Color aspectColor = AccessibilitySettings.GetAspectColor(leader.keyAspect);
            string aspectName = AspectColors.GetDisplayName(leader.keyAspect);
            string aspectIcon = AccessibilitySettings.AspectIcons.TryGetValue(leader.keyAspect, out var icon) ? icon + " " : "";

            // リーダーイラスト読み込み
            if (cardIllustration)
            {
                string illustrationKey = leader.id.Replace("LDR_", "").ToLower() switch
                {
                    "con_01" => "aldric",
                    "whi_01" => "vael",
                    "wea_01" => "hinagi",
                    "ver_01" => "amara",
                    "man_01" => "rahim",
                    "hus_01" => "suihou",
                    _ => null
                };
                var sprite = LoadIllustration(illustrationKey, "CardIllustrations/Leaders");
                if (sprite != null)
                {
                    cardIllustration.sprite = sprite;
                    cardIllustration.enabled = true;
                }
                else
                {
                    cardIllustration.enabled = false;
                }
            }

            if (cardNameText)
            {
                // "Aldric（アルドリック）" → "Aldric" + sub
                string displayName = leader.leaderName.Contains("（")
                    ? leader.leaderName.Substring(0, leader.leaderName.IndexOf("（"))
                    : leader.leaderName;
                cardNameText.text = displayName;
                cardNameText.fontSize = AccessibilitySettings.CardNameFontSize;
            }
            if (costText) costText.text = "0";
            if (costBackground) costBackground.color = aspectColor;
            if (aspectChip) aspectChip.color = aspectColor;
            if (aspectGlow)
            {
                var glowColor = aspectColor;
                glowColor.a = 0.3f;
                aspectGlow.color = glowColor;
            }
            if (typeText) typeText.text = $"{aspectIcon}願主 [{aspectName}]";
            if (powerText) { powerText.gameObject.SetActive(true); powerText.text = $"戦力 {leader.basePower}"; }
            if (wishDamageText)
            {
                wishDamageText.gameObject.SetActive(true);
                string suffix = leader.wishDamageType == "current" ? "%" : "";
                wishDamageText.text = $"願撃 {leader.baseWishDamage}{suffix}";
            }
            if (keywordsText) keywordsText.text = "";
            if (effectText && leader.leaderSkills != null)
            {
                string desc = "";
                foreach (var skill in leader.leaderSkills)
                    desc += $"[Lv{skill.unlockLevel}] {skill.name}: {skill.description}\n";
                effectText.text = desc.Trim();
                effectText.fontSize = AccessibilitySettings.EffectTextFontSize;
            }
        }

        void ApplyRarityShader(string rarity, Aspect aspect)
        {
            if (cardIllustration == null) return;
            if (rarity == "C" || string.IsNullOrEmpty(rarity))
            {
                if (_shaderController != null) _shaderController.ResetToDefault();
                return;
            }
            if (_shaderController == null)
                _shaderController = cardIllustration.GetComponent<CardShaderController>();
            if (_shaderController == null)
                _shaderController = cardIllustration.gameObject.AddComponent<CardShaderController>();
            _shaderController.ApplyRarity(rarity, aspect);
        }

        Sprite LoadIllustration(string illustrationId, string basePath = "CardIllustrations")
        {
            if (string.IsNullOrEmpty(illustrationId)) return null;
            return Resources.Load<Sprite>($"{basePath}/{illustrationId}");
        }

        string GetSpellDescription(CardData data)
        {
            return data.effectKey switch
            {
                "SPELL_PUSH_SMALL" => $"願力を{data.baseGaugeDelta}押す",
                "SPELL_PUSH_MEDIUM" => $"願力を{data.baseGaugeDelta}押す",
                "SPELL_POWER_PLUS" => $"味方の戦力+{data.powerDelta}",
                "SPELL_WISHDMG_PLUS" => $"味方の願撃+{data.wishDamageDelta}",
                "SPELL_REST" => $"敵{data.restTargets}体を消耗にする",
                "SPELL_REMOVE_DAMAGED" => $"条件を満たす敵を除去",
                _ => data.effectKey
            };
        }

        string GetAlgorithmDescription(CardData data)
        {
            string desc = "";
            if (data.globalRule != null)
                desc += $"[全体] {FormatAlgoRule(data.globalRule)}\n";
            if (data.ownerBonus != null)
                desc += $"[設置者] {FormatAlgoRule(data.ownerBonus)}";
            return desc.Trim();
        }

        string FormatAlgoRule(AlgorithmRule rule)
        {
            return rule.kind switch
            {
                "spell_gauge_plus" => $"詠術の願力変動+{rule.value}",
                "power_plus" => $"戦力+{rule.value}",
                "grant_rush" => "Rush付与",
                "wish_damage_plus" => $"願撃+{rule.value}",
                "direct_hit_plus" => $"直撃の願撃+{rule.value}",
                _ => rule.kind
            };
        }
    }
}
