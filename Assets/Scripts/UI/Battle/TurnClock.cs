using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Config;
using Banganka.Core.Feedback;
using Banganka.Audio;

namespace Banganka.UI.Battle
{
    /// <summary>
    /// ターン制限を時計盤で表示する。
    /// 界律カードの上に配置し、ターン経過を視覚化する。
    /// 残り時間に応じた緊迫演出 (Hearthstoneの燃えるロープに相当)。
    /// </summary>
    public class TurnClock : MonoBehaviour
    {
        [SerializeField] RectTransform hand;        // 時計の針
        [SerializeField] TextMeshProUGUI centerText; // 中央のターン数表示
        [SerializeField] Image dialImage;           // 盤面の色変化用

        int _maxTurns = 24;
        int _currentTurn;

        // Timer urgency state
        float _remainingSeconds = -1f;
        float _maxTimerSeconds = 60f;
        bool _warning10Fired;
        bool _warning5Fired;
        float _pulseTimer;
        Color _dialBaseColor;
        Vector3 _handBaseScale = Vector3.one;

        // Vignette overlay (created at runtime)
        Image _vignetteOverlay;

        public void SetMaxTurns(int max) => _maxTurns = max;

        public void SetTurn(int turn)
        {
            _currentTurn = Mathf.Clamp(turn, 0, _maxTurns);

            // Rotate hand: 0 turns = 12 o'clock, max turns = full rotation
            float angle = -(360f * _currentTurn / _maxTurns);
            if (hand != null)
                hand.localRotation = Quaternion.Euler(0, 0, angle);

            // Update center text
            if (centerText != null)
                centerText.text = $"{_currentTurn}";

            // Color shift: calm → warm → urgent as turns progress
            if (dialImage != null)
            {
                float ratio = (float)_currentTurn / _maxTurns;
                if (ratio < 0.5f)
                    _dialBaseColor = Color.Lerp(
                        new Color(0.15f, 0.20f, 0.25f, 0.8f),
                        new Color(0.25f, 0.22f, 0.10f, 0.8f),
                        ratio * 2f);
                else
                    _dialBaseColor = Color.Lerp(
                        new Color(0.25f, 0.22f, 0.10f, 0.8f),
                        new Color(0.35f, 0.10f, 0.10f, 0.9f),
                        (ratio - 0.5f) * 2f);
                dialImage.color = _dialBaseColor;
            }
        }

        /// <summary>ターンタイマーの残り秒数を設定 (MatchControllerから毎フレーム呼ぶ)</summary>
        public void SetRemainingSeconds(float remaining, float maxSeconds)
        {
            _maxTimerSeconds = maxSeconds;
            _remainingSeconds = remaining;

            // 15秒: パルス開始
            // 10秒: 警告SE + テキスト色変化
            // 5秒: 赤ビネット + 心拍パルス + 針揺れ
            if (remaining <= 10f && remaining > 5f && !_warning10Fired)
            {
                _warning10Fired = true;
                SoundManager.Instance?.PlaySE("se_timer_warning");
                HapticService.TimerWarning();
            }
            else if (remaining <= 5f && !_warning5Fired)
            {
                _warning5Fired = true;
                SoundManager.Instance?.PlaySE("se_timer_warning");
                HapticService.TimerWarning();
            }
        }

        /// <summary>新ターン開始時にタイマー警告状態をリセット</summary>
        public void ResetTimerWarnings()
        {
            _warning10Fired = false;
            _warning5Fired = false;
            _remainingSeconds = -1f;
            // Remove vignette
            if (_vignetteOverlay != null)
                _vignetteOverlay.color = new Color(0.8f, 0f, 0f, 0f);
        }

        void Update()
        {
            if (_remainingSeconds < 0f) return;

            _pulseTimer += Time.deltaTime;

            // --- 15秒以下: 盤面パルス ---
            if (_remainingSeconds <= 15f && _remainingSeconds > 5f)
            {
                float pulseSpeed = _remainingSeconds <= 10f ? 4f : 2f;
                float pulse = Mathf.Sin(_pulseTimer * pulseSpeed) * 0.5f + 0.5f;

                if (dialImage != null)
                {
                    Color warning = Color.Lerp(_dialBaseColor, new Color(0.6f, 0.3f, 0.05f, 0.9f), pulse * 0.4f);
                    dialImage.color = warning;
                }

                // Center text pulse
                if (centerText != null && _remainingSeconds <= 10f)
                {
                    centerText.color = Color.Lerp(Color.white, new Color(1f, 0.4f, 0.2f), pulse);
                    int secs = Mathf.CeilToInt(_remainingSeconds);
                    centerText.text = $"{secs}";
                }
            }

            // --- 5秒以下: 危機演出 ---
            if (_remainingSeconds <= 5f)
            {
                // Heartbeat pulse (fast)
                float heartbeat = Mathf.Sin(_pulseTimer * 8f) * 0.5f + 0.5f;

                // Dial turns deep red with pulse
                if (dialImage != null)
                    dialImage.color = Color.Lerp(
                        new Color(0.5f, 0.05f, 0.05f, 0.95f),
                        new Color(0.8f, 0.1f, 0.1f, 1f),
                        heartbeat);

                // Center text: countdown with scale pulse
                if (centerText != null)
                {
                    int secs = Mathf.CeilToInt(_remainingSeconds);
                    centerText.text = $"{secs}";
                    centerText.color = Color.Lerp(new Color(1f, 0.3f, 0.2f), Color.white, heartbeat);
                    float scale = 1f + heartbeat * 0.15f;
                    centerText.transform.localScale = new Vector3(scale, scale, 1f);
                }

                // Hand shake
                if (hand != null)
                {
                    float shake = Mathf.Sin(_pulseTimer * 20f) * 2f * (_remainingSeconds < 3f ? 1.5f : 1f);
                    float baseAngle = -(360f * _currentTurn / _maxTurns);
                    hand.localRotation = Quaternion.Euler(0, 0, baseAngle + shake);
                }

                // Vignette overlay
                EnsureVignette();
                if (_vignetteOverlay != null)
                {
                    float vigAlpha = (1f - _remainingSeconds / 5f) * 0.25f;
                    vigAlpha += heartbeat * 0.05f;
                    _vignetteOverlay.color = new Color(0.8f, 0f, 0f, vigAlpha);
                }
            }
            else
            {
                // Reset crisis state
                if (centerText != null)
                    centerText.transform.localScale = Vector3.one;
                if (_vignetteOverlay != null)
                    _vignetteOverlay.color = new Color(0.8f, 0f, 0f, 0f);
            }
        }

        void EnsureVignette()
        {
            if (_vignetteOverlay != null) return;

            // Find the top-level canvas to overlay
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var obj = new GameObject("TimerVignette");
            obj.transform.SetParent(canvas.transform, false);
            obj.transform.SetAsLastSibling();
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _vignetteOverlay = obj.AddComponent<Image>();
            _vignetteOverlay.color = new Color(0.8f, 0f, 0f, 0f);
            _vignetteOverlay.raycastTarget = false;
        }
    }
}
