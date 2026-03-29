using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Data;
using Banganka.Core.Config;
using Banganka.Core.Feedback;
using Banganka.Audio;

namespace Banganka.UI.Battle
{
    /// <summary>
    /// 界律フィールド変化演出。
    /// 界律カード設置時にフィールド全体の色調変化 + 波紋エフェクト。
    /// 上書き時は旧色から新色へのトランジション。
    /// </summary>
    public class AlgorithmFieldEffect : MonoBehaviour
    {
        RectTransform _fieldArea;
        Image _fieldTint;
        Image _ripple;
        TextMeshProUGUI _algoLabel;
        Aspect? _currentAspect;

        float DurationScale => AccessibilitySettings.ReduceMotion ? 0.25f : 1f;

        public void SetFieldArea(RectTransform fieldArea)
        {
            _fieldArea = fieldArea;
        }

        /// <summary>界律設置演出を再生</summary>
        public void PlayAlgorithmSet(CardData algoCard, bool isOverwrite)
        {
            if (_fieldArea == null) return;

            string se = isOverwrite ? "se_law_overwrite" : "se_law_set";
            SoundManager.Instance?.PlaySE(se);
            HapticService.Trigger(HapticService.HapticType.Heavy);

            Color aspectColor = AspectColors.GetColor(algoCard.aspect);
            StartCoroutine(AnimateAlgorithmSet(algoCard, aspectColor, isOverwrite));
        }

        /// <summary>界律除去 (上書きされた側) 演出</summary>
        public void PlayAlgorithmRemoved()
        {
            if (_fieldTint == null) return;
            StartCoroutine(AnimateAlgorithmRemoved());
        }

        IEnumerator AnimateAlgorithmSet(CardData card, Color aspectColor, bool isOverwrite)
        {
            EnsureFieldTint();
            EnsureRipple();

            Color tintTarget = new Color(aspectColor.r, aspectColor.g, aspectColor.b, 0.08f);
            Color oldTint = _fieldTint != null ? _fieldTint.color : new Color(0, 0, 0, 0);

            // --- Phase 1: Ripple expansion ---
            if (_ripple != null)
            {
                _ripple.color = new Color(aspectColor.r, aspectColor.g, aspectColor.b, 0.4f);
                _ripple.gameObject.SetActive(true);
                var rippleRt = _ripple.rectTransform;

                float rippleDuration = 0.6f * DurationScale;
                float elapsed = 0;
                rippleRt.localScale = Vector3.zero;

                while (elapsed < rippleDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / rippleDuration;
                    float scale = EaseOutQuad(t) * 3f;
                    rippleRt.localScale = new Vector3(scale, scale, 1);
                    _ripple.color = new Color(aspectColor.r, aspectColor.g, aspectColor.b, 0.4f * (1f - t));
                    yield return null;
                }

                _ripple.gameObject.SetActive(false);
            }

            // --- Phase 2: Field tint transition ---
            if (_fieldTint != null)
            {
                float tintDuration = 0.5f * DurationScale;
                float elapsed = 0;

                while (elapsed < tintDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = EaseOutQuad(elapsed / tintDuration);
                    _fieldTint.color = Color.Lerp(oldTint, tintTarget, t);
                    yield return null;
                }

                _fieldTint.color = tintTarget;
            }

            _currentAspect = card.aspect;

            // --- Phase 3: Algorithm name flash ---
            EnsureLabel();
            if (_algoLabel != null)
            {
                _algoLabel.text = $"界律: {card.cardName}";
                _algoLabel.color = new Color(aspectColor.r, aspectColor.g, aspectColor.b, 1f);
                _algoLabel.gameObject.SetActive(true);

                float labelDuration = 1.2f * DurationScale;
                float elapsed = 0;
                _algoLabel.transform.localScale = Vector3.one * 1.3f;

                while (elapsed < labelDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / labelDuration;

                    if (t < 0.2f)
                    {
                        float scaleT = EaseOutBack(t / 0.2f);
                        _algoLabel.transform.localScale = Vector3.Lerp(Vector3.one * 1.3f, Vector3.one, scaleT);
                    }

                    if (t > 0.7f)
                    {
                        float fadeT = (t - 0.7f) / 0.3f;
                        _algoLabel.color = new Color(aspectColor.r, aspectColor.g, aspectColor.b, 1f - fadeT);
                    }

                    yield return null;
                }

                _algoLabel.gameObject.SetActive(false);
            }
        }

        IEnumerator AnimateAlgorithmRemoved()
        {
            if (_fieldTint == null) yield break;

            Color startColor = _fieldTint.color;
            Color endColor = new Color(0, 0, 0, 0);
            float duration = 0.4f * DurationScale;
            float elapsed = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                _fieldTint.color = Color.Lerp(startColor, endColor, t);
                yield return null;
            }

            _fieldTint.color = endColor;
            _currentAspect = null;
        }

        /// <summary>現在の界律アスペクトに基づく微弱パルスをUpdateで実行</summary>
        void Update()
        {
            if (_fieldTint == null || !_currentAspect.HasValue) return;

            Color baseColor = AspectColors.GetColor(_currentAspect.Value);
            float pulse = Mathf.Sin(Time.time * 1.5f) * 0.5f + 0.5f;
            float alpha = 0.06f + pulse * 0.04f; // 0.06 - 0.10 subtle pulse
            _fieldTint.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }

        void EnsureFieldTint()
        {
            if (_fieldTint != null || _fieldArea == null) return;

            var obj = new GameObject("AlgoFieldTint");
            obj.transform.SetParent(_fieldArea, false);
            obj.transform.SetAsFirstSibling(); // Behind all field units
            _fieldTint = obj.AddComponent<Image>();
            _fieldTint.color = new Color(0, 0, 0, 0);
            _fieldTint.raycastTarget = false;

            var rt = _fieldTint.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-20, -20);
            rt.offsetMax = new Vector2(20, 20);
        }

        void EnsureRipple()
        {
            if (_ripple != null || _fieldArea == null) return;

            var obj = new GameObject("AlgoRipple");
            obj.transform.SetParent(_fieldArea, false);
            _ripple = obj.AddComponent<Image>();
            _ripple.color = new Color(1, 1, 1, 0);
            _ripple.raycastTarget = false;

            var rt = _ripple.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100, 100);
            rt.localScale = Vector3.zero;
            obj.SetActive(false);
        }

        void EnsureLabel()
        {
            if (_algoLabel != null || _fieldArea == null) return;

            var obj = new GameObject("AlgoLabel");
            obj.transform.SetParent(_fieldArea, false);
            obj.transform.SetAsLastSibling();

            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.4f);
            rt.anchorMax = new Vector2(0.9f, 0.6f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _algoLabel = obj.AddComponent<TextMeshProUGUI>();
            _algoLabel.fontSize = 36;
            _algoLabel.fontStyle = FontStyles.Bold;
            _algoLabel.alignment = TextAlignmentOptions.Center;
            _algoLabel.raycastTarget = false;
            obj.SetActive(false);
        }

        static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
