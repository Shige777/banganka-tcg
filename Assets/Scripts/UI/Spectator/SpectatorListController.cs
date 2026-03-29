using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Network;
using Banganka.Audio;

namespace Banganka.UI.Spectator
{
    /// <summary>
    /// 観戦モードUI。観戦可能試合一覧 + 観戦中表示。
    /// ホーム画面から起動。
    /// </summary>
    public class SpectatorListController : MonoBehaviour
    {
        Canvas _canvas;
        GameObject _root;
        GameObject _listPanel;
        ScrollRect _scrollRect;
        TextMeshProUGUI _emptyText;
        TextMeshProUGUI _spectateStatusText;
        Button _refreshBtn;
        Button _stopBtn;
        readonly List<GameObject> _listItems = new();

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Show()
        {
            EnsureUI();
            _root.SetActive(true);
            ShowListView();
            RequestRefresh();
            SoundManager.Instance?.PlayUISE("se_screen_open");
        }

        public void Hide()
        {
            if (SpectatorService.Instance != null && SpectatorService.Instance.IsSpectating)
                SpectatorService.Instance.StopSpectating();
            if (_root != null) _root.SetActive(false);
        }

        void OnDestroy()
        {
            if (SpectatorService.Instance != null)
            {
                SpectatorService.Instance.OnMatchListUpdated -= OnMatchListReceived;
                SpectatorService.Instance.OnStateUpdated -= OnSpectateStateUpdated;
                SpectatorService.Instance.OnMatchEnded -= OnSpectateMatchEnded;
            }
            if (_root != null) Destroy(_root);
        }

        // ================================================================
        // UI Construction
        // ================================================================

        void EnsureUI()
        {
            if (_root != null) return;

            if (_canvas == null)
            {
                var cObj = new GameObject("SpectatorCanvas");
                cObj.transform.SetParent(transform, false);
                _canvas = cObj.AddComponent<Canvas>();
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = 88;
                cObj.AddComponent<GraphicRaycaster>();
                var scaler = cObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
            }

            _root = new GameObject("SpectatorRoot");
            _root.transform.SetParent(_canvas.transform, false);
            var rootRt = _root.AddComponent<RectTransform>();
            StretchFull(rootRt);
            _root.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.10f, 0.96f);

            // Header
            var header = CreateChild(_root.transform, "Header");
            SetAnchored(header.GetComponent<RectTransform>(), 0, 0.92f, 1, 1);
            header.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f);
            var titleTmp = header.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "観戦";
            titleTmp.fontSize = 32;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Center;

            // Close
            var closeObj = CreateChild(_root.transform, "Close");
            SetAnchored(closeObj.GetComponent<RectTransform>(), 0.88f, 0.93f, 0.98f, 0.99f);
            closeObj.AddComponent<Image>().color = new Color(0.3f, 0.15f, 0.15f);
            closeObj.AddComponent<Button>().onClick.AddListener(Hide);
            var closeTmp = closeObj.AddComponent<TextMeshProUGUI>();
            closeTmp.text = "X";
            closeTmp.fontSize = 22;
            closeTmp.alignment = TextAlignmentOptions.Center;
            closeTmp.color = Color.white;

            // Refresh button
            var refreshObj = CreateChild(_root.transform, "Refresh");
            SetAnchored(refreshObj.GetComponent<RectTransform>(), 0.02f, 0.93f, 0.15f, 0.99f);
            refreshObj.AddComponent<Image>().color = new Color(0.2f, 0.35f, 0.55f);
            _refreshBtn = refreshObj.AddComponent<Button>();
            _refreshBtn.onClick.AddListener(RequestRefresh);
            var refTmp = refreshObj.AddComponent<TextMeshProUGUI>();
            refTmp.text = "更新";
            refTmp.fontSize = 18;
            refTmp.alignment = TextAlignmentOptions.Center;
            refTmp.color = Color.white;

            // Scroll list
            var scrollObj = CreateChild(_root.transform, "Scroll");
            SetAnchored(scrollObj.GetComponent<RectTransform>(), 0.02f, 0.02f, 0.98f, 0.91f);
            _scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            scrollObj.AddComponent<Mask>().showMaskGraphic = false;

            _listPanel = new GameObject("Content");
            _listPanel.transform.SetParent(scrollObj.transform, false);
            var contentRt = _listPanel.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = Vector2.one;
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.sizeDelta = Vector2.zero;

            var vlg = _listPanel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(8, 8, 8, 8);

            _listPanel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scrollRect.content = contentRt;
            _scrollRect.vertical = true;
            _scrollRect.horizontal = false;

            // Empty text
            var emptyObj = CreateChild(scrollObj.transform, "Empty");
            SetAnchored(emptyObj.GetComponent<RectTransform>(), 0.1f, 0.35f, 0.9f, 0.65f);
            _emptyText = emptyObj.AddComponent<TextMeshProUGUI>();
            _emptyText.text = "現在観戦可能な試合はありません";
            _emptyText.fontSize = 20;
            _emptyText.color = new Color(0.5f, 0.5f, 0.55f);
            _emptyText.alignment = TextAlignmentOptions.Center;

            // Spectate status (shown during spectating)
            var statusObj = CreateChild(_root.transform, "Status");
            SetAnchored(statusObj.GetComponent<RectTransform>(), 0.05f, 0.4f, 0.95f, 0.6f);
            _spectateStatusText = statusObj.AddComponent<TextMeshProUGUI>();
            _spectateStatusText.fontSize = 24;
            _spectateStatusText.color = Color.white;
            _spectateStatusText.alignment = TextAlignmentOptions.Center;
            statusObj.SetActive(false);

            // Stop spectating button
            var stopObj = CreateChild(_root.transform, "Stop");
            SetAnchored(stopObj.GetComponent<RectTransform>(), 0.3f, 0.25f, 0.7f, 0.35f);
            stopObj.AddComponent<Image>().color = new Color(0.4f, 0.15f, 0.15f);
            _stopBtn = stopObj.AddComponent<Button>();
            _stopBtn.onClick.AddListener(OnStopSpectating);
            var stopTmp = stopObj.AddComponent<TextMeshProUGUI>();
            stopTmp.text = "観戦を終了";
            stopTmp.fontSize = 20;
            stopTmp.alignment = TextAlignmentOptions.Center;
            stopTmp.color = Color.white;
            stopObj.SetActive(false);

            // Register events
            if (SpectatorService.Instance != null)
            {
                SpectatorService.Instance.OnMatchListUpdated += OnMatchListReceived;
                SpectatorService.Instance.OnStateUpdated += OnSpectateStateUpdated;
                SpectatorService.Instance.OnMatchEnded += OnSpectateMatchEnded;
            }
        }

        // ================================================================
        // Logic
        // ================================================================

        void ShowListView()
        {
            _scrollRect.gameObject.SetActive(true);
            _refreshBtn.gameObject.SetActive(true);
            _spectateStatusText.gameObject.SetActive(false);
            _stopBtn.gameObject.SetActive(false);
        }

        void ShowSpectateView(string matchId)
        {
            _scrollRect.gameObject.SetActive(false);
            _refreshBtn.gameObject.SetActive(false);
            _spectateStatusText.gameObject.SetActive(true);
            _spectateStatusText.text = $"試合 {matchId} を観戦中...";
            _stopBtn.gameObject.SetActive(true);
        }

        void RequestRefresh()
        {
            if (SpectatorService.Instance != null)
                SpectatorService.Instance.FetchSpectateList();
            else
                _emptyText.gameObject.SetActive(true);
        }

        void OnMatchListReceived(List<SpectateMatch> matches)
        {
            foreach (var item in _listItems)
                if (item != null) Destroy(item);
            _listItems.Clear();

            _emptyText.gameObject.SetActive(matches.Count == 0);

            foreach (var match in matches)
                CreateMatchItem(match);
        }

        void CreateMatchItem(SpectateMatch match)
        {
            var item = new GameObject("Match");
            item.transform.SetParent(_listPanel.transform, false);
            item.AddComponent<LayoutElement>().preferredHeight = 90;
            item.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f);

            // Info
            var infoObj = CreateChild(item.transform, "Info");
            SetAnchored(infoObj.GetComponent<RectTransform>(), 0.03f, 0.1f, 0.7f, 0.9f);
            var infoTmp = infoObj.AddComponent<TextMeshProUGUI>();
            infoTmp.text = $"<b>{match.player1Name}</b> (R:{match.player1Rating}) vs " +
                           $"<b>{match.player2Name}</b> (R:{match.player2Rating})\n" +
                           $"<size=14>ターン {match.currentTurn}  観戦者: {match.spectatorCount}人</size>";
            infoTmp.fontSize = 17;
            infoTmp.color = Color.white;
            infoTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Watch button
            var watchObj = CreateChild(item.transform, "Watch");
            SetAnchored(watchObj.GetComponent<RectTransform>(), 0.72f, 0.15f, 0.97f, 0.85f);
            watchObj.AddComponent<Image>().color = new Color(0.2f, 0.4f, 0.6f);
            var btn = watchObj.AddComponent<Button>();
            string id = match.matchId;
            btn.onClick.AddListener(() => OnWatchMatch(id));
            var btnTmp = watchObj.AddComponent<TextMeshProUGUI>();
            btnTmp.text = "観戦";
            btnTmp.fontSize = 18;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;

            _listItems.Add(item);
        }

        void OnWatchMatch(string matchId)
        {
            if (SpectatorService.Instance == null) return;
            SpectatorService.Instance.StartSpectating(matchId);
            ShowSpectateView(matchId);
        }

        void OnStopSpectating()
        {
            SpectatorService.Instance?.StopSpectating();
            ShowListView();
            RequestRefresh();
        }

        void OnSpectateStateUpdated(Banganka.Core.Battle.BattleState state)
        {
            if (_spectateStatusText != null && _spectateStatusText.gameObject.activeSelf)
            {
                _spectateStatusText.text = $"観戦中  ターン {state.turnTotal}\n" +
                                            $"P1 HP:{state.player1.hp}  P2 HP:{state.player2.hp}";
            }
        }

        void OnSpectateMatchEnded(string reason)
        {
            if (_spectateStatusText != null)
                _spectateStatusText.text = $"試合終了: {reason}";

            // 3秒後にリストに戻る
            Invoke(nameof(ReturnToList), 3f);
        }

        void ReturnToList()
        {
            ShowListView();
            RequestRefresh();
        }

        // ================================================================
        // Helpers
        // ================================================================

        static GameObject CreateChild(Transform parent, string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            return obj;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void SetAnchored(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
