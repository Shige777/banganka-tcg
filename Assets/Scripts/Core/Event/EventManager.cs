using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Banganka.Core.Event
{
    public enum EventType
    {
        RestrictedBattle, // 構築制限イベント (月1〜2回)
        Ranking,          // ランキングイベント (シーズンごと)
        Draft,            // ドラフトイベント (月1回)
        StoryEvent,       // ストーリーイベント (2〜3ヶ月に1回)
        Collab,           // コラボイベント (不定期, Phase 4+)
        Seasonal          // 季節イベント (年4〜6回)
    }

    [Serializable]
    public class EventData
    {
        public string eventId;
        public EventType type;
        public string title;
        public string subtitle;
        public string startAt;       // ISO 8601 (例: "2026-04-01T00:00:00+09:00")
        public string endAt;         // ISO 8601
        public string bannerImageUrl;
        public EventRules rules;
        public List<EventReward> rewards;

        /// <summary>
        /// 報酬受取猶予期間 (イベント終了後48時間)
        /// EVENT_SYSTEM_SPEC.md §5.2
        /// </summary>
        public const int RewardGracePeriodHours = 48;
    }

    [Serializable]
    public class EventRules
    {
        public string maxRarity;       // "C", "R", "SR", "SSR" or null
        public int deckSize;           // 通常は34 (BalanceConfig.DeckSize)
        public string[] specialRules;  // "single_aspect", "low_cost_only", "no_algorithm", "random_deck" 等
    }

    [Serializable]
    public class EventReward
    {
        public int threshold;     // 勝利数 or ポイント到達値
        public string rewardType; // "gold", "pack_ticket", "cosmetic", "premium", "craft_material"
        public int amount;
        public string itemId;     // cosmetic等のアイテムID (null可)
    }

    [Serializable]
    public class EventProgress
    {
        public int wins;
        public int points;
        public List<int> claimedThresholds = new();
    }

    /// <summary>
    /// ライブオプス・イベント管理 (EVENT_SYSTEM_SPEC.md)
    /// Remote Config から取得したイベント情報の管理、進捗記録、報酬配布を行う。
    /// MVP段階ではPlayerPrefsでローカル永続化。
    /// </summary>
    public class EventManager : MonoBehaviour
    {
        public static EventManager Instance { get; private set; }

        /// <summary>同時開催イベント上限 (EVENT_SYSTEM_SPEC.md §7.1)</summary>
        public const int MaxConcurrentEvents = 3;

        const string EventsPrefsKey = "event_data_cache";
        const string ProgressPrefsPrefix = "event_progress_";

        List<EventData> _activeEvents = new();
        readonly Dictionary<string, EventProgress> _progressCache = new();

        // Events
        public event Action OnEventsRefreshed;
        public event Action<string> OnProgressUpdated;
        public event Action<string, EventReward> OnRewardClaimed;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            LoadCachedEvents();
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Remote Config (or ローカルフォールバック) からイベント一覧を取得して更新する。
        /// EVENT_SYSTEM_SPEC.md §8: アプリ起動時fetch + 12時間ごと
        /// </summary>
        public void FetchActiveEvents(List<EventData> remoteEvents = null)
        {
            if (remoteEvents != null)
            {
                _activeEvents = remoteEvents
                    .Where(e => IsWithinEventWindow(e))
                    .Take(MaxConcurrentEvents)
                    .ToList();
            }

            // ローカルフォールバック (Remote Configが取得できない場合はキャッシュ→季節テンプレート)
            if (_activeEvents.Count == 0)
            {
                LoadCachedEvents();
            }
            if (_activeEvents.Count == 0)
            {
                _activeEvents = SeasonalEventTemplates.GetCurrentSeasonalEvents()
                    .FindAll(e => IsWithinEventWindow(e));
            }
            else
            {
                SaveEventsToCache();
            }

            OnEventsRefreshed?.Invoke();
            Debug.Log($"[EventManager] Active events refreshed: {_activeEvents.Count}件");
        }

        /// <summary>
        /// 現在有効なイベント一覧を取得する。
        /// バナー優先度順: 残り時間が短い順 (EVENT_SYSTEM_SPEC.md §4.2)
        /// </summary>
        public List<EventData> GetActiveEvents()
        {
            return _activeEvents
                .Where(e => IsEventActive(e.eventId))
                .OrderBy(e => GetTimeRemaining(e.eventId).TotalSeconds)
                .ToList();
        }

        /// <summary>
        /// イベントが現在有効かどうかを判定する。
        /// </summary>
        public bool IsEventActive(string eventId)
        {
            var evt = FindEvent(eventId);
            if (evt == null) return false;

            var now = DateTime.UtcNow;
            if (!DateTime.TryParse(evt.startAt, out var start)) return false;
            if (!DateTime.TryParse(evt.endAt, out var end)) return false;

            return now >= start.ToUniversalTime() && now <= end.ToUniversalTime();
        }

        /// <summary>
        /// イベント終了までの残り時間を返す。
        /// </summary>
        public TimeSpan GetTimeRemaining(string eventId)
        {
            var evt = FindEvent(eventId);
            if (evt == null) return TimeSpan.Zero;

            if (!DateTime.TryParse(evt.endAt, out var end)) return TimeSpan.Zero;

            var remaining = end.ToUniversalTime() - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        /// <summary>
        /// 報酬受取猶予期間 (終了後48時間) 内かどうかを判定する。
        /// EVENT_SYSTEM_SPEC.md §5.2
        /// </summary>
        public bool IsInRewardGracePeriod(string eventId)
        {
            var evt = FindEvent(eventId);
            if (evt == null) return false;

            if (!DateTime.TryParse(evt.endAt, out var end)) return false;

            var now = DateTime.UtcNow;
            var endUtc = end.ToUniversalTime();
            var graceEnd = endUtc.AddHours(EventData.RewardGracePeriodHours);

            return now > endUtc && now <= graceEnd;
        }

        /// <summary>
        /// イベント用デッキバリデーション (EVENT_SYSTEM_SPEC.md §3.2)
        /// </summary>
        /// <returns>(isValid, errorMessage)</returns>
        public (bool isValid, string errorMessage) ValidateDeckForEvent(string eventId, List<string> cardIds)
        {
            var evt = FindEvent(eventId);
            if (evt == null) return (false, "イベントが見つかりません");
            if (evt.rules == null) return (true, "");

            // デッキサイズ検証
            int requiredSize = evt.rules.deckSize > 0 ? evt.rules.deckSize : Config.BalanceConfig.DeckSize;
            if (cardIds.Count != requiredSize)
                return (false, $"デッキは{requiredSize}枚である必要があります (現在{cardIds.Count}枚)");

            // レアリティ制限検証
            if (!string.IsNullOrEmpty(evt.rules.maxRarity))
            {
                foreach (var cardId in cardIds)
                {
                    var card = Data.CardDatabase.AllCards.ContainsKey(cardId)
                        ? Data.CardDatabase.AllCards[cardId]
                        : null;
                    if (card == null) continue;

                    if (!IsRarityAllowed(card.rarity, evt.rules.maxRarity))
                        return (false, $"「{card.cardName}」はこのイベントでは使用できません (レアリティ制限: {evt.rules.maxRarity}以下)");
                }
            }

            // 特殊ルール検証
            if (evt.rules.specialRules != null)
            {
                foreach (var rule in evt.rules.specialRules)
                {
                    var result = ValidateSpecialRule(rule, cardIds);
                    if (!result.isValid) return result;
                }
            }

            return (true, "");
        }

        /// <summary>
        /// 対戦結果をイベント進捗に記録する。
        /// 複数イベントの進捗が同時に進むことを許可 (EVENT_SYSTEM_SPEC.md §7.3)
        /// </summary>
        public void RecordEventProgress(string eventId, bool isWin, int pointsEarned = 0)
        {
            if (!IsEventActive(eventId)) return;

            var progress = GetEventProgress(eventId);
            if (isWin) progress.wins++;
            progress.points += pointsEarned;

            SaveProgress(eventId, progress);
            OnProgressUpdated?.Invoke(eventId);

            Debug.Log($"[EventManager] Progress updated: {eventId} wins={progress.wins} points={progress.points}");
        }

        /// <summary>
        /// 全アクティブイベントの進捗を一括で記録する。
        /// 1回の対戦で複数イベントの進捗を進める (EVENT_SYSTEM_SPEC.md §7.3)
        /// </summary>
        public void RecordProgressForAllActiveEvents(bool isWin, int pointsEarned = 0)
        {
            foreach (var evt in _activeEvents)
            {
                if (IsEventActive(evt.eventId))
                {
                    RecordEventProgress(evt.eventId, isWin, pointsEarned);
                }
            }
        }

        /// <summary>
        /// イベント進捗を取得する。
        /// </summary>
        public EventProgress GetEventProgress(string eventId)
        {
            if (_progressCache.TryGetValue(eventId, out var cached))
                return cached;

            var progress = LoadProgress(eventId);
            _progressCache[eventId] = progress;
            return progress;
        }

        /// <summary>
        /// 報酬を受け取る (EVENT_SYSTEM_SPEC.md §5.1)
        /// threshold到達かつ未受取の場合のみ受取可能。
        /// イベント終了後48時間の猶予期間内も受取可 (§5.2)
        /// </summary>
        public EventReward ClaimReward(string eventId, int threshold)
        {
            // イベント有効 or 猶予期間内であること
            if (!IsEventActive(eventId) && !IsInRewardGracePeriod(eventId))
            {
                Debug.LogWarning($"[EventManager] Cannot claim reward: event {eventId} is not active or in grace period");
                return null;
            }

            var evt = FindEvent(eventId);
            if (evt == null) return null;

            var progress = GetEventProgress(eventId);

            // 既に受取済み
            if (progress.claimedThresholds.Contains(threshold))
            {
                Debug.LogWarning($"[EventManager] Reward already claimed: {eventId} threshold={threshold}");
                return null;
            }

            // threshold到達チェック (勝利数 or ポイントで判定)
            var reward = evt.rewards?.FirstOrDefault(r => r.threshold == threshold);
            if (reward == null) return null;

            bool thresholdReached = progress.wins >= threshold || progress.points >= threshold;
            if (!thresholdReached)
            {
                Debug.LogWarning($"[EventManager] Threshold not reached: {eventId} threshold={threshold} (wins={progress.wins}, points={progress.points})");
                return null;
            }

            // 報酬付与
            GrantReward(reward);

            // 受取済みに追加
            progress.claimedThresholds.Add(threshold);
            SaveProgress(eventId, progress);

            OnRewardClaimed?.Invoke(eventId, reward);
            Debug.Log($"[EventManager] Reward claimed: {eventId} threshold={threshold} type={reward.rewardType} amount={reward.amount}");

            return reward;
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        EventData FindEvent(string eventId)
        {
            return _activeEvents.FirstOrDefault(e => e.eventId == eventId);
        }

        /// <summary>
        /// イベント開催期間 or 報酬猶予期間内かどうかを判定する (フェッチ時フィルタ用)。
        /// </summary>
        bool IsWithinEventWindow(EventData evt)
        {
            var now = DateTime.UtcNow;
            if (!DateTime.TryParse(evt.startAt, out var start)) return false;
            if (!DateTime.TryParse(evt.endAt, out var end)) return false;

            var graceEnd = end.ToUniversalTime().AddHours(EventData.RewardGracePeriodHours);
            return now >= start.ToUniversalTime() && now <= graceEnd;
        }

        static readonly string[] RarityOrder = { "C", "R", "SR", "SSR" };

        bool IsRarityAllowed(string cardRarity, string maxRarity)
        {
            int cardIndex = Array.IndexOf(RarityOrder, cardRarity);
            int maxIndex = Array.IndexOf(RarityOrder, maxRarity);
            if (cardIndex < 0 || maxIndex < 0) return true; // 不明なレアリティは許可
            return cardIndex <= maxIndex;
        }

        (bool isValid, string errorMessage) ValidateSpecialRule(string rule, List<string> cardIds)
        {
            switch (rule)
            {
                case "single_aspect":
                    // 単願相杯: 1つの願相のカードのみ使用可
                    var aspects = new HashSet<string>();
                    foreach (var cardId in cardIds)
                    {
                        if (Data.CardDatabase.AllCards.TryGetValue(cardId, out var card))
                            aspects.Add(card.aspect.ToString());
                    }
                    if (aspects.Count > 1)
                        return (false, "このイベントでは1つの願相のカードのみ使用可能です");
                    break;

                case "low_cost_only":
                    // 低コスト戦: コスト5以下のカードのみ
                    foreach (var cardId in cardIds)
                    {
                        if (Data.CardDatabase.AllCards.TryGetValue(cardId, out var card) && card.cpCost > 5)
                            return (false, $"「{card.cardName}」はコスト{card.cpCost}のためこのイベントでは使用できません (コスト5以下限定)");
                    }
                    break;

                case "no_algorithm":
                    // 界律なし杯: 界律カード使用禁止
                    foreach (var cardId in cardIds)
                    {
                        if (Data.CardDatabase.AllCards.TryGetValue(cardId, out var card) && card.type == Data.CardType.Algorithm)
                            return (false, $"「{card.cardName}」は界律カードのためこのイベントでは使用できません");
                    }
                    break;
            }

            return (true, "");
        }

        void GrantReward(EventReward reward)
        {
            switch (reward.rewardType)
            {
                case "gold":
                    Economy.CurrencyManager.AddGold(reward.amount);
                    break;
                case "premium":
                    Economy.CurrencyManager.AddPremium(reward.amount);
                    break;
                case "pack_ticket":
                    for (int i = 0; i < reward.amount; i++)
                        Economy.PackSystem.OpenPack();
                    break;
                case "cosmetic":
                    if (!string.IsNullOrEmpty(reward.itemId))
                        Cosmetic.CosmeticSystem.Grant(reward.itemId);
                    Debug.Log($"[EventManager] Cosmetic reward granted: {reward.itemId}");
                    break;
                case "craft_material":
                    int current = PlayerPrefs.GetInt("craft_material", 0);
                    PlayerPrefs.SetInt("craft_material", current + reward.amount);
                    PlayerPrefs.Save();
                    Debug.Log($"[EventManager] Craft material reward granted: +{reward.amount} (total: {current + reward.amount})");
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Persistence (MVP: PlayerPrefs)
        // ------------------------------------------------------------------

        void SaveEventsToCache()
        {
            var wrapper = new EventListWrapper { events = _activeEvents };
            string json = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString(EventsPrefsKey, json);
            PlayerPrefs.Save();
        }

        void LoadCachedEvents()
        {
            string json = PlayerPrefs.GetString(EventsPrefsKey, "");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var wrapper = JsonUtility.FromJson<EventListWrapper>(json);
                if (wrapper?.events != null)
                {
                    _activeEvents = wrapper.events
                        .Where(e => IsWithinEventWindow(e))
                        .Take(MaxConcurrentEvents)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EventManager] Failed to load cached events: {ex.Message}");
                _activeEvents = new List<EventData>();
            }
        }

        void SaveProgress(string eventId, EventProgress progress)
        {
            _progressCache[eventId] = progress;
            string json = JsonUtility.ToJson(progress);
            PlayerPrefs.SetString($"{ProgressPrefsPrefix}{eventId}", json);
            PlayerPrefs.Save();
        }

        EventProgress LoadProgress(string eventId)
        {
            string json = PlayerPrefs.GetString($"{ProgressPrefsPrefix}{eventId}", "");
            if (string.IsNullOrEmpty(json)) return new EventProgress();

            try
            {
                return JsonUtility.FromJson<EventProgress>(json) ?? new EventProgress();
            }
            catch
            {
                return new EventProgress();
            }
        }

        [Serializable]
        class EventListWrapper
        {
            public List<EventData> events;
        }
    }
}
