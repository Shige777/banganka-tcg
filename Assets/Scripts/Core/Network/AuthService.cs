using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if FIREBASE_ENABLED
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using Firebase.Messaging;
#endif

namespace Banganka.Core.Network
{
    /// <summary>
    /// 認証サービス抽象化。
    /// FIREBASE_ENABLED 定義時は Firebase Auth を使用、未定義時はローカルモードで動作。
    /// </summary>
    public class AuthService : MonoBehaviour
    {
        public static AuthService Instance { get; private set; }

        public string Uid { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsAuthenticated { get; private set; }
        public bool IsAnonymous { get; private set; }
        public string ProviderId { get; private set; }

        public event Action<string> OnAuthSuccess;
        public event Action<string> OnAuthFailed;
#pragma warning disable CS0067 // Firebase統合時に使用予定
        public event Action OnAccountLinked;
#pragma warning restore CS0067
        public event Action OnAccountDeleted;
        public event Action OnSessionRefreshed;

        // Multi-device: FCM token + device ID
        public string FcmToken { get; private set; }
        public string DeviceId { get; private set; }

        // Session auto-refresh interval (seconds)
        const float TOKEN_REFRESH_INTERVAL = 3300f; // 55 minutes (tokens expire at 60 min)
        Coroutine _refreshCoroutine;

#if FIREBASE_ENABLED
        FirebaseAuth _auth;
        FirebaseFirestore _firestore;
        FirebaseUser _currentUser;
#endif

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DeviceId = SystemInfo.deviceUniqueIdentifier;
        }

        void Start()
        {
#if FIREBASE_ENABLED
            InitializeFirebase();
#endif
        }

        void OnDestroy()
        {
#if FIREBASE_ENABLED
            if (_auth != null)
            {
                _auth.StateChanged -= OnAuthStateChanged;
                _auth.IdTokenChanged -= OnIdTokenChanged;
            }
#endif
            if (_refreshCoroutine != null)
                StopCoroutine(_refreshCoroutine);
        }

        // ============================================================
        // Firebase Initialization
        // ============================================================

#if FIREBASE_ENABLED
        void InitializeFirebase()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result == DependencyStatus.Available)
                {
                    _auth = FirebaseAuth.DefaultInstance;
                    _firestore = FirebaseFirestore.DefaultInstance;
                    _auth.StateChanged += OnAuthStateChanged;
                    _auth.IdTokenChanged += OnIdTokenChanged;

                    // FCM token registration
                    FirebaseMessaging.TokenReceived += OnFcmTokenReceived;
                    FirebaseMessaging.GetTokenAsync().ContinueWithOnMainThread(t =>
                    {
                        if (t.IsCompletedSuccessfully)
                            FcmToken = t.Result;
                    });

                    Debug.Log("[AuthService] Firebase initialized");

                    // Restore session if user exists
                    if (_auth.CurrentUser != null)
                    {
                        ApplyFirebaseUser(_auth.CurrentUser);
                        StartTokenRefresh();
                    }
                }
                else
                {
                    Debug.LogError($"[AuthService] Firebase dependency error: {task.Result}");
                    OnAuthFailed?.Invoke("Firebase initialization failed");
                }
            });
        }

        void OnAuthStateChanged(object sender, EventArgs args)
        {
            var user = _auth.CurrentUser;
            if (user != null && (Uid != user.UserId))
            {
                ApplyFirebaseUser(user);
            }
            else if (user == null && IsAuthenticated)
            {
                ClearSession();
            }
        }

        void OnIdTokenChanged(object sender, EventArgs args)
        {
            OnSessionRefreshed?.Invoke();
            Debug.Log("[AuthService] ID token refreshed");
        }

        void OnFcmTokenReceived(object sender, Firebase.Messaging.TokenReceivedEventArgs args)
        {
            FcmToken = args.Token;
            Debug.Log($"[AuthService] FCM token received: {FcmToken[..12]}...");
            if (IsAuthenticated)
                StoreDeviceInfo();
        }

        void ApplyFirebaseUser(FirebaseUser user)
        {
            _currentUser = user;
            Uid = user.UserId;
            DisplayName = string.IsNullOrEmpty(user.DisplayName) ? "果求者" : user.DisplayName;
            IsAuthenticated = true;
            IsAnonymous = user.IsAnonymous;
            ProviderId = user.IsAnonymous ? "anonymous" : "apple.com";

            Debug.Log($"[AuthService] Authenticated (Firebase): {Uid}, anonymous={IsAnonymous}");
            OnAuthSuccess?.Invoke(Uid);

            StoreDeviceInfo();
            StartTokenRefresh();
        }

        /// <summary>
        /// FCM token と device ID を Firestore users/{uid}/devices/{deviceId} に保存
        /// </summary>
        void StoreDeviceInfo()
        {
            if (_firestore == null || string.IsNullOrEmpty(Uid)) return;

            var deviceDoc = _firestore
                .Collection("users").Document(Uid)
                .Collection("devices").Document(DeviceId);

            var data = new Dictionary<string, object>
            {
                { "fcmToken", FcmToken ?? "" },
                { "deviceId", DeviceId },
                { "platform", Application.platform.ToString() },
                { "lastSeen", FieldValue.ServerTimestamp },
                { "appVersion", Application.version }
            };

            deviceDoc.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogWarning($"[AuthService] Failed to store device info: {task.Exception}");
                else
                    Debug.Log("[AuthService] Device info stored in Firestore");
            });
        }
#endif

        // ============================================================
        // Anonymous Sign-In
        // ============================================================

        public void SignInAnonymous()
        {
#if FIREBASE_ENABLED
            if (_auth == null)
            {
                Debug.LogError("[AuthService] Firebase not initialized");
                OnAuthFailed?.Invoke("Firebase not initialized");
                return;
            }

            _auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    string error = task.Exception?.Flatten().InnerException?.Message ?? "Unknown error";
                    Debug.LogError($"[AuthService] Anonymous sign-in failed: {error}");
                    OnAuthFailed?.Invoke(error);
                    return;
                }

                var result = task.Result;
                IsAnonymous = true;
                ProviderId = "anonymous";
                ApplyFirebaseUser(result.User);
            });
#else
            // Local simulation
            Uid = "local_" + SystemInfo.deviceUniqueIdentifier[..8];
            DisplayName = "果求者";
            IsAuthenticated = true;
            IsAnonymous = true;
            ProviderId = "anonymous";
            Debug.Log($"[AuthService] Signed in (local): {Uid}");
            OnAuthSuccess?.Invoke(Uid);
#endif
        }

        // ============================================================
        // Apple Sign-In
        // ============================================================

        /// <summary>
        /// Apple Sign-In で認証。identityToken は Sign in with Apple のJWTトークン。
        /// 既にAnonymous認証済みの場合はアカウントリンクを試行する。
        /// </summary>
        public void SignInWithApple(string identityToken)
        {
            SignInWithApple(identityToken, nonce: null, fullName: null);
        }

        /// <summary>
        /// Apple Sign-In (full parameters)。
        /// nonce は ASAuthorizationAppleIDRequest に渡した rawNonce の SHA256。
        /// fullName は初回認証時のみ取得可能。
        /// </summary>
        public void SignInWithApple(string identityToken, string nonce, string fullName)
        {
            if (string.IsNullOrEmpty(identityToken))
            {
                Debug.LogError("[AuthService] Apple identity token is null or empty");
                OnAuthFailed?.Invoke("Invalid Apple identity token");
                return;
            }

#if FIREBASE_ENABLED
            if (_auth == null)
            {
                Debug.LogError("[AuthService] Firebase not initialized");
                OnAuthFailed?.Invoke("Firebase not initialized");
                return;
            }

            // Create Apple credential for Firebase
            var credential = OAuthProvider.GetCredential(
                "apple.com",
                identityToken,
                nonce ?? "",
                null // accessToken not needed for Apple
            );

            // If currently anonymous, link instead of sign-in
            if (IsAnonymous && _currentUser != null)
            {
                LinkAnonymousWithApple(credential, fullName);
                return;
            }

            // Fresh sign-in
            _auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    string error = task.Exception?.Flatten().InnerException?.Message ?? "Unknown error";
                    Debug.LogError($"[AuthService] Apple sign-in failed: {error}");
                    OnAuthFailed?.Invoke(error);
                    return;
                }

                var result = task.Result;
                IsAnonymous = false;
                ProviderId = "apple.com";

                // Apple only provides display name on first auth
                if (!string.IsNullOrEmpty(fullName))
                {
                    var profile = result.User.UpdateUserProfileAsync(new UserProfile
                    {
                        DisplayName = fullName
                    });
                }

                ApplyFirebaseUser(result.User);
            });
#else
            // Local simulation
            Uid = "apple_" + identityToken.GetHashCode().ToString("X8");
            DisplayName = string.IsNullOrEmpty(fullName) ? "果求者" : fullName;
            IsAuthenticated = true;
            IsAnonymous = false;
            ProviderId = "apple.com";
            Debug.Log($"[AuthService] Signed in with Apple (local): {Uid}");
            OnAuthSuccess?.Invoke(Uid);
#endif
        }

        // ============================================================
        // Anonymous → Apple Account Linking
        // ============================================================

#if FIREBASE_ENABLED
        /// <summary>
        /// Anonymous アカウントを Apple 認証にリンクし、データを維持したまま永続アカウントへ昇格。
        /// </summary>
        void LinkAnonymousWithApple(Credential credential, string fullName)
        {
            _currentUser.LinkWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    var ex = task.Exception?.Flatten().InnerException;

                    // If linking fails because credential already linked to another account,
                    // sign in with credential instead (user has existing account)
                    if (ex is FirebaseAccountLinkException linkEx)
                    {
                        Debug.LogWarning("[AuthService] Link failed (credential in use), signing in with existing account");
                        _auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(signInTask =>
                        {
                            if (signInTask.IsFaulted || signInTask.IsCanceled)
                            {
                                string error = signInTask.Exception?.Flatten().InnerException?.Message ?? "Link fallback failed";
                                Debug.LogError($"[AuthService] Fallback sign-in failed: {error}");
                                OnAuthFailed?.Invoke(error);
                                return;
                            }
                            IsAnonymous = false;
                            ProviderId = "apple.com";
                            ApplyFirebaseUser(signInTask.Result.User);
                        });
                        return;
                    }

                    string errorMsg = ex?.Message ?? "Unknown linking error";
                    Debug.LogError($"[AuthService] Account linking failed: {errorMsg}");
                    OnAuthFailed?.Invoke(errorMsg);
                    return;
                }

                var result = task.Result;
                IsAnonymous = false;
                ProviderId = "apple.com";

                if (!string.IsNullOrEmpty(fullName))
                {
                    result.User.UpdateUserProfileAsync(new UserProfile
                    {
                        DisplayName = fullName
                    });
                }

                Debug.Log($"[AuthService] Anonymous account linked to Apple: {result.User.UserId}");
                ApplyFirebaseUser(result.User);
                OnAccountLinked?.Invoke();
            });
        }
#endif

        // ============================================================
        // Session Auto-Refresh
        // ============================================================

        void StartTokenRefresh()
        {
            if (_refreshCoroutine != null)
                StopCoroutine(_refreshCoroutine);
            _refreshCoroutine = StartCoroutine(TokenRefreshLoop());
        }

        IEnumerator TokenRefreshLoop()
        {
            while (IsAuthenticated)
            {
                yield return new WaitForSeconds(TOKEN_REFRESH_INTERVAL);

                if (!IsAuthenticated) yield break;

#if FIREBASE_ENABLED
                if (_currentUser != null)
                {
                    _currentUser.TokenAsync(forceRefresh: true).ContinueWithOnMainThread(task =>
                    {
                        if (task.IsFaulted)
                        {
                            Debug.LogWarning($"[AuthService] Token refresh failed: {task.Exception?.Message}");
                            // If refresh fails, could mean revoked session
                            return;
                        }
                        Debug.Log("[AuthService] Token refreshed successfully");
                        OnSessionRefreshed?.Invoke();
                    });
                }
#else
                Debug.Log("[AuthService] Token refresh (local, no-op)");
                OnSessionRefreshed?.Invoke();
#endif
            }
        }

        // ============================================================
        // Sign Out
        // ============================================================

        public void SignOut()
        {
#if FIREBASE_ENABLED
            if (_auth != null)
            {
                // Remove device record before signing out
                RemoveDeviceRecord();
                _auth.SignOut();
            }
#endif
            ClearSession();
            Debug.Log("[AuthService] Signed out");
        }

        void ClearSession()
        {
            Uid = null;
            DisplayName = null;
            IsAuthenticated = false;
            IsAnonymous = false;
            ProviderId = null;
#if FIREBASE_ENABLED
            _currentUser = null;
#endif
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }

        // ============================================================
        // Delete Account
        // ============================================================

        /// <summary>
        /// アカウント削除。Cloud Function deleteAccount() でサーバー側データを消去後、
        /// Firebase Auth アカウントを削除する。
        /// </summary>
        public void DeleteAccount(Action<bool> callback)
        {
#if FIREBASE_ENABLED
            if (_currentUser == null)
            {
                Debug.LogWarning("[AuthService] No user to delete");
                callback?.Invoke(false);
                return;
            }

            // Step 1: Call Cloud Function to delete server-side data
            // (Firestore user doc, match history, etc.)
            var functions = Firebase.Functions.FirebaseFunctions.DefaultInstance;
            var deleteFunc = functions.GetHttpsCallable("deleteAccount");

            deleteFunc.CallAsync(new Dictionary<string, object>
            {
                { "uid", Uid },
                { "deviceId", DeviceId }
            }).ContinueWithOnMainThread(funcTask =>
            {
                if (funcTask.IsFaulted)
                {
                    Debug.LogError($"[AuthService] Cloud Function deleteAccount failed: {funcTask.Exception?.Message}");
                    // Continue with auth deletion anyway - server cleanup can be retried
                }
                else
                {
                    Debug.Log("[AuthService] Server-side data deletion requested");
                }

                // Step 2: Delete Firebase Auth account
                _currentUser.DeleteAsync().ContinueWithOnMainThread(deleteTask =>
                {
                    if (deleteTask.IsFaulted)
                    {
                        string error = deleteTask.Exception?.Flatten().InnerException?.Message ?? "Delete failed";
                        Debug.LogError($"[AuthService] Auth account deletion failed: {error}");
                        callback?.Invoke(false);
                        return;
                    }

                    Debug.Log("[AuthService] Account deleted successfully");
                    ClearSession();
                    OnAccountDeleted?.Invoke();
                    callback?.Invoke(true);
                });
            });
#else
            // Local simulation
            Debug.Log("[AuthService] Account deleted (local)");
            ClearSession();
            PlayerPrefs.DeleteAll();
            OnAccountDeleted?.Invoke();
            callback?.Invoke(true);
#endif
        }

        // ============================================================
        // Multi-device Helpers
        // ============================================================

#if FIREBASE_ENABLED
        void RemoveDeviceRecord()
        {
            if (_firestore == null || string.IsNullOrEmpty(Uid)) return;

            _firestore
                .Collection("users").Document(Uid)
                .Collection("devices").Document(DeviceId)
                .DeleteAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted)
                        Debug.LogWarning($"[AuthService] Failed to remove device record: {task.Exception}");
                });
        }
#endif

        /// <summary>
        /// 現在の認証トークンを取得 (APIリクエスト用)。
        /// </summary>
        public void GetIdToken(Action<string> callback, bool forceRefresh = false)
        {
#if FIREBASE_ENABLED
            if (_currentUser == null)
            {
                callback?.Invoke(null);
                return;
            }

            _currentUser.TokenAsync(forceRefresh).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning("[AuthService] Failed to get ID token");
                    callback?.Invoke(null);
                    return;
                }
                callback?.Invoke(task.Result);
            });
#else
            // Local simulation: return a fake token
            callback?.Invoke($"local_token_{Uid}");
#endif
        }
    }
}
