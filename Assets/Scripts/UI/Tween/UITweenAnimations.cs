using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Banganka.UI.Tween
{
    /// <summary>
    /// Ready-made UI animation patterns using DOTween.
    /// </summary>
    public static class UITweenAnimations
    {
        // Card appears: scale from 0 with OutBack + fade in
        public static void CardAppear(Transform card, float delay = 0f)
        {
            card.localScale = Vector3.zero;
            card.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack).SetDelay(delay);

            var cg = card.GetComponent<CanvasGroup>();
            if (cg == null) cg = card.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            cg.DOFade(1f, 0.2f).SetEase(Ease.OutQuad).SetDelay(delay);
        }

        // Stagger cards in a parent container
        public static void StaggerCards(Transform parent, float staggerDelay = 0.05f)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                CardAppear(child, i * staggerDelay);
            }
        }

        // Screen slide in from bottom
        public static void ScreenSlideUp(RectTransform rt, float duration = 0.4f)
        {
            var pos = rt.anchoredPosition;
            rt.anchoredPosition = pos + new Vector2(0, -200);
            rt.DOAnchorPos(pos, duration).SetEase(Ease.OutCubic);

            var cg = rt.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0;
                cg.DOFade(1f, duration * 0.6f).SetEase(Ease.OutQuad);
            }
        }

        // Screen slide in from right
        public static void ScreenSlideRight(RectTransform rt, float duration = 0.35f)
        {
            var pos = rt.anchoredPosition;
            rt.anchoredPosition = pos + new Vector2(300, 0);
            rt.DOAnchorPos(pos, duration).SetEase(Ease.OutCubic);
        }

        // Panel pop in (for overlays/modals)
        public static void PanelPopIn(Transform panel, float duration = 0.3f)
        {
            panel.localScale = new Vector3(0.8f, 0.8f, 1f);
            panel.DOScale(Vector3.one, duration).SetEase(Ease.OutBack);

            var cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            cg.DOFade(1f, duration * 0.5f).SetEase(Ease.OutQuad);
        }

        // Panel pop out
        public static void PanelPopOut(Transform panel, float duration = 0.2f, System.Action onDone = null)
        {
            panel.DOScale(new Vector3(0.85f, 0.85f, 1f), duration).SetEase(Ease.InQuad);

            var cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.gameObject.AddComponent<CanvasGroup>();
            cg.DOFade(0f, duration).SetEase(Ease.InQuad).OnComplete(() =>
            {
                panel.gameObject.SetActive(false);
                panel.localScale = Vector3.one;
                cg.alpha = 1;
                onDone?.Invoke();
            });
        }

        // Button press feedback (quick punch)
        public static void ButtonPunch(Transform btn)
        {
            btn.DOPunchScale(Vector3.one * 0.15f, 0.25f, 6, 0.5f);
        }

        // Gauge smooth update
        public static Tweener GaugeAnimate(RectTransform indicator, Vector2 targetPos, float duration = 0.6f)
        {
            return indicator.DOAnchorPos(targetPos, duration).SetEase(Ease.OutCubic);
        }

        // Damage flash (red flash on element)
        public static void DamageFlash(Image img, float duration = 0.3f)
        {
            var original = img.color;
            var flash = new Color(1f, 0.2f, 0.2f, original.a);
            img.DOColor(flash, duration * 0.3f).SetEase(Ease.OutQuad)
                .OnComplete(() => img.DOColor(original, duration * 0.7f).SetEase(Ease.InQuad));
        }

        // Damage shake on a panel/card
        public static void DamageShake(RectTransform rt, float strength = 15f, float duration = 0.3f)
        {
            rt.DOShakeAnchorPos(duration, strength, 12, 90, false, true);
        }

        // Victory/defeat text drop in
        public static void ResultTextDrop(Transform textObj, float duration = 0.5f)
        {
            textObj.localScale = new Vector3(2f, 2f, 1f);
            textObj.DOScale(Vector3.one, duration).SetEase(Ease.OutBack);

            var cg = textObj.GetComponent<CanvasGroup>();
            if (cg == null) cg = textObj.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            cg.DOFade(1f, duration * 0.4f).SetEase(Ease.OutQuad);
        }

        // Card play animation: card moves from hand to field
        public static void CardPlayToField(RectTransform card, Vector2 fieldPos, float duration = 0.4f, System.Action onDone = null)
        {
            var seq = DOTween.Sequence();
            seq.Join(card.DOAnchorPos(fieldPos, duration).SetEase(Ease.OutCubic));
            seq.Join(card.DOScale(new Vector3(0.6f, 0.6f, 1f), duration).SetEase(Ease.OutQuad));
            seq.OnComplete(() => onDone?.Invoke());
        }

        // Counter/number tick up animation
        public static void NumberTick(TextMeshProUGUI tmp, int from, int to, float duration = 0.5f)
        {
            float val = from;
            DOTween.To(() => val, x => { val = x; if (tmp) tmp.text = Mathf.RoundToInt(val).ToString(); }, to, duration)
                .SetEase(Ease.OutQuad);
        }

        // Shimmer effect on an image
        public static Tweener Shimmer(Image img, float intensity = 0.3f, float duration = 1.5f)
        {
            var baseColor = img.color;
            var bright = new Color(
                Mathf.Min(1, baseColor.r + intensity),
                Mathf.Min(1, baseColor.g + intensity),
                Mathf.Min(1, baseColor.b + intensity),
                baseColor.a);

            return img.DOColor(bright, duration * 0.5f).SetEase(Ease.InOutQuad)
                .SetLoops(2, LoopType.Yoyo);
        }

        // Sequence helper: fade overlay in, run action, fade out
        public static void TransitionOverlay(CanvasGroup overlay, System.Action midAction, float duration = 0.4f)
        {
            var seq = DOTween.Sequence();
            seq.Append(overlay.DOFade(1f, duration * 0.5f).SetEase(Ease.InQuad));
            seq.AppendCallback(() => midAction?.Invoke());
            seq.Append(overlay.DOFade(0f, duration * 0.5f).SetEase(Ease.OutQuad));
        }
    }

    /// <summary>
    /// Attach to a screen to auto-animate children on enable.
    /// </summary>
    public class ScreenEntranceAnimator : MonoBehaviour
    {
        public enum EntranceType { SlideUp, SlideRight, FadeIn }
        public EntranceType entranceType = EntranceType.SlideUp;

        void OnEnable()
        {
            var rt = GetComponent<RectTransform>();
            switch (entranceType)
            {
                case EntranceType.SlideUp:
                    UITweenAnimations.ScreenSlideUp(rt);
                    break;
                case EntranceType.SlideRight:
                    UITweenAnimations.ScreenSlideRight(rt);
                    break;
                case EntranceType.FadeIn:
                    var cg = GetComponent<CanvasGroup>();
                    if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0;
                    cg.DOFade(1f, 0.3f).SetEase(Ease.OutQuad);
                    break;
            }
        }
    }
}
