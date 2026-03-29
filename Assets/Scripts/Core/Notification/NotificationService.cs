using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif
#if FIREBASE_ENABLED
using Firebase.Messaging;
using Firebase.Firestore;
#endif

namespace Banganka.Core.Notification
{
    /// <summary>
    /// 通知サービス (NOTIFICATION_SPEC.md)
    /// FCMプッシュ + ローカル通知 + アプリ内通知 + ディープリンク
    /// </summary>
    public class NotificationService : MonoBehaviour
    {
        public static NotificationService Instance { get; private set; }

        // ====================================================================
        // Notification Types (8 push + 2 local)
        // ====================================================================

        public enum NotificationType
        {
            // Push (FCM)
            FriendBattleInvite,  // P01
            FriendRequest,       // P02
            DailyMissionReset,   // P03
            BattlePassReset,     // P04
            EventStart,          // P05
            Maintenance,         // P06
            ReturnReminder,      // P07
            NewContent,          // P08
            // Local
            LoginReminder,       // L01
            BattlePassExpiry,    // L02
        }

        // ====================================================================
        // Settings (per-type toggle)
        // ====================================================================

        static readonly Dictionary<NotificationType, bool> _settings = new()
        {
            { NotificationType.FriendBattleInvite, true },
            { NotificationType.FriendRequest, true },
            { NotificationType.DailyMissionReset, true },
            { NotificationType.BattlePassReset, true },
            { NotificationType.EventStart, true },
            { NotificationType.Maintenance, true }, // Cannot disable
            { NotificationType.ReturnReminder, true },
            { NotificationType.NewContent, true },
            { NotificationType.LoginReminder, true },
            { NotificationType.BattlePassExpiry, true },
        };

        // Cooldown: max 5/day, same type min 4h, quiet hours 22:00-8:00
        const int MaxDailyNotifications = 5;
        const float SameTypeCooldownHours = 4f;
        const int QuietHourStart = 22;
        const int QuietHourEnd = 8;

        int _dailyCount;
        readonly Dictionary<NotificationType, DateTime> _lastSent = new();
        string _fcmToken;

        // ====================================================================
        // In-App Notifications
        // ====================================================================

        public event Action<string, string> OnInAppNotification; // (title, message)
        public event Action<string> OnDeepLink; // deeplink URL

        // ====================================================================
        // Lifecycle
        // ====================================================================

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Initialize()
        {
            LoadSettings();
            RequestPermission();
            RegisterFCMToken();
            ScheduleLocalNotifications();
        }

        // ====================================================================
        // Permission
        // ====================================================================

        void RequestPermission()
        {
#if UNITY_IOS && !UNITY_EDITOR
            using var req = new AuthorizationRequest(
                AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound,
                registerForRemoteNotifications: true);
            Debug.Log("[Notification] iOS permission requested");
#else
            Debug.Log("[Notification] Permission requested (non-iOS or Editor)");
#endif
        }

        // ====================================================================
        // FCM Token
        // ====================================================================

        void RegisterFCMToken()
        {
#if FIREBASE_ENABLED
            FirebaseMessaging.TokenReceived += (_, args) => OnTokenReceived(args.Token);
            FirebaseMessaging.MessageReceived += (_, args) =>
            {
                var n = args.Message.Notification;
                var data = args.Message.Data != null
                    ? new Dictionary<string, string>(args.Message.Data)
                    : null;
                OnMessageReceived(n?.Title ?? "", n?.Body ?? "", data);
            };
            Debug.Log("[Notification] FCM listener registered");
#else
            Debug.Log("[Notification] FCM not available — local mode");
#endif
        }

        void OnTokenReceived(string token)
        {
            _fcmToken = token;
#if FIREBASE_ENABLED
            var uid = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
            if (!string.IsNullOrEmpty(uid))
            {
                var deviceId = SystemInfo.deviceUniqueIdentifier;
                FirebaseFirestore.DefaultInstance
                    .Collection("users").Document(uid)
                    .Collection("fcmTokens").Document(deviceId)
                    .SetAsync(new Dictionary<string, object>
                    {
                        { "token", token },
                        { "platform", Application.platform.ToString() },
                        { "updatedAt", FieldValue.ServerTimestamp }
                    });
            }
#endif
            Debug.Log($"[Notification] FCM token: {token[..Mathf.Min(20, token.Length)]}...");
        }

        void OnMessageReceived(string title, string body, Dictionary<string, string> data)
        {
            // Determine notification type from data
            NotificationType type = NotificationType.NewContent;
            if (data != null && data.TryGetValue("type", out string typeStr))
            {
                if (Enum.TryParse(typeStr, out NotificationType parsed))
                    type = parsed;
            }

            if (!CanSend(type)) return;
            RecordSent(type);

            // Parse deep link
            if (data != null && data.TryGetValue("deeplink", out string deeplink))
            {
                OnDeepLink?.Invoke(deeplink);
            }

            // Show in-app notification
            OnInAppNotification?.Invoke(title, body);
        }

        /// <summary>
        /// アプリ内から通知を表示する (クールダウン制御付き)。
        /// </summary>
        public void ShowInApp(NotificationType type, string title, string message, string deeplink = null)
        {
            if (!CanSend(type)) return;
            RecordSent(type);

            OnInAppNotification?.Invoke(title, message);
            if (!string.IsNullOrEmpty(deeplink))
                OnDeepLink?.Invoke(deeplink);
        }

        // ====================================================================
        // Local Notifications
        // ====================================================================

        void ScheduleLocalNotifications()
        {
            // L01: Login reminder — 20h after last login
            if (_settings[NotificationType.LoginReminder])
            {
                ScheduleLocal(
                    "万願果",
                    "交界があなたを待っています。ログインボーナスを受け取ろう！",
                    TimeSpan.FromHours(20),
                    "login_reminder"
                );
            }

            // L02: Battle pass expiry — schedule when season info available
            // (handled separately when season data is loaded)
        }

        public void ScheduleBattlePassExpiry(DateTime seasonEnd)
        {
            if (!_settings[NotificationType.BattlePassExpiry]) return;
            var notifyTime = seasonEnd.AddDays(-3);
            if (notifyTime <= DateTime.UtcNow) return;

            ScheduleLocal(
                "願道パス",
                "シーズン終了まであと3日！報酬を受け取り忘れないように。",
                notifyTime - DateTime.UtcNow,
                "battlepass_expiry"
            );
        }

        void ScheduleLocal(string title, string body, TimeSpan delay, string identifier)
        {
            // Respect quiet hours
            var fireTime = DateTime.Now + delay;
            int hour = fireTime.Hour;
            if (hour >= QuietHourStart || hour < QuietHourEnd)
            {
                // Delay to 8:00 next day
                fireTime = fireTime.Date.AddDays(hour >= QuietHourStart ? 1 : 0).AddHours(QuietHourEnd);
                delay = fireTime - DateTime.Now;
                if (delay < TimeSpan.Zero) return;
            }

#if UNITY_IOS && !UNITY_EDITOR
            var trigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = delay,
                Repeats = false
            };
            var notification = new iOSNotification(identifier)
            {
                Title = title,
                Body = body,
                ShowInForeground = true,
                ForegroundPresentationOption =
                    PresentationOption.Alert | PresentationOption.Sound,
                Trigger = trigger
            };
            iOSNotificationCenter.ScheduleNotification(notification);
#endif
            Debug.Log($"[Notification] Scheduled local: '{title}' in {delay.TotalHours:F1}h");
        }

        public void CancelAllLocal()
        {
#if UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
#endif
            Debug.Log("[Notification] Cancelled all local notifications");
        }

        // ====================================================================
        // Send Rules (Cooldown)
        // ====================================================================

        public bool CanSend(NotificationType type)
        {
            if (type == NotificationType.Maintenance) return true; // Always send

            if (!_settings.TryGetValue(type, out bool enabled) || !enabled)
                return false;

            if (_dailyCount >= MaxDailyNotifications)
                return false;

            if (_lastSent.TryGetValue(type, out DateTime last))
            {
                if ((DateTime.UtcNow - last).TotalHours < SameTypeCooldownHours)
                    return false;
            }

            return true;
        }

        void RecordSent(NotificationType type)
        {
            _dailyCount++;
            _lastSent[type] = DateTime.UtcNow;
        }

        // ====================================================================
        // Deep Link Routing
        // ====================================================================

        public static (string screen, string param) ParseDeepLink(string url)
        {
            // banngannka://battle/join/{roomId}
            // banngannka://friends
            // banngannka://home
            // banngannka://event/{eventId}
            // banngannka://shop
            // banngannka://battlepass
            // banngannka://news

            if (string.IsNullOrEmpty(url)) return (null, null);

            string path = url.Replace("banngannka://", "");
            string[] parts = path.Split('/');

            if (parts.Length == 0) return (null, null);

            string screen = parts[0];
            string param = parts.Length > 2 ? parts[2] : parts.Length > 1 ? parts[1] : null;

            return (screen, param);
        }

        // ====================================================================
        // Settings
        // ====================================================================

        public static void SetEnabled(NotificationType type, bool enabled)
        {
            if (type == NotificationType.Maintenance) return; // Cannot disable
            _settings[type] = enabled;
            SaveSettings();
        }

        public static bool IsEnabled(NotificationType type)
            => _settings.TryGetValue(type, out bool e) && e;

        static void SaveSettings()
        {
            foreach (var kv in _settings)
                PlayerPrefs.SetInt($"notif_{kv.Key}", kv.Value ? 1 : 0);
            PlayerPrefs.Save();
        }

        static void LoadSettings()
        {
            var keys = new List<NotificationType>(_settings.Keys);
            foreach (var key in keys)
            {
                if (PlayerPrefs.HasKey($"notif_{key}"))
                    _settings[key] = PlayerPrefs.GetInt($"notif_{key}") == 1;
            }
        }
    }
}
