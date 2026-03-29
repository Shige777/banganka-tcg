using System;
using System.Collections.Generic;
using UnityEngine;

namespace Banganka.Core.Config
{
    /// <summary>
    /// アクセシビリティ設定 (ACCESSIBILITY_SPEC.md)
    /// WCAG 2.1 AA準拠 — 色覚補正 / テキストサイズ / アニメーション低減
    /// </summary>
    public static class AccessibilitySettings
    {
        // ====================================================================
        // Color Vision Mode (§1.4)
        // ====================================================================

        public enum ColorVisionMode
        {
            Normal,         // 標準カラー + アイコン
            ProtanDeutan,   // P/D型サポート (赤緑補正)
            Tritan,         // T型サポート (青紫補正)
            HighContrast,   // ハイコントラスト
        }

        static ColorVisionMode _colorMode = ColorVisionMode.Normal;
        public static ColorVisionMode ColorMode
        {
            get => _colorMode;
            set { _colorMode = value; OnSettingsChanged?.Invoke(); Save(); }
        }

        // ====================================================================
        // Text Size (§2.1)
        // ====================================================================

        public enum TextSize { Small, Medium, Large }

        static TextSize _textSize = TextSize.Medium;
        public static TextSize CurrentTextSize
        {
            get => _textSize;
            set { _textSize = value; OnSettingsChanged?.Invoke(); Save(); }
        }

        public static float BodyFontSize => _textSize switch
        {
            TextSize.Small => 13f,
            TextSize.Large => 18f,
            _ => 15f,
        };

        public static float CardNameFontSize => _textSize switch
        {
            TextSize.Small => 14f,
            TextSize.Large => 19f,
            _ => 16f,
        };

        public static float EffectTextFontSize => _textSize switch
        {
            TextSize.Small => 11f,
            TextSize.Large => 15f,
            _ => 13f,
        };

        public static float ButtonFontSize => _textSize switch
        {
            TextSize.Small => 15f,
            TextSize.Large => 20f,
            _ => 17f,
        };

        // ====================================================================
        // Animation Reduction (§4.1)
        // ====================================================================

        static bool _reduceMotion;
        public static bool ReduceMotion
        {
            get => _reduceMotion;
            set { _reduceMotion = value; OnSettingsChanged?.Invoke(); Save(); }
        }

        // ====================================================================
        // Aspect Icons (§1.2) — 二重識別システム
        // ====================================================================

        public static readonly Dictionary<Data.Aspect, string> AspectIcons = new()
        {
            { Data.Aspect.Whisper,  "\U0001f300" }, // 🌀 風の渦
            { Data.Aspect.Contest,  "\U0001f525" }, // 🔥 炎
            { Data.Aspect.Weave,    "\U0001f33f" }, // 🌿 木の葉
            { Data.Aspect.Verse,    "\U0001f319" }, // 🌙 三日月
            { Data.Aspect.Manifest, "\u2699\ufe0f" }, // ⚙️ 歯車
            { Data.Aspect.Hush,     "\U0001f512" }, // 🔒 盾/鍵
        };

        // ====================================================================
        // Color Correction (§1.5)
        // ====================================================================

        public static Color GetAspectColor(Data.Aspect aspect)
        {
            return _colorMode switch
            {
                ColorVisionMode.ProtanDeutan => GetProtanDeutanColor(aspect),
                ColorVisionMode.Tritan => GetTritanColor(aspect),
                ColorVisionMode.HighContrast => GetHighContrastColor(aspect),
                _ => GetNormalColor(aspect),
            };
        }

        static Color GetNormalColor(Data.Aspect aspect)
        {
            ColorUtility.TryParseHtmlString(aspect switch
            {
                Data.Aspect.Whisper  => "#4DA3FF",
                Data.Aspect.Contest  => "#FF5A36",
                Data.Aspect.Weave    => "#59C36A",
                Data.Aspect.Verse    => "#9A5BFF",
                Data.Aspect.Manifest => "#F4C542",
                Data.Aspect.Hush     => "#3A3A46",
                _ => "#FFFFFF",
            }, out var c);
            return c;
        }

        static Color GetProtanDeutanColor(Data.Aspect aspect)
        {
            ColorUtility.TryParseHtmlString(aspect switch
            {
                Data.Aspect.Whisper  => "#4DA3FF",
                Data.Aspect.Contest  => "#FF8C00", // オレンジ寄り
                Data.Aspect.Weave    => "#00BCD4", // シアン寄り
                Data.Aspect.Verse    => "#9A5BFF",
                Data.Aspect.Manifest => "#F4C542",
                Data.Aspect.Hush     => "#3A3A46",
                _ => "#FFFFFF",
            }, out var c);
            return c;
        }

        static Color GetTritanColor(Data.Aspect aspect)
        {
            ColorUtility.TryParseHtmlString(aspect switch
            {
                Data.Aspect.Whisper  => "#00CED1", // シアン
                Data.Aspect.Contest  => "#FF5A36",
                Data.Aspect.Weave    => "#59C36A",
                Data.Aspect.Verse    => "#FF00FF", // マゼンタ
                Data.Aspect.Manifest => "#F4C542",
                Data.Aspect.Hush     => "#3A3A46",
                _ => "#FFFFFF",
            }, out var c);
            return c;
        }

        static Color GetHighContrastColor(Data.Aspect aspect)
        {
            ColorUtility.TryParseHtmlString(aspect switch
            {
                Data.Aspect.Whisper  => "#FFFFFF",
                Data.Aspect.Contest  => "#FF0000",
                Data.Aspect.Weave    => "#00FF00",
                Data.Aspect.Verse    => "#FF00FF",
                Data.Aspect.Manifest => "#FFFF00",
                Data.Aspect.Hush     => "#808080",
                _ => "#FFFFFF",
            }, out var c);
            return c;
        }

        // ====================================================================
        // Contrast Check (§2.2 WCAG 2.1 AA)
        // ====================================================================

        /// <summary>
        /// WCAG relative luminance
        /// </summary>
        public static float RelativeLuminance(Color c)
        {
            float R = c.r <= 0.03928f ? c.r / 12.92f : Mathf.Pow((c.r + 0.055f) / 1.055f, 2.4f);
            float G = c.g <= 0.03928f ? c.g / 12.92f : Mathf.Pow((c.g + 0.055f) / 1.055f, 2.4f);
            float B = c.b <= 0.03928f ? c.b / 12.92f : Mathf.Pow((c.b + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * R + 0.7152f * G + 0.0722f * B;
        }

        /// <summary>
        /// WCAG contrast ratio between two colors
        /// </summary>
        public static float ContrastRatio(Color fg, Color bg)
        {
            float l1 = Mathf.Max(RelativeLuminance(fg), RelativeLuminance(bg));
            float l2 = Mathf.Min(RelativeLuminance(fg), RelativeLuminance(bg));
            return (l1 + 0.05f) / (l2 + 0.05f);
        }

        /// <summary>
        /// Check WCAG 2.1 AA compliance (4.5:1 for normal text, 3:1 for large text)
        /// </summary>
        public static bool MeetsAA(Color fg, Color bg, bool isLargeText = false)
        {
            float ratio = ContrastRatio(fg, bg);
            return isLargeText ? ratio >= 3f : ratio >= 4.5f;
        }

        // ====================================================================
        // Tap Target Validation (§3.1)
        // ====================================================================

        public const float MinTapTargetPt = 44f;
        public const float RecommendedTapTargetPt = 48f;

        // ====================================================================
        // Events & Persistence
        // ====================================================================

        public static event Action OnSettingsChanged;

        static void Save()
        {
            PlayerPrefs.SetInt("a11y_colorMode", (int)_colorMode);
            PlayerPrefs.SetInt("a11y_textSize", (int)_textSize);
            PlayerPrefs.SetInt("a11y_reduceMotion", _reduceMotion ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void Load()
        {
            _colorMode = (ColorVisionMode)PlayerPrefs.GetInt("a11y_colorMode", 0);
            _textSize = (TextSize)PlayerPrefs.GetInt("a11y_textSize", 1);
            _reduceMotion = PlayerPrefs.GetInt("a11y_reduceMotion", 0) == 1;
        }
    }
}
