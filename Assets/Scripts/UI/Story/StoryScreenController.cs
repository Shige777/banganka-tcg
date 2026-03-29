using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Data;

namespace Banganka.UI.Story
{
    /// <summary>
    /// ストーリー画面 (STORY_BIBLE.md / STORY_CHAPTERS.md)
    /// 6章構成 — 各願主の物語
    /// </summary>
    public class StoryScreenController : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI progressText;
        [SerializeField] Transform chapterListParent;
        [SerializeField] GameObject chapterNodePrefab;
        [SerializeField] GameObject chapterDetailPanel;
        [SerializeField] TextMeshProUGUI detailTitle;
        [SerializeField] TextMeshProUGUI detailDesc;
        [SerializeField] TextMeshProUGUI detailCharacter;
        [SerializeField] Image detailAspectIcon;
        [SerializeField] Button playButton;
        [SerializeField] StorySceneController storySceneController;
        [SerializeField] LeaderArchiveController leaderArchiveController;

        readonly List<GameObject> _nodeInstances = new();
        StoryChapter _selectedChapter;

        // Aspect hex colors from CLAUDE.md
        static readonly Dictionary<Aspect, Color> AspectColors = new()
        {
            { Aspect.Contest,  HexColor("#FF5A36") },
            { Aspect.Whisper,  HexColor("#4DA3FF") },
            { Aspect.Weave,    HexColor("#59C36A") },
            { Aspect.Verse,    HexColor("#9A5BFF") },
            { Aspect.Manifest, HexColor("#F4C542") },
            { Aspect.Hush,     HexColor("#3A3A46") },
        };

        void OnEnable()
        {
            if (titleText) titleText.text = "ストーリー";
            if (chapterDetailPanel) chapterDetailPanel.SetActive(false);

            SyncPlayerProgress();
            BuildChapterList();
            UpdateProgress();
        }

        void SyncPlayerProgress()
        {
            var pd = PlayerData.Instance;
            var chapters = StoryDatabase.Chapters;
            for (int i = 0; i < chapters.Count; i++)
            {
                if (i < pd.storyChapter - 1)
                    chapters[i].completed = true;
                chapters[i].unlocked = i < pd.storyChapter;
            }
        }

        void BuildChapterList()
        {
            foreach (var inst in _nodeInstances)
                Destroy(inst);
            _nodeInstances.Clear();

            foreach (var ch in StoryDatabase.Chapters)
            {
                if (chapterNodePrefab == null || chapterListParent == null) continue;

                var obj = Instantiate(chapterNodePrefab, chapterListParent);
                _nodeInstances.Add(obj);

                // Title text
                var titleTmp = obj.GetComponentInChildren<TextMeshProUGUI>();
                if (titleTmp != null)
                {
                    string status = ch.completed ? " ✓" : ch.unlocked ? "" : " 🔒";
                    titleTmp.text = $"第{ch.number}章{status}\n{ch.title}";
                    titleTmp.color = ch.unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                }

                // Aspect color bar
                var img = obj.GetComponent<Image>();
                if (img != null && AspectColors.TryGetValue(ch.themeAspect, out var col))
                {
                    col.a = ch.unlocked ? 0.8f : 0.3f;
                    img.color = col;
                }

                // Click handler
                var btn = obj.GetComponent<Button>();
                if (btn == null) btn = obj.AddComponent<Button>();
                var captured = ch;
                btn.onClick.AddListener(() => SelectChapter(captured));
                btn.interactable = ch.unlocked;
            }
        }

        void UpdateProgress()
        {
            if (progressText == null) return;
            int completed = 0;
            foreach (var ch in StoryDatabase.Chapters)
                if (ch.completed) completed++;
            progressText.text = $"進行度: {completed}/{StoryDatabase.Chapters.Count}章完了";
        }

        void SelectChapter(StoryChapter ch)
        {
            _selectedChapter = ch;
            if (chapterDetailPanel) chapterDetailPanel.SetActive(true);
            if (detailTitle) detailTitle.text = $"第{ch.number}章 — {ch.title}";
            if (detailDesc) detailDesc.text = ch.description;
            if (detailCharacter) detailCharacter.text = $"願主: {ch.keyCharacter}";

            if (detailAspectIcon && AspectColors.TryGetValue(ch.themeAspect, out var col))
                detailAspectIcon.color = col;

            if (playButton)
            {
                playButton.interactable = ch.unlocked && !ch.completed;
                var btnText = playButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                    btnText.text = ch.completed ? "完了済み" : "開始する";
            }
        }

        public void OnPlayChapter()
        {
            if (_selectedChapter == null || !_selectedChapter.unlocked) return;
            Debug.Log($"[Story] Starting chapter: {_selectedChapter.id}");

            if (storySceneController != null)
            {
                storySceneController.OnChapterCompleted -= OnChapterFinished;
                storySceneController.OnChapterCompleted += OnChapterFinished;
                storySceneController.PlayChapter(_selectedChapter.id);
            }
        }

        void OnChapterFinished(string chapterId)
        {
            if (storySceneController != null)
                storySceneController.OnChapterCompleted -= OnChapterFinished;

            SyncPlayerProgress();
            BuildChapterList();
            UpdateProgress();

            if (chapterDetailPanel) chapterDetailPanel.SetActive(false);
        }

        public void OnCloseDetail()
        {
            if (chapterDetailPanel) chapterDetailPanel.SetActive(false);
        }

        /// <summary>願主秘話画面を開く（ストーリー画面のボタンから呼ぶ）</summary>
        public void OnOpenArchive()
        {
            if (leaderArchiveController != null)
                leaderArchiveController.gameObject.SetActive(true);
        }

        static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
