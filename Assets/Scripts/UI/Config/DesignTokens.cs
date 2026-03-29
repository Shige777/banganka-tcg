using UnityEngine;

namespace Banganka.UI.Config
{
    /// <summary>
    /// UIデザイントークン (UI_STYLE_GUIDE.md)
    /// 色 / タイポグラフィ / 8ptグリッドスペーシング / ボーダー / シャドウ
    /// </summary>
    public static class DesignTokens
    {
        // ====================================================================
        // Base Colors (§1)
        // ====================================================================

        public static readonly Color BgPrimary    = HexColor("#0D0D14");
        public static readonly Color BgSecondary  = HexColor("#1A1A2E");
        public static readonly Color BgTertiary   = HexColor("#252540");
        public static readonly Color BgElevated   = HexColor("#2E2E4A");

        public static readonly Color TextPrimary   = HexColor("#FFFFFF");
        public static readonly Color TextSecondary = HexColor("#B0B0CC");
        public static readonly Color TextTertiary  = HexColor("#6E6E8A");
        public static readonly Color TextDisabled  = HexColor("#4A4A60");

        public static readonly Color AccentGold    = HexColor("#D4A843");
        public static readonly Color AccentBlue    = HexColor("#4DA3FF");

        public static readonly Color StatusSuccess = HexColor("#4CAF50");
        public static readonly Color StatusError   = HexColor("#F44336");
        public static readonly Color StatusWarning = HexColor("#FF9800");

        // ====================================================================
        // Aspect Colors (§1.2) — 6願相
        // ====================================================================

        public static readonly Color AspectContest  = HexColor("#FF5A36"); // 曙赤
        public static readonly Color AspectWhisper  = HexColor("#4DA3FF"); // 空青
        public static readonly Color AspectWeave    = HexColor("#59C36A"); // 穏緑
        public static readonly Color AspectVerse    = HexColor("#9A5BFF"); // 妖紫
        public static readonly Color AspectManifest = HexColor("#F4C542"); // 遊黄
        public static readonly Color AspectHush     = HexColor("#3A3A46"); // 玄白

        public static Color GetAspectColor(Core.Data.Aspect aspect) => aspect switch
        {
            Core.Data.Aspect.Contest  => AspectContest,
            Core.Data.Aspect.Whisper  => AspectWhisper,
            Core.Data.Aspect.Weave    => AspectWeave,
            Core.Data.Aspect.Verse    => AspectVerse,
            Core.Data.Aspect.Manifest => AspectManifest,
            Core.Data.Aspect.Hush     => AspectHush,
            _ => TextPrimary,
        };

        // ====================================================================
        // Typography (§2) — Font Sizes
        // ====================================================================

        public const float FontDisplayLg  = 32f;
        public const float FontDisplayMd  = 28f;
        public const float FontHeadingLg  = 24f;
        public const float FontHeadingMd  = 20f;
        public const float FontHeadingSm  = 18f;
        public const float FontBodyLg     = 16f;
        public const float FontBodyMd     = 14f;
        public const float FontBodySm     = 12f;
        public const float FontCaption    = 11f;
        public const float FontOverline   = 10f;

        // Card-specific (fixed, no Dynamic Type scaling)
        public const float FontCardCost   = 22f;
        public const float FontCardPower  = 16f;
        public const float FontCardName   = 13f;
        public const float FontCardEffect = 10f;

        // Dynamic Type scaling factor range
        public const float DynamicTypeMin = 0.85f;
        public const float DynamicTypeMax = 1.35f;

        // ====================================================================
        // 8pt Grid Spacing (§3)
        // ====================================================================

        public const float SpaceXxs = 2f;
        public const float SpaceXs  = 4f;
        public const float SpaceSm  = 8f;
        public const float SpaceMd  = 16f;
        public const float SpaceLg  = 24f;
        public const float SpaceXl  = 32f;
        public const float SpaceXxl = 48f;

        // ====================================================================
        // Border Radius (§4)
        // ====================================================================

        public const float RadiusSm   = 4f;
        public const float RadiusMd   = 8f;
        public const float RadiusLg   = 12f;
        public const float RadiusFull = 9999f;

        // ====================================================================
        // Tap Targets (ACCESSIBILITY_SPEC.md §3.1)
        // ====================================================================

        public const float TapTargetMin         = 44f;
        public const float TapTargetRecommended = 48f;
        public const float CardGridMinWidth     = 60f;
        public const float CardGridMinHeight    = 84f;

        // ====================================================================
        // Card Aspect Ratio
        // ====================================================================

        public const float CardAspectRatio = 5f / 7f; // width / height

        // ====================================================================
        // Animation Durations (ANIMATION_SPEC.md)
        // ====================================================================

        public const float AnimFadeQuick   = 0.15f;
        public const float AnimFadeNormal  = 0.25f;
        public const float AnimFadeSlow    = 0.40f;
        public const float AnimCardPlay    = 0.35f;
        public const float AnimAttackDash  = 0.20f;
        public const float AnimScreenTrans = 0.30f;

        // ====================================================================
        // Loading States (SCREEN_SPEC.md §8)
        // ====================================================================

        public const float LoadingSpinnerDelay = 0.3f;  // 300ms before showing spinner
        public const float LoadingTextDelay    = 2.0f;  // 2s before adding text
        public const float LoadingCancelDelay  = 5.0f;  // 5s before showing cancel

        // ====================================================================
        // Utility
        // ====================================================================

        static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
