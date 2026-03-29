using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Banganka.Core.Config;
using Banganka.Core.Data;
using Banganka.Core.Economy;

namespace Banganka.Core.Game
{
    public enum DraftPhase
    {
        LeaderSelect,
        CardSelect,
        DeckReview,
        Battling,
        Complete
    }

    public enum DraftCurrencyType
    {
        Gold,
        Premium
    }

    /// <summary>
    /// ドラフトモード管理 (GAME_DESIGN.md §15)
    /// 参加費300G or 150願晶、30ラウンドのカード選択 → 対戦(最大5勝 or 2敗)
    /// </summary>
    public class DraftModeManager : MonoBehaviour
    {
        // --- Constants ---
        const int EntryFeeGold = 300;
        const int EntryFeePremium = 150;
        const int TotalSelectionRounds = 30;
        const int ChoicesPerRound = 3;
        const int LeaderChoiceCount = 3;
        const int AutoFillCount = 4; // 34 - 30 = 4
        const int MaxWins = 5;
        const int MaxLosses = 2;
        const int EarlyRoundThreshold = 10;

        // --- Reward Table (§15.4) ---
        static readonly int[] RewardGold = { 50, 100, 150, 200, 300, 500 };
        static readonly int[] RewardPackTickets = { 0, 0, 0, 1, 1, 2 };

        // --- Draft State ---
        DraftPhase _phase;
        LeaderData _selectedLeader;
        List<CardData> _selectedCards = new();
        List<CardData> _draftDeck = new();
        int _wins;
        int _losses;
        int _currentRound;
        CardData[] _currentChoices;
        LeaderData[] _leaderChoices;
        Dictionary<Aspect, int> _aspectCounts = new();
        Dictionary<string, int> _cardPickCounts = new();

        // --- Properties ---
        public DraftPhase Phase => _phase;
        public LeaderData SelectedLeader => _selectedLeader;
        public IReadOnlyList<CardData> SelectedCards => _selectedCards;
        public IReadOnlyList<CardData> DraftDeck => _draftDeck;
        public int Wins => _wins;
        public int Losses => _losses;
        public int CurrentRound => _currentRound;
        public CardData[] CurrentChoices => _currentChoices;
        public LeaderData[] LeaderChoices => _leaderChoices;
        public bool IsDraftActive => _phase != DraftPhase.Complete;
        public bool IsBattlePhaseOver => _wins >= MaxWins || _losses >= MaxLosses;

        // --- Events ---
        public event Action<DraftPhase> OnPhaseChanged;
        public event Action<LeaderData[]> OnLeaderChoicesPresented;
        public event Action<CardData[]> OnCardChoicesPresented;
        public event Action<LeaderData> OnLeaderSelected;
        public event Action<CardData> OnCardSelected;
        public event Action<int> OnRoundAdvanced;
        public event Action<List<CardData>> OnDeckBuilt;
        public event Action<int, int> OnMatchResultRecorded; // wins, losses
        public event Action<int, int> OnRewardsCalculated;   // gold, packTickets

        // --- Singleton ---
        static DraftModeManager _instance;
        public static DraftModeManager Instance => _instance;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        /// <summary>
        /// ドラフト開始: 参加費を徴収し、願主選択フェーズへ移行
        /// </summary>
        public bool StartDraft(DraftCurrencyType currencyType)
        {
            bool paid;
            if (currencyType == DraftCurrencyType.Gold)
                paid = CurrencyManager.Spend(EntryFeeGold);
            else
                paid = CurrencyManager.Spend(0, EntryFeePremium);

            if (!paid)
            {
                Debug.LogWarning("[DraftMode] 参加費が不足しています");
                return false;
            }

            // 状態リセット
            _phase = DraftPhase.LeaderSelect;
            _selectedLeader = null;
            _selectedCards.Clear();
            _draftDeck.Clear();
            _wins = 0;
            _losses = 0;
            _currentRound = 0;
            _currentChoices = null;
            _leaderChoices = null;
            _aspectCounts.Clear();
            _cardPickCounts.Clear();

            OnPhaseChanged?.Invoke(_phase);
            PresentLeaderChoices();
            return true;
        }

        /// <summary>
        /// 願主候補3体を提示
        /// </summary>
        public void PresentLeaderChoices()
        {
            var allLeaders = CardDatabase.AllLeaders.Values.ToList();
            Shuffle(allLeaders);
            _leaderChoices = allLeaders.Take(LeaderChoiceCount).ToArray();
            OnLeaderChoicesPresented?.Invoke(_leaderChoices);
        }

        /// <summary>
        /// 願主を選択 (index: 0-2)
        /// </summary>
        public bool SelectLeader(int index)
        {
            if (_phase != DraftPhase.LeaderSelect) return false;
            if (index < 0 || index >= _leaderChoices.Length) return false;

            _selectedLeader = _leaderChoices[index];
            OnLeaderSelected?.Invoke(_selectedLeader);

            _phase = DraftPhase.CardSelect;
            _currentRound = 1;
            OnPhaseChanged?.Invoke(_phase);
            OnRoundAdvanced?.Invoke(_currentRound);
            PresentCardChoices();
            return true;
        }

        /// <summary>
        /// カード候補3枚を提示 (§15.3 アルゴリズム準拠)
        /// - 各選択肢に最低1枚R以上確定
        /// - 願相バランス調整
        /// - 同名カード3枚制限
        /// </summary>
        public void PresentCardChoices()
        {
            if (_phase != DraftPhase.CardSelect) return;

            var pool = GetDraftPool();
            _currentChoices = new CardData[ChoicesPerRound];

            // 1枠目: R以上確定
            var rarePool = pool.Where(c => IsRareOrAbove(c.rarity) && CanOffer(c)).ToList();
            if (rarePool.Count > 0)
            {
                Shuffle(rarePool);
                _currentChoices[0] = rarePool[0];
            }
            else
            {
                // R以上が枯渇した場合はプール全体からフォールバック
                var fallback = pool.Where(c => CanOffer(c)).ToList();
                Shuffle(fallback);
                _currentChoices[0] = fallback[0];
            }

            // 2-3枠目: 願相バランスを考慮して選出
            var remaining = pool.Where(c => CanOffer(c) && c.id != _currentChoices[0].id).ToList();
            remaining = ApplyAspectBias(remaining);
            Shuffle(remaining);

            int filled = 1;
            foreach (var card in remaining)
            {
                if (filled >= ChoicesPerRound) break;

                // 同一カードが選択肢内に重複しないようにする
                bool duplicate = false;
                for (int i = 0; i < filled; i++)
                {
                    if (_currentChoices[i].id == card.id)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate) continue;

                _currentChoices[filled] = card;
                filled++;
            }

            OnCardChoicesPresented?.Invoke(_currentChoices);
        }

        /// <summary>
        /// カードを選択 (index: 0-2)
        /// </summary>
        public bool SelectCard(int index)
        {
            if (_phase != DraftPhase.CardSelect) return false;
            if (_currentChoices == null || index < 0 || index >= _currentChoices.Length) return false;

            var card = _currentChoices[index];
            _selectedCards.Add(card);

            // 願相カウント追跡
            if (!_aspectCounts.ContainsKey(card.aspect))
                _aspectCounts[card.aspect] = 0;
            _aspectCounts[card.aspect]++;

            // 同名カード回数追跡
            if (!_cardPickCounts.ContainsKey(card.id))
                _cardPickCounts[card.id] = 0;
            _cardPickCounts[card.id]++;

            OnCardSelected?.Invoke(card);

            _currentRound++;
            if (_currentRound > TotalSelectionRounds)
            {
                // 30ラウンド完了 → 自動補完 → デッキレビューへ
                AutoCompleteRemainingCards();
                BuildDraftDeck();
                _phase = DraftPhase.DeckReview;
                OnPhaseChanged?.Invoke(_phase);
            }
            else
            {
                OnRoundAdvanced?.Invoke(_currentRound);
                PresentCardChoices();
            }

            return true;
        }

        /// <summary>
        /// 残り4枚を願相バランスに基づくバニラ顕現で自動補完 (§15.2)
        /// </summary>
        void AutoCompleteRemainingCards()
        {
            var pool = GetDraftPool()
                .Where(c => c.type == CardType.Manifest && IsVanilla(c))
                .ToList();

            // 不足している願相を優先的に補完
            var targetAspects = GetUnderrepresentedAspects();
            int filled = 0;

            foreach (var aspect in targetAspects)
            {
                if (filled >= AutoFillCount) break;

                var candidates = pool
                    .Where(c => c.aspect == aspect && CanAddToDeck(c))
                    .ToList();

                if (candidates.Count > 0)
                {
                    Shuffle(candidates);
                    _selectedCards.Add(candidates[0]);
                    TrackCardPick(candidates[0]);
                    filled++;
                }
            }

            // それでも足りなければ残りプールからランダム補完
            if (filled < AutoFillCount)
            {
                var remaining = pool.Where(c => CanAddToDeck(c)).ToList();
                Shuffle(remaining);
                foreach (var card in remaining)
                {
                    if (filled >= AutoFillCount) break;
                    _selectedCards.Add(card);
                    TrackCardPick(card);
                    filled++;
                }
            }
        }

        /// <summary>
        /// 34枚のドラフトデッキを構築
        /// </summary>
        void BuildDraftDeck()
        {
            _draftDeck.Clear();
            _draftDeck.AddRange(_selectedCards);
            OnDeckBuilt?.Invoke(_draftDeck);

            if (_draftDeck.Count != BalanceConfig.DeckSize)
            {
                Debug.LogWarning($"[DraftMode] デッキ枚数不正: {_draftDeck.Count} (期待値: {BalanceConfig.DeckSize})");
            }
        }

        /// <summary>
        /// デッキレビュー完了 → 対戦フェーズへ移行
        /// </summary>
        public void ConfirmDeckAndStartBattles()
        {
            if (_phase != DraftPhase.DeckReview) return;

            _phase = DraftPhase.Battling;
            OnPhaseChanged?.Invoke(_phase);
        }

        /// <summary>
        /// 対戦結果を記録 (ランク変動なし: §15.5)
        /// </summary>
        public void RecordMatchResult(bool won)
        {
            if (_phase != DraftPhase.Battling) return;

            if (won)
                _wins++;
            else
                _losses++;

            OnMatchResultRecorded?.Invoke(_wins, _losses);

            if (IsBattlePhaseOver)
            {
                _phase = DraftPhase.Complete;
                OnPhaseChanged?.Invoke(_phase);
                CalculateRewards();
            }
        }

        /// <summary>
        /// 勝利数に応じた報酬を算出・付与 (§15.4)
        /// </summary>
        public (int gold, int packTickets) CalculateRewards()
        {
            int goldReward = RewardGold[Mathf.Clamp(_wins, 0, MaxWins)];
            int ticketReward = RewardPackTickets[Mathf.Clamp(_wins, 0, MaxWins)];

            CurrencyManager.AddGold(goldReward);
            if (ticketReward > 0)
            {
                PlayerData.Instance.packTickets += ticketReward;
                PlayerData.Save();
            }

            OnRewardsCalculated?.Invoke(goldReward, ticketReward);
            return (goldReward, ticketReward);
        }

        /// <summary>
        /// ドラフトプールを取得: 界律を除外した全カード (§15.5)
        /// </summary>
        public List<CardData> GetDraftPool()
        {
            return CardDatabase.AllCards.Values
                .Where(c => c.type != CardType.Algorithm)
                .ToList();
        }

        // --- Internal Helpers ---

        /// <summary>
        /// 提示可能か判定: 同名3枚以上ピック済みのカードは提示しない
        /// </summary>
        bool CanOffer(CardData card)
        {
            if (_cardPickCounts.TryGetValue(card.id, out int count))
                return count < BalanceConfig.SameNameLimit;
            return true;
        }

        /// <summary>
        /// デッキに追加可能か判定 (自動補完用)
        /// </summary>
        bool CanAddToDeck(CardData card)
        {
            if (_cardPickCounts.TryGetValue(card.id, out int count))
                return count < BalanceConfig.SameNameLimit;
            return true;
        }

        /// <summary>
        /// R以上のレアリティか判定
        /// </summary>
        static bool IsRareOrAbove(string rarity)
        {
            return rarity == "R" || rarity == "SR" || rarity == "SSR";
        }

        /// <summary>
        /// バニラ顕現か判定 (キーワード・特殊効果なし)
        /// </summary>
        static bool IsVanilla(CardData card)
        {
            return card.type == CardType.Manifest
                && (card.keywords == null || card.keywords.Length == 0)
                && string.IsNullOrEmpty(card.effectKey);
        }

        /// <summary>
        /// 願相バイアスを適用: ラウンドに応じて多様性 or 傾向追従を調整 (§15.3)
        /// 序盤(1-10): 多様な願相を優先
        /// 終盤(11-30): プレイヤーの選択傾向に沿った願相を優先
        /// </summary>
        List<CardData> ApplyAspectBias(List<CardData> candidates)
        {
            if (_currentRound <= EarlyRoundThreshold)
            {
                // 序盤: まだ選ばれていない願相を優先
                var underrepresented = GetUnderrepresentedAspects();
                return candidates
                    .OrderByDescending(c => underrepresented.Contains(c.aspect) ? 1 : 0)
                    .ThenBy(_ => UnityEngine.Random.value)
                    .ToList();
            }
            else
            {
                // 終盤: プレイヤーの主要願相に寄せる
                var dominant = GetDominantAspects();
                return candidates
                    .OrderByDescending(c => dominant.Contains(c.aspect) ? 1 : 0)
                    .ThenBy(_ => UnityEngine.Random.value)
                    .ToList();
            }
        }

        /// <summary>
        /// 選択数が少ない願相リストを返す
        /// </summary>
        List<Aspect> GetUnderrepresentedAspects()
        {
            var allAspects = (Aspect[])Enum.GetValues(typeof(Aspect));
            return allAspects
                .OrderBy(a => _aspectCounts.TryGetValue(a, out int c) ? c : 0)
                .Take(3)
                .ToList();
        }

        /// <summary>
        /// 最も多く選択されている願相リストを返す
        /// </summary>
        List<Aspect> GetDominantAspects()
        {
            return _aspectCounts
                .OrderByDescending(kv => kv.Value)
                .Take(2)
                .Select(kv => kv.Key)
                .ToList();
        }

        void TrackCardPick(CardData card)
        {
            if (!_aspectCounts.ContainsKey(card.aspect))
                _aspectCounts[card.aspect] = 0;
            _aspectCounts[card.aspect]++;

            if (!_cardPickCounts.ContainsKey(card.id))
                _cardPickCounts[card.id] = 0;
            _cardPickCounts[card.id]++;
        }

        /// <summary>
        /// Fisher-Yatesシャッフル
        /// </summary>
        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// 現在のドラフト状態をDeckDataとして出力 (対戦用)
        /// </summary>
        public DeckData ToDeckData()
        {
            if (_selectedLeader == null || _draftDeck.Count == 0) return null;

            return new DeckData
            {
                deckId = $"draft_{DateTime.UtcNow:yyyyMMddHHmmss}",
                name = $"ドラフト ({_selectedLeader.leaderName})",
                leaderId = _selectedLeader.id,
                cardIds = _draftDeck.Select(c => c.id).ToList(),
                isPreset = false
            };
        }

        /// <summary>
        /// 切断復帰用: 現在の勝敗状態を保持しているか確認 (§15.5)
        /// </summary>
        public bool HasActiveSession => _phase == DraftPhase.Battling && !IsBattlePhaseOver;
    }
}
