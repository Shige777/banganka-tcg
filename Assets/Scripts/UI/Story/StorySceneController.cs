using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Data;
using Banganka.Core.Battle;
using Banganka.Core.Config;
using Banganka.Audio;
using Banganka.UI.Tutorial;

namespace Banganka.UI.Story
{
    /// <summary>
    /// ストーリーシーン再生 (STORY_CHAPTERS.md / STORY_BIBLE.md)
    /// ビジュアルノベル形式 — ナルとの対話 + 願主との対峙
    /// </summary>
    public class StorySceneController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] Image backgroundImage;
        [SerializeField] Image characterImage;
        [SerializeField] TextMeshProUGUI speakerName;
        [SerializeField] TextMeshProUGUI dialogueText;
        [SerializeField] Button nextButton;
        [SerializeField] Button skipButton;
        [SerializeField] CompanionNal nal;
        [SerializeField] GameObject choicePanel;
        [SerializeField] Button[] choiceButtons;

        [Header("Transition")]
        [SerializeField] CanvasGroup fadeOverlay;

        int _currentNodeIndex;
        List<StoryNode> _nodes;
        string _currentChapterId;
        bool _waitingForInput;
        Coroutine _playbackCoroutine;

        public event Action<string> OnChapterCompleted; // chapterId

        // ====================================================================
        // Story Node Types
        // ====================================================================

        public enum NodeType { Dialogue, NalDialogue, Choice, Battle, Transition }

        [Serializable]
        public class StoryNode
        {
            public NodeType type;
            public string speaker;
            public string text;
            public string expression;
            public string bgm;
            public string backgroundKey;
            public string characterKey;
            public string[] choiceTexts;
            public int[] choiceTargets; // node index to jump to
        }

        // ====================================================================
        // Public API
        // ====================================================================

        public void PlayChapter(string chapterId)
        {
            _currentChapterId = chapterId;
            _nodes = GetChapterNodes(chapterId);
            _currentNodeIndex = 0;

            if (choicePanel) choicePanel.SetActive(false);
            gameObject.SetActive(true);

            if (_playbackCoroutine != null) StopCoroutine(_playbackCoroutine);
            _playbackCoroutine = StartCoroutine(PlayNodes());
        }

        // ====================================================================
        // Playback
        // ====================================================================

        IEnumerator PlayNodes()
        {
            while (_currentNodeIndex < _nodes.Count)
            {
                var node = _nodes[_currentNodeIndex];

                // BGM change
                if (!string.IsNullOrEmpty(node.bgm))
                    SoundManager.Instance?.PlayBGM(node.bgm);

                // Background change with fade
                if (!string.IsNullOrEmpty(node.backgroundKey))
                    yield return FadeTransition(0.3f);

                switch (node.type)
                {
                    case NodeType.Dialogue:
                        yield return ShowDialogue(node);
                        break;

                    case NodeType.NalDialogue:
                        yield return ShowNalDialogue(node);
                        break;

                    case NodeType.Choice:
                        yield return ShowChoice(node);
                        break;

                    case NodeType.Battle:
                        yield return RunStoryBattle(_currentChapterId);
                        break;

                    case NodeType.Transition:
                        yield return FadeTransition(0.5f);
                        break;
                }

                _currentNodeIndex++;
            }

            CompleteChapter();
        }

        IEnumerator ShowDialogue(StoryNode node)
        {
            if (speakerName) speakerName.text = node.speaker ?? "";
            if (nal != null) nal.Hide();

            // Character image
            if (characterImage != null && !string.IsNullOrEmpty(node.characterKey))
            {
                var sprite = Resources.Load<Sprite>($"Story/Characters/{node.characterKey}");
                if (sprite != null) characterImage.sprite = sprite;
                characterImage.gameObject.SetActive(true);
            }

            // Typewriter text
            yield return TypewriterText(node.text);

            // Wait for tap
            _waitingForInput = true;
            yield return new WaitUntil(() => !_waitingForInput);
        }

        IEnumerator ShowNalDialogue(StoryNode node)
        {
            if (speakerName) speakerName.text = "ナル";
            if (characterImage) characterImage.gameObject.SetActive(false);

            if (nal != null)
                nal.ShowDialogue(node.text, node.expression ?? "normal");
            else
                yield return TypewriterText(node.text);

            _waitingForInput = true;
            yield return new WaitUntil(() => !_waitingForInput);
        }

        IEnumerator ShowChoice(StoryNode node)
        {
            if (choicePanel == null || choiceButtons == null) yield break;

            choicePanel.SetActive(true);
            int selectedChoice = -1;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (i < node.choiceTexts.Length)
                {
                    choiceButtons[i].gameObject.SetActive(true);
                    var tmp = choiceButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = node.choiceTexts[i];

                    int choiceIndex = i;
                    choiceButtons[i].onClick.RemoveAllListeners();
                    choiceButtons[i].onClick.AddListener(() => selectedChoice = choiceIndex);
                }
                else
                {
                    choiceButtons[i].gameObject.SetActive(false);
                }
            }

            yield return new WaitUntil(() => selectedChoice >= 0);
            choicePanel.SetActive(false);

            // Jump to target node if specified
            if (node.choiceTargets != null && selectedChoice < node.choiceTargets.Length)
                _currentNodeIndex = node.choiceTargets[selectedChoice] - 1; // -1 because loop increments
        }

        // ====================================================================
        // Text & Transitions
        // ====================================================================

        IEnumerator TypewriterText(string text)
        {
            if (dialogueText == null) yield break;
            dialogueText.text = "";
            foreach (char c in text)
            {
                dialogueText.text += c;
                yield return new WaitForSeconds(0.03f);
            }
        }

        IEnumerator FadeTransition(float duration)
        {
            if (fadeOverlay == null) yield break;

            fadeOverlay.gameObject.SetActive(true);
            float elapsed = 0;

            // Fade in
            while (elapsed < duration / 2)
            {
                elapsed += Time.deltaTime;
                fadeOverlay.alpha = elapsed / (duration / 2);
                yield return null;
            }
            fadeOverlay.alpha = 1;

            yield return new WaitForSeconds(0.1f);

            // Fade out
            elapsed = 0;
            while (elapsed < duration / 2)
            {
                elapsed += Time.deltaTime;
                fadeOverlay.alpha = 1f - elapsed / (duration / 2);
                yield return null;
            }
            fadeOverlay.alpha = 0;
            fadeOverlay.gameObject.SetActive(false);
        }

        // ====================================================================
        // Story Battle
        // ====================================================================

#pragma warning disable CS0414 // バトル結果分岐で使用予定
        bool _battleFinished;
#pragma warning restore CS0414
        bool _battleWon;

        /// <summary>
        /// 章ごとの願主との対戦をBot AIで実行。
        /// チュートリアル同様のローカルBattleEngineを使用。
        /// </summary>
        IEnumerator RunStoryBattle(string chapterId)
        {
            _battleFinished = false;
            _battleWon = false;

            // Determine opponent leader by chapter
            string opponentLeaderId = chapterId switch
            {
                "CH_01" => "LEADER_WEAVE",    // 灯凪 (穏緑)
                "CH_02" => "LEADER_CONTEST",   // Aldric (曙赤)
                "CH_03" => "LEADER_HUSH",      // 崔鋒 (玄白)
                "CH_04" => "LEADER_MANIFEST",  // Rahim (遊黄)
                "CH_05" => "LEADER_VERSE",     // Amara (妖紫)
                "CH_06" => "LEADER_WHISPER",   // Vael (空青)
                _ => "LEADER_CONTEST",
            };

            var activeDeck = PlayerData.Instance?.decks
                ?.FirstOrDefault(d => d.deckId == PlayerData.Instance.selectedDeckId)
                ?? PlayerData.Instance?.decks?.FirstOrDefault();
            var playerLeaderId = activeDeck?.leaderId ?? "LEADER_CONTEST";
            var playerLeader = CardDatabase.GetLeader(playerLeaderId)
                ?? CardDatabase.DefaultLeader;
            var opponentLeader = CardDatabase.GetLeader(opponentLeaderId)
                ?? CardDatabase.DefaultLeader;

            // Player deck from active deck, opponent from preset
            var presets = CardDatabase.PresetDecks;
            if (presets == null || presets.Count == 0)
            {
                Debug.LogError("[StoryScene] No preset decks available");
                yield break;
            }
            var playerDeck = CardDatabase.BuildDeck(
                activeDeck?.cardIds ?? presets.Values.First().cardIds);
            var presetKey = presets.Keys
                .FirstOrDefault(k => k.Contains(opponentLeaderId.Replace("LEADER_", "")))
                ?? presets.Keys.First();
            var opponentDeck = CardDatabase.BuildDeck(presets[presetKey].cardIds);

            // Init engine (story battles use normal HP)
            var engine = new BattleEngine(Environment.TickCount);
            engine.InitMatch(playerLeader, opponentLeader,
                new List<CardData>(playerDeck), new List<CardData>(opponentDeck));

            // AI plays opponent side
            var ai = new SimpleAI(engine, PlayerSide.Player2, BotDifficulty.Easy);

            engine.StartTurn();

            // Auto-play (story battles are cinematic — AI vs AI for now)
            var playerAI = new SimpleAI(engine, PlayerSide.Player1, BotDifficulty.Normal);
            int maxIter = BalanceConfig.TurnLimitTotal * 2 + 10;
            int iter = 0;

            while (!engine.State.isGameOver && iter < maxIter)
            {
                iter++;
                if (engine.State.activePlayer == PlayerSide.Player1)
                    playerAI.PlayTurn();
                else
                    ai.PlayTurn();

                if (!engine.State.isGameOver)
                    engine.EndTurn();

                // Yield every few turns for visual breathing room
                if (iter % 4 == 0)
                    yield return null;
            }

            _battleWon = engine.State.result == MatchResult.Player1Win;
            _battleFinished = true;

            // Brief pause after battle
            yield return new WaitForSeconds(1f);

            Debug.Log($"[Story] Battle {chapterId} result: {(_battleWon ? "WIN" : "LOSE")}");
        }

        // ====================================================================
        // Input
        // ====================================================================

        public void OnTapNext()
        {
            if (_waitingForInput)
                _waitingForInput = false;
        }

        public void OnSkip()
        {
            if (_playbackCoroutine != null) StopCoroutine(_playbackCoroutine);
            CompleteChapter();
        }

        // ====================================================================
        // Completion
        // ====================================================================

        void CompleteChapter()
        {
            if (!string.IsNullOrEmpty(_currentChapterId))
            {
                StoryDatabase.CompleteChapter(_currentChapterId);
                var pd = PlayerData.Instance;
                var chapters = StoryDatabase.Chapters;
                for (int i = 0; i < chapters.Count; i++)
                {
                    if (chapters[i].id == _currentChapterId && i + 1 < chapters.Count)
                    {
                        pd.storyChapter = i + 2; // Next chapter number
                        break;
                    }
                }
                OnChapterCompleted?.Invoke(_currentChapterId);
            }

            gameObject.SetActive(false);
        }

        // ====================================================================
        // Chapter Node Data (STORY_CHAPTERS.md — 6章)
        // ====================================================================

        static List<StoryNode> GetChapterNodes(string chapterId)
        {
            return chapterId switch
            {
                "CH_01" => GetChapter1Nodes(),
                "CH_02" => GetChapter2Nodes(),
                "CH_03" => GetChapter3Nodes(),
                "CH_04" => GetChapter4Nodes(),
                "CH_05" => GetChapter5Nodes(),
                "CH_06" => GetChapter6Nodes(),
                _ => new List<StoryNode>
                {
                    new() { type = NodeType.NalDialogue, text = "この章はまだ準備中みたい。", expression = "normal" },
                },
            };
        }

        // ============================================================
        // 第1章 — 灯凪の章「届けたい声」
        // 舞台: 滅びた星間植民都市国家の残響
        // テーマ: 声は届くのか。語ることに意味はあるのか。
        // ============================================================
        static List<StoryNode> GetChapter1Nodes() => new()
        {
            // ── 導入：交界の霧の中 ──
            new() { type = NodeType.Transition, bgm = "bgm_story_calm", backgroundKey = "boundary_fog" },
            new() { type = NodeType.NalDialogue, text = "ここは……静かだね。霧が深い。何も見えないや。", expression = "normal" },
            new() { type = NodeType.NalDialogue, text = "でも、何か聞こえる気がする。遠くから……声？", expression = "serious" },

            // ── 灯凪との出会い ──
            new() { type = NodeType.Transition, backgroundKey = "ruined_plaza" },
            new() { type = NodeType.NalDialogue, text = "あ、誰かいる。石畳の広場みたいな場所に……女の子？", expression = "normal" },
            new() { type = NodeType.NalDialogue, text = "裸足だ。目を閉じてる。白い衣に金の耳飾り……片方だけ。", expression = "normal" },
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "……聞こえる？ あんたにも、声が聞こえるの。",
                characterKey = "hinagi" },
            new() { type = NodeType.NalDialogue, text = "この子、目が見えないのか……でも、俺たちの方をまっすぐ「見てる」。", expression = "serious" },
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "あんた、果求者だね。足音で分かる。迷子みたいな歩き方してるから。",
                characterKey = "hinagi" },
            new() { type = NodeType.NalDialogue, text = "（……鋭い）", expression = "serious" },

            // ── 灯凪の世界 ──
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "私は灯凪。語り部。声の巫女。……まあ、元の世界じゃそう呼ばれてた。",
                characterKey = "hinagi" },
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "私の街は遠い星から来た人たちが建てた。文字がないから、全部声で伝えた。歴史も、法律も、子守唄も。",
                characterKey = "hinagi" },
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "語り部はその声を預かる役目。この金の耳飾りがその証。",
                characterKey = "hinagi" },
            new() { type = NodeType.NalDialogue, text = "片方しかないのは……", expression = "normal" },
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "もう片方は滅んだ街に置いてきた。取りに帰る場所がもうないだけ。",
                characterKey = "hinagi" },

            // ── 都市国家の記憶 ──
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "毎朝、子どもたちが私の髪を結いに来た。私は自分じゃ結えないから。",
                characterKey = "hinagi" },
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "織江が糸で飾って、花守が花を挿してくれた。そして夕方、広場で物語を語った。",
                characterKey = "hinagi" },
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "母星との通信が途絶えて、街はゆっくり壊れていった。外敵じゃない。孤立と、内側からの崩壊。",
                characterKey = "hinagi" },
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "最後の日、全員の声を聞いた。死んでいく人たちの声を。全部覚えてる。語り部だから。",
                characterKey = "hinagi" },
            new() { type = NodeType.NalDialogue, text = "……なんか泣けてくるな。", expression = "normal" },

            // ── 灯凪の願い ──
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "泣くな。……いや、泣いてもいいけど、嘘泣きだったら怒るよ。",
                characterKey = "hinagi" },
            new() { type = NodeType.NalDialogue, text = "嘘泣きじゃないよ！", expression = "surprise" },
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "……分かってる。あんたの声、嘘がない。透明だ。",
                characterKey = "hinagi" },
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "私の願いはひとつ。「私が語った物語を、誰かひとりに届けたい」。",
                characterKey = "hinagi" },
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "聞く人がいない物語は、存在しないのと同じ。声は届いて初めて声になる。",
                characterKey = "hinagi" },

            // ── 選択肢 ──
            new() { type = NodeType.Choice,
                text = "灯凪の願いにどう答える？",
                choiceTexts = new[] { "聞かせてほしい", "その声は、もう届いてる" },
                choiceTargets = new[] { -1, -1 } },

            // ── バトル ──
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "……ふん。口だけじゃないか試させて。語り部の声は、嘘つきには届かない。",
                characterKey = "hinagi" },
            new() { type = NodeType.NalDialogue, text = "来るよ、果求者！", expression = "surprise" },
            new() { type = NodeType.Battle },

            // ── クライマックス ──
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "……強いね。嘘のない戦い方だった。",
                characterKey = "hinagi" },
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "ありがとう、果求者。あんたが聞いてくれた。",
                characterKey = "hinagi" },
            new() { type = NodeType.Dialogue, speaker = "灯凪",
                text = "それだけで、私の物語は完成する。……ほんの少しだけ。",
                characterKey = "hinagi" },

            // ── 余韻 ──
            new() { type = NodeType.NalDialogue, text = "……よかった、ね。", expression = "smile" },
            new() { type = NodeType.NalDialogue, text = "（俺、この子の物語を聞いた時……なんか「鳴った」気がした。なんだろ）", expression = "normal" },
        };

        // ============================================================
        // 第2章 — Aldricの章「贖罪の剣」
        // 舞台: 次元裂開が走る焦土の征服地
        // テーマ: 贖罪は自己満足か。それでも花を植える意味。
        // ============================================================
        static List<StoryNode> GetChapter2Nodes() => new()
        {
            // ── 導入：焦土の大地 ──
            new() { type = NodeType.Transition, bgm = "bgm_story_tension", backgroundKey = "scorched_battlefield" },
            new() { type = NodeType.NalDialogue, text = "うわ……ここ、焼け野原だ。でも、花が咲いてる？ 焦げた地面から。", expression = "surprise" },
            new() { type = NodeType.NalDialogue, text = "あれ、でっかい人がいる。鎧着て……花に水やってる？", expression = "surprise" },

            // ── アルドリックとの出会い ──
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "……。",
                characterKey = "aldric" },
            new() { type = NodeType.NalDialogue, text = "（無視された。でっかい。191cmって……怖っ）", expression = "surprise" },
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "果求者か。……お前に用はない。道を開けろ。",
                characterKey = "aldric" },
            new() { type = NodeType.NalDialogue, text = "え、怖い怖い。でもこの人、目の下すごいクマだ。全然寝てないのかな。", expression = "serious" },

            // ── 征服王の正体 ──
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "俺はアルドリック。征服王と呼ばれていた。……暴君、とも。",
                characterKey = "aldric" },
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "大陸を征服した。次元の亀裂を兵器にして。空間を裂き、敵の背後に軍を送り込む。",
                characterKey = "aldric" },
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "無敵だった。だが亀裂を開くたびに、世界が壊れた。使った俺たち自身の世界が。",
                characterKey = "aldric" },
            new() { type = NodeType.NalDialogue, text = "それって……自分の世界を壊しながら戦ってたってこと？", expression = "serious" },
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "そうだ。そして征服した街には、必ず孤児が残った。",
                characterKey = "aldric" },

            // ── 花園の秘密 ──
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "……焼いた後に花を植え始めた。いつからか。覚えていない。",
                characterKey = "aldric" },
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "孤児院のガキが、鎧の内側にクレヨンで花を描いた。テオという名の少年だ。",
                characterKey = "aldric" },
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "消すなと命じた。……鎧匠は理由を聞かなかった。",
                characterKey = "aldric" },
            new() { type = NodeType.NalDialogue, text = "（この人、怖い顔してるけど……花を植える手が優しい）", expression = "normal" },

            // ── ガーネットの影 ──
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "副将のガーネットだけが知っている。俺が毎夜、踏みにじった民の顔を夢に見ることを。",
                characterKey = "aldric" },
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "「王よ、また園芸ですか」……あいつはそう言って笑う。俺は黙る。",
                characterKey = "aldric" },

            // ── 願いの核心 ──
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "贖罪か自己満足か──そんなことはどうでもいい。",
                characterKey = "aldric" },
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "俺の願いは一つ。焼いた大地に、もう一度花を咲かせること。それだけだ。",
                characterKey = "aldric" },
            new() { type = NodeType.Choice,
                text = "アルドリックにどう向き合う？",
                choiceTexts = new[] { "その花は本物だ", "贖罪かどうかは、花が知ってる" },
                choiceTargets = new[] { -1, -1 } },

            // ── バトル ──
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "……口だけか確かめる。来い。",
                characterKey = "aldric" },
            new() { type = NodeType.Battle },

            // ── クライマックス ──
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "……意外と真面目じゃないか、お前。",
                characterKey = "aldric" },
            new() { type = NodeType.Dialogue, speaker = "Aldric",
                text = "お前が踏んだ地面から、何か生えるといいな。……冗談だ。",
                characterKey = "aldric" },
            new() { type = NodeType.NalDialogue, text = "冗談言うんだ、この人。……あ、ちょっとだけ笑った？", expression = "smile" },
            new() { type = NodeType.NalDialogue, text = "（征服王って呼ばれた人が、花を植えてる。世界って不思議だな）", expression = "normal" },
        };

        // ============================================================
        // 第3章 — 崔鋒の章「敗者の真実」
        // 舞台: 記憶結晶が砕かれた革命後の城門
        // テーマ: 歴史は誰のものか。消された名前の重み。
        // ============================================================
        static List<StoryNode> GetChapter3Nodes() => new()
        {
            // ── 導入：壊された城門 ──
            new() { type = NodeType.Transition, bgm = "bgm_story_tension", backgroundKey = "broken_fortress" },
            new() { type = NodeType.NalDialogue, text = "ここは……城門？ でも紋章が削り取られてる。誰かが意図的に消したみたいだ。", expression = "serious" },
            new() { type = NodeType.NalDialogue, text = "焚き火の匂い。あと……飯の匂い？", expression = "normal" },

            // ── 崔鋒との出会い ──
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "来たか。座れ。飯がある。",
                characterKey = "suihou" },
            new() { type = NodeType.NalDialogue, text = "（え？ いきなり飯？ この人60過ぎのじいさんだけど、目が……鋭い）", expression = "surprise" },
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "お前は何者だ、小さき者。",
                characterKey = "suihou" },
            new() { type = NodeType.NalDialogue, text = "……ッ。（見透かされた。この人の目、全部見えてる）", expression = "serious" },
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "まあいい。食え。腹が減っては話もできん。",
                characterKey = "suihou" },

            // ── 消された歴史 ──
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "俺は崔鋒。この世界では「革命に敗れた逆賊」ということになっている。",
                characterKey = "suihou" },
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "この世界には記憶を結晶化する技術がある。歴史を水晶に刻み、永遠に保存する。",
                characterKey = "suihou" },
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "だが革命軍は結晶の書き換え技術を開発した。歴史そのものを上書きした。",
                characterKey = "suihou" },
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "俺の部下は三千いた。全員、記録から消された。存在しなかったことにされた。",
                characterKey = "suihou" },
            new() { type = NodeType.NalDialogue, text = "三千人の名前を……消した？", expression = "serious" },

            // ── 李明の話 ──
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "一人目の名前は李明。門番だった。真面目な奴だ。飯の食い方だけは雑だった。",
                characterKey = "suihou" },
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "革命軍に殺された。結晶からも名前を消された。",
                characterKey = "suihou" },
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "だが俺の記憶からは消せない。人間の脳は、まだ結晶化されていないからな。",
                characterKey = "suihou" },
            new() { type = NodeType.NalDialogue, text = "……ちょっと黙っとく。", expression = "serious" },

            // ── 髭と矜持 ──
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "勝者が歴史を書く。だが俺は、敗者の真実を残す。この身体に刻む。",
                characterKey = "suihou" },
            new() { type = NodeType.NalDialogue, text = "あの……なんで髭だけそんなに綺麗に手入れしてるんですか。", expression = "normal" },
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "将軍は部下の前に立つとき、整っていなければならん。",
                characterKey = "suihou" },
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "たとえ部下がもういなくても。",
                characterKey = "suihou" },

            // ── 選択肢 ──
            new() { type = NodeType.Choice,
                text = "崔鋒にどう向き合う？",
                choiceTexts = new[] { "その名前、俺も覚える", "三千人の声を聞かせてくれ" },
                choiceTargets = new[] { -1, -1 } },

            // ── バトル ──
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "口だけの奴は嫌いだ。証明しろ。",
                characterKey = "suihou" },
            new() { type = NodeType.Battle },

            // ── クライマックス ──
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "……悪くない。お前の拳には嘘がなかった。",
                characterKey = "suihou" },
            new() { type = NodeType.Dialogue, speaker = "崔鋒",
                text = "記録は残った。それで十分だ。……飯のおかわりはあるぞ。",
                characterKey = "suihou" },
            new() { type = NodeType.NalDialogue, text = "（この人、怒ってるようで……本当は全部、守りたいだけなんだ）", expression = "normal" },
        };

        // ============================================================
        // 第4章 — Rahimの章「姉への祈り」
        // 舞台: 煤煙の立ち込める工場街
        // テーマ: 蘇らせたい。でも姉の言葉を裏切れない。
        // ============================================================
        static List<StoryNode> GetChapter4Nodes() => new()
        {
            // ── 導入：工場街 ──
            new() { type = NodeType.Transition, bgm = "bgm_story_calm", backgroundKey = "factory_district" },
            new() { type = NodeType.NalDialogue, text = "煙がすごい。空が黒い。工場がいっぱいある……でも半分壊れてる。", expression = "normal" },
            new() { type = NodeType.NalDialogue, text = "あ、子どもたちが走ってる。あの先頭の子は……", expression = "normal" },

            // ── ラヒムとの出会い ──
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "おっ、あんたが果求者？ 話は聞いてるよ！ 俺はラヒム！",
                characterKey = "rahim" },
            new() { type = NodeType.NalDialogue, text = "元気だなこの子。14歳？ ……あ、髪が不揃いだ。自分で切ってるのかな。", expression = "smile" },
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "こっち来なよ。路地裏のみんなに紹介するから！",
                characterKey = "rahim" },

            // ── 路地裏の仲間たち ──
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "こいつはアリ。幻灯機を直す天才。こっちはダリア、電気糸の名人。ハッサンは鏡細工。",
                characterKey = "rahim" },
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "みんな孤児なんだ。……俺もだけど。",
                characterKey = "rahim" },
            new() { type = NodeType.NalDialogue, text = "背中に背負ってるのは……工具箱？ すごくピカピカだ。他は全部ボロボロなのに。", expression = "normal" },

            // ── 姉の記憶 ──
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "……ああ、これ？ 姉ちゃんの。",
                characterKey = "rahim" },
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "姉ちゃんは工場で働いてた。次元共鳴炉ってやつの技師。俺の髪も切ってくれた。",
                characterKey = "rahim" },
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "あの日、炉が制御不能になって、工場ごと崩壊した。",
                characterKey = "rahim" },
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "見つかったのは、この工具箱だけだった。",
                characterKey = "rahim" },
            new() { type = NodeType.NalDialogue, text = "……。", expression = "serious" },
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "姉ちゃんは俺に言ってた。「誰かを助けられる人間になれ」って。",
                characterKey = "rahim" },
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "だから俺、止まれないんだ。止まったら考えちゃうから。姉ちゃんのこと。",
                characterKey = "rahim" },

            // ── 願いの矛盾 ──
            new() { type = NodeType.NalDialogue, text = "ラヒム……お前の願いは？", expression = "serious" },
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "姉ちゃんを……蘇らせたい。",
                characterKey = "rahim" },
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "……でもさ。もし誰かを犠牲にしないと蘇らせられないなら、それは姉ちゃんの教えに背くことになる。",
                characterKey = "rahim" },
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "だから分かんない。俺、どうすればいいのか。",
                characterKey = "rahim" },

            // ── 選択肢 ──
            new() { type = NodeType.Choice,
                text = "ラヒムにどう答える？",
                choiceTexts = new[] { "答えは急がなくていい", "姉さんの教えを守ってること自体が答えだ" },
                choiceTargets = new[] { -1, -1 } },

            // ── バトル ──
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "……よし。じゃあ試合しよう。俺、動いてないと考えちゃうからさ！",
                characterKey = "rahim" },
            new() { type = NodeType.Battle },

            // ── クライマックス ──
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "ありがとな、果求者。俺、もうちょっと頑張るよ。",
                characterKey = "rahim" },
            new() { type = NodeType.Dialogue, speaker = "Rahim",
                text = "姉ちゃんの工具箱、今日もピカピカに磨くね。",
                characterKey = "rahim" },
            new() { type = NodeType.NalDialogue, text = "（この子、笑ってる。でも工具箱を握る手が白い。……気づいてるよ）", expression = "normal" },
        };

        // ============================================================
        // 第5章 — Amaraの章「永遠の破壊者」
        // 舞台: 完璧に設計された、美しく息苦しい都市
        // テーマ: 永遠は呪い。美しいものを壊す勇気。
        // ============================================================
        static List<StoryNode> GetChapter5Nodes() => new()
        {
            // ── 導入：設計都市 ──
            new() { type = NodeType.Transition, bgm = "bgm_story_tension", backgroundKey = "designed_city" },
            new() { type = NodeType.NalDialogue, text = "うわ……綺麗。建物が全部完璧に並んでる。道路もピカピカ。", expression = "surprise" },
            new() { type = NodeType.NalDialogue, text = "……でも、人がいない。いや、いるけど……目が死んでる。", expression = "serious" },

            // ── アマラとの出会い ──
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "ようこそ、果求者。……この都市を「美しい」と思いましたか？",
                characterKey = "amara" },
            new() { type = NodeType.NalDialogue, text = "（この人ちょっと怖い。なんか俺のこと分析してる気がする……）", expression = "serious" },
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "私はアマラ。この都市を設計した建築家です。",
                characterKey = "amara" },
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "犯罪率ゼロ。飢餓ゼロ。病気ゼロ。完璧な都市。",
                characterKey = "amara" },
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "そして自由もゼロ。夢もゼロ。完璧に設計された場所には、望む必要がない。",
                characterKey = "amara" },
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "望まない人間は、生きていても死んでいる。",
                characterKey = "amara" },

            // ── 砂の模型 ──
            new() { type = NodeType.NalDialogue, text = "あの……テーブルの上の砂の模型、これ毎日作ってるんですか？", expression = "normal" },
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "ええ。毎日作って、毎日壊します。",
                characterKey = "amara" },
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "確認しているの。私にまだ壊せるか。壊す勇気があるか。",
                characterKey = "amara" },
            new() { type = NodeType.NalDialogue, text = "（壊した後に作り直す手が……震えてる）", expression = "serious" },

            // ── インク染みの秘密 ──
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "この指のインク染み、洗っても取れないの。設計図を書きすぎて。",
                characterKey = "amara" },
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "創ることを止められない証拠。呪いみたいなものね。",
                characterKey = "amara" },
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "「永遠」という名前を持つ女が、永遠を消そうとしている。皮肉でしょう？",
                characterKey = "amara" },

            // ── 願いの核心 ──
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "私の願いは、私が設計したこの世界を根こそぎ消すこと。",
                characterKey = "amara" },
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "「永遠」なんて、呪いと同じよ。",
                characterKey = "amara" },

            // ── 選択肢 ──
            new() { type = NodeType.Choice,
                text = "アマラにどう向き合う？",
                choiceTexts = new[] { "壊すことも、創ることの一部だ", "インク染みは呪いじゃなく、証だ" },
                choiceTargets = new[] { -1, -1 } },

            // ── バトル ──
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "……面白い解釈ね。では、その言葉に設計図通りの強度があるか確かめましょう。",
                characterKey = "amara" },
            new() { type = NodeType.Battle },

            // ── クライマックス ──
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "……あなたは面白い存在ね。壊すでもなく、作るでもない。",
                characterKey = "amara" },
            new() { type = NodeType.Dialogue, speaker = "Amara",
                text = "私の設計図に載っていない人間。……少しだけ、安心しました。",
                characterKey = "amara" },
            new() { type = NodeType.NalDialogue, text = "（あ、今一瞬だけ……丁寧語じゃなかった。初めて見た、この人の素の顔）", expression = "normal" },
        };

        // ============================================================
        // 第6章 — Vaelの章「存在の証明」
        // 舞台: 交界の最深部、全てが曖昧になる場所
        // テーマ: 存在とは何か。覚えてもらうこと。
        // ============================================================
        static List<StoryNode> GetChapter6Nodes() => new()
        {
            // ── 導入：交界の最深部 ──
            new() { type = NodeType.Transition, bgm = "bgm_story_calm", backgroundKey = "boundary_deep" },
            new() { type = NodeType.NalDialogue, text = "ここは……霧が濃すぎる。自分の手も見えない。", expression = "serious" },
            new() { type = NodeType.NalDialogue, text = "あれ？ あの人、浮いてる……足が地面に着いてない。", expression = "surprise" },

            // ── ヴァエルとの出会い ──
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "よう！ やっと来たな、果求者！ 待ってたぜ！",
                characterKey = "vael" },
            new() { type = NodeType.NalDialogue, text = "（明るい。すごく明るい。でも……影がない。この人、影がない）", expression = "serious" },
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "俺はヴァエル。名前の由来？ ないよ。どの言語にもない。名付けた人間がいないからな。",
                characterKey = "vael" },
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "あ、そうだ。この服はアルドリックのおっさんのシャツ。人の匂いがするだろ？",
                characterKey = "vael" },
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "それを着てると、自分がここにいる気がするんだ。",
                characterKey = "vael" },

            // ── 存在の不安定さ ──
            new() { type = NodeType.NalDialogue, text = "ヴァエル……お前って、ここで何してるの？", expression = "normal" },
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "いい質問。俺もよく分からない。記憶がない。過去がない。記録もない。",
                characterKey = "vael" },
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "交界に最初からいたのか、界主に作られたのか。誰も知らない。俺も知らない。",
                characterKey = "vael" },
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "影がない。足跡が残らない。鏡に映らないこともある。",
                characterKey = "vael" },
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "だから俺は喋り続ける。3秒黙ったら消えそうだから。……冗談じゃなく、本気で。",
                characterKey = "vael" },

            // ── ラヒムとの友情 ──
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "ラヒムとはよく走る。理由もなく。あいつは止まると考えちゃうから走る。俺は止まると消えそうだから走る。",
                characterKey = "vael" },
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "あいつが「明日返せよ」ってマフラー貸してくれた時……嬉しかった。",
                characterKey = "vael" },
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "「明日」があるってことは、俺が明日もここにいるって信じてくれたってことだから。",
                characterKey = "vael" },

            // ── ナルの異変 ──
            new() { type = NodeType.NalDialogue, text = "……。", expression = "serious" },
            new() { type = NodeType.Dialogue, speaker = "（ナルが黙っている）",
                text = "……。" },
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "お。小さいの、黙ったな。珍しいじゃん。",
                characterKey = "vael" },
            new() { type = NodeType.NalDialogue, text = "（……分からない。この人を見てると、何か……。言葉にならない）", expression = "serious" },

            // ── 願いの核心 ──
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "お前は俺を覚えてくれるか。俺がここにいたことを。",
                characterKey = "vael" },
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "名前のない存在が、名前を求めて戦う。それは滑稽か？",
                characterKey = "vael" },

            // ── 選択肢 ──
            new() { type = NodeType.Choice,
                text = "ヴァエルにどう答える？",
                choiceTexts = new[] { "覚えてる。お前はここにいる", "お前の名前を呼ぶよ、何度でも" },
                choiceTargets = new[] { -1, -1 } },

            // ── バトル ──
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "……ッ。なんだよ、急にそんなこと言って。……よし、じゃあ証明しろ。お前の言葉が本物かどうか。",
                characterKey = "vael" },
            new() { type = NodeType.Battle },

            // ── クライマックス ──
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "……ありがとう。それだけでいい。",
                characterKey = "vael" },
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "お前が俺の名前を呼んでくれる。それだけで、俺はここにいる。",
                characterKey = "vael" },

            // ── 余韻：影が映る ──
            new() { type = NodeType.NalDialogue, text = "あ……果求者、見て。ヴァエルの足元……", expression = "surprise" },
            new() { type = NodeType.NalDialogue, text = "影だ。薄いけど……影が映ってる。一瞬だけ。", expression = "surprise" },
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "……え？",
                characterKey = "vael" },
            new() { type = NodeType.Dialogue, speaker = "Vael",
                text = "……へへ。マジか。",
                characterKey = "vael" },
            new() { type = NodeType.NalDialogue, text = "あ、今俺も鳴ったかも。なんだろ、これ。", expression = "normal" },
            new() { type = NodeType.NalDialogue, text = "（6人の願主と出会った。それぞれの世界と、願いと、痛み。）", expression = "serious" },
            new() { type = NodeType.NalDialogue, text = "（果求者。お前の願いは、まだ聞いてなかったな。……いつか、聞かせてくれ）", expression = "normal" },
        };
    }
}
