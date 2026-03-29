using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Banganka.Core.Event
{
    /// <summary>
    /// ホーム画面イベントバナー管理 (EVENT_SYSTEM_SPEC.md §4)
    /// 横スクロール + 5秒自動切替。バナー優先度に従って表示。
    /// </summary>
    public class EventBannerController : MonoBehaviour
    {
        [SerializeField] Transform bannerContainer;

        [Tooltip("バナー自動切替間隔 (秒) — EVENT_SYSTEM_SPEC.md §4.1")]
        [SerializeField] float autoRotateInterval = 5f;

        [SerializeField] GameObject bannerItemPrefab;

        /// <summary>バナータップ時に発火。eventIdを通知。</summary>
        public event Action<string> OnEventSelected;

        readonly List<BannerEntry> _bannerEntries = new();
        int _currentIndex;
        Coroutine _autoRotateCoroutine;

        // ------------------------------------------------------------------
        // Banner Priority (EVENT_SYSTEM_SPEC.md §4.2)
        // 1: メンテナンス告知
        // 2: 進行中イベント (残り時間が短い順)
        // 3: 新シリーズ/パック告知
        // 4: バトルパスシーズン告知
        // 5: 常設コンテンツ案内
        // ------------------------------------------------------------------

        public enum BannerPriority
        {
            Maintenance = 1,
            ActiveEvent = 2,
            NewPack = 3,
            BattlePass = 4,
            Permanent = 5,
        }

        [Serializable]
        public class BannerEntry
        {
            public string eventId;
            public string title;
            public string subtitle;
            public string imageUrl;
            public BannerPriority priority;
            public float sortKey; // 優先度内のソート (残り時間等)
        }

        void OnEnable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnEventsRefreshed += RefreshBanners;
        }

        void OnDisable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnEventsRefreshed -= RefreshBanners;

            StopAutoRotate();
        }

        /// <summary>
        /// イベント一覧からバナーを生成する。
        /// </summary>
        public void RefreshBanners()
        {
            _bannerEntries.Clear();

            // アクティブイベントからバナーを生成
            if (EventManager.Instance != null)
            {
                var activeEvents = EventManager.Instance.GetActiveEvents();
                foreach (var evt in activeEvents)
                {
                    var remaining = EventManager.Instance.GetTimeRemaining(evt.eventId);

                    // EVENT_SYSTEM_SPEC.md §7.2: イベント内の優先度
                    float sortKey;
                    if (remaining.TotalHours <= 24)
                        sortKey = 0f; // 残り24時間以内 = 最優先
                    else if (DateTime.TryParse(evt.startAt, out var start) &&
                             (DateTime.UtcNow - start.ToUniversalTime()).TotalHours <= 24)
                        sortKey = 1f; // 開始から24時間以内 = 次に優先
                    else
                        sortKey = (float)remaining.TotalHours; // それ以外は残り時間順

                    _bannerEntries.Add(new BannerEntry
                    {
                        eventId = evt.eventId,
                        title = evt.title,
                        subtitle = evt.subtitle,
                        imageUrl = evt.bannerImageUrl,
                        priority = BannerPriority.ActiveEvent,
                        sortKey = sortKey,
                    });
                }
            }

            // バナー優先度順 → 同一優先度内はsortKey昇順
            _bannerEntries.Sort((a, b) =>
            {
                int cmp = a.priority.CompareTo(b.priority);
                return cmp != 0 ? cmp : a.sortKey.CompareTo(b.sortKey);
            });

            CreateBannerItems();
            _currentIndex = 0;
            ShowBanner(_currentIndex);

            // 自動切替開始
            StopAutoRotate();
            if (_bannerEntries.Count > 1)
                _autoRotateCoroutine = StartCoroutine(AutoRotateCoroutine());
        }

        /// <summary>
        /// 外部からバナーを追加する (メンテナンス告知、パック告知、バトルパス告知等)。
        /// </summary>
        public void AddBanner(BannerEntry entry)
        {
            _bannerEntries.Add(entry);
            _bannerEntries.Sort((a, b) =>
            {
                int cmp = a.priority.CompareTo(b.priority);
                return cmp != 0 ? cmp : a.sortKey.CompareTo(b.sortKey);
            });
            CreateBannerItems();
        }

        /// <summary>
        /// バナーをタップした時の処理。
        /// </summary>
        public void OnBannerTap(int index)
        {
            if (index < 0 || index >= _bannerEntries.Count) return;

            var entry = _bannerEntries[index];
            if (!string.IsNullOrEmpty(entry.eventId))
            {
                OnEventSelected?.Invoke(entry.eventId);
                Debug.Log($"[EventBanner] Banner tapped: {entry.eventId} ({entry.title})");
            }
        }

        /// <summary>
        /// 現在表示中のバナーをタップした時の処理。
        /// </summary>
        public void OnCurrentBannerTap()
        {
            OnBannerTap(_currentIndex);
        }

        /// <summary>
        /// 次のバナーに切り替える。
        /// </summary>
        public void ShowNext()
        {
            if (_bannerEntries.Count == 0) return;
            _currentIndex = (_currentIndex + 1) % _bannerEntries.Count;
            ShowBanner(_currentIndex);
        }

        /// <summary>
        /// 前のバナーに切り替える。
        /// </summary>
        public void ShowPrevious()
        {
            if (_bannerEntries.Count == 0) return;
            _currentIndex = (_currentIndex - 1 + _bannerEntries.Count) % _bannerEntries.Count;
            ShowBanner(_currentIndex);
        }

        // ------------------------------------------------------------------
        // Private
        // ------------------------------------------------------------------

        void CreateBannerItems()
        {
            if (bannerContainer == null) return;

            // 既存バナーをクリア
            for (int i = bannerContainer.childCount - 1; i >= 0; i--)
                Destroy(bannerContainer.GetChild(i).gameObject);

            // バナーアイテムを生成
            foreach (var entry in _bannerEntries)
            {
                if (bannerItemPrefab == null)
                {
                    // プレハブがない場合は空のGameObjectで代用 (開発中)
                    var go = new GameObject($"Banner_{entry.eventId ?? entry.title}");
                    go.transform.SetParent(bannerContainer, false);
                }
                else
                {
                    var go = Instantiate(bannerItemPrefab, bannerContainer);
                    go.name = $"Banner_{entry.eventId ?? entry.title}";

                    // バナーテキスト設定
                    var titleTmp = go.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (titleTmp != null)
                        titleTmp.text = !string.IsNullOrEmpty(entry.subtitle)
                            ? $"{entry.title}\n<size=70%>{entry.subtitle}"
                            : entry.title;
                }
            }
        }

        void ShowBanner(int index)
        {
            if (bannerContainer == null) return;

            for (int i = 0; i < bannerContainer.childCount; i++)
            {
                bannerContainer.GetChild(i).gameObject.SetActive(i == index);
            }
        }

        IEnumerator AutoRotateCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(autoRotateInterval);
                ShowNext();
            }
        }

        void StopAutoRotate()
        {
            if (_autoRotateCoroutine != null)
            {
                StopCoroutine(_autoRotateCoroutine);
                _autoRotateCoroutine = null;
            }
        }
    }
}
