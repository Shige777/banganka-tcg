using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Data;
using Banganka.Core.Battle;
using Banganka.Audio;

namespace Banganka.UI.Tutorial
{
    /// <summary>
    /// 12ステップ チュートリアル (TUTORIAL_FLOW.md)
    /// ナルが案内する固定デッキ・固定ドロー対戦
    /// HP=30、AI固定行動パターン
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] GameObject tutorialOverlay;
        [SerializeField] CompanionNal nal;
        [SerializeField] Button nextButton;
        [SerializeField] Button skipButton;
        [SerializeField] GameObject skipConfirmDialog;
        [SerializeField] TextMeshProUGUI stepIndicator;
        [SerializeField] GameObject highlightPrefab;
        [SerializeField] GameObject victoryBanner;

        [Header("Battle References")]
        [SerializeField] RectTransform hpGaugeArea;
        [SerializeField] RectTransform leaderPanel;
        [SerializeField] RectTransform handArea;
        [SerializeField] RectTransform fieldArea;
        [SerializeField] Button endTurnButton;

        int _currentStep;
        int _retryCount;
        bool _waitingForInput;
        bool _tutorialActive;
        const int TotalSteps = 12;
        const int TutorialHP = 30;

        BattleEngine _engine;

        // Fixed draw order (TUTORIAL_FLOW.md §3.1)
        static readonly string[] PlayerDrawOrder =
        {
            "MAN_CON_01", // 断章の剣士
            "SPL_WHI_01", // 囁きの誘導
            "ALG_01",     // 詠術増幅圏
            "MAN_CON_03", // 迅刃の追跡者
            "SPL_CON_01", // 断罪の号令
            "MAN_CON_02", // 誓鎧の衛士
            "SPL_CON_02", // 追撃の誓文
            "MAN_MAN_01", // 現世渡りの旅人
        };

        // AI固定デッキ用カードID (ブロッカー持ちの弱いユニット中心)
        static readonly string[] AIFixedDeckIds =
        {
            "MAN_WHI_01", "MAN_WHI_01", "MAN_WHI_01",
            "MAN_WHI_02", "MAN_WHI_02", "MAN_WHI_02",
            "MAN_WHI_03", "MAN_WHI_03", "MAN_WHI_03",
            "MAN_WEA_01", "MAN_WEA_01", "MAN_WEA_01",
            "MAN_WEA_02", "MAN_WEA_02", "MAN_WEA_02",
            "MAN_WEA_03", "MAN_WEA_03", "MAN_WEA_03",
            "SPL_WHI_01", "SPL_WHI_01", "SPL_WHI_01",
            "SPL_WEA_01", "SPL_WEA_01", "SPL_WEA_01",
            "SPL_WEA_02", "SPL_WEA_02", "SPL_WEA_02",
            "ALG_01", "ALG_01",
            "ALG_WHI_01", "ALG_WHI_01",
            "MAN_HUS_01", "MAN_HUS_01", "MAN_HUS_01",
        };

        public event Action OnTutorialCompleted;
        public event Action OnTutorialSkipped;

        public bool IsActive => _tutorialActive;
        public BattleEngine Engine => _engine;

        // ====================================================================
        // Tutorial Steps Data
        // ====================================================================

        struct TutorialStep
        {
            public string narDialogue;
            public string narExpression; // normal, explain, point, serious, smile, surprise
            public string highlightTarget; // null = none
            public bool waitForNext; // true = wait for "次へ" button
            public bool waitForAction; // true = wait for specific player action
            public string requiredAction; // action identifier
        }

        static readonly TutorialStep[] Steps =
        {
            // Step 1: 導入
            new()
            {
                narDialogue = "ようこそ、果求者さん。ここは交界——異なる時代が重なり合う場所。",
                narExpression = "normal",
                waitForNext = true,
            },
            // Step 2: バトル画面説明 (HP)
            new()
            {
                narDialogue = "上にあるのが HP ゲージ。あなたのHPが100から0になると負けちゃう。相手のHPを0にすれば勝ちだ。",
                narExpression = "point",
                highlightTarget = "hp_gauge",
                waitForNext = true,
            },
            // Step 3: 顕現の召喚
            new()
            {
                narDialogue = "まずは顕現を召喚してみよう。手札のカードを選んで、前列に置いてみて。",
                narExpression = "explain",
                highlightTarget = "hand_card_0",
                waitForAction = true,
                requiredAction = "play_manifest",
            },
            // Step 4: ターン終了
            new()
            {
                narDialogue = "召喚したターンは攻撃できない。これを召喚酔いって言うよ。ターンを終了しよう。",
                narExpression = "explain",
                highlightTarget = "end_turn",
                waitForAction = true,
                requiredAction = "end_turn",
            },
            // Step 5: 攻撃の宣言
            new()
            {
                narDialogue = "断章の剣士が待機状態に戻った。攻撃してみよう！相手の願主を直接狙うよ。",
                narExpression = "serious",
                highlightTarget = "field_unit_0",
                waitForAction = true,
                requiredAction = "declare_attack",
            },
            // Step 6: ブロックの説明
            new()
            {
                narDialogue = "おっと！相手がブロッカーで防いできた。ブロッカーを持つカードは攻撃を受け止められるんだ。",
                narExpression = "surprise",
                waitForNext = true,
            },
            // Step 7: 詠術の使用
            new()
            {
                narDialogue = "次は詠術カードを使ってみよう。詠術は使ったらすぐ効果が発動して消えるよ。",
                narExpression = "explain",
                highlightTarget = "hand_spell",
                waitForAction = true,
                requiredAction = "play_spell",
            },
            // Step 8: 願主への直撃
            new()
            {
                narDialogue = "相手の盤面が空だ。今なら願主に直撃できる！攻撃してみよう。",
                narExpression = "serious",
                highlightTarget = "field_unit_0",
                waitForAction = true,
                requiredAction = "direct_attack",
            },
            // Step 9: 界律の説明
            new()
            {
                narDialogue = "最後にひとつ。界律カードは盤面のルールを書き換える特別なカード。場に1枚だけ置けて、両者に影響するよ。",
                narExpression = "explain",
                highlightTarget = "hand_algorithm",
                waitForAction = true,
                requiredAction = "play_algorithm",
            },
            // Step 10: 願成の説明
            new()
            {
                narDialogue = "願主は願札を使うたびに成長する。願相が合うカードを使うとゲージが溜まって、レベルアップすると強くなるよ。",
                narExpression = "explain",
                highlightTarget = "leader_panel",
                waitForNext = true,
            },
            // Step 11: 勝利
            new()
            {
                narDialogue = "おめでとう！相手のHPが0になった。これが鯱鉾勝利だよ！",
                narExpression = "smile",
                waitForNext = true,
            },
            // Step 12: 完了
            new()
            {
                narDialogue = "これで基本はバッチリ。あとは実際の対戦で腕を磨こう。きっと色々な願いを持った相手と出会えるよ。",
                narExpression = "smile",
                waitForNext = true,
            },
        };

        // ====================================================================
        // Lifecycle
        // ====================================================================

        public void StartTutorial()
        {
            _tutorialActive = true;
            _currentStep = 0;
            _retryCount = 0;

            if (tutorialOverlay) tutorialOverlay.SetActive(true);
            if (skipConfirmDialog) skipConfirmDialog.SetActive(false);
            if (victoryBanner) victoryBanner.SetActive(false);

            InitTutorialBattle();

            SoundManager.Instance?.PlayBGM("bgm_tutorial");
            StartCoroutine(RunTutorial());
        }

        void InitTutorialBattle()
        {
            var playerLeader = CardDatabase.GetLeader("LDR_CON_01") ?? CardDatabase.DefaultLeader;
            var botLeader = CardDatabase.GetLeader("LDR_WHI_01") ?? CardDatabase.DefaultLeader;

            var playerDeck = CardDatabase.BuildDeck(CardDatabase.StarterDeckIds);
            var botDeck = CardDatabase.BuildDeck(AIFixedDeckIds.ToList());

            _engine = new BattleEngine(42); // Fixed seed for deterministic tutorial
            _engine.InitTutorialMatch(playerLeader, botLeader, playerDeck, botDeck,
                TutorialHP, PlayerDrawOrder);

            // Skip mulligan, start first turn immediately
            _engine.StartTurn();
        }

        IEnumerator RunTutorial()
        {
            for (_currentStep = 0; _currentStep < TotalSteps; _currentStep++)
            {
                var step = Steps[_currentStep];
                UpdateStepIndicator();

                // Show Nal dialogue
                if (nal != null)
                    nal.ShowDialogue(step.narDialogue, step.narExpression);

                // Highlight target
                ShowHighlight(step.highlightTarget);

                if (step.waitForNext)
                {
                    // Wait for "次へ" button
                    _waitingForInput = true;
                    if (nextButton) nextButton.gameObject.SetActive(true);
                    yield return new WaitUntil(() => !_waitingForInput);
                    if (nextButton) nextButton.gameObject.SetActive(false);
                }
                else if (step.waitForAction)
                {
                    // Wait for specific player action
                    _waitingForInput = true;
                    if (nextButton) nextButton.gameObject.SetActive(false);
                    yield return new WaitUntil(() => !_waitingForInput);
                }

                ClearHighlight();

                // AI actions between steps
                yield return HandleAIAction(_currentStep);

                // Post-step dialogue
                yield return HandlePostStep(_currentStep);
            }

            CompleteTutorial();
        }

        // ====================================================================
        // AI Fixed Actions (TUTORIAL_FLOW.md §3.2)
        // ====================================================================

        IEnumerator HandleAIAction(int stepIndex)
        {
            if (_engine == null) yield break;

            switch (stepIndex)
            {
                case 3: // After Step 4 (end turn) — AI turn: summon a blocker
                {
                    // AI's turn starts after player ends turn
                    // AI plays a card with Blocker keyword if possible
                    var aiState = _engine.State.player2;
                    for (int i = 0; i < aiState.hand.Count; i++)
                    {
                        var card = aiState.hand[i];
                        if (card.type == CardType.Manifest &&
                            card.keywords != null && card.keywords.Contains("Blocker") &&
                            _engine.CanPlayCard(PlayerSide.Player2, i))
                        {
                            _engine.PlayCard(PlayerSide.Player2, i);
                            break;
                        }
                    }

                    // If no blocker found, play any affordable manifest
                    if (aiState.field.Count == 0)
                    {
                        for (int i = 0; i < aiState.hand.Count; i++)
                        {
                            if (aiState.hand[i].type == CardType.Manifest &&
                                _engine.CanPlayCard(PlayerSide.Player2, i))
                            {
                                _engine.PlayCard(PlayerSide.Player2, i);
                                break;
                            }
                        }
                    }

                    if (nal != null)
                        nal.ShowDialogue("相手がブロッカーを召喚してきた…", "normal");
                    yield return new WaitForSeconds(1.5f);

                    // AI ends turn, back to player
                    _engine.EndTurn();
                    break;
                }

                case 5: // After Step 6 (block explanation) — resolve the block combat
                {
                    if (nal != null)
                        nal.ShowDialogue("戦力で上回ったから、相手のカードを倒せた！でも願主への直撃は防がれちゃったね。", "smile");
                    yield return new WaitForSeconds(2f);
                    break;
                }

                case 7: // After Step 8 (direct attack) — AI turn: do nothing, just end
                {
                    // AI's turn (if active) — pass without action
                    if (_engine.State.activePlayer == PlayerSide.Player2)
                        _engine.EndTurn();
                    break;
                }
            }
        }

        IEnumerator HandlePostStep(int stepIndex)
        {
            switch (stepIndex)
            {
                case 2: // After manifest summon
                    if (nal != null)
                        nal.ShowDialogue("よくできた！顕現は盤面に残って、次のターンから攻撃できるようになるよ。", "smile");
                    yield return new WaitForSeconds(1.5f);
                    break;

                case 6: // After spell use
                    if (nal != null)
                        nal.ShowDialogue("HPが少し回復した！詠術で直接HPに影響を与えたり、回復したりもできるんだ。", "smile");
                    yield return new WaitForSeconds(1.5f);
                    break;

                case 7: // After direct hit
                    if (nal != null)
                        nal.ShowDialogue("やった！願主に攻撃が通ると相手のHPを削ることができる。相手のHPを0にすれば勝ちだ。", "smile");
                    yield return new WaitForSeconds(1.5f);
                    break;

                case 8: // After algorithm play
                    if (nal != null)
                        nal.ShowDialogue("今は詠術の威力が上がった。自分が設置した界律なら、さらにボーナスが乗るんだ。でも、相手が新しい界律を出すと上書きされるから注意。", "explain");
                    yield return new WaitForSeconds(2f);
                    break;
            }
        }

        // ====================================================================
        // Player Input
        // ====================================================================

        public void OnNextButton()
        {
            if (_waitingForInput)
                _waitingForInput = false;
        }

        /// <summary>
        /// MatchControllerやUI側からプレイヤーの行動を通知する。
        /// チュートリアルの該当ステップで待っているアクションと一致すれば進行する。
        /// </summary>
        public void ReportAction(string action)
        {
            if (!_tutorialActive || !_waitingForInput) return;

            var step = Steps[_currentStep];
            if (step.waitForAction && step.requiredAction == action)
                _waitingForInput = false;
        }

        /// <summary>
        /// プレイヤーがカードをプレイした時にMatchControllerから呼ばれる。
        /// カードタイプに応じて適切なアクションを報告する。
        /// </summary>
        public void ReportCardPlayed(CardData card)
        {
            if (!_tutorialActive || card == null) return;

            switch (card.type)
            {
                case CardType.Manifest:
                    ReportAction("play_manifest");
                    break;
                case CardType.Spell:
                    ReportAction("play_spell");
                    break;
                case CardType.Algorithm:
                    ReportAction("play_algorithm");
                    break;
            }
        }

        /// <summary>
        /// プレイヤーが攻撃を宣言した時にMatchControllerから呼ばれる。
        /// </summary>
        public void ReportAttack(bool isDirectHit)
        {
            if (!_tutorialActive) return;

            if (isDirectHit)
                ReportAction("direct_attack");
            else
                ReportAction("declare_attack");
        }

        // ====================================================================
        // Highlight System
        // ====================================================================

        GameObject _activeHighlight;

        void ShowHighlight(string target)
        {
            ClearHighlight();
            if (string.IsNullOrEmpty(target) || highlightPrefab == null) return;

            RectTransform anchor = target switch
            {
                "hp_gauge" => hpGaugeArea,
                "leader_panel" => leaderPanel,
                "end_turn" => endTurnButton != null ? endTurnButton.GetComponent<RectTransform>() : null,
                _ when target.StartsWith("hand") => handArea,
                _ when target.StartsWith("field") => fieldArea,
                _ => null,
            };

            if (anchor == null) return;
            _activeHighlight = Instantiate(highlightPrefab, anchor);
        }

        void ClearHighlight()
        {
            if (_activeHighlight != null)
            {
                Destroy(_activeHighlight);
                _activeHighlight = null;
            }
        }

        // ====================================================================
        // Skip
        // ====================================================================

        public void OnSkipButton()
        {
            if (skipConfirmDialog) skipConfirmDialog.SetActive(true);
        }

        public void OnConfirmSkip()
        {
            _tutorialActive = false;
            StopAllCoroutines();
            ClearHighlight();
            if (tutorialOverlay) tutorialOverlay.SetActive(false);
            if (nal != null) nal.Hide();
            OnTutorialSkipped?.Invoke();
        }

        public void OnCancelSkip()
        {
            if (skipConfirmDialog) skipConfirmDialog.SetActive(false);
        }

        // ====================================================================
        // Failure Handling (TUTORIAL_FLOW.md §5)
        // ====================================================================

        public void OnPlayerDefeated()
        {
            _retryCount++;
            if (_retryCount <= 3)
            {
                if (nal != null)
                    nal.ShowDialogue("大丈夫、もう一回やってみよう！", "smile");
                // Restart tutorial battle from scratch
                StopAllCoroutines();
                InitTutorialBattle();
                _currentStep = 0;
                StartCoroutine(RunTutorial());
            }
            else
            {
                // Force victory after too many failures
                if (nal != null)
                    nal.ShowDialogue("今回は特別に…勝利にしておくね！", "smile");
                if (_engine != null)
                    _engine.State.player2.hp = 0;
                _currentStep = 10; // Jump to victory step
                _waitingForInput = false;
            }
        }

        // ====================================================================
        // Completion
        // ====================================================================

        void CompleteTutorial()
        {
            _tutorialActive = false;
            PlayerData.Instance.tutorialCompleted = true;
            PlayerData.Save();

            if (victoryBanner) victoryBanner.SetActive(true);
            if (tutorialOverlay) tutorialOverlay.SetActive(false);

            SoundManager.Instance?.PlayBGM("bgm_victory");
            OnTutorialCompleted?.Invoke();
        }

        void UpdateStepIndicator()
        {
            if (stepIndicator)
                stepIndicator.text = $"{_currentStep + 1}/{TotalSteps}";
        }
    }
}
