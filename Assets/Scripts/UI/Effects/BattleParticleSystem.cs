using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Banganka.Core.Config;

namespace Banganka.UI.Effects
{
    /// <summary>
    /// バトル中のパーティクルエフェクトを管理する。
    /// ANIMATION_SPEC.md §3, §10 準拠。
    ///
    /// 願相別召喚エフェクト、攻撃エフェクト、退場エフェクト、
    /// レベルアップ光柱、願力発動波紋をUIキャンバス上で再生する。
    ///
    /// ParticleEffectForUGUI (mob-sakai) 導入前はImage + コルーチンで
    /// 軽量パーティクルを代替実装する。
    /// </summary>
    public class BattleParticleSystem : MonoBehaviour
    {
        public static BattleParticleSystem Instance { get; private set; }

        [SerializeField] RectTransform effectLayer; // L3 エフェクトレイヤー

        float DurationScale => AccessibilitySettings.ReduceMotion ? 0.25f : 1f;

        // 願相別カラー
        static readonly Color[] AspectColors = {
            new Color(1f, 0.35f, 0.21f),   // 0: 曙 Contest #FF5A36
            new Color(0.30f, 0.64f, 1f),   // 1: 空 Whisper #4DA3FF
            new Color(0.35f, 0.76f, 0.42f), // 2: 穏 Weave #59C36A
            new Color(0.60f, 0.36f, 1f),   // 3: 妖 Verse #9A5BFF
            new Color(0.96f, 0.77f, 0.26f), // 4: 遊 Manifest #F4C542
            new Color(0.23f, 0.23f, 0.27f), // 5: 玄 Hush #3A3A46
        };

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>エフェクトレイヤーを設定（MatchControllerから）</summary>
        public void SetEffectLayer(RectTransform layer)
        {
            effectLayer = layer;
        }

        RectTransform GetLayer()
        {
            return effectLayer != null ? effectLayer : (RectTransform)transform;
        }

        // ====================================================================
        // 召喚エフェクト (ANIMATION_SPEC.md §3.2, 0.5s)
        // ====================================================================

        /// <summary>願相別の召喚パーティクルを再生</summary>
        public void PlaySummonEffect(Vector2 position, int aspect, string rarity = "C")
        {
            StartCoroutine(SummonEffectCoroutine(position, aspect, rarity));
        }

        IEnumerator SummonEffectCoroutine(Vector2 position, int aspect, string rarity)
        {
            Color color = aspect >= 0 && aspect < AspectColors.Length ? AspectColors[aspect] : Color.white;
            float duration = 0.5f * DurationScale;
            float scale = rarity switch { "SSR" => 2.0f, "SR" => 1.5f, "R" => 1.2f, _ => 1.0f };

            // 中心の光球
            var core = CreateParticle(position, color, 40f * scale);
            // 放射パーティクル（8方向）
            var particles = new GameObject[8];
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                particles[i] = CreateParticle(position, color, 12f * scale);
            }

            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 中心の光球: 拡大→縮小
                float coreScale = t < 0.3f
                    ? Mathf.Lerp(0, 1.5f, t / 0.3f)
                    : Mathf.Lerp(1.5f, 0f, (t - 0.3f) / 0.7f);
                if (core != null)
                {
                    core.transform.localScale = Vector3.one * coreScale;
                    SetAlpha(core, 1f - t);
                }

                // 放射パーティクル: 外側に広がりつつフェードアウト
                for (int i = 0; i < 8; i++)
                {
                    if (particles[i] == null) continue;
                    float angle = i * Mathf.PI * 2f / 8f;
                    float radius = t * 80f * scale;
                    var rt = particles[i].GetComponent<RectTransform>();
                    rt.anchoredPosition = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    float pScale = (1f - t) * 1.2f;
                    particles[i].transform.localScale = Vector3.one * pScale;
                    SetAlpha(particles[i], 1f - t * t);
                }

                yield return null;
            }

            Destroy(core);
            foreach (var p in particles) if (p != null) Destroy(p);
        }

        // ====================================================================
        // 攻撃衝撃エフェクト (ANIMATION_SPEC.md §3.3)
        // ====================================================================

        /// <summary>攻撃ヒット時の衝撃波</summary>
        public void PlayAttackHitEffect(Vector2 position, bool isDirectHit)
        {
            StartCoroutine(AttackHitCoroutine(position, isDirectHit));
        }

        IEnumerator AttackHitCoroutine(Vector2 position, bool isDirectHit)
        {
            float duration = (isDirectHit ? 0.4f : 0.25f) * DurationScale;
            float maxRadius = isDirectHit ? 120f : 60f;
            Color color = isDirectHit ? new Color(1f, 0.3f, 0.1f, 0.8f) : new Color(1f, 1f, 1f, 0.6f);

            // 衝撃波リング
            var ring = CreateRing(position, color, 10f);
            // ヒットスパーク（6方向）
            int sparkCount = isDirectHit ? 12 : 6;
            var sparks = new GameObject[sparkCount];
            for (int i = 0; i < sparkCount; i++)
                sparks[i] = CreateParticle(position, Color.white, 6f);

            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // リング拡大
                if (ring != null)
                {
                    var rt = ring.GetComponent<RectTransform>();
                    float size = Mathf.Lerp(10f, maxRadius, t);
                    rt.sizeDelta = new Vector2(size, size);
                    SetAlpha(ring, 1f - t);
                }

                // スパーク散布
                for (int i = 0; i < sparkCount; i++)
                {
                    if (sparks[i] == null) continue;
                    float angle = i * Mathf.PI * 2f / sparkCount + UnityEngine.Random.Range(-0.1f, 0.1f);
                    float radius = t * maxRadius * 0.8f;
                    var srt = sparks[i].GetComponent<RectTransform>();
                    srt.anchoredPosition = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    sparks[i].transform.localScale = Vector3.one * (1f - t);
                    SetAlpha(sparks[i], 1f - t);
                }

                yield return null;
            }

            Destroy(ring);
            foreach (var s in sparks) if (s != null) Destroy(s);
        }

        // ====================================================================
        // 退場エフェクト (ANIMATION_SPEC.md §3.4)
        // ====================================================================

        /// <summary>退場時の粒子分解エフェクト</summary>
        public void PlayExitEffect(Vector2 position, int aspect)
        {
            StartCoroutine(ExitEffectCoroutine(position, aspect));
        }

        IEnumerator ExitEffectCoroutine(Vector2 position, int aspect)
        {
            Color color = aspect >= 0 && aspect < AspectColors.Length ? AspectColors[aspect] : Color.gray;
            float duration = 0.6f * DurationScale;
            int particleCount = 16;

            var particles = new GameObject[particleCount];
            var velocities = new Vector2[particleCount];
            for (int i = 0; i < particleCount; i++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float speed = UnityEngine.Random.Range(40f, 120f);
                velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                float size = UnityEngine.Random.Range(4f, 10f);
                particles[i] = CreateParticle(position, color, size);
            }

            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                for (int i = 0; i < particleCount; i++)
                {
                    if (particles[i] == null) continue;
                    var rt = particles[i].GetComponent<RectTransform>();
                    // 重力 + 初速
                    Vector2 gravity = new Vector2(0, -100f * t);
                    rt.anchoredPosition = position + velocities[i] * t + gravity * t;
                    particles[i].transform.localScale = Vector3.one * (1f - t * 0.8f);
                    SetAlpha(particles[i], 1f - t);
                }

                yield return null;
            }

            foreach (var p in particles) if (p != null) Destroy(p);
        }

        // ====================================================================
        // レベルアップ光柱 (ANIMATION_SPEC.md §3.7)
        // ====================================================================

        /// <summary>願主進化時の光柱エフェクト</summary>
        public void PlayLevelUpEffect(Vector2 position, int aspect)
        {
            StartCoroutine(LevelUpCoroutine(position, aspect));
        }

        IEnumerator LevelUpCoroutine(Vector2 position, int aspect)
        {
            Color color = aspect >= 0 && aspect < AspectColors.Length ? AspectColors[aspect] : Color.yellow;
            float duration = 0.8f * DurationScale;

            // 光柱（縦長の矩形）
            var pillar = new GameObject("LevelUpPillar");
            pillar.transform.SetParent(GetLayer(), false);
            var rt = pillar.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            var img = pillar.AddComponent<Image>();
            Color pillarColor = color;
            pillarColor.a = 0.6f;
            img.color = pillarColor;
            img.raycastTarget = false;
            rt.sizeDelta = new Vector2(60, 0);

            // 周囲の上昇パーティクル
            var risers = new GameObject[10];
            for (int i = 0; i < 10; i++)
                risers[i] = CreateParticle(position + new Vector2(UnityEngine.Random.Range(-30f, 30f), 0), color, 8f);

            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 光柱: 下から上に伸びてフェードアウト
                float height = t < 0.4f
                    ? Mathf.Lerp(0, 400f, t / 0.4f)
                    : 400f;
                rt.sizeDelta = new Vector2(60f * (1f - t * 0.5f), height);
                float alpha = t < 0.4f ? 0.7f : Mathf.Lerp(0.7f, 0f, (t - 0.4f) / 0.6f);
                img.color = new Color(color.r, color.g, color.b, alpha);

                // 上昇パーティクル
                for (int i = 0; i < 10; i++)
                {
                    if (risers[i] == null) continue;
                    var rrt = risers[i].GetComponent<RectTransform>();
                    float riseSpeed = 150f + i * 20f;
                    rrt.anchoredPosition += new Vector2(0, riseSpeed * Time.deltaTime);
                    SetAlpha(risers[i], 1f - t);
                }

                yield return null;
            }

            Destroy(pillar);
            foreach (var r in risers) if (r != null) Destroy(r);
        }

        // ====================================================================
        // 願力発動波紋
        // ====================================================================

        /// <summary>願力閾値突破時の波紋エフェクト</summary>
        public void PlayWishRippleEffect(Vector2 position)
        {
            StartCoroutine(WishRippleCoroutine(position));
        }

        IEnumerator WishRippleCoroutine(Vector2 position)
        {
            float duration = 0.6f * DurationScale;
            Color color = new Color(1f, 0.85f, 0.3f, 0.7f);

            // 3重波紋
            var rings = new GameObject[3];
            for (int i = 0; i < 3; i++)
                rings[i] = CreateRing(position, color, 20f);

            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                for (int i = 0; i < 3; i++)
                {
                    if (rings[i] == null) continue;
                    float delay = i * 0.15f;
                    float localT = Mathf.Clamp01((t - delay) / (1f - delay));
                    float size = Mathf.Lerp(20f, 200f, localT);
                    var rrt = rings[i].GetComponent<RectTransform>();
                    rrt.sizeDelta = new Vector2(size, size);
                    SetAlpha(rings[i], (1f - localT) * 0.7f);
                }

                yield return null;
            }

            foreach (var r in rings) if (r != null) Destroy(r);
        }

        // ====================================================================
        // ヘルパー
        // ====================================================================

        GameObject CreateParticle(Vector2 position, Color color, float size)
        {
            var obj = new GameObject("Particle");
            obj.transform.SetParent(GetLayer(), false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(size, size);
            var img = obj.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return obj;
        }

        GameObject CreateRing(Vector2 position, Color color, float size)
        {
            var obj = new GameObject("Ring");
            obj.transform.SetParent(GetLayer(), false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(size, size);
            var outline = obj.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(2, 2);
            var img = obj.AddComponent<Image>();
            Color fillColor = color;
            fillColor.a = 0.1f;
            img.color = fillColor;
            img.raycastTarget = false;
            return obj;
        }

        void SetAlpha(GameObject obj, float alpha)
        {
            var img = obj.GetComponent<Image>();
            if (img != null)
            {
                var c = img.color;
                c.a = Mathf.Clamp01(alpha);
                img.color = c;
            }
        }
    }
}
