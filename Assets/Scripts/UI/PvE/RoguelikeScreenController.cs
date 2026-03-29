using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Battle;
using Banganka.Core.Config;
using Banganka.Core.Data;
using Banganka.Core.PvE;
using Banganka.Audio;

namespace Banganka.UI.PvE
{
    /// <summary>
    /// PvEローグライクモードUI。
    /// ラン開始/継続、ステージマップ、報酬選択を管理。
    /// </summary>
    public class RoguelikeScreenController : MonoBehaviour
    {
        Canvas _canvas;
        GameObject _root;
        TextMeshProUGUI _runStatusText;
        TextMeshProUGUI _stageInfoText;
        TextMeshProUGUI _deckInfoText;
        TextMeshProUGUI _artifactText;
        GameObject _rewardPanel;
        readonly List<GameObject> _rewardItems = new();

        Button _startBtn;
        Button _battleBtn;
        Button _abandonBtn;

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Show()
        {
            EnsureUI();
            RoguelikeManager.LoadRun();
            RefreshUI();
            _root.SetActive(true);
            SoundManager.Instance?.PlayUISE("se_screen_open");
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root);
        }

        // ================================================================
        // UI Build
        // ================================================================

        void EnsureUI()
        {
            if (_root != null) return;

            if (_canvas == null)
            {
                var cObj = new GameObject("RoguelikeCanvas");
                cObj.transform.SetParent(transform, false);
                _canvas = cObj.AddComponent<Canvas>();
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = 92;
                cObj.AddComponent<GraphicRaycaster>();
                var scaler = cObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
            }

            _root = new GameObject("RoguelikeRoot");
            _root.transform.SetParent(_canvas.transform, false);
            var rootRt = _root.AddComponent<RectTransform>();
            StretchFull(rootRt);
            _root.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.08f, 0.97f);

            // Header
            var header = CreateChild(_root.transform, "Header");
            SetAnchored(header.GetComponent<RectTransform>(), 0, 0.92f, 1, 1);
            header.AddComponent<Image>().color = new Color(0.12f, 0.08f, 0.18f);
            var titleTmp = header.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "交界探索";
            titleTmp.fontSize = 34;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color(0.9f, 0.75f, 1f);
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

            // Run status
            var statusObj = CreateChild(_root.transform, "Status");
            SetAnchored(statusObj.GetComponent<RectTransform>(), 0.05f, 0.78f, 0.95f, 0.91f);
            _runStatusText = statusObj.AddComponent<TextMeshProUGUI>();
            _runStatusText.fontSize = 20;
            _runStatusText.color = Color.white;
            _runStatusText.alignment = TextAlignmentOptions.Center;

            // Stage info
            var stageObj = CreateChild(_root.transform, "StageInfo");
            SetAnchored(stageObj.GetComponent<RectTransform>(), 0.05f, 0.58f, 0.95f, 0.77f);
            _stageInfoText = stageObj.AddComponent<TextMeshProUGUI>();
            _stageInfoText.fontSize = 18;
            _stageInfoText.color = new Color(0.8f, 0.8f, 0.85f);
            _stageInfoText.alignment = TextAlignmentOptions.Center;

            // Deck info
            var deckObj = CreateChild(_root.transform, "DeckInfo");
            SetAnchored(deckObj.GetComponent<RectTransform>(), 0.05f, 0.48f, 0.5f, 0.57f);
            _deckInfoText = deckObj.AddComponent<TextMeshProUGUI>();
            _deckInfoText.fontSize = 15;
            _deckInfoText.color = new Color(0.7f, 0.7f, 0.75f);
            _deckInfoText.alignment = TextAlignmentOptions.MidlineLeft;

            // Artifacts
            var artObj = CreateChild(_root.transform, "Artifacts");
            SetAnchored(artObj.GetComponent<RectTransform>(), 0.5f, 0.48f, 0.95f, 0.57f);
            _artifactText = artObj.AddComponent<TextMeshProUGUI>();
            _artifactText.fontSize = 15;
            _artifactText.color = new Color(0.8f, 0.7f, 0.5f);
            _artifactText.alignment = TextAlignmentOptions.MidlineRight;

            // Buttons
            var startObj = CreateChild(_root.transform, "Start");
            SetAnchored(startObj.GetComponent<RectTransform>(), 0.2f, 0.38f, 0.8f, 0.46f);
            startObj.AddComponent<Image>().color = new Color(0.25f, 0.15f, 0.45f);
            _startBtn = startObj.AddComponent<Button>();
            _startBtn.onClick.AddListener(OnStartRun);
            var startTmp = startObj.AddComponent<TextMeshProUGUI>();
            startTmp.text = "探索開始";
            startTmp.fontSize = 22;
            startTmp.alignment = TextAlignmentOptions.Center;
            startTmp.color = Color.white;

            var battleObj = CreateChild(_root.transform, "Battle");
            SetAnchored(battleObj.GetComponent<RectTransform>(), 0.2f, 0.38f, 0.8f, 0.46f);
            battleObj.AddComponent<Image>().color = new Color(0.15f, 0.35f, 0.55f);
            _battleBtn = battleObj.AddComponent<Button>();
            _battleBtn.onClick.AddListener(OnBattleStage);
            var battleTmp = battleObj.AddComponent<TextMeshProUGUI>();
            battleTmp.text = "戦闘開始";
            battleTmp.fontSize = 22;
            battleTmp.alignment = TextAlignmentOptions.Center;
            battleTmp.color = Color.white;

            var abandonObj = CreateChild(_root.transform, "Abandon");
            SetAnchored(abandonObj.GetComponent<RectTransform>(), 0.3f, 0.28f, 0.7f, 0.35f);
            abandonObj.AddComponent<Image>().color = new Color(0.35f, 0.12f, 0.12f);
            _abandonBtn = abandonObj.AddComponent<Button>();
            _abandonBtn.onClick.AddListener(OnAbandon);
            var abandonTmp = abandonObj.AddComponent<TextMeshProUGUI>();
            abandonTmp.text = "探索放棄";
            abandonTmp.fontSize = 16;
            abandonTmp.alignment = TextAlignmentOptions.Center;
            abandonTmp.color = Color.white;

            // Reward panel
            _rewardPanel = new GameObject("RewardPanel");
            _rewardPanel.transform.SetParent(_root.transform, false);
            SetAnchored(_rewardPanel.AddComponent<RectTransform>(), 0.05f, 0.05f, 0.95f, 0.45f);
            _rewardPanel.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            _rewardPanel.SetActive(false);
        }

        // ================================================================
        // Refresh
        // ================================================================

        void RefreshUI()
        {
            bool hasRun = RoguelikeManager.HasActiveRun;
            var run = RoguelikeManager.CurrentRun;

            _startBtn.gameObject.SetActive(!hasRun);
            _battleBtn.gameObject.SetActive(hasRun && run.IsAlive && !run.IsComplete);
            _abandonBtn.gameObject.SetActive(hasRun);

            if (!hasRun)
            {
                _runStatusText.text = "交界の深淵を探索し、カードを集めて強くなれ";
                _stageInfoText.text = "アスペクトを選んで探索を開始しよう\n8ステージを突破すると豪華報酬!";
                _deckInfoText.text = "";
                _artifactText.text = "";
                HideRewards();
                return;
            }

            _runStatusText.text = $"ステージ {run.currentStage + 1}/{run.maxStage}  " +
                                   $"HP: {run.hp}/{run.maxHp}  ゴールド: {run.gold}G";

            if (run.IsComplete)
            {
                _stageInfoText.text = "<b>探索完了!</b>\n全ステージを突破しました!";
                _battleBtn.gameObject.SetActive(false);
            }
            else if (!run.IsAlive)
            {
                _stageInfoText.text = "<b>探索終了</b>\nHPが尽きました...";
                _battleBtn.gameObject.SetActive(false);
            }
            else
            {
                var stage = RoguelikeManager.GetCurrentStage();
                if (stage != null)
                {
                    _stageInfoText.text = $"次の敵: <b>{stage.enemyName}</b>\n" +
                                           $"難易度: {stage.difficulty}  報酬: +{stage.bonusGold}G";
                }
            }

            _deckInfoText.text = $"デッキ: {run.deckCards.Count}枚";
            _artifactText.text = run.artifacts.Count > 0
                ? $"アーティファクト: {run.artifacts.Count}個"
                : "";
        }

        // ================================================================
        // Actions
        // ================================================================

        void OnStartRun()
        {
            string leaderId = CardDatabase.DefaultLeader?.id ?? "aldric";
            RoguelikeManager.StartNewRun(leaderId);
            RefreshUI();
            SoundManager.Instance?.PlayUISE("se_match_start");
        }

        void OnBattleStage()
        {
            var stage = RoguelikeManager.GetCurrentStage();
            if (stage == null) return;

            var run = RoguelikeManager.CurrentRun;
            Debug.Log($"[Roguelike] Battle stage {stage.stageIndex + 1} vs {stage.enemyName}");

            // プレイヤーデッキ（ランのカード群）
            var playerDeck = new List<CardData>();
            foreach (var cardId in run.deckCards)
            {
                if (CardDatabase.AllCards.TryGetValue(cardId, out var card))
                    playerDeck.Add(card);
            }

            // Botデッキ（敵アスペクトのカードIDリスト → CardData変換）
            var botDeckIds = AutoDeckBuilder.Build(stage.enemyAspect, null);
            var botDeck = new List<CardData>();
            foreach (var id in botDeckIds)
            {
                if (CardDatabase.AllCards.TryGetValue(id, out var card))
                    botDeck.Add(card);
            }

            // リーダー
            var playerLeader = CardDatabase.GetLeader(run.leaderId) ?? CardDatabase.DefaultLeader;
            LeaderData botLeader = CardDatabase.DefaultLeader;
            foreach (var kv in CardDatabase.AllLeaders)
            {
                if (kv.Value.keyAspect == stage.enemyAspect) { botLeader = kv.Value; break; }
            }

            // BotのDifficulty（ステージ難易度に連動）
            var botDiff = stage.difficulty switch
            {
                DifficultyTier.Easy => BotDifficulty.Easy,
                DifficultyTier.Boss => BotDifficulty.Hard,
                _ => BotDifficulty.Normal
            };

            // ヘッドレスバトル実行
            var engine = new BattleEngine();
            engine.InitMatch(playerLeader, botLeader, playerDeck, botDeck);
            engine.StartTurn();

            var playerAI = new SimpleAI(engine, PlayerSide.Player1, BotDifficulty.Normal);
            var enemyAI = new SimpleAI(engine, PlayerSide.Player2, botDiff);
            const int maxIterations = 200; // 安全弁

            for (int i = 0; i < maxIterations && !engine.State.isGameOver; i++)
            {
                if (engine.State.activePlayer == PlayerSide.Player1)
                    playerAI.PlayTurn();
                else
                    enemyAI.PlayTurn();

                if (!engine.State.isGameOver)
                    engine.EndTurn();
            }

            // 結果判定
            bool won = engine.State.result == MatchResult.Player1Win;
            // HP損失: 残りHP比率からローグライク用ダメージを算出
            int playerHpRemaining = engine.State.player1.hp;
            int maxHp = MatchModeConfig.Current.hp;
            int hpLost = maxHp > 0 ? Mathf.CeilToInt((1f - (float)playerHpRemaining / maxHp) * 10f) : 5;
            if (won) hpLost = Mathf.Max(0, hpLost / 2); // 勝利時はダメージ半減

            Debug.Log($"[Roguelike] Result: {(won ? "WIN" : "LOSE")}, hpLost={hpLost}, turns={engine.State.turnTotal}");

            RoguelikeManager.RecordStageResult(won, hpLost);

            if (won && RoguelikeManager.HasActiveRun && RoguelikeManager.CurrentRun.IsAlive)
                ShowRewardChoices();
            else
                RefreshUI();
        }

        void OnAbandon()
        {
            RoguelikeManager.AbandonRun();
            RefreshUI();
        }

        // ================================================================
        // Reward Selection
        // ================================================================

        void ShowRewardChoices()
        {
            var choices = RoguelikeManager.GenerateRewardChoices();
            if (choices.Count == 0)
            {
                RoguelikeManager.AdvanceToNextStage(new StageReward { type = RewardType.Gold, goldAmount = 0 });
                RefreshUI();
                return;
            }

            _rewardPanel.SetActive(true);
            _battleBtn.gameObject.SetActive(false);

            foreach (var item in _rewardItems)
                if (item != null) Destroy(item);
            _rewardItems.Clear();

            // Title
            var titleObj = CreateChild(_rewardPanel.transform, "RewardTitle");
            SetAnchored(titleObj.GetComponent<RectTransform>(), 0.05f, 0.8f, 0.95f, 0.95f);
            var titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "報酬を1つ選択";
            titleTmp.fontSize = 22;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color(1f, 0.85f, 0.4f);
            titleTmp.alignment = TextAlignmentOptions.Center;
            _rewardItems.Add(titleObj);

            float yStart = 0.65f;
            float itemHeight = 0.2f;

            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                float y1 = yStart - i * (itemHeight + 0.03f);
                float y0 = y1 - itemHeight;

                var itemObj = CreateChild(_rewardPanel.transform, $"Choice{i}");
                SetAnchored(itemObj.GetComponent<RectTransform>(), 0.05f, y0, 0.95f, y1);
                itemObj.AddComponent<Image>().color = new Color(0.18f, 0.16f, 0.25f);

                var btn = itemObj.AddComponent<Button>();
                var captured = choice;
                btn.onClick.AddListener(() => OnRewardChosen(captured));

                var infoObj = CreateChild(itemObj.transform, "Info");
                SetAnchored(infoObj.GetComponent<RectTransform>(), 0.05f, 0.1f, 0.95f, 0.9f);
                var infoTmp = infoObj.AddComponent<TextMeshProUGUI>();
                infoTmp.text = $"<b>{choice.displayName}</b>\n<size=14>{choice.description}</size>";
                infoTmp.fontSize = 18;
                infoTmp.color = Color.white;
                infoTmp.alignment = TextAlignmentOptions.Center;

                _rewardItems.Add(itemObj);
            }
        }

        void HideRewards()
        {
            foreach (var item in _rewardItems)
                if (item != null) Destroy(item);
            _rewardItems.Clear();
            if (_rewardPanel != null) _rewardPanel.SetActive(false);
        }

        void OnRewardChosen(StageReward reward)
        {
            RoguelikeManager.AdvanceToNextStage(reward);
            HideRewards();
            RefreshUI();
            SoundManager.Instance?.PlayUISE("se_reward_claim");
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
