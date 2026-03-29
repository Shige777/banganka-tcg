using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Battle;
using Banganka.Core.Config;
using Banganka.Core.Feedback;
using Banganka.Audio;
using Banganka.UI.Effects;

namespace Banganka.UI.Battle
{
    /// <summary>
    /// バトルアニメーション制御 (ANIMATION_SPEC.md)
    /// カードプレイ、攻撃、退場、願力カード発動、勝敗演出
    /// ダメージスケーリング + ハプティクス統合
    /// </summary>
    public class BattleAnimator : MonoBehaviour
    {
        [SerializeField] RectTransform battleField;
        [SerializeField] GameObject damageTextPrefab;
        [SerializeField] GameObject wishTriggerEffectPrefab;

        /// <summary>ReduceMotion時はアニメーション時間を1/4に短縮</summary>
        float DurationScale => AccessibilitySettings.ReduceMotion ? 0.25f : 1f;

        // ====================================================================
        // Card Play Animation
        // ====================================================================

        public IEnumerator AnimateCardPlay(RectTransform cardUI, Vector2 targetPos, Action onComplete = null)
        {
            SoundManager.Instance?.PlaySE("se_card_play");
            HapticService.CardPlay();

            Vector2 startPos = cardUI.anchoredPosition;
            Vector3 startScale = cardUI.localScale;
            float duration = 0.35f * DurationScale;
            float elapsed = 0;

            // Enlarge slightly then move to field
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float eased = EaseOutBack(t);

                cardUI.anchoredPosition = Vector2.Lerp(startPos, targetPos, eased);
                float scale = t < 0.3f ? Mathf.Lerp(1f, 1.15f, t / 0.3f) : Mathf.Lerp(1.15f, 1f, (t - 0.3f) / 0.7f);
                cardUI.localScale = startScale * scale;

                yield return null;
            }

            cardUI.anchoredPosition = targetPos;
            cardUI.localScale = startScale;
            onComplete?.Invoke();
        }

        // ====================================================================
        // Attack Animation
        // ====================================================================

        public IEnumerator AnimateAttack(RectTransform attacker, RectTransform target, bool isDirectHit, Action onImpact = null)
        {
            SoundManager.Instance?.PlaySE("se_attack_declare");

            Vector2 startPos = attacker.anchoredPosition;
            Vector2 targetPos = target != null ? target.anchoredPosition : startPos + Vector2.up * 200;
            float dashDuration = 0.2f * DurationScale;
            float returnDuration = 0.3f * DurationScale;
            float elapsed = 0;

            // Dash towards target
            while (elapsed < dashDuration)
            {
                elapsed += Time.deltaTime;
                float t = EaseInQuad(elapsed / dashDuration);
                attacker.anchoredPosition = Vector2.Lerp(startPos, targetPos, t * 0.7f);
                yield return null;
            }

            // Impact
            if (isDirectHit)
            {
                SoundManager.Instance?.PlaySE("se_direct_hit");
                HapticService.DirectHit();
            }
            else
            {
                SoundManager.Instance?.PlaySE("se_attack_hit");
                HapticService.AttackHit();
            }

            onImpact?.Invoke();

            // パーティクルエフェクト
            if (BattleParticleSystem.Instance != null)
                BattleParticleSystem.Instance.PlayAttackHitEffect(
                    target != null ? target.anchoredPosition : targetPos, isDirectHit);

            // Screen shake on direct hit
            if (isDirectHit && battleField != null)
                yield return ScreenShake(battleField, 0.15f, 8f);

            // Return
            elapsed = 0;
            Vector2 currentPos = attacker.anchoredPosition;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutQuad(elapsed / returnDuration);
                attacker.anchoredPosition = Vector2.Lerp(currentPos, startPos, t);
                yield return null;
            }

            attacker.anchoredPosition = startPos;
        }

        // ====================================================================
        // Unit Exit (退場) Animation
        // ====================================================================

        public IEnumerator AnimateUnitExit(RectTransform unitUI, Action onComplete = null)
        {
            SoundManager.Instance?.PlaySE("se_unit_exit");

            // 退場パーティクル
            if (BattleParticleSystem.Instance != null)
                BattleParticleSystem.Instance.PlayExitEffect(unitUI.anchoredPosition, -1);

            float duration = 0.4f * DurationScale;
            float elapsed = 0;
            Vector3 startScale = unitUI.localScale;
            CanvasGroup cg = unitUI.GetComponent<CanvasGroup>();
            if (cg == null) cg = unitUI.gameObject.AddComponent<CanvasGroup>();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                cg.alpha = 1f - t;
                unitUI.localScale = startScale * (1f + t * 0.2f); // Slight expand then fade
                yield return null;
            }

            onComplete?.Invoke();
        }

        // ====================================================================
        // HP Damage Number — SCALED by damage severity
        // ====================================================================

        /// <summary>
        /// ダメージ表示 (スケーリング対応)。
        /// damagePercent = HP最大値に対する割合 (0-100)。
        /// </summary>
        public IEnumerator AnimateHpDamage(RectTransform targetUI, int damage, int maxHp = 100)
        {
            float percent = maxHp > 0 ? (float)damage / maxHp * 100f : 0f;
            DamageTier tier = ClassifyDamage(percent);

            // SE
            string se = tier switch
            {
                DamageTier.Critical => "se_damage_critical",
                DamageTier.Large => "se_damage_large",
                DamageTier.Medium => "se_damage_medium",
                _ => "se_hp_damage",
            };
            SoundManager.Instance?.PlaySE(se);

            // Haptic
            switch (tier)
            {
                case DamageTier.Critical: HapticService.DamageCritical(); break;
                case DamageTier.Large: HapticService.DamageLarge(); break;
                case DamageTier.Medium: HapticService.DamageMedium(); break;
                default: HapticService.DamageSmall(); break;
            }

            // Screen shake for medium+
            if (tier >= DamageTier.Medium && battleField != null)
            {
                float shakeMag = tier switch
                {
                    DamageTier.Critical => 20f,
                    DamageTier.Large => 12f,
                    _ => 5f,
                };
                float shakeDur = tier switch
                {
                    DamageTier.Critical => 0.5f,
                    DamageTier.Large => 0.3f,
                    _ => 0.15f,
                };
                StartCoroutine(ScreenShake(battleField, shakeDur * DurationScale, shakeMag));
            }

            // Damage text
            var obj = new GameObject("DamageText");
            obj.transform.SetParent(targetUI, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"-{damage}";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            // Scale text by tier
            switch (tier)
            {
                case DamageTier.Critical:
                    tmp.fontSize = 56;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.color = new Color(1f, 0.15f, 0.1f);
                    tmp.outlineWidth = 0.3f;
                    tmp.outlineColor = Color.black;
                    break;
                case DamageTier.Large:
                    tmp.fontSize = 44;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.color = new Color(1f, 0.2f, 0.15f);
                    break;
                case DamageTier.Medium:
                    tmp.fontSize = 34;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.color = new Color(1f, 0.7f, 0.2f);
                    break;
                default:
                    tmp.fontSize = 24;
                    tmp.color = new Color(1f, 1f, 1f, 0.9f);
                    break;
            }

            float duration = (tier >= DamageTier.Large ? 1.0f : 0.8f) * DurationScale;
            float elapsed = 0;
            float riseHeight = tier switch
            {
                DamageTier.Critical => 70f,
                DamageTier.Large => 55f,
                DamageTier.Medium => 45f,
                _ => 35f,
            };

            // Pop-in scale for large+
            Vector3 startScale = tier >= DamageTier.Large ? Vector3.one * 1.5f : Vector3.one;
            obj.transform.localScale = startScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rt.anchoredPosition = new Vector2(0, EaseOutQuad(t) * riseHeight);
                tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 1f - t * t);

                // Scale pop: shrink from 1.5 to 1.0 in first 20%
                if (tier >= DamageTier.Large && t < 0.2f)
                    obj.transform.localScale = Vector3.Lerp(startScale, Vector3.one, t / 0.2f);

                yield return null;
            }

            Destroy(obj);
        }

        // Legacy overload without maxHp
        public IEnumerator AnimateHpDamage(RectTransform targetUI, int damage)
        {
            yield return AnimateHpDamage(targetUI, damage, 100);
        }

        enum DamageTier { Small, Medium, Large, Critical }

        static DamageTier ClassifyDamage(float percent)
        {
            if (percent >= 20f) return DamageTier.Critical;
            if (percent >= 10f) return DamageTier.Large;
            if (percent >= 4f) return DamageTier.Medium;
            return DamageTier.Small;
        }

        // ====================================================================
        // Wish Card Trigger Animation — stagger support
        // ====================================================================

        public IEnumerator AnimateWishTrigger(RectTransform hpBar, int threshold, string cardName)
        {
            SoundManager.Instance?.PlaySE("se_wish_trigger");
            HapticService.WishTrigger();

            var obj = new GameObject("WishTrigger");
            obj.transform.SetParent(hpBar != null ? hpBar : transform, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 40);
            var bg = obj.AddComponent<Image>();
            bg.color = new Color(1f, 0.85f, 0.3f, 0.9f);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var trt = textObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"願力発動 [{threshold}%] {cardName}";
            tmp.fontSize = 14;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;

            // Appear with scale + screen micro-shake
            if (battleField != null)
                StartCoroutine(ScreenShake(battleField, 0.1f * DurationScale, 5f));

            float duration = 1.2f * DurationScale;
            float elapsed = 0;
            obj.transform.localScale = Vector3.zero;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (t < 0.2f)
                    obj.transform.localScale = Vector3.one * EaseOutBack(t / 0.2f);
                else if (t > 0.8f)
                {
                    float fadeT = (t - 0.8f) / 0.2f;
                    bg.color = new Color(1f, 0.85f, 0.3f, 0.9f * (1 - fadeT));
                    tmp.color = new Color(0, 0, 0, 1 - fadeT);
                }

                yield return null;
            }

            Destroy(obj);
        }

        /// <summary>複数願力を0.6秒間隔でスタッガー表示</summary>
        public IEnumerator AnimateWishTriggersStaggered(RectTransform hpBar, WishCardSlot[] triggered)
        {
            foreach (var slot in triggered)
            {
                string name = slot.card != null ? slot.card.cardName : "???";
                yield return AnimateWishTrigger(hpBar, slot.threshold, name);
                yield return new WaitForSeconds(0.2f * DurationScale);
            }
        }

        // ====================================================================
        // Victory/Defeat Animation — enhanced
        // ====================================================================

        public IEnumerator AnimateVictory(RectTransform resultPanel, string text)
        {
            SoundManager.Instance?.PlayBGM("bgm_victory");
            HapticService.Victory();
            yield return AnimateResultText(resultPanel, text, new Color(1f, 0.85f, 0.3f));

            // Post-result shimmer
            if (battleField != null)
                StartCoroutine(ScreenShake(battleField, 0.3f * DurationScale, 3f));
        }

        public IEnumerator AnimateDefeat(RectTransform resultPanel, string text)
        {
            SoundManager.Instance?.PlayBGM("bgm_defeat");
            HapticService.Defeat();
            yield return AnimateResultText(resultPanel, text, new Color(0.5f, 0.5f, 0.6f));
        }

        IEnumerator AnimateResultText(RectTransform panel, string text, Color color)
        {
            panel.gameObject.SetActive(true);
            var tmp = panel.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = text;
                tmp.color = color;
            }

            // Slow-mo effect: scale from 2x with deceleration
            panel.localScale = Vector3.one * 2f;
            float duration = 0.6f * DurationScale;
            float elapsed = 0;

            CanvasGroup cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float eased = EaseOutBack(t);
                panel.localScale = Vector3.Lerp(Vector3.one * 2f, Vector3.one, eased);
                cg.alpha = Mathf.Clamp01(t * 3f); // Fade in fast in first 33%
                yield return null;
            }

            panel.localScale = Vector3.one;
            cg.alpha = 1f;
        }

        // ====================================================================
        // Leader Level-Up Animation
        // ====================================================================

        public IEnumerator AnimateLeaderLevelUp(RectTransform leaderUI, int newLevel)
        {
            SoundManager.Instance?.PlaySE("se_leader_levelup");
            HapticService.Trigger(HapticService.HapticType.Heavy);

            // レベルアップ光柱パーティクル
            if (BattleParticleSystem.Instance != null)
                BattleParticleSystem.Instance.PlayLevelUpEffect(leaderUI.anchoredPosition, -1);

            var flash = new GameObject("LevelUpFlash");
            flash.transform.SetParent(leaderUI, false);
            var frt = flash.AddComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(-10, -10);
            frt.offsetMax = new Vector2(10, 10);
            var img = flash.AddComponent<Image>();
            img.color = new Color(1f, 0.9f, 0.4f, 0.8f);
            img.raycastTarget = false;

            float duration = 0.6f * DurationScale;
            float elapsed = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                img.color = new Color(1f, 0.9f, 0.4f, 0.8f * (1 - t));
                float scale = 1f + t * 0.3f;
                frt.localScale = new Vector3(scale, scale, 1);
                yield return null;
            }

            Destroy(flash);
        }

        // ====================================================================
        // Utility
        // ====================================================================

        public IEnumerator ScreenShake(RectTransform target, float duration, float magnitude)
        {
            Vector2 original = target.anchoredPosition;
            float elapsed = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float decay = 1f - (elapsed / duration); // Shake decays over time
                float x = UnityEngine.Random.Range(-1f, 1f) * magnitude * decay;
                float y = UnityEngine.Random.Range(-1f, 1f) * magnitude * decay;
                target.anchoredPosition = original + new Vector2(x, y);
                yield return null;
            }

            target.anchoredPosition = original;
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        static float EaseInQuad(float t) => t * t;
        static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    }
}
