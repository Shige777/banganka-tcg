using System;
using UnityEngine;
using Banganka.Core.Config;
using Banganka.Core.Data;

namespace Banganka.Core.Network
{
    public enum MatchmakingState
    {
        Idle,
        Searching,
        Found,
        Starting,
        Error,
    }

    /// <summary>
    /// マッチメイキングサービス。
    /// ランダムマッチ / ランクマッチ / フレンド対戦 / Bot対戦 を管理。
    /// GAME_DESIGN.md §14 マッチメイキング参照。
    /// </summary>
    public class MatchmakingService : MonoBehaviour
    {
        public static MatchmakingService Instance { get; private set; }

        public MatchmakingState State { get; private set; } = MatchmakingState.Idle;
        public float SearchTime { get; private set; }

        public event Action<MatchmakingState> OnStateChanged;
        public event Action<string> OnMatchFound; // matchId

        /// <summary>Bot提案ダイアログのコールバック。UIが設定する。</summary>
        public event Action<Action<bool>> OnBotOfferRequested;

        // ランクマッチ: マッチメイキング時間階段 (§14.2)
        const float RankedSearchInitial = 10f;    // 初期待機時間 (±2 tier)
        const float RankedSearchExpand = 30f;     // 拡大タイミング (±3 tierに拡大)
        const float RankedSearchBotOffer = 60f;   // Bot提案タイミング

        // その他
        const float CasualSearchTime = 10f;
        const float BotFallbackTime = 10f;

        string _selectedDeckId;
        bool _isRankedMatch = false;
        MatchMode _matchMode = MatchMode.Standard;
        bool _rangeExpanded;
        bool _botOffered;

        public MatchMode CurrentMatchMode => _matchMode;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>ランダムマッチ検索を開始</summary>
        public void StartSearch(string deckId, MatchMode mode = MatchMode.Standard)
        {
            _selectedDeckId = deckId;
            _isRankedMatch = false;
            _matchMode = mode;
            MatchModeConfig.CurrentMode = mode;
            ResetSearchState();
            Debug.Log($"[Matchmaking] Casual search started (mode={mode})");
        }

        /// <summary>ランクマッチ検索を開始 (§14.2)</summary>
        public void StartRankedSearch(string deckId)
        {
            // BAN中か確認 — タイムアウト3回連続敗北によるペナルティ
            if (RankingService.Instance != null)
            {
                var rank = RankingService.Instance.GetCurrentRank();
                if (rank != null && rank.streak <= -3)
                {
                    Debug.LogWarning("[Matchmaking] Player has excessive losses (penalty state). Proceeding with warning.");
                    // ペナルティ状態だが検索は許可（将来的にBAN機能追加時に拡張）
                }
            }

            _selectedDeckId = deckId;
            _isRankedMatch = true;
            ResetSearchState();
            Debug.Log("[Matchmaking] Ranked search started");
        }

        void ResetSearchState()
        {
            State = MatchmakingState.Searching;
            SearchTime = 0;
            _rangeExpanded = false;
            _botOffered = false;
            OnStateChanged?.Invoke(State);
        }

        public void CancelSearch()
        {
            State = MatchmakingState.Idle;
            SearchTime = 0;
            OnStateChanged?.Invoke(State);
        }

        public void StartBotMatch(string difficulty = "Normal")
        {
            State = MatchmakingState.Starting;
            OnStateChanged?.Invoke(State);

            CloudFunctionClient.Instance?.CreateBotMatch(difficulty, _selectedDeckId, matchId =>
            {
                State = MatchmakingState.Found;
                OnStateChanged?.Invoke(State);
                OnMatchFound?.Invoke(matchId);
            });
        }

        public void StartFriendMatch(string roomCode)
        {
            State = MatchmakingState.Starting;
            OnStateChanged?.Invoke(State);

            CloudFunctionClient.Instance?.JoinRoom(roomCode, success =>
            {
                if (success)
                {
                    State = MatchmakingState.Found;
                    OnStateChanged?.Invoke(State);
                }
                else
                {
                    State = MatchmakingState.Error;
                    OnStateChanged?.Invoke(State);
                }
            });
        }

        void Update()
        {
            if (State != MatchmakingState.Searching) return;

            SearchTime += Time.deltaTime;

            if (_isRankedMatch)
            {
                // ランク対戦のマッチメイキング (§14.2)
                if (!_botOffered && SearchTime >= RankedSearchBotOffer)
                {
                    _botOffered = true;
                    Debug.Log("[Matchmaking] Offering bot match (ranked)");

                    // Bot提案ダイアログをUIに委譲
                    if (OnBotOfferRequested != null)
                    {
                        OnBotOfferRequested.Invoke(accepted =>
                        {
                            if (accepted && State == MatchmakingState.Searching)
                                StartBotMatch("Normal");
                            // 拒否時は検索を継続
                        });
                    }
                    else
                    {
                        // UIリスナーがない場合はフォールバック
                        StartBotMatch("Normal");
                    }
                }
                else if (!_rangeExpanded && SearchTime >= RankedSearchExpand)
                {
                    _rangeExpanded = true;
                    // バックエンド側にマッチング範囲拡大を通知
                    CloudFunctionClient.Instance?.ExpandMatchRange(_selectedDeckId, 3);
                    Debug.Log("[Matchmaking] Expanded search range (±3 tier)");
                }
            }
            else
            {
                // カジュアルマッチ: 自動フォールバック
                if (SearchTime >= BotFallbackTime)
                {
                    Debug.Log("[Matchmaking] Falling back to bot match (casual)");
                    StartBotMatch("Normal");
                }
            }
        }
    }
}
