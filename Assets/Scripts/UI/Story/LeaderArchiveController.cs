using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Data;
using Banganka.Core.Config;

namespace Banganka.UI.Story
{
    /// <summary>
    /// 願主秘話画面 (ZZZエージェント秘話相当)
    /// ストーリー画面から遷移、または各願主のプロフィールから開く。
    ///
    /// 構成:
    ///   左: 願主一覧（6人）
    ///   右上: 選択願主のプロフィール（世界・時代・概要）
    ///   右下: エピソード一覧（解放済み/ロック中）
    ///   エピソード選択時: 全画面テキスト閲覧パネル
    /// </summary>
    public class LeaderArchiveController : MonoBehaviour
    {
        [Header("願主一覧")]
        [SerializeField] Transform leaderListParent;
        [SerializeField] GameObject leaderNodePrefab;

        [Header("プロフィールパネル")]
        [SerializeField] GameObject profilePanel;
        [SerializeField] TextMeshProUGUI leaderNameText;
        [SerializeField] TextMeshProUGUI worldNameText;
        [SerializeField] TextMeshProUGUI eraText;
        [SerializeField] TextMeshProUGUI sfElementText;
        [SerializeField] TextMeshProUGUI profileSummaryText;
        [SerializeField] Image aspectColorBar;
        [SerializeField] Image leaderPortrait;
        [SerializeField] TextMeshProUGUI winsCountText;

        [Header("エピソード一覧")]
        [SerializeField] Transform episodeListParent;
        [SerializeField] GameObject episodeNodePrefab;

        [Header("エピソード閲覧パネル")]
        [SerializeField] GameObject readingPanel;
        [SerializeField] TextMeshProUGUI readingTitleText;
        [SerializeField] TextMeshProUGUI readingBodyText;
        [SerializeField] TextMeshProUGUI relatedCardsText;
        [SerializeField] Button closeReadingButton;

        readonly List<GameObject> _leaderNodes = new();
        readonly List<GameObject> _episodeNodes = new();
        LeaderArchive _selectedArchive;

        void OnEnable()
        {
            if (readingPanel) readingPanel.SetActive(false);
            if (profilePanel) profilePanel.SetActive(false);
            BuildLeaderList();
            if (closeReadingButton)
                closeReadingButton.onClick.AddListener(CloseReading);
        }

        void OnDisable()
        {
            if (closeReadingButton)
                closeReadingButton.onClick.RemoveListener(CloseReading);
        }

        // ── 願主一覧 ──

        void BuildLeaderList()
        {
            foreach (var n in _leaderNodes) Destroy(n);
            _leaderNodes.Clear();

            foreach (var archive in LeaderArchiveDatabase.Archives)
            {
                if (leaderNodePrefab == null || leaderListParent == null) continue;

                var obj = Instantiate(leaderNodePrefab, leaderListParent);
                _leaderNodes.Add(obj);

                var tmp = obj.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    string aspectName = AspectColors.GetDisplayName(archive.aspect);
                    tmp.text = $"{archive.leaderName}\n<size=70%>{aspectName}</size>";
                }

                var img = obj.GetComponent<Image>();
                if (img != null)
                {
                    var col = AspectColors.GetColor(archive.aspect);
                    col.a = 0.7f;
                    img.color = col;
                }

                var btn = obj.GetComponent<Button>() ?? obj.AddComponent<Button>();
                var captured = archive;
                btn.onClick.AddListener(() => SelectLeader(captured));
            }
        }

        // ── 願主選択 ──

        void SelectLeader(LeaderArchive archive)
        {
            _selectedArchive = archive;
            if (profilePanel) profilePanel.SetActive(true);
            if (readingPanel) readingPanel.SetActive(false);

            // プロフィール表示
            if (leaderNameText) leaderNameText.text = archive.leaderName;
            if (worldNameText) worldNameText.text = archive.worldName;
            if (eraText) eraText.text = archive.era;
            if (sfElementText) sfElementText.text = archive.sfElement;
            if (profileSummaryText) profileSummaryText.text = archive.profileSummary;

            if (aspectColorBar)
            {
                var col = AspectColors.GetColor(archive.aspect);
                col.a = 0.8f;
                aspectColorBar.color = col;
            }

            // リーダーポートレイト
            if (leaderPortrait)
            {
                string portraitKey = archive.leaderId.Replace("LDR_", "").ToLower() switch
                {
                    "con_01" => "aldric",
                    "whi_01" => "vael",
                    "wea_01" => "hinagi",
                    "ver_01" => "amara",
                    "man_01" => "rahim",
                    "hus_01" => "suihou",
                    _ => null
                };
                if (portraitKey != null)
                {
                    var sprite = Resources.Load<Sprite>($"CardIllustrations/Leaders/{portraitKey}_lv1");
                    if (sprite != null)
                    {
                        leaderPortrait.sprite = sprite;
                        leaderPortrait.enabled = true;
                    }
                    else
                    {
                        leaderPortrait.enabled = false;
                    }
                }
            }

            // 勝利数
            if (winsCountText)
            {
                int leaderWins = PlayerData.Instance.GetLeaderWins(archive.leaderId);
                winsCountText.text = $"勝利数: {leaderWins}";
            }

            BuildEpisodeList();
        }

        // ── エピソード一覧 ──

        void BuildEpisodeList()
        {
            foreach (var n in _episodeNodes) Destroy(n);
            _episodeNodes.Clear();

            if (_selectedArchive == null) return;

            var player = PlayerData.Instance;

            foreach (var ep in _selectedArchive.episodes)
            {
                if (episodeNodePrefab == null || episodeListParent == null) continue;

                var obj = Instantiate(episodeNodePrefab, episodeListParent);
                _episodeNodes.Add(obj);

                bool unlocked = LeaderArchiveDatabase.IsEpisodeUnlocked(ep, player);

                var tmp = obj.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (unlocked)
                    {
                        string typeLabel = ep.type switch
                        {
                            ArchiveEpisodeType.Profile => "人物",
                            ArchiveEpisodeType.Origin => "世界",
                            ArchiveEpisodeType.Companion => "仲間",
                            ArchiveEpisodeType.Memory => "記憶",
                            ArchiveEpisodeType.CrossLeader => "交流",
                            ArchiveEpisodeType.Secret => "秘密",
                            _ => ""
                        };
                        tmp.text = $"[{typeLabel}] {ep.title}";
                        tmp.color = Color.white;
                    }
                    else
                    {
                        tmp.text = $"??? — {ep.unlockCondition}";
                        tmp.color = new Color(0.5f, 0.5f, 0.5f);
                    }
                }

                var img = obj.GetComponent<Image>();
                if (img != null)
                {
                    img.color = unlocked
                        ? new Color(0.2f, 0.2f, 0.25f, 0.9f)
                        : new Color(0.15f, 0.15f, 0.15f, 0.6f);
                }

                var btn = obj.GetComponent<Button>() ?? obj.AddComponent<Button>();
                btn.interactable = unlocked;
                if (unlocked)
                {
                    var captured = ep;
                    btn.onClick.AddListener(() => OpenEpisode(captured));
                }
            }
        }

        // ── エピソード閲覧 ──

        void OpenEpisode(ArchiveEpisode ep)
        {
            if (readingPanel == null) return;
            readingPanel.SetActive(true);

            if (readingTitleText) readingTitleText.text = ep.title;

            if (readingBodyText)
            {
                readingBodyText.text = string.Join("\n\n", ep.paragraphs);
                readingBodyText.fontSize = AccessibilitySettings.BodyFontSize;
            }

            // 関連カード表示
            if (relatedCardsText)
            {
                if (ep.relatedCards != null && ep.relatedCards.Length > 0)
                {
                    var names = new List<string>();
                    foreach (var cardId in ep.relatedCards)
                    {
                        var card = CardDatabase.GetCard(cardId);
                        if (card != null)
                            names.Add(card.cardName);
                        else
                            names.Add(cardId);
                    }
                    relatedCardsText.text = "関連カード: " + string.Join("、", names);
                    relatedCardsText.gameObject.SetActive(true);
                }
                else
                {
                    relatedCardsText.gameObject.SetActive(false);
                }
            }
        }

        void CloseReading()
        {
            if (readingPanel) readingPanel.SetActive(false);
        }

        public void OnClose()
        {
            gameObject.SetActive(false);
        }
    }
}
