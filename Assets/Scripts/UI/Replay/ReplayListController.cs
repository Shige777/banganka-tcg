using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Config;
using Banganka.Core.Replay;
using Banganka.Audio;

namespace Banganka.UI.Replay
{
    /// <summary>
    /// リプレイ一覧・視聴UI。
    /// ホーム画面から呼び出し、保存済みリプレイの一覧表示・再生・お気に入り・削除。
    /// </summary>
    public class ReplayListController : MonoBehaviour
    {
        Canvas _canvas;
        GameObject _root;
        GameObject _listPanel;
        GameObject _playerPanel;
        ScrollRect _scrollRect;
        TextMeshProUGUI _emptyText;
        readonly List<GameObject> _listItems = new();

        // 再生UI
        ReplayPlayer _player;
        TextMeshProUGUI _turnText;
        TextMeshProUGUI _speedText;
        TextMeshProUGUI _stateText;
        Button _playPauseBtn;
        Button _prevTurnBtn;
        Button _nextTurnBtn;
        Button _speedBtn;
        Button _stopBtn;
        Slider _seekSlider;
        bool _isSeeking;

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Show()
        {
            EnsureCanvas();
            BuildListUI();
            RefreshList();
            _root.SetActive(true);
            SoundManager.Instance?.PlayUISE("se_screen_open");
        }

        /// <summary>指定リプレイを即座に再生する（結果画面から呼ばれる）</summary>
        public void ShowAndPlay(string replayId)
        {
            EnsureCanvas();
            BuildListUI();
            _root.SetActive(true);
            OnPlayReplay(replayId);
        }

        public void Hide()
        {
            StopPlayback();
            if (_root != null) _root.SetActive(false);
        }

        void OnDestroy()
        {
            StopPlayback();
            if (_root != null) Destroy(_root);
        }

        // ================================================================
        // List UI
        // ================================================================

        void EnsureCanvas()
        {
            if (_canvas != null) return;
            var obj = new GameObject("ReplayCanvas");
            obj.transform.SetParent(transform, false);
            _canvas = obj.AddComponent<Canvas>();
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 90;
            obj.AddComponent<GraphicRaycaster>();
            var scaler = obj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
        }

        void BuildListUI()
        {
            if (_root != null) return;

            _root = new GameObject("ReplayRoot");
            _root.transform.SetParent(_canvas.transform, false);
            var rootRt = _root.AddComponent<RectTransform>();
            StretchFull(rootRt);

            // Backdrop
            var backdrop = CreateChild(_root.transform, "Backdrop");
            var bdImg = backdrop.AddComponent<Image>();
            bdImg.color = new Color(0.06f, 0.06f, 0.10f, 0.95f);
            StretchFull(backdrop.GetComponent<RectTransform>());

            // Header
            var header = CreateChild(_root.transform, "Header");
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0, 0.9f);
            headerRt.anchorMax = Vector2.one;
            headerRt.offsetMin = Vector2.zero;
            headerRt.offsetMax = Vector2.zero;

            var titleTmp = header.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "リプレイ";
            titleTmp.fontSize = 36;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Center;

            // Close button
            var closeObj = CreateChild(_root.transform, "Close");
            var closeRt = closeObj.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.85f, 0.92f);
            closeRt.anchorMax = new Vector2(0.98f, 0.98f);
            closeRt.offsetMin = Vector2.zero;
            closeRt.offsetMax = Vector2.zero;
            var closeBg = closeObj.AddComponent<Image>();
            closeBg.color = new Color(0.3f, 0.15f, 0.15f);
            var closeBtn = closeObj.AddComponent<Button>();
            closeBtn.onClick.AddListener(Hide);
            var closeTmp = closeObj.AddComponent<TextMeshProUGUI>();
            closeTmp.text = "X";
            closeTmp.fontSize = 24;
            closeTmp.alignment = TextAlignmentOptions.Center;
            closeTmp.color = Color.white;

            // Scroll area
            var scrollObj = CreateChild(_root.transform, "Scroll");
            var scrollRt = scrollObj.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.02f, 0.02f);
            scrollRt.anchorMax = new Vector2(0.98f, 0.89f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;
            _scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f); // viewport mask
            scrollObj.AddComponent<Mask>().showMaskGraphic = false;

            // Content
            _listPanel = new GameObject("Content");
            _listPanel.transform.SetParent(scrollObj.transform, false);
            var contentRt = _listPanel.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = Vector2.one;
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.sizeDelta = new Vector2(0, 0);

            var vlg = _listPanel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(8, 8, 8, 8);

            var csf = _listPanel.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect.content = contentRt;
            _scrollRect.vertical = true;
            _scrollRect.horizontal = false;

            // Empty text
            var emptyObj = CreateChild(scrollObj.transform, "Empty");
            var emptyRt = emptyObj.GetComponent<RectTransform>();
            emptyRt.anchorMin = new Vector2(0.1f, 0.4f);
            emptyRt.anchorMax = new Vector2(0.9f, 0.6f);
            emptyRt.offsetMin = Vector2.zero;
            emptyRt.offsetMax = Vector2.zero;
            _emptyText = emptyObj.AddComponent<TextMeshProUGUI>();
            _emptyText.text = "リプレイがありません";
            _emptyText.fontSize = 22;
            _emptyText.color = new Color(0.5f, 0.5f, 0.55f);
            _emptyText.alignment = TextAlignmentOptions.Center;

            // Player panel (initially hidden)
            BuildPlayerPanel();
        }

        void RefreshList()
        {
            foreach (var item in _listItems)
                if (item != null) Destroy(item);
            _listItems.Clear();

            var replays = ReplayStorage.GetReplayList();
            _emptyText.gameObject.SetActive(replays.Count == 0);

            foreach (var summary in replays)
                CreateReplayItem(summary);
        }

        void CreateReplayItem(ReplaySummary summary)
        {
            var item = new GameObject("ReplayItem");
            item.transform.SetParent(_listPanel.transform, false);
            var le = item.AddComponent<LayoutElement>();
            le.preferredHeight = 100;

            var bg = item.AddComponent<Image>();
            bg.color = summary.isFavorite
                ? new Color(0.18f, 0.16f, 0.10f)
                : new Color(0.12f, 0.12f, 0.16f);

            // Match info
            var infoObj = CreateChild(item.transform, "Info");
            var infoRt = infoObj.GetComponent<RectTransform>();
            infoRt.anchorMin = new Vector2(0.02f, 0.1f);
            infoRt.anchorMax = new Vector2(0.55f, 0.9f);
            infoRt.offsetMin = Vector2.zero;
            infoRt.offsetMax = Vector2.zero;

            var infoTmp = infoObj.AddComponent<TextMeshProUGUI>();
            string winMark = summary.winner == 1 ? "WIN" : summary.winner == 2 ? "LOSE" : "DRAW";
            string dateStr = "";
            if (DateTime.TryParse(summary.createdAt, out DateTime dt))
                dateStr = dt.ToLocalTime().ToString("MM/dd HH:mm");
            infoTmp.text = $"<b>{winMark}</b>  {summary.player1Name} vs {summary.player2Name}\n" +
                           $"<size=14>{summary.totalTurns}ターン  {summary.reason}  {dateStr}</size>";
            infoTmp.fontSize = 18;
            infoTmp.color = Color.white;
            infoTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Play button
            var playObj = CreateChild(item.transform, "Play");
            var playRt = playObj.GetComponent<RectTransform>();
            playRt.anchorMin = new Vector2(0.56f, 0.15f);
            playRt.anchorMax = new Vector2(0.72f, 0.85f);
            playRt.offsetMin = Vector2.zero;
            playRt.offsetMax = Vector2.zero;
            playObj.AddComponent<Image>().color = new Color(0.2f, 0.4f, 0.7f);
            var playBtn = playObj.AddComponent<Button>();
            string replayId = summary.replayId;
            playBtn.onClick.AddListener(() => OnPlayReplay(replayId));
            var playTmp = playObj.AddComponent<TextMeshProUGUI>();
            playTmp.text = "再生";
            playTmp.fontSize = 16;
            playTmp.alignment = TextAlignmentOptions.Center;
            playTmp.color = Color.white;

            // Favorite button
            var favObj = CreateChild(item.transform, "Fav");
            var favRt = favObj.GetComponent<RectTransform>();
            favRt.anchorMin = new Vector2(0.74f, 0.15f);
            favRt.anchorMax = new Vector2(0.86f, 0.85f);
            favRt.offsetMin = Vector2.zero;
            favRt.offsetMax = Vector2.zero;
            favObj.AddComponent<Image>().color = summary.isFavorite
                ? new Color(0.8f, 0.65f, 0.2f)
                : new Color(0.25f, 0.25f, 0.3f);
            var favBtn = favObj.AddComponent<Button>();
            favBtn.onClick.AddListener(() =>
            {
                ReplayStorage.ToggleFavorite(replayId);
                RefreshList();
            });
            var favTmp = favObj.AddComponent<TextMeshProUGUI>();
            favTmp.text = summary.isFavorite ? "★" : "☆";
            favTmp.fontSize = 22;
            favTmp.alignment = TextAlignmentOptions.Center;
            favTmp.color = Color.white;

            // Delete button
            var delObj = CreateChild(item.transform, "Del");
            var delRt = delObj.GetComponent<RectTransform>();
            delRt.anchorMin = new Vector2(0.88f, 0.15f);
            delRt.anchorMax = new Vector2(0.98f, 0.85f);
            delRt.offsetMin = Vector2.zero;
            delRt.offsetMax = Vector2.zero;
            delObj.AddComponent<Image>().color = new Color(0.5f, 0.15f, 0.15f);
            var delBtn = delObj.AddComponent<Button>();
            delBtn.onClick.AddListener(() =>
            {
                ReplayStorage.DeleteReplay(replayId);
                RefreshList();
            });
            var delTmp = delObj.AddComponent<TextMeshProUGUI>();
            delTmp.text = "削除";
            delTmp.fontSize = 14;
            delTmp.alignment = TextAlignmentOptions.Center;
            delTmp.color = Color.white;

            _listItems.Add(item);
        }

        // ================================================================
        // Playback UI
        // ================================================================

        void BuildPlayerPanel()
        {
            _playerPanel = new GameObject("PlayerPanel");
            _playerPanel.transform.SetParent(_root.transform, false);
            var panelRt = _playerPanel.AddComponent<RectTransform>();
            StretchFull(panelRt);
            _playerPanel.SetActive(false);

            // Dark background
            var bg = _playerPanel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.98f);

            // Turn info
            var turnObj = CreateChild(_playerPanel.transform, "TurnInfo");
            var turnRt = turnObj.GetComponent<RectTransform>();
            turnRt.anchorMin = new Vector2(0.05f, 0.88f);
            turnRt.anchorMax = new Vector2(0.95f, 0.96f);
            turnRt.offsetMin = Vector2.zero;
            turnRt.offsetMax = Vector2.zero;
            _turnText = turnObj.AddComponent<TextMeshProUGUI>();
            _turnText.text = "ターン: 0 / 0";
            _turnText.fontSize = 24;
            _turnText.color = Color.white;
            _turnText.alignment = TextAlignmentOptions.Center;

            // State text
            var stateObj = CreateChild(_playerPanel.transform, "State");
            var stateRt = stateObj.GetComponent<RectTransform>();
            stateRt.anchorMin = new Vector2(0.1f, 0.3f);
            stateRt.anchorMax = new Vector2(0.9f, 0.85f);
            stateRt.offsetMin = Vector2.zero;
            stateRt.offsetMax = Vector2.zero;
            _stateText = stateObj.AddComponent<TextMeshProUGUI>();
            _stateText.text = "";
            _stateText.fontSize = 16;
            _stateText.color = new Color(0.7f, 0.7f, 0.75f);
            _stateText.alignment = TextAlignmentOptions.TopLeft;

            // Seek slider
            var sliderObj = CreateChild(_playerPanel.transform, "Seek");
            var sliderRt = sliderObj.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0.05f, 0.22f);
            sliderRt.anchorMax = new Vector2(0.95f, 0.28f);
            sliderRt.offsetMin = Vector2.zero;
            sliderRt.offsetMax = Vector2.zero;

            // Slider background
            var sliderBgObj = CreateChild(sliderObj.transform, "Background");
            var sliderBgRt = sliderBgObj.GetComponent<RectTransform>();
            StretchFull(sliderBgRt);
            sliderBgObj.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

            // Slider fill area
            var fillArea = CreateChild(sliderObj.transform, "FillArea");
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            StretchFull(fillAreaRt);

            var fillObj = CreateChild(fillArea.transform, "Fill");
            var fillRt = fillObj.GetComponent<RectTransform>();
            StretchFull(fillRt);
            fillObj.AddComponent<Image>().color = new Color(0.3f, 0.5f, 0.8f);

            // Slider handle
            var handleArea = CreateChild(sliderObj.transform, "HandleArea");
            var handleAreaRt = handleArea.GetComponent<RectTransform>();
            StretchFull(handleAreaRt);

            var handleObj = CreateChild(handleArea.transform, "Handle");
            var handleRt = handleObj.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(20, 0);
            handleObj.AddComponent<Image>().color = Color.white;

            _seekSlider = sliderObj.AddComponent<Slider>();
            _seekSlider.fillRect = fillRt;
            _seekSlider.handleRect = handleRt;
            _seekSlider.minValue = 0;
            _seekSlider.maxValue = 1;
            _seekSlider.wholeNumbers = false;
            _seekSlider.onValueChanged.AddListener(OnSeekChanged);

            // Control buttons row
            float btnY0 = 0.08f, btnY1 = 0.18f;

            // Stop
            _stopBtn = CreateControlButton(_playerPanel.transform, "停止", 0.02f, 0.18f, btnY0, btnY1);
            _stopBtn.onClick.AddListener(OnStopButton);

            // Prev turn
            _prevTurnBtn = CreateControlButton(_playerPanel.transform, "<<", 0.20f, 0.33f, btnY0, btnY1);
            _prevTurnBtn.onClick.AddListener(() => _player?.PrevTurn());

            // Play/Pause
            _playPauseBtn = CreateControlButton(_playerPanel.transform, "再生", 0.35f, 0.65f, btnY0, btnY1);
            _playPauseBtn.onClick.AddListener(OnPlayPauseButton);

            // Next turn
            _nextTurnBtn = CreateControlButton(_playerPanel.transform, ">>", 0.67f, 0.80f, btnY0, btnY1);
            _nextTurnBtn.onClick.AddListener(() => _player?.NextTurn());

            // Speed
            _speedBtn = CreateControlButton(_playerPanel.transform, "1x", 0.82f, 0.98f, btnY0, btnY1);
            _speedBtn.onClick.AddListener(OnSpeedButton);
            _speedText = _speedBtn.GetComponentInChildren<TextMeshProUGUI>();
        }

        Button CreateControlButton(Transform parent, string label, float xMin, float xMax, float yMin, float yMax)
        {
            var obj = CreateChild(parent, label);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            obj.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.22f);
            var btn = obj.AddComponent<Button>();
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return btn;
        }

        // ================================================================
        // Playback Logic
        // ================================================================

        void OnPlayReplay(string replayId)
        {
            var data = ReplayStorage.LoadReplay(replayId);
            if (data == null)
            {
                Debug.LogWarning($"[ReplayList] Failed to load replay: {replayId}");
                return;
            }

            _player = new ReplayPlayer();
            _player.Load(data, this);
            _player.OnTurnChanged += OnTurnChanged;
            _player.OnCommandExecuted += OnCommandExecuted;
            _player.OnPlaybackComplete += OnPlaybackComplete;
            _player.OnStateChanged += OnPlaybackStateChanged;

            _listPanel.transform.parent.gameObject.SetActive(false);
            _playerPanel.SetActive(true);

            _seekSlider.maxValue = Math.Max(1, _player.TotalCommands - 1);
            _seekSlider.value = 0;
            _stateText.text = $"{data.players[0].name} vs {data.players[1].name}\n" +
                              $"結果: {data.result.reason}\n\n再生ボタンを押してください";
            UpdateTurnDisplay();

            SoundManager.Instance?.PlayUISE("se_screen_open");
        }

        void OnPlayPauseButton()
        {
            if (_player == null) return;

            if (_player.State == ReplayPlayer.PlaybackState.Playing)
                _player.Pause();
            else
                _player.Play();
        }

        void OnStopButton()
        {
            StopPlayback();
            _playerPanel.SetActive(false);
            _listPanel.transform.parent.gameObject.SetActive(true);
        }

        void OnSpeedButton()
        {
            if (_player == null) return;

            _player.SetSpeed(_player.Speed switch
            {
                ReplayPlayer.PlaybackSpeed.Normal => ReplayPlayer.PlaybackSpeed.Fast,
                ReplayPlayer.PlaybackSpeed.Fast => ReplayPlayer.PlaybackSpeed.VeryFast,
                _ => ReplayPlayer.PlaybackSpeed.Normal
            });

            if (_speedText) _speedText.text = $"{(int)_player.Speed}x";
        }

        void OnSeekChanged(float value)
        {
            if (_player == null || _isSeeking) return;
            _isSeeking = true;
            int cmdIndex = Mathf.RoundToInt(value);
            // Find the turn for this command index
            if (_player.ReplayData?.commands != null && cmdIndex < _player.ReplayData.commands.Count)
            {
                int targetTurn = _player.ReplayData.commands[cmdIndex].turn;
                _player.SeekToTurn(targetTurn);
            }
            _isSeeking = false;
        }

        void StopPlayback()
        {
            if (_player != null)
            {
                _player.Stop();
                _player.OnTurnChanged -= OnTurnChanged;
                _player.OnCommandExecuted -= OnCommandExecuted;
                _player.OnPlaybackComplete -= OnPlaybackComplete;
                _player.OnStateChanged -= OnPlaybackStateChanged;
                _player = null;
            }
        }

        // ================================================================
        // Playback Events
        // ================================================================

        void OnTurnChanged(int turn) => UpdateTurnDisplay();

        void OnCommandExecuted(ReplayCommand cmd)
        {
            if (_stateText)
            {
                string playerName = "?";
                if (_player?.ReplayData?.players != null && cmd.player >= 1 && cmd.player <= _player.ReplayData.players.Length)
                    playerName = _player.ReplayData.players[cmd.player - 1].name;

                _stateText.text = $"T{cmd.turn} [{playerName}] {cmd.type}";
            }

            if (!_isSeeking && _seekSlider != null)
            {
                _isSeeking = true;
                _seekSlider.value = _player.CurrentCommandIndex;
                _isSeeking = false;
            }
        }

        void OnPlaybackComplete()
        {
            if (_stateText)
                _stateText.text += "\n\n再生完了";
            UpdatePlayPauseLabel();
        }

        void OnPlaybackStateChanged(ReplayPlayer.PlaybackState state) => UpdatePlayPauseLabel();

        void UpdateTurnDisplay()
        {
            if (_turnText && _player != null)
                _turnText.text = $"ターン: {_player.CurrentTurn} / {_player.TotalTurns}";
        }

        void UpdatePlayPauseLabel()
        {
            if (_playPauseBtn == null || _player == null) return;
            var tmp = _playPauseBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
                tmp.text = _player.State == ReplayPlayer.PlaybackState.Playing ? "一時停止" : "再生";
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
    }
}
