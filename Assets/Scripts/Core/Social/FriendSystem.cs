using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if FIREBASE_ENABLED
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
#endif

namespace Banganka.Core.Social
{
    public enum FriendStatus { Pending, Accepted, Blocked }

    [Serializable]
    public class FriendEntry
    {
        public string uid;
        public string displayName;
        public FriendStatus status;
        public bool isOnline;
        public string lastSeen; // ISO date string
        public int rating;
    }

    /// <summary>
    /// フレンドシステム (SOCIAL_SPEC.md P0)
    /// フレンド申請 / 承認 / ブロック / フレンド対戦招待
    /// </summary>
    public static class FriendSystem
    {
        static readonly List<FriendEntry> _friends = new();

        public const int MaxFriends = 100;

        public static IReadOnlyList<FriendEntry> Friends => _friends;

        public static event Action OnFriendsUpdated;

        public static IReadOnlyList<FriendEntry> AcceptedFriends =>
            _friends.Where(f => f.status == FriendStatus.Accepted).ToList();

        public static IReadOnlyList<FriendEntry> PendingRequests =>
            _friends.Where(f => f.status == FriendStatus.Pending).ToList();

        public static int PendingCount => _friends.Count(f => f.status == FriendStatus.Pending);

        // ------------------------------------------------------------------
        // Friend Request
        // ------------------------------------------------------------------

        public static bool SendRequest(string targetUid, string targetName)
        {
            if (_friends.Count(f => f.status == FriendStatus.Accepted) >= MaxFriends)
                return false;
            if (_friends.Any(f => f.uid == targetUid))
                return false;

            _friends.Add(new FriendEntry
            {
                uid = targetUid,
                displayName = targetName,
                status = FriendStatus.Pending,
            });

            OnFriendsUpdated?.Invoke();
#if FIREBASE_ENABLED
            var myUid = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
            if (!string.IsNullOrEmpty(myUid))
            {
                var db = FirebaseFirestore.DefaultInstance;
                // Write to both users' friends subcollections
                var myDoc = db.Collection("users").Document(myUid)
                    .Collection("friends").Document(targetUid);
                var theirDoc = db.Collection("users").Document(targetUid)
                    .Collection("friends").Document(myUid);
                var batch = db.StartBatch();
                batch.Set(myDoc, new Dictionary<string, object>
                {
                    { "status", "pending_sent" },
                    { "displayName", targetName },
                    { "createdAt", FieldValue.ServerTimestamp }
                });
                batch.Set(theirDoc, new Dictionary<string, object>
                {
                    { "status", "pending_received" },
                    { "displayName", Data.PlayerData.DisplayName },
                    { "createdAt", FieldValue.ServerTimestamp }
                });
                batch.CommitAsync().ContinueWithOnMainThread(t =>
                {
                    if (t.IsFaulted)
                        Debug.LogWarning($"[FriendSystem] Firestore write failed: {t.Exception?.Message}");
                });
            }
#endif
            return true;
        }

        public static bool AcceptRequest(string uid)
        {
            var entry = _friends.FirstOrDefault(f => f.uid == uid && f.status == FriendStatus.Pending);
            if (entry == null) return false;

            entry.status = FriendStatus.Accepted;
            OnFriendsUpdated?.Invoke();
            return true;
        }

        public static bool RejectRequest(string uid)
        {
            return _friends.RemoveAll(f => f.uid == uid && f.status == FriendStatus.Pending) > 0;
        }

        // ------------------------------------------------------------------
        // Remove / Block
        // ------------------------------------------------------------------

        public static bool RemoveFriend(string uid)
        {
            bool removed = _friends.RemoveAll(f => f.uid == uid) > 0;
            if (removed) OnFriendsUpdated?.Invoke();
            return removed;
        }

        public static bool BlockUser(string uid)
        {
            var entry = _friends.FirstOrDefault(f => f.uid == uid);
            if (entry != null)
            {
                entry.status = FriendStatus.Blocked;
            }
            else
            {
                _friends.Add(new FriendEntry
                {
                    uid = uid,
                    status = FriendStatus.Blocked,
                });
            }
            OnFriendsUpdated?.Invoke();
            return true;
        }

        public static bool UnblockUser(string uid)
        {
            return _friends.RemoveAll(f => f.uid == uid && f.status == FriendStatus.Blocked) > 0;
        }

        public static bool IsBlocked(string uid)
        {
            return _friends.Any(f => f.uid == uid && f.status == FriendStatus.Blocked);
        }

        // ------------------------------------------------------------------
        // Friend Match Invite
        // ------------------------------------------------------------------

        public static event Action<string, string> OnMatchInviteReceived; // (uid, displayName)

        public static bool InviteToMatch(string friendUid)
        {
            var friend = _friends.FirstOrDefault(f => f.uid == friendUid && f.status == FriendStatus.Accepted);
            if (friend == null || !friend.isOnline) return false;

            // Create friend match room via CloudFunctionClient
            Network.CloudFunctionClient.Instance?.CreateRoom(roomId =>
            {
                Debug.Log($"[FriendSystem] Room {roomId} created for friend invite to {friend.displayName}");
                // Send push notification to friend with deep link
#if FIREBASE_ENABLED
                var myUid = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
                if (!string.IsNullOrEmpty(myUid))
                {
                    FirebaseFirestore.DefaultInstance
                        .Collection("users").Document(friendUid)
                        .Collection("invites").Document()
                        .SetAsync(new Dictionary<string, object>
                        {
                            { "fromUid", myUid },
                            { "fromName", Data.PlayerData.DisplayName },
                            { "roomId", roomId },
                            { "createdAt", FieldValue.ServerTimestamp }
                        });
                }
#endif
            });
            Debug.Log($"[FriendSystem] Invited {friend.displayName} to match");
            return true;
        }

        public static void ReceiveMatchInvite(string fromUid, string fromName)
        {
            if (IsBlocked(fromUid)) return;
            OnMatchInviteReceived?.Invoke(fromUid, fromName);
        }

        // ------------------------------------------------------------------
        // Sync (called from network layer)
        // ------------------------------------------------------------------

        public static void SyncFromServer(List<FriendEntry> serverFriends)
        {
            _friends.Clear();
            _friends.AddRange(serverFriends);
            OnFriendsUpdated?.Invoke();
        }

        public static void UpdateOnlineStatus(string uid, bool isOnline)
        {
            var entry = _friends.FirstOrDefault(f => f.uid == uid);
            if (entry != null)
            {
                entry.isOnline = isOnline;
                OnFriendsUpdated?.Invoke();
            }
        }
    }
}
