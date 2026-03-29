namespace Banganka.Core.Config
{
    /// <summary>
    /// Firebase環境設定 — Dev/Staging/Prod切替
    /// Crashlytics + Analytics の環境分離
    /// </summary>
    public static class FirebaseConfig
    {
        public enum Environment { Development, Staging, Production }

        public static Environment CurrentEnv
        {
            get
            {
#if ENV_PROD
                return Environment.Production;
#elif ENV_STAGING
                return Environment.Staging;
#else
                return Environment.Development;
#endif
            }
        }

        public static string ProjectId => CurrentEnv switch
        {
            Environment.Production => "banganka-prod",
            Environment.Staging    => "banganka-staging",
            _                      => "banganka-dev",
        };

        public static string FunctionsRegion => "asia-northeast1";

        public static string FunctionsBaseUrl => CurrentEnv switch
        {
            Environment.Production => $"https://{FunctionsRegion}-{ProjectId}.cloudfunctions.net",
            Environment.Staging    => $"https://{FunctionsRegion}-{ProjectId}.cloudfunctions.net",
            _                      => "http://localhost:5001/banganka-dev/asia-northeast1",
        };

        public static string RtdbUrl => CurrentEnv switch
        {
            Environment.Production => $"https://{ProjectId}-default-rtdb.asia-southeast1.firebasedatabase.app",
            Environment.Staging    => $"https://{ProjectId}-default-rtdb.asia-southeast1.firebasedatabase.app",
            _                      => "http://localhost:9000/?ns=banganka-dev",
        };

        public static bool UseEmulator => CurrentEnv == Environment.Development;

        public static bool EnableCrashlytics => CurrentEnv != Environment.Development;

        public static bool EnableAnalytics => CurrentEnv != Environment.Development;

        public static bool VerboseLogging => CurrentEnv == Environment.Development;
    }
}
