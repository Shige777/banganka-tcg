using UnityEngine;
using UnityEngine.UI;
using Banganka.Core.Config;
using Banganka.Core.Battle;

namespace Banganka.UI.Common
{
    /// <summary>
    /// HP表示ビュー: 各プレイヤーのHPバー + 願力カード閾値表示
    /// 遅延ゲージ (白ゴースト) + 大ダメージ時フラッシュ演出
    /// </summary>
    public class GaugeView : MonoBehaviour
    {
        [SerializeField] RectTransform p1HpBar;
        [SerializeField] RectTransform p2HpBar;
        [SerializeField] Image p1HpFill;
        [SerializeField] Image p2HpFill;

        [SerializeField] Color healthyColor = new(0.3f, 0.8f, 0.4f);
        [SerializeField] Color dangerColor = new(1f, 0.2f, 0.2f);
        [SerializeField] Color finalColor = new(0.5f, 0.1f, 0.1f);

        // ハイコントラストモード用の代替色
        static readonly Color HcHealthy = new(0.2f, 0.9f, 0.2f);
        static readonly Color HcDanger = new(1f, 0.1f, 0.1f);
        static readonly Color HcFinal = new(0.6f, 0f, 0f);

        Color ActiveHealthy => AccessibilitySettings.ColorMode == AccessibilitySettings.ColorVisionMode.HighContrast ? HcHealthy : healthyColor;
        Color ActiveDanger => AccessibilitySettings.ColorMode == AccessibilitySettings.ColorVisionMode.HighContrast ? HcDanger : dangerColor;
        Color ActiveFinal => AccessibilitySettings.ColorMode == AccessibilitySettings.ColorVisionMode.HighContrast ? HcFinal : finalColor;

        // 遅延ゲージ (格ゲー式ホワイトゲージ)
        Image _p1Ghost;
        Image _p2Ghost;
        float _p1GhostRatio = 1f;
        float _p2GhostRatio = 1f;
        float _p1GhostDelay;
        float _p2GhostDelay;
        const float GhostHoldTime = 0.5f;    // ダメージ後に静止する時間
        const float GhostDrainSpeed = 0.8f;   // ゲージが追従する速度 (/秒)
        static readonly Color GhostColor = new(1f, 1f, 1f, 0.6f);

        // ダメージフラッシュ
        Image _p1Flash;
        Image _p2Flash;
        float _p1FlashTimer;
        float _p2FlashTimer;
        const float FlashDuration = 0.3f;
        const float LargeDamageThreshold = 0.1f; // maxHPの10%以上で発火

        public void UpdateHP(PlayerState p1, PlayerState p2)
        {
            float oldP1 = p1HpFill != null ? p1HpFill.rectTransform.anchorMax.x : 1f;
            float oldP2 = p2HpFill != null ? p2HpFill.rectTransform.anchorMax.x : 1f;

            UpdateBar(p1HpFill, p1);
            UpdateBar(p2HpFill, p2);

            float newP1 = p1.maxHp > 0 ? (float)p1.hp / p1.maxHp : 0f;
            float newP2 = p2.maxHp > 0 ? (float)p2.hp / p2.maxHp : 0f;

            // 遅延ゲージ: ダメージを受けた場合のみゴースト保持
            if (newP1 < oldP1)
            {
                EnsureGhost(ref _p1Ghost, p1HpFill);
                if (_p1GhostRatio < oldP1) _p1GhostRatio = oldP1; // 前の値を保持
                else _p1GhostRatio = oldP1;
                _p1GhostDelay = GhostHoldTime;

                if (oldP1 - newP1 >= LargeDamageThreshold)
                    TriggerFlash(ref _p1Flash, p1HpFill, ref _p1FlashTimer);
            }

            if (newP2 < oldP2)
            {
                EnsureGhost(ref _p2Ghost, p2HpFill);
                if (_p2GhostRatio < oldP2) _p2GhostRatio = oldP2;
                else _p2GhostRatio = oldP2;
                _p2GhostDelay = GhostHoldTime;

                if (oldP2 - newP2 >= LargeDamageThreshold)
                    TriggerFlash(ref _p2Flash, p2HpFill, ref _p2FlashTimer);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // P1 遅延ゲージ更新
            if (_p1Ghost != null && p1HpFill != null)
            {
                float targetRatio = p1HpFill.rectTransform.anchorMax.x;
                UpdateGhostBar(_p1Ghost, ref _p1GhostRatio, ref _p1GhostDelay, targetRatio, dt);
            }

            // P2 遅延ゲージ更新
            if (_p2Ghost != null && p2HpFill != null)
            {
                float targetRatio = p2HpFill.rectTransform.anchorMax.x;
                UpdateGhostBar(_p2Ghost, ref _p2GhostRatio, ref _p2GhostDelay, targetRatio, dt);
            }

            // フラッシュ減衰
            UpdateFlash(_p1Flash, ref _p1FlashTimer, dt);
            UpdateFlash(_p2Flash, ref _p2FlashTimer, dt);
        }

        void UpdateGhostBar(Image ghost, ref float ghostRatio, ref float delay, float target, float dt)
        {
            if (ghostRatio <= target)
            {
                ghostRatio = target;
                ghost.rectTransform.anchorMax = new Vector2(target, ghost.rectTransform.anchorMax.y);
                return;
            }

            if (delay > 0f)
            {
                delay -= dt;
                return;
            }

            ghostRatio = Mathf.MoveTowards(ghostRatio, target, GhostDrainSpeed * dt);
            ghost.rectTransform.anchorMax = new Vector2(ghostRatio, ghost.rectTransform.anchorMax.y);
        }

        void UpdateFlash(Image flash, ref float timer, float dt)
        {
            if (flash == null || timer <= 0f) return;
            timer -= dt;
            float alpha = Mathf.Clamp01(timer / FlashDuration) * 0.5f;
            flash.color = new Color(1f, 1f, 1f, alpha);
        }

        void UpdateBar(Image fill, PlayerState p)
        {
            if (fill == null) return;
            float ratio = p.maxHp > 0 ? (float)p.hp / p.maxHp : 0f;

            // anchorMax.x でバーの幅を制御（スプライト不要）
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(ratio), rt.anchorMax.y);

            if (p.isFinal)
                fill.color = ActiveFinal;
            else if (ratio < 0.25f)
                fill.color = ActiveDanger;
            else
                fill.color = Color.Lerp(ActiveDanger, ActiveHealthy, ratio);
        }

        void EnsureGhost(ref Image ghost, Image fill)
        {
            if (ghost != null || fill == null) return;

            var obj = new GameObject("HpGhost");
            obj.transform.SetParent(fill.transform.parent, false);
            // ゴーストを実ゲージの下に配置
            obj.transform.SetSiblingIndex(fill.transform.GetSiblingIndex());

            ghost = obj.AddComponent<Image>();
            ghost.color = GhostColor;
            ghost.raycastTarget = false;

            var rt = ghost.rectTransform;
            rt.anchorMin = fill.rectTransform.anchorMin;
            rt.anchorMax = fill.rectTransform.anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void TriggerFlash(ref Image flash, Image fill, ref float timer)
        {
            if (fill == null) return;
            if (AccessibilitySettings.ReduceMotion) return; // 光感受性対応

            if (flash == null)
            {
                var obj = new GameObject("HpFlash");
                obj.transform.SetParent(fill.transform.parent, false);
                obj.transform.SetAsLastSibling();

                flash = obj.AddComponent<Image>();
                flash.raycastTarget = false;

                var rt = flash.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            flash.color = new Color(1f, 1f, 1f, 0.5f);
            timer = FlashDuration;
        }

        /// <summary>
        /// Legacy compatibility: 単一ゲージ値でP1バーを更新する。
        /// </summary>
        public void UpdateGauge(int gaugeValue)
        {
            if (p1HpFill == null) return;
            float ratio = BalanceConfig.MaxHP > 0 ? (float)gaugeValue / BalanceConfig.MaxHP : 0f;
            ratio = Mathf.Clamp01(ratio);

            var rt = p1HpFill.rectTransform;
            rt.anchorMax = new Vector2(ratio, rt.anchorMax.y);

            if (ratio <= 0f)
                p1HpFill.color = ActiveFinal;
            else if (ratio < 0.25f)
                p1HpFill.color = ActiveDanger;
            else
                p1HpFill.color = Color.Lerp(ActiveDanger, ActiveHealthy, ratio);
        }
    }
}
