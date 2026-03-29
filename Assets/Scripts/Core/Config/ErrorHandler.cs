using System;
using UnityEngine;

namespace Banganka.Core.Config
{
    /// <summary>
    /// グローバルエラーハンドリング (ERROR_HANDLING.md)
    /// 未処理例外キャッチ + ユーザー通知 + ログ送信
    /// </summary>
    public class ErrorHandler : MonoBehaviour
    {
        public static ErrorHandler Instance { get; private set; }

        public event Action<string, string> OnErrorDisplayed; // (title, message)

        [SerializeField] bool enableCrashReporting = true;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Application.logMessageReceived += OnLogMessage;

            // リリースビルドでは Debug.Log を抑制
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            Debug.unityLogger.filterLogType = LogType.Warning;
#endif
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                HandleException(condition, stackTrace);
            }
        }

        // ====================================================================
        // Error Categories
        // ====================================================================

        public enum ErrorCategory
        {
            Network,      // 通信エラー
            Auth,         // 認証エラー
            Battle,       // バトルロジック不整合
            Data,         // データ読込失敗
            Purchase,     // 購入処理エラー
            Unknown,      // 不明
        }

        public void HandleError(ErrorCategory category, string message, Exception ex = null)
        {
            string userMessage = category switch
            {
                ErrorCategory.Network  => "通信エラーが発生しました。接続を確認してください。",
                ErrorCategory.Auth     => "認証に失敗しました。再ログインしてください。",
                ErrorCategory.Battle   => "バトル中にエラーが発生しました。",
                ErrorCategory.Data     => "データの読み込みに失敗しました。",
                ErrorCategory.Purchase => "購入処理中にエラーが発生しました。",
                _                      => "予期しないエラーが発生しました。",
            };

            Debug.LogError($"[ErrorHandler] [{category}] {message}\n{ex}");

            OnErrorDisplayed?.Invoke(GetCategoryTitle(category), userMessage);

            if (enableCrashReporting && ex != null)
                ReportToAnalytics(category, message, ex);
        }

        void HandleException(string condition, string stackTrace)
        {
            var category = ClassifyException(condition);
            Debug.LogError($"[ErrorHandler] Unhandled: {condition}");

            OnErrorDisplayed?.Invoke("エラー", "予期しないエラーが発生しました。アプリを再起動してください。");

            if (enableCrashReporting)
                ReportToAnalytics(category, condition, null);
        }

        static ErrorCategory ClassifyException(string condition)
        {
            if (condition.Contains("Network") || condition.Contains("WebRequest") || condition.Contains("Socket"))
                return ErrorCategory.Network;
            if (condition.Contains("Auth") || condition.Contains("Firebase"))
                return ErrorCategory.Auth;
            if (condition.Contains("Battle") || condition.Contains("BattleEngine"))
                return ErrorCategory.Battle;
            if (condition.Contains("NullReference") || condition.Contains("KeyNotFound"))
                return ErrorCategory.Data;
            return ErrorCategory.Unknown;
        }

        static string GetCategoryTitle(ErrorCategory category) => category switch
        {
            ErrorCategory.Network  => "通信エラー",
            ErrorCategory.Auth     => "認証エラー",
            ErrorCategory.Battle   => "バトルエラー",
            ErrorCategory.Data     => "データエラー",
            ErrorCategory.Purchase => "購入エラー",
            _                      => "エラー",
        };

        // ====================================================================
        // Retry Logic
        // ====================================================================

        public static void RetryWithBackoff(Action action, int maxRetries = 3, float baseDelay = 1f)
        {
            if (Instance != null)
                Instance.StartCoroutine(RetryCoroutine(action, maxRetries, baseDelay));
            else
                action(); // Instanceがなければ即実行のみ
        }

        static System.Collections.IEnumerator RetryCoroutine(Action action, int maxRetries, float baseDelay)
        {
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                bool success = false;
                Exception caught = null;

                try
                {
                    action();
                    success = true;
                }
                catch (Exception ex)
                {
                    caught = ex;
                }

                if (success)
                    yield break;

                if (attempt < maxRetries)
                {
                    float delay = baseDelay * Mathf.Pow(2, attempt);
                    Debug.LogWarning($"[ErrorHandler] Retry {attempt + 1}/{maxRetries} in {delay}s: {caught.Message}");
                    yield return new WaitForSeconds(delay);
                }
                else
                {
                    Instance?.HandleError(ErrorCategory.Unknown, caught.Message, caught);
                }
            }
        }

        // ====================================================================
        // Analytics (stub)
        // ====================================================================

        static void ReportToAnalytics(ErrorCategory category, string message, Exception ex)
        {
#if FIREBASE_ENABLED
            Firebase.Crashlytics.Crashlytics.SetCustomKey("error_category", category.ToString());
            if (ex != null)
                Firebase.Crashlytics.Crashlytics.LogException(ex);
            else
                Firebase.Crashlytics.Crashlytics.Log($"[{category}] {message}");
#endif
            Debug.Log($"[Analytics] Error reported: [{category}] {message}");
        }
    }
}
