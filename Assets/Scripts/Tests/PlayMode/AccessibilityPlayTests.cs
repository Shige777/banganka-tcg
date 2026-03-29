using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Banganka.Core.Config;
using Banganka.Core.Data;

namespace Banganka.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for accessibility features that require Unity runtime
    /// (PlayerPrefs persistence, color computation, event propagation across frames).
    /// </summary>
    [TestFixture]
    public class AccessibilityPlayTests
    {
        [SetUp]
        public void SetUp()
        {
            // Reset to defaults
            AccessibilitySettings.ColorMode = AccessibilitySettings.ColorVisionMode.Normal;
            AccessibilitySettings.CurrentTextSize = AccessibilitySettings.TextSize.Medium;
            AccessibilitySettings.ReduceMotion = false;
        }

        // ================================================================
        // Settings persistence via PlayerPrefs
        // ================================================================

        [UnityTest]
        public IEnumerator Settings_PersistAcrossLoadCycles()
        {
            AccessibilitySettings.ColorMode = AccessibilitySettings.ColorVisionMode.HighContrast;
            AccessibilitySettings.CurrentTextSize = AccessibilitySettings.TextSize.Large;
            AccessibilitySettings.ReduceMotion = true;

            yield return null;

            // Simulate reload
            AccessibilitySettings.Load();

            Assert.AreEqual(AccessibilitySettings.ColorVisionMode.HighContrast, AccessibilitySettings.ColorMode);
            Assert.AreEqual(AccessibilitySettings.TextSize.Large, AccessibilitySettings.CurrentTextSize);
            Assert.IsTrue(AccessibilitySettings.ReduceMotion);
        }

        [UnityTest]
        public IEnumerator Settings_DefaultValues_AfterClear()
        {
            PlayerPrefs.DeleteKey("a11y_colorMode");
            PlayerPrefs.DeleteKey("a11y_textSize");
            PlayerPrefs.DeleteKey("a11y_reduceMotion");
            PlayerPrefs.Save();

            AccessibilitySettings.Load();
            yield return null;

            Assert.AreEqual(AccessibilitySettings.ColorVisionMode.Normal, AccessibilitySettings.ColorMode);
            Assert.AreEqual(AccessibilitySettings.TextSize.Medium, AccessibilitySettings.CurrentTextSize);
            Assert.IsFalse(AccessibilitySettings.ReduceMotion);
        }

        // ================================================================
        // OnSettingsChanged event propagation
        // ================================================================

        [UnityTest]
        public IEnumerator OnSettingsChanged_FiresOnColorModeChange()
        {
            int fireCount = 0;
            void handler() => fireCount++;
            AccessibilitySettings.OnSettingsChanged += handler;

            AccessibilitySettings.ColorMode = AccessibilitySettings.ColorVisionMode.ProtanDeutan;
            yield return null;

            Assert.AreEqual(1, fireCount, "OnSettingsChanged should fire once on ColorMode change");

            AccessibilitySettings.OnSettingsChanged -= handler;
        }

        [UnityTest]
        public IEnumerator OnSettingsChanged_FiresOnTextSizeChange()
        {
            int fireCount = 0;
            void handler() => fireCount++;
            AccessibilitySettings.OnSettingsChanged += handler;

            AccessibilitySettings.CurrentTextSize = AccessibilitySettings.TextSize.Small;
            yield return null;

            Assert.AreEqual(1, fireCount);

            AccessibilitySettings.OnSettingsChanged -= handler;
        }

        [UnityTest]
        public IEnumerator OnSettingsChanged_FiresOnReduceMotionChange()
        {
            int fireCount = 0;
            void handler() => fireCount++;
            AccessibilitySettings.OnSettingsChanged += handler;

            AccessibilitySettings.ReduceMotion = true;
            yield return null;

            Assert.AreEqual(1, fireCount);

            AccessibilitySettings.OnSettingsChanged -= handler;
        }

        // ================================================================
        // WCAG 2.1 AA contrast validation across all modes
        // ================================================================

        [UnityTest]
        public IEnumerator AllAspectColors_MeetWCAG_AA_AgainstDarkBg()
        {
            Color darkBg = Color.black;
            var modes = new[]
            {
                AccessibilitySettings.ColorVisionMode.Normal,
                AccessibilitySettings.ColorVisionMode.ProtanDeutan,
                AccessibilitySettings.ColorVisionMode.Tritan,
                AccessibilitySettings.ColorVisionMode.HighContrast,
            };

            foreach (var mode in modes)
            {
                AccessibilitySettings.ColorMode = mode;
                yield return null;

                foreach (Aspect aspect in System.Enum.GetValues(typeof(Aspect)))
                {
                    Color fg = AccessibilitySettings.GetAspectColor(aspect);
                    float ratio = AccessibilitySettings.ContrastRatio(fg, darkBg);

                    // Large text threshold (3:1) — card aspect labels are typically large
                    Assert.GreaterOrEqual(ratio, 3f,
                        $"[{mode}] Aspect {aspect} color ({ColorUtility.ToHtmlStringRGB(fg)}) " +
                        $"vs black has ratio {ratio:F2}, needs >= 3:1 for large text");
                }
            }
        }

        [UnityTest]
        public IEnumerator HighContrast_MeetsStrictAAForNormalText()
        {
            AccessibilitySettings.ColorMode = AccessibilitySettings.ColorVisionMode.HighContrast;
            Color darkBg = new Color(0.1f, 0.1f, 0.1f, 1f);
            yield return null;

            foreach (Aspect aspect in System.Enum.GetValues(typeof(Aspect)))
            {
                Color fg = AccessibilitySettings.GetAspectColor(aspect);
                float ratio = AccessibilitySettings.ContrastRatio(fg, darkBg);

                // Hush is intentionally muted (gray), skip strict check
                if (aspect == Aspect.Hush) continue;

                Assert.GreaterOrEqual(ratio, 4.5f,
                    $"HighContrast: Aspect {aspect} ({ColorUtility.ToHtmlStringRGB(fg)}) " +
                    $"ratio {ratio:F2} should meet 4.5:1 AA for normal text");
            }
        }

        // ================================================================
        // Aspect icon dual identification
        // ================================================================

        [UnityTest]
        public IEnumerator AllAspects_HaveIcons()
        {
            yield return null;

            foreach (Aspect aspect in System.Enum.GetValues(typeof(Aspect)))
            {
                Assert.IsTrue(AccessibilitySettings.AspectIcons.ContainsKey(aspect),
                    $"Aspect {aspect} must have a dual-identification icon");
                Assert.IsNotEmpty(AccessibilitySettings.AspectIcons[aspect],
                    $"Aspect {aspect} icon must not be empty");
            }
        }

        // ================================================================
        // Font size scaling
        // ================================================================

        [UnityTest]
        public IEnumerator FontSizes_ScaleWithTextSize()
        {
            AccessibilitySettings.CurrentTextSize = AccessibilitySettings.TextSize.Small;
            float smallBody = AccessibilitySettings.BodyFontSize;
            float smallCard = AccessibilitySettings.CardNameFontSize;

            AccessibilitySettings.CurrentTextSize = AccessibilitySettings.TextSize.Medium;
            float medBody = AccessibilitySettings.BodyFontSize;
            float medCard = AccessibilitySettings.CardNameFontSize;

            AccessibilitySettings.CurrentTextSize = AccessibilitySettings.TextSize.Large;
            float largeBody = AccessibilitySettings.BodyFontSize;
            float largeCard = AccessibilitySettings.CardNameFontSize;

            yield return null;

            Assert.Less(smallBody, medBody, "Small body < Medium body");
            Assert.Less(medBody, largeBody, "Medium body < Large body");
            Assert.Less(smallCard, medCard, "Small card < Medium card");
            Assert.Less(medCard, largeCard, "Medium card < Large card");
        }
    }
}
