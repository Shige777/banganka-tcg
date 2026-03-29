using UnityEngine;
using Banganka.Core.Config;

namespace Banganka.Core.Feedback
{
    /// <summary>
    /// ハプティクス (触覚フィードバック) サービス。
    /// iOS: UIImpactFeedbackGenerator / Android: Vibrator API をラップ。
    /// Marvel Snap / ポケポケ級のモバイル触覚体験を提供。
    /// </summary>
    public static class HapticService
    {
        public enum HapticType
        {
            Light,      // カード出し、UIタップ
            Medium,     // 攻撃命中、願力発動
            Heavy,      // 直接攻撃、大ダメージ
            Success,    // 勝利、ランクアップ
            Warning,    // タイマー警告
            Error,      // 敗北
        }

        static bool _enabled = true;
        static float _intensity = 1f; // 0-1

        public static bool Enabled
        {
            get => _enabled;
            set { _enabled = value; PlayerPrefs.SetInt("haptic_enabled", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static float Intensity
        {
            get => _intensity;
            set { _intensity = Mathf.Clamp01(value); PlayerPrefs.SetFloat("haptic_intensity", _intensity); PlayerPrefs.Save(); }
        }

        public static void Load()
        {
            _enabled = PlayerPrefs.GetInt("haptic_enabled", 1) == 1;
            _intensity = PlayerPrefs.GetFloat("haptic_intensity", 1f);
        }

        /// <summary>単発ハプティクス</summary>
        public static void Trigger(HapticType type)
        {
            if (!_enabled || _intensity <= 0f) return;
            if (AccessibilitySettings.ReduceMotion) return; // ReduceMotion時は無効

            long durationMs = type switch
            {
                HapticType.Light   => (long)(10 * _intensity),
                HapticType.Medium  => (long)(30 * _intensity),
                HapticType.Heavy   => (long)(50 * _intensity),
                HapticType.Success => (long)(40 * _intensity),
                HapticType.Warning => (long)(25 * _intensity),
                HapticType.Error   => (long)(35 * _intensity),
                _ => 15,
            };

            if (durationMs < 1) return;

#if UNITY_IOS && !UNITY_EDITOR
            TriggerIOS(type);
#elif UNITY_ANDROID && !UNITY_EDITOR
            TriggerAndroid(durationMs);
#else
            // Editor: ログのみ
            Debug.Log($"[Haptic] {type} ({durationMs}ms)");
#endif
        }

        /// <summary>パルスパターン (勝利演出等)</summary>
        public static void TriggerPattern(HapticType type, int pulseCount, float intervalMs = 80f)
        {
            if (!_enabled || _intensity <= 0f) return;
            if (AccessibilitySettings.ReduceMotion) return;

            // コルーチン不要: 最初の1パルスだけ即発火、残りはAndroid patternで処理
#if UNITY_ANDROID && !UNITY_EDITOR
            long on = type switch
            {
                HapticType.Light => 10, HapticType.Medium => 25,
                HapticType.Heavy => 40, _ => 20,
            };
            on = (long)(on * _intensity);
            long off = (long)intervalMs;
            var pattern = new long[pulseCount * 2];
            for (int i = 0; i < pulseCount; i++)
            {
                pattern[i * 2] = i == 0 ? 0 : off;
                pattern[i * 2 + 1] = on;
            }
            VibrateAndroidPattern(pattern);
#else
            Trigger(type);
#endif
        }

        // ---- カード出し ----
        public static void CardPlay() => Trigger(HapticType.Light);

        // ---- 攻撃命中 ----
        public static void AttackHit() => Trigger(HapticType.Medium);

        // ---- 直接攻撃 (願主へ) ----
        public static void DirectHit() => Trigger(HapticType.Heavy);

        // ---- ダメージ段階別 ----
        public static void DamageSmall() => Trigger(HapticType.Light);
        public static void DamageMedium() => Trigger(HapticType.Medium);
        public static void DamageLarge() => Trigger(HapticType.Heavy);
        public static void DamageCritical() => TriggerPattern(HapticType.Heavy, 2, 60f);

        // ---- 願力発動 ----
        public static void WishTrigger() => TriggerPattern(HapticType.Medium, 2, 100f);

        // ---- 勝利/敗北 ----
        public static void Victory() => TriggerPattern(HapticType.Success, 3, 80f);
        public static void Defeat() => Trigger(HapticType.Error);

        // ---- パック開封 ----
        public static void PackTear() => Trigger(HapticType.Heavy);
        public static void CardRevealSSR() => TriggerPattern(HapticType.Heavy, 2, 50f);
        public static void CardRevealSR() => Trigger(HapticType.Medium);

        // ---- タイマー警告 ----
        public static void TimerWarning() => Trigger(HapticType.Warning);

        // ---- 願主カットイン ----
        public static void LeaderCutin() => Trigger(HapticType.Heavy);

        // ================================================================
        // Platform-specific implementations
        // ================================================================

#if UNITY_IOS && !UNITY_EDITOR
        static void TriggerIOS(HapticType type)
        {
            // UIImpactFeedbackGenerator style mapping
            int style = type switch
            {
                HapticType.Light => 0,   // UIImpactFeedbackStyleLight
                HapticType.Medium => 1,  // UIImpactFeedbackStyleMedium
                HapticType.Heavy => 2,   // UIImpactFeedbackStyleHeavy
                HapticType.Success => 0, // UINotificationFeedbackTypeSuccess
                HapticType.Warning => 1, // UINotificationFeedbackTypeWarning
                HapticType.Error => 2,   // UINotificationFeedbackTypeError
                _ => 1,
            };

            if (type == HapticType.Success || type == HapticType.Warning || type == HapticType.Error)
                _TriggerNotificationFeedback(style);
            else
                _TriggerImpactFeedback(style);
        }

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void _TriggerImpactFeedback(int style);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void _TriggerNotificationFeedback(int style);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;

        static void TriggerAndroid(long durationMs)
        {
            if (_vibrator == null)
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            _vibrator?.Call("vibrate", durationMs);
        }

        static void VibrateAndroidPattern(long[] pattern)
        {
            if (_vibrator == null)
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            _vibrator?.Call("vibrate", pattern, -1); // -1 = no repeat
        }
#endif
    }
}
