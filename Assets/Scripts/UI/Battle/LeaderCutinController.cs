using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Battle;
using Banganka.Core.Data;
using Banganka.Core.Config;
using Banganka.Core.Feedback;
using Banganka.Audio;

namespace Banganka.UI.Battle
{
    /// <summary>
    /// 願主カットイン演出コントローラ。
    /// スキル発動時に画面を横切るカットイン + スキル名表示。
    /// Marvel Snap / Shadowverse のリーダー演出に相当。
    /// </summary>
    public class LeaderCutinController : MonoBehaviour
    {
        Canvas _overlayCanvas;

        float DurationScale => AccessibilitySettings.ReduceMotion ? 0.25f : 1f;

        void EnsureCanvas()
        {
            if (_overlayCanvas != null) return;

            var obj = new GameObject("CutinCanvas");
            obj.transform.SetParent(transform, false);
            _overlayCanvas = obj.AddComponent<Canvas>();
            _overlayCanvas.overrideSorting = true;
            _overlayCanvas.sortingOrder = 100;
            obj.AddComponent<GraphicRaycaster>();
            var scaler = obj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
        }

        // ====================================================================
        // Leader Skill Cutin
        // ====================================================================

        /// <summary>
        /// 願主スキル発動カットイン演出。
        /// </summary>
        public IEnumerator PlaySkillCutin(LeaderState leader, int skillLevel, PlayerSide side)
        {
            EnsureCanvas();
            SoundManager.Instance?.PlaySE("se_leader_cutin");
            HapticService.LeaderCutin();

            var skill = GetSkill(leader.baseData, skillLevel);
            string skillName = skill != null ? skill.name : $"Lv{skillLevel}スキル";
            Color aspectColor = AspectColors.GetColor(leader.baseData.keyAspect);

            // --- Build cutin UI ---
            var root = new GameObject("Cutin");
            root.transform.SetParent(_overlayCanvas.transform, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // Dark overlay
            var overlay = CreateChild(root.transform, "Overlay");
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0);
            overlayImg.raycastTarget = true; // Block input during cutin
            StretchFull(overlay.GetComponent<RectTransform>());

            // Slash band (斜めの帯)
            var band = CreateChild(root.transform, "Band");
            var bandRt = band.GetComponent<RectTransform>();
            bandRt.anchorMin = new Vector2(0, 0.35f);
            bandRt.anchorMax = new Vector2(1, 0.65f);
            bandRt.offsetMin = Vector2.zero;
            bandRt.offsetMax = Vector2.zero;
            band.transform.localRotation = Quaternion.Euler(0, 0, -3f);
            var bandImg = band.AddComponent<Image>();
            bandImg.color = new Color(aspectColor.r, aspectColor.g, aspectColor.b, 0.85f);

            // Leader portrait (left or right based on side)
            var portrait = CreateChild(band.transform, "Portrait");
            var portraitRt = portrait.GetComponent<RectTransform>();
            bool isP1 = side == PlayerSide.Player1;
            portraitRt.anchorMin = isP1 ? new Vector2(0, 0) : new Vector2(0.55f, 0);
            portraitRt.anchorMax = isP1 ? new Vector2(0.45f, 1) : new Vector2(1, 1);
            portraitRt.offsetMin = Vector2.zero;
            portraitRt.offsetMax = Vector2.zero;
            var portraitImg = portrait.AddComponent<Image>();
            portraitImg.raycastTarget = false;
            var sprite = LoadLeaderSprite(leader.baseData.id);
            if (sprite != null)
            {
                portraitImg.sprite = sprite;
                portraitImg.preserveAspect = true;
            }
            else
            {
                portraitImg.color = new Color(aspectColor.r * 0.5f, aspectColor.g * 0.5f, aspectColor.b * 0.5f, 0.8f);
            }

            // Skill name text
            var textObj = CreateChild(band.transform, "SkillName");
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = isP1 ? new Vector2(0.4f, 0.2f) : new Vector2(0.05f, 0.2f);
            textRt.anchorMax = isP1 ? new Vector2(0.95f, 0.8f) : new Vector2(0.6f, 0.8f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = skillName;
            tmp.fontSize = 42;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = isP1 ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            // Leader name sub-text
            var nameObj = CreateChild(band.transform, "LeaderName");
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchorMin = isP1 ? new Vector2(0.4f, 0.05f) : new Vector2(0.05f, 0.05f);
            nameRt.anchorMax = isP1 ? new Vector2(0.95f, 0.25f) : new Vector2(0.6f, 0.25f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = leader.baseData.leaderName;
            nameTmp.fontSize = 22;
            nameTmp.color = new Color(1f, 1f, 1f, 0.7f);
            nameTmp.alignment = isP1 ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;

            // --- Animate ---
            float slideDir = isP1 ? -1f : 1f;
            float totalDuration = 1.5f * DurationScale;

            // Phase 1: Slide in (0-0.25)
            // Phase 2: Hold (0.25-0.75)
            // Phase 3: Slide out (0.75-1.0)
            float elapsed = 0;
            Vector2 bandStart = new Vector2(slideDir * 1200f, 0);
            bandRt.anchoredPosition = bandStart;

            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / totalDuration;

                // Overlay fade
                float overlayAlpha = t < 0.2f ? t / 0.2f * 0.4f
                    : t > 0.8f ? (1f - t) / 0.2f * 0.4f
                    : 0.4f;
                overlayImg.color = new Color(0, 0, 0, overlayAlpha);

                // Band position
                if (t < 0.25f)
                {
                    float slideT = EaseOutBack(t / 0.25f);
                    bandRt.anchoredPosition = Vector2.Lerp(bandStart, Vector2.zero, slideT);
                }
                else if (t > 0.75f)
                {
                    float slideT = EaseInQuad((t - 0.75f) / 0.25f);
                    bandRt.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(-slideDir * 1200f, 0), slideT);
                }
                else
                {
                    bandRt.anchoredPosition = Vector2.zero;
                }

                // Text scale punch at midpoint
                if (t > 0.2f && t < 0.4f)
                {
                    float punchT = (t - 0.2f) / 0.2f;
                    float scale = 1f + Mathf.Sin(punchT * Mathf.PI) * 0.1f;
                    textObj.transform.localScale = new Vector3(scale, scale, 1);
                }
                else
                {
                    textObj.transform.localScale = Vector3.one;
                }

                yield return null;
            }

            Destroy(root);
        }

        // ====================================================================
        // Leader Level-Up Cutin (shorter, celebratory)
        // ====================================================================

        public IEnumerator PlayLevelUpCutin(LeaderState leader, int newLevel)
        {
            EnsureCanvas();
            SoundManager.Instance?.PlaySE("se_leader_levelup");
            HapticService.Trigger(HapticService.HapticType.Heavy);

            Color aspectColor = AspectColors.GetColor(leader.baseData.keyAspect);

            var root = new GameObject("LevelUpCutin");
            root.transform.SetParent(_overlayCanvas.transform, false);
            var rootRt = root.AddComponent<RectTransform>();
            StretchFull(rootRt);

            // Flash overlay
            var flash = CreateChild(root.transform, "Flash");
            var flashImg = flash.AddComponent<Image>();
            flashImg.color = new Color(aspectColor.r, aspectColor.g, aspectColor.b, 0f);
            flashImg.raycastTarget = false;
            StretchFull(flash.GetComponent<RectTransform>());

            // Level text
            var textObj = CreateChild(root.transform, "LevelText");
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.1f, 0.4f);
            textRt.anchorMax = new Vector2(0.9f, 0.6f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"Lv{newLevel}";
            tmp.fontSize = 72;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(1f, 0.9f, 0.4f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.outlineWidth = 0.3f;
            tmp.outlineColor = new Color(0.3f, 0.15f, 0f);

            float duration = 0.8f * DurationScale;
            float elapsed = 0;
            textObj.transform.localScale = Vector3.one * 2.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Flash: quick burst then fade
                float flashAlpha = t < 0.15f ? t / 0.15f * 0.4f : 0.4f * (1f - (t - 0.15f) / 0.85f);
                flashImg.color = new Color(aspectColor.r, aspectColor.g, aspectColor.b,
                    Mathf.Max(0, flashAlpha));

                // Text: scale down with bounce
                if (t < 0.4f)
                {
                    float scaleT = EaseOutBack(t / 0.4f);
                    textObj.transform.localScale = Vector3.Lerp(Vector3.one * 2.5f, Vector3.one, scaleT);
                }

                // Fade out in last 30%
                if (t > 0.7f)
                {
                    float fadeT = (t - 0.7f) / 0.3f;
                    tmp.color = new Color(1f, 0.9f, 0.4f, 1f - fadeT);
                }

                yield return null;
            }

            Destroy(root);
        }

        // ====================================================================
        // Wish Trigger Cutin (small banner flash)
        // ====================================================================

        public IEnumerator PlayWishTriggerCutin(int threshold, string cardName, Aspect aspect)
        {
            EnsureCanvas();
            Color aspectColor = AspectColors.GetColor(aspect);

            var root = new GameObject("WishCutin");
            root.transform.SetParent(_overlayCanvas.transform, false);
            var rootRt = root.AddComponent<RectTransform>();
            StretchFull(rootRt);

            // Banner
            var banner = CreateChild(root.transform, "Banner");
            var bannerRt = banner.GetComponent<RectTransform>();
            bannerRt.anchorMin = new Vector2(0, 0.42f);
            bannerRt.anchorMax = new Vector2(1, 0.58f);
            bannerRt.offsetMin = Vector2.zero;
            bannerRt.offsetMax = Vector2.zero;
            var bannerImg = banner.AddComponent<Image>();
            bannerImg.color = new Color(1f, 0.85f, 0.3f, 0.9f);

            // Accent line
            var accent = CreateChild(banner.transform, "Accent");
            var accentRt = accent.GetComponent<RectTransform>();
            accentRt.anchorMin = new Vector2(0, 0);
            accentRt.anchorMax = new Vector2(1, 0.06f);
            accentRt.offsetMin = Vector2.zero;
            accentRt.offsetMax = Vector2.zero;
            var accentImg = accent.AddComponent<Image>();
            accentImg.color = aspectColor;

            // Text
            var textObj = CreateChild(banner.transform, "Text");
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.05f, 0.1f);
            textRt.anchorMax = new Vector2(0.95f, 0.9f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"願力発動 [{threshold}%] {cardName}";
            tmp.fontSize = 28;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;

            SoundManager.Instance?.PlaySE("se_wish_trigger");
            HapticService.WishTrigger();

            float duration = 1.4f * DurationScale;
            float elapsed = 0;
            bannerRt.anchoredPosition = new Vector2(1200f, 0);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (t < 0.2f)
                {
                    float slideT = EaseOutBack(t / 0.2f);
                    bannerRt.anchoredPosition = Vector2.Lerp(new Vector2(1200f, 0), Vector2.zero, slideT);
                }
                else if (t > 0.8f)
                {
                    float slideT = (t - 0.8f) / 0.2f;
                    bannerImg.color = new Color(1f, 0.85f, 0.3f, 0.9f * (1f - slideT));
                    tmp.color = new Color(0, 0, 0, 1f - slideT);
                }

                yield return null;
            }

            Destroy(root);
        }

        // ====================================================================
        // Utility
        // ====================================================================

        static LeaderSkill GetSkill(LeaderData data, int skillLevel)
        {
            if (data?.leaderSkills == null) return null;
            int idx = skillLevel - 2;
            if (idx < 0 || idx >= data.leaderSkills.Length) return null;
            return data.leaderSkills[idx];
        }

        Sprite LoadLeaderSprite(string leaderId)
        {
            string key = leaderId?.Replace("LDR_", "").ToLower() switch
            {
                "con_01" => "aldric",
                "whi_01" => "vael",
                "wea_01" => "hinagi",
                "ver_01" => "amara",
                "man_01" => "rahim",
                "hus_01" => "suihou",
                _ => null
            };
            if (key == null) return null;
            return Resources.Load<Sprite>($"CardIllustrations/Leaders/{key}");
        }

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

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        static float EaseInQuad(float t) => t * t;
    }
}
