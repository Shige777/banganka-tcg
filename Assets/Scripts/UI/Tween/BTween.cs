// BTween is now replaced by DOTween.
// This file provides backward-compatible extension methods that delegate to DOTween.
// Existing code using BTween API continues to work.

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Banganka.UI.Tween
{
    /// <summary>
    /// Compatibility shim: delegates to DOTween.
    /// New code should use DG.Tweening directly.
    /// </summary>
    public static class BTween
    {
        public static Tweener Move(RectTransform rt, Vector2 target, float duration)
            => rt.DOAnchorPos(target, duration);

        public static Tweener MoveFrom(RectTransform rt, Vector2 from, float duration)
        {
            var target = rt.anchoredPosition;
            rt.anchoredPosition = from;
            return rt.DOAnchorPos(target, duration);
        }

        public static Tweener Scale(Transform tr, Vector3 target, float duration)
            => tr.DOScale(target, duration);

        public static Tweener ScaleFrom(Transform tr, Vector3 from, float duration)
        {
            var target = tr.localScale;
            tr.localScale = from;
            return tr.DOScale(target, duration);
        }

        public static Tweener Fade(CanvasGroup cg, float target, float duration)
            => cg.DOFade(target, duration);

        public static Tweener FadeImage(Image img, float targetAlpha, float duration)
            => img.DOFade(targetAlpha, duration);

        public static Tweener ColorTo(Image img, Color target, float duration)
            => img.DOColor(target, duration);

        public static Tweener TextColor(TextMeshProUGUI tmp, Color target, float duration)
            => tmp.DOColor(target, duration);

        public static Tweener Rotate(Transform tr, Vector3 targetEuler, float duration)
            => tr.DOLocalRotate(targetEuler, duration);

        public static Tweener FloatValue(float from, float to, float duration, Action<float> setter)
            => DOTween.To(() => from, x => setter(x), to, duration);

        public static Tweener SizeDelta(RectTransform rt, Vector2 target, float duration)
            => rt.DOSizeDelta(target, duration);

        public static Tweener PunchScale(Transform tr, float punch, float duration)
            => tr.DOPunchScale(Vector3.one * punch, duration, 6, 0.5f);

        public static Tweener ShakePosition(RectTransform rt, float strength, float duration)
            => rt.DOShakeAnchorPos(duration, strength, 10, 90, false, true);

        public static void KillAll() => DOTween.KillAll();
    }
}
