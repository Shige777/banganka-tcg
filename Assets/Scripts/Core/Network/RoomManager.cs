using System;
using UnityEngine;

namespace Banganka.Core.Network
{
    public enum RoomState
    {
        None,
        Creating,
        WaitingForOpponent,
        Joining,
        Connected,
        MatchStarting,
        Error
    }

    public class RoomManager : MonoBehaviour
    {
        public static RoomManager Instance { get; private set; }

        public RoomState State { get; private set; } = RoomState.None;
        public string RoomId { get; private set; } = "";
        public string ErrorMessage { get; private set; } = "";
        public bool IsHost { get; private set; }

        public event Action<RoomState> OnStateChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void CreateRoom()
        {
            if (State == RoomState.Creating || State == RoomState.WaitingForOpponent) return;

            State = RoomState.Creating;
            IsHost = true;
            OnStateChanged?.Invoke(State);

            // Generate a 6-character room ID
            RoomId = GenerateRoomId();
            State = RoomState.WaitingForOpponent;
            OnStateChanged?.Invoke(State);

            Debug.Log($"[RoomManager] Room created: {RoomId}");
        }

        public void JoinRoom(string roomId)
        {
            if (string.IsNullOrEmpty(roomId) || roomId.Length != 6)
            {
                SetError("ルームIDは6文字で入力してください");
                return;
            }

            State = RoomState.Joining;
            IsHost = false;
            RoomId = roomId.ToUpper();
            OnStateChanged?.Invoke(State);

            // Simulate connection (in production, this would be real networking)
            Invoke(nameof(SimulateJoinSuccess), 1.0f);

            Debug.Log($"[RoomManager] Joining room: {RoomId}");
        }

        public void StartMatch()
        {
            if (State != RoomState.Connected) return;
            State = RoomState.MatchStarting;
            OnStateChanged?.Invoke(State);
            Debug.Log("[RoomManager] Match starting");
        }

        public void LeaveRoom()
        {
            State = RoomState.None;
            RoomId = "";
            ErrorMessage = "";
            IsHost = false;
            OnStateChanged?.Invoke(State);
            Debug.Log("[RoomManager] Left room");
        }

        void SimulateJoinSuccess()
        {
            State = RoomState.Connected;
            OnStateChanged?.Invoke(State);
            Debug.Log("[RoomManager] Connected to room");
        }

        public void SimulateOpponentJoin()
        {
            if (State != RoomState.WaitingForOpponent) return;
            State = RoomState.Connected;
            OnStateChanged?.Invoke(State);
            Debug.Log("[RoomManager] Opponent joined");
        }

        void SetError(string msg)
        {
            ErrorMessage = msg;
            State = RoomState.Error;
            OnStateChanged?.Invoke(State);
        }

        static string GenerateRoomId()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var result = new char[6];
            for (int i = 0; i < 6; i++)
                result[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
            return new string(result);
        }
    }
}
