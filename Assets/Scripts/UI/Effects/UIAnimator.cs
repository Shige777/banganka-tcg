using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Banganka.UI.Effects
{
    public class UIAnimator : MonoBehaviour
    {
        // Fade in from transparent
        public static Coroutine FadeIn(MonoBehaviour host, CanvasGroup cg, float duration = 0.3f, float delay = 0f)
        {
            return host.StartCoroutine(FadeRoutine(cg, 0f, 1f, duration, delay));
        }

        // Fade out to transparent
        public static Coroutine FadeOut(MonoBehaviour host, CanvasGroup cg, float duration = 0.3f, float delay = 0f)
        {
            return host.StartCoroutine(FadeRoutine(cg, 1f, 0f, duration, delay));
        }

        static IEnumerator FadeRoutine(CanvasGroup cg, float from, float to, float duration, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            cg.alpha = from;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                cg.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            cg.alpha = to;
        }

        // Slide in from direction
        public static Coroutine SlideIn(MonoBehaviour host, RectTransform rt, Vector2 fromOffset, float duration = 0.35f, float delay = 0f)
        {
            return host.StartCoroutine(SlideRoutine(rt, fromOffset, Vector2.zero, duration, delay));
        }

        static IEnumerator SlideRoutine(RectTransform rt, Vector2 fromOffset, Vector2 toOffset, float duration, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            var startPos = rt.anchoredPosition + fromOffset;
            var endPos = rt.anchoredPosition + toOffset;
            rt.anchoredPosition = startPos;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutBack(elapsed / duration);
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                yield return null;
            }
            rt.anchoredPosition = endPos;
        }

        // Scale pop (for buttons, cards appearing)
        public static Coroutine ScalePop(MonoBehaviour host, Transform t, float duration = 0.25f, float delay = 0f)
        {
            return host.StartCoroutine(ScalePopRoutine(t, duration, delay));
        }

        static IEnumerator ScalePopRoutine(Transform t, float duration, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            t.localScale = Vector3.zero;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float s = EaseOutBack(elapsed / duration);
                t.localScale = Vector3.one * s;
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        // Staggered children animation
        public static Coroutine StaggerChildren(MonoBehaviour host, Transform parent, float staggerDelay = 0.06f, float duration = 0.3f)
        {
            return host.StartCoroutine(StaggerRoutine(host, parent, staggerDelay, duration));
        }

        static IEnumerator StaggerRoutine(MonoBehaviour host, Transform parent, float staggerDelay, float duration)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var cg = child.GetComponent<CanvasGroup>();
                if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0;
            }
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var cg = child.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    host.StartCoroutine(FadeRoutine(cg, 0, 1, duration, 0));
                    ScalePop(host, child, duration);
                }
                yield return new WaitForSeconds(staggerDelay);
            }
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }

    // Attach to any UI element for continuous floating animation
    public class FloatEffect : MonoBehaviour
    {
        public float amplitude = 5f;
        public float frequency = 1f;
        public float phaseOffset;

        RectTransform _rt;
        Vector2 _origin;

        void Start()
        {
            _rt = GetComponent<RectTransform>();
            _origin = _rt.anchoredPosition;
            if (phaseOffset == 0) phaseOffset = Random.value * Mathf.PI * 2;
        }

        void Update()
        {
            float y = Mathf.Sin(Time.time * frequency + phaseOffset) * amplitude;
            _rt.anchoredPosition = _origin + new Vector2(0, y);
        }
    }

    // Subtle pulse glow on element
    public class PulseEffect : MonoBehaviour
    {
        public float minAlpha = 0.6f;
        public float maxAlpha = 1.0f;
        public float speed = 2f;

        CanvasGroup _cg;

        void Start()
        {
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        }

        void Update()
        {
            float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
            _cg.alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
        }
    }

    // Color pulse for Image components (e.g., glowing borders)
    public class ColorPulse : MonoBehaviour
    {
        public Color colorA = Color.white;
        public Color colorB = new Color(1, 1, 1, 0.3f);
        public float speed = 1.5f;

        Image _img;

        void Start() { _img = GetComponent<Image>(); }

        void Update()
        {
            if (_img == null) return;
            float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
            _img.color = Color.Lerp(colorA, colorB, t);
        }
    }

    // Button scale feedback on press
    public class ButtonScaleEffect : MonoBehaviour
    {
        public float pressScale = 0.92f;
        public float hoverScale = 1.05f;
        public float speed = 12f;

        float _targetScale = 1f;
        Transform _transform;

        void Start()
        {
            _transform = transform;
            var btn = GetComponent<Button>();
            if (btn != null)
            {
                var trigger = gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

                var pointerDown = new UnityEngine.EventSystems.EventTrigger.Entry
                    { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
                pointerDown.callback.AddListener(_ => _targetScale = pressScale);
                trigger.triggers.Add(pointerDown);

                var pointerUp = new UnityEngine.EventSystems.EventTrigger.Entry
                    { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
                pointerUp.callback.AddListener(_ => _targetScale = 1f);
                trigger.triggers.Add(pointerUp);

                var pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry
                    { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
                pointerEnter.callback.AddListener(_ => { if (_targetScale >= 1f) _targetScale = hoverScale; });
                trigger.triggers.Add(pointerEnter);

                var pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry
                    { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
                pointerExit.callback.AddListener(_ => _targetScale = 1f);
                trigger.triggers.Add(pointerExit);
            }
        }

        void Update()
        {
            float current = _transform.localScale.x;
            float next = Mathf.Lerp(current, _targetScale, Time.deltaTime * speed);
            _transform.localScale = new Vector3(next, next, 1f);
        }
    }

    // Procedural particle-like floating dots for backgrounds
    public class BackgroundParticles : MonoBehaviour
    {
        public int particleCount = 20;
        public Color particleColor = new Color(1, 1, 1, 0.08f);
        public float minSize = 2f;
        public float maxSize = 8f;
        public float speed = 15f;

        struct Particle
        {
            public RectTransform rt;
            public Vector2 velocity;
            public float size;
        }

        Particle[] _particles;
        RectTransform _rect;

        void Start()
        {
            _rect = GetComponent<RectTransform>();
            _particles = new Particle[particleCount];

            for (int i = 0; i < particleCount; i++)
            {
                var obj = new GameObject($"Particle_{i}");
                obj.transform.SetParent(transform, false);
                var rt = obj.AddComponent<RectTransform>();
                var img = obj.AddComponent<Image>();

                float size = Random.Range(minSize, maxSize);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(
                    Random.Range(-540f, 540f),
                    Random.Range(-960f, 960f)
                );

                float alpha = Random.Range(0.03f, particleColor.a);
                img.color = new Color(particleColor.r, particleColor.g, particleColor.b, alpha);
                img.raycastTarget = false;

                _particles[i] = new Particle
                {
                    rt = rt,
                    velocity = new Vector2(
                        Random.Range(-1f, 1f),
                        Random.Range(0.2f, 1f)
                    ).normalized * Random.Range(speed * 0.3f, speed),
                    size = size
                };
            }
        }

        void Update()
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                var p = _particles[i];
                if (p.rt == null) continue;

                var pos = p.rt.anchoredPosition;
                pos += p.velocity * Time.deltaTime;

                // Wrap around
                if (pos.y > 960f) pos.y = -960f;
                if (pos.y < -960f) pos.y = 960f;
                if (pos.x > 540f) pos.x = -540f;
                if (pos.x < -540f) pos.x = 540f;

                p.rt.anchoredPosition = pos;
            }
        }
    }

    // Procedural gradient overlay
    public class GradientOverlay : MonoBehaviour
    {
        public Color topColor = new Color(0, 0, 0, 0);
        public Color bottomColor = new Color(0, 0, 0, 0.8f);

        void Start()
        {
            var img = GetComponent<Image>();
            if (img == null) return;

            // Create a simple gradient texture
            var tex = new Texture2D(1, 64);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < 64; y++)
            {
                float t = y / 63f;
                tex.SetPixel(0, y, Color.Lerp(bottomColor, topColor, t));
            }
            tex.Apply();

            img.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
    }

    // Screen transition manager
    public class ScreenTransitionOverlay : MonoBehaviour
    {
        static ScreenTransitionOverlay _instance;
        CanvasGroup _cg;
        Image _img;

        public static ScreenTransitionOverlay Instance => _instance;

        void Awake()
        {
            _instance = this;
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
            _img = GetComponent<Image>();
            _cg.alpha = 0;
            _cg.blocksRaycasts = false;
        }

        public void PlayTransition(MonoBehaviour host, System.Action midAction, float duration = 0.4f)
        {
            host.StartCoroutine(TransitionRoutine(host, midAction, duration));
        }

        IEnumerator TransitionRoutine(MonoBehaviour host, System.Action midAction, float duration)
        {
            _cg.blocksRaycasts = true;
            float half = duration * 0.5f;

            // Fade in
            float elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                _cg.alpha = Mathf.SmoothStep(0, 1, elapsed / half);
                yield return null;
            }
            _cg.alpha = 1f;

            midAction?.Invoke();
            yield return null;

            // Fade out
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                _cg.alpha = Mathf.SmoothStep(1, 0, elapsed / half);
                yield return null;
            }
            _cg.alpha = 0f;
            _cg.blocksRaycasts = false;
        }
    }
}
