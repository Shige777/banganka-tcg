#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Banganka.Editor
{
    /// <summary>
    /// ビルドツール — ビルド番号自動インクリメント + 環境切替
    /// </summary>
    public static class BuildTools
    {
        // ====================================================================
        // Build Number Increment
        // ====================================================================

        [MenuItem("Banganka/Build/Increment Build Number")]
        public static void IncrementBuildNumber()
        {
            int current = PlayerSettings.iOS.buildNumber != null
                ? int.TryParse(PlayerSettings.iOS.buildNumber, out int n) ? n : 0
                : 0;

            current++;
            PlayerSettings.iOS.buildNumber = current.ToString();
            PlayerSettings.Android.bundleVersionCode = current;

            Debug.Log($"[BuildTools] Build number incremented to {current}");
        }

        // ====================================================================
        // Environment Switching
        // ====================================================================

        public enum BuildEnv { Development, Staging, Production }

        [MenuItem("Banganka/Environment/Development")]
        public static void SetDevelopment() => SetEnvironment(BuildEnv.Development);

        [MenuItem("Banganka/Environment/Staging")]
        public static void SetStaging() => SetEnvironment(BuildEnv.Staging);

        [MenuItem("Banganka/Environment/Production")]
        public static void SetProduction() => SetEnvironment(BuildEnv.Production);

        static void SetEnvironment(BuildEnv env)
        {
            var namedTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);

            // Remove existing env defines
            defines = defines.Replace("ENV_DEV", "").Replace("ENV_STAGING", "").Replace("ENV_PROD", "");
            defines = defines.Replace(";;", ";").Trim(';');

            string envDefine = env switch
            {
                BuildEnv.Development => "ENV_DEV",
                BuildEnv.Staging => "ENV_STAGING",
                BuildEnv.Production => "ENV_PROD",
                _ => "ENV_DEV",
            };

            defines = string.IsNullOrEmpty(defines) ? envDefine : $"{defines};{envDefine}";

            PlayerSettings.SetScriptingDefineSymbols(namedTarget, defines);

            Debug.Log($"[BuildTools] Environment set to {env} ({envDefine})");
        }

        // ====================================================================
        // Pre-Build Validation
        // ====================================================================

        [MenuItem("Banganka/Build/Validate Pre-Build")]
        public static void ValidatePreBuild()
        {
            int issues = 0;

            // Check bundle identifier
            if (!PlayerSettings.applicationIdentifier.StartsWith("com.banganka"))
            {
                Debug.LogWarning("[PreBuild] Bundle identifier should start with 'com.banganka'");
                issues++;
            }

            // Check minimum iOS version
            if (string.Compare(PlayerSettings.iOS.targetOSVersionString, "15.0") < 0)
            {
                Debug.LogWarning("[PreBuild] Minimum iOS version should be 15.0+");
                issues++;
            }

            // Run terminology audit (via reflection — test assembly may not be loaded)
            var auditType = System.Type.GetType("Banganka.Tests.EditMode.TerminologyAudit, Banganka.Tests.EditMode");
            if (auditType != null)
            {
                var method = auditType.GetMethod("LogAuditResults", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                method?.Invoke(null, null);
            }

            if (issues == 0)
                Debug.Log("[PreBuild] All pre-build checks passed!");
            else
                Debug.LogWarning($"[PreBuild] {issues} issue(s) found");
        }
    }
}
#endif
