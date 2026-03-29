using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using Banganka.Core.Battle;
using Banganka.Core.Data;
using Banganka.Core.Replay;
using Banganka.Game;
using Banganka.UI.Common;
using Banganka.Core.Config;
using Banganka.Core.Game;
using Banganka.Audio;
using UnityEngine.EventSystems;
using System.Collections;

namespace Banganka.UI.Battle
{
    public class MatchController : MonoBehaviour
    {
        [SerializeField] GaugeView gaugeView;
        [SerializeField] BattleAnimator animator;

        [SerializeField] TextMeshProUGUI p1LeaderNameText;
        [SerializeField] TextMeshProUGUI p1LeaderPowerText;
        [SerializeField] TextMeshProUGUI p1LeaderEvoText;
        [SerializeField] TextMeshProUGUI p1CpText;
        [SerializeField] Transform p1FieldParent;
        [SerializeField] Transform p1FieldFront;
        [SerializeField] Transform p1FieldBack;
        [SerializeField] Transform p1HandParent;

        [SerializeField] TextMeshProUGUI p2LeaderNameText;
        [SerializeField] TextMeshProUGUI p2LeaderPowerText;
        [SerializeField] TextMeshProUGUI p2LeaderEvoText;
        [SerializeField] TextMeshProUGUI p2CpText;
        [SerializeField] Transform p2FieldParent;
        [SerializeField] Transform p2FieldFront;
        [SerializeField] Transform p2FieldBack;

        [SerializeField] TextMeshProUGUI sharedAlgoText;
        [SerializeField] TextMeshProUGUI turnText;
        [SerializeField] TextMeshProUGUI activePlayerText;
        [SerializeField] TextMeshProUGUI phaseText;
        [SerializeField] Button endTurnButton;
        [SerializeField] Button surrenderButton;
        [SerializeField] GameObject logPanel;
        [SerializeField] Button logToggleButton;
        [SerializeField] TextMeshProUGUI logText;
        [SerializeField] TextMeshProUGUI turnAnnounceText;
        [SerializeField] TurnClock turnClock;

        [SerializeField] GameObject resultPanel;
        [SerializeField] TextMeshProUGUI resultText;
        [SerializeField] TextMeshProUGUI victoryTypeText;
        [SerializeField] TextMeshProUGUI matchIdText;
        [SerializeField] TextMeshProUGUI finalHpText;
        [SerializeField] TextMeshProUGUI rewardGoldText;
        [SerializeField] TextMeshProUGUI rewardBpXpText;
        [SerializeField] TextMeshProUGUI rewardMissionText;
        [SerializeField] Image resultP1HpBar;
        [SerializeField] Image resultP2HpBar;
        [SerializeField] Button backToLobbyButton;
        [SerializeField] Button playAgainButton;
        [SerializeField] Button viewReplayButton;
        [SerializeField] ScreenManager screenManager;

        // Leader Cutin
        [SerializeField] LeaderCutinController cutinController;

        // 3D Card Inspector + Algorithm Field Effect
        [SerializeField] Card3DInspector card3DInspector;
        [SerializeField] AlgorithmFieldEffect algoFieldEffect;

        // Turn Timer
        [SerializeField] TextMeshProUGUI timerText;

        // HP number on gauge bar
        [SerializeField] TextMeshProUGUI p1HpText;
        [SerializeField] TextMeshProUGUI p2HpText;

        // Leader Skill Buttons
        [SerializeField] Button skillLv2Button;
        [SerializeField] Button skillLv3Button;

        // Mulligan
        [SerializeField] GameObject mulliganPanel;
        [SerializeField] Transform mulliganHandParent;
        [SerializeField] Button mulliganConfirmButton;
        [SerializeField] Button mulliganSkipButton;

        BattleEngine _engine;
        ReplayRecorder _replayRecorder;
        readonly List<GameObject> _fieldInstances = new();
        readonly List<GameObject> _handInstances = new();

        // Leader illustration cache
        readonly Dictionary<string, Sprite> _leaderSpriteCache = new();

        // Turn timer state
        float _turnTimer;
        int _lastKnownTurn;
        PlayerSide _lastKnownActivePlayer;

        enum InteractionMode { None, SelectingTarget, SelectingBlocker, SelectingAmbush }
        InteractionMode _mode = InteractionMode.None;
        BattleEngine.AttackDeclaration _pendingAttack;
        PlayerSide _pendingAttackSide;

        // Blocker selection state
        Action<string> _blockerCallback;

        // Ambush state
        string _ambushTrigger;
        Action<int> _ambushCallback;

        // Mulligan state
        bool _mulliganPhase;
        readonly HashSet<int> _mulliganSelected = new();
        readonly List<GameObject> _mulliganHandInstances = new();

        void OnEnable()
        {
            if (resultPanel) resultPanel.SetActive(false);
            _engine = GameManager.Instance?.BattleEngine;
            if (_engine == null) return;

            // Start replay recording
            _replayRecorder = new ReplayRecorder();
            var pd = PlayerData.Instance;
            var botPd = new PlayerData { uid = "bot", displayName = "AI" };
            var gm = GameManager.Instance;
            var playerLeaderId = pd.decks?.Find(d => d.deckId == pd.selectedDeckId)?.leaderId ?? "LEADER_CONTEST";
            var botLeaderId = gm?.BotAI != null ? "LEADER_BOT" : "LEADER_CONTEST";
            _replayRecorder.StartRecording(_engine, System.Guid.NewGuid().ToString("N")[..8],
                pd, botPd, playerLeaderId, botLeaderId,
                _engine.State.player1.deck, _engine.State.player2.deck);

            _engine.OnStateChanged += RefreshUI;
            _engine.OnMatchEnd += OnMatchEnd;
            _engine.OnLog += OnLogEntry;
            _engine.OnCardPlayed += OnCardPlayedAnim;
            _engine.OnAttackResolved += OnAttackResolvedAnim;
            _engine.OnUnitDestroyed += OnUnitDestroyedAnim;
            _engine.OnHpDamaged += OnHpDamagedAnim;
            _engine.OnLeaderLevelUp += OnLeaderLevelUpAnim;
            _engine.OnWishTrigger += OnWishTriggerAnim;

            // Auto-create animator if not assigned
            if (animator == null)
                animator = gameObject.AddComponent<BattleAnimator>();

            // Auto-create cutin controller if not assigned
            if (cutinController == null)
                cutinController = gameObject.AddComponent<LeaderCutinController>();

            // Auto-create 3D card inspector
            if (card3DInspector == null)
                card3DInspector = gameObject.AddComponent<Card3DInspector>();

            // Auto-create algorithm field effect
            if (algoFieldEffect == null)
            {
                algoFieldEffect = gameObject.AddComponent<AlgorithmFieldEffect>();
                // Use p1FieldParent as the field area for the effect
                if (p1FieldParent != null)
                    algoFieldEffect.SetFieldArea(p1FieldParent.GetComponent<RectTransform>());
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveAllListeners();
                endTurnButton.onClick.AddListener(OnEndTurn);
            }
            if (surrenderButton != null)
            {
                surrenderButton.onClick.RemoveAllListeners();
                surrenderButton.onClick.AddListener(OnSurrender);
            }
            // Attack buttons removed — attacks use drag-and-drop (DragAttackHandler / DropAttackTarget)

            if (logToggleButton != null)
            {
                logToggleButton.onClick.RemoveAllListeners();
                logToggleButton.onClick.AddListener(() =>
                {
                    if (logPanel != null) logPanel.SetActive(!logPanel.activeSelf);
                });
            }
            if (logPanel != null) logPanel.SetActive(false);

            var declineBtn = FindInParent("DeclineBlockButton");
            if (declineBtn != null)
            {
                var btn = declineBtn.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(OnDeclineBlock);
                }
            }

            // Leader skill buttons
            if (skillLv2Button != null)
            {
                skillLv2Button.onClick.RemoveAllListeners();
                skillLv2Button.onClick.AddListener(() => OnUseLeaderSkill(2));
            }
            if (skillLv3Button != null)
            {
                skillLv3Button.onClick.RemoveAllListeners();
                skillLv3Button.onClick.AddListener(() => OnUseLeaderSkill(3));
            }

            // Mulligan buttons
            if (mulliganConfirmButton != null)
            {
                mulliganConfirmButton.onClick.RemoveAllListeners();
                mulliganConfirmButton.onClick.AddListener(OnMulliganConfirm);
            }
            if (mulliganSkipButton != null)
            {
                mulliganSkipButton.onClick.RemoveAllListeners();
                mulliganSkipButton.onClick.AddListener(OnMulliganSkip);
            }

            // Initialize turn timer
            _turnTimer = BalanceConfig.TurnTimerSeconds;
            _lastKnownTurn = 0;
            _lastKnownActivePlayer = PlayerSide.Player1;

            // Start mulligan phase
            _mulliganPhase = true;
            _mulliganSelected.Clear();
            ShowMulliganPanel();
        }

        void OnDisable()
        {
            if (_replayRecorder != null && _replayRecorder.IsRecording)
                _replayRecorder.CancelRecording();
            _replayRecorder = null;

            if (_engine != null)
            {
                _engine.OnStateChanged -= RefreshUI;
                _engine.OnMatchEnd -= OnMatchEnd;
                _engine.OnLog -= OnLogEntry;
                _engine.OnCardPlayed -= OnCardPlayedAnim;
                _engine.OnAttackResolved -= OnAttackResolvedAnim;
                _engine.OnUnitDestroyed -= OnUnitDestroyedAnim;
                _engine.OnHpDamaged -= OnHpDamagedAnim;
                _engine.OnLeaderLevelUp -= OnLeaderLevelUpAnim;
                _engine.OnWishTrigger -= OnWishTriggerAnim;
            }
        }

        void Update()
        {
            if (_engine == null || _mulliganPhase) return;
            var s = _engine.State;
            if (s.isGameOver) return;

            // Detect turn change and reset timer
            if (s.turnTotal != _lastKnownTurn || s.activePlayer != _lastKnownActivePlayer)
            {
                _turnTimer = BalanceConfig.TurnTimerSeconds;
                _lastKnownTurn = s.turnTotal;
                _lastKnownActivePlayer = s.activePlayer;
                if (turnClock) turnClock.ResetTimerWarnings();
            }

            // Only count down for the active player's turn
            _turnTimer -= Time.deltaTime;

            // Update timer display
            UpdateTimerDisplay();

            // Feed remaining time to TurnClock for urgency effects
            if (turnClock)
                turnClock.SetRemainingSeconds(_turnTimer, BalanceConfig.TurnTimerSeconds);

            // Handle timeout
            if (_turnTimer <= 0f)
            {
                _turnTimer = 0f;
                _engine.HandleTurnTimeout(s.activePlayer);
            }
        }

        void UpdateTimerDisplay()
        {
            if (timerText == null) return;

            int seconds = Mathf.CeilToInt(Mathf.Max(0f, _turnTimer));
            timerText.text = $"{seconds}";

            if (_turnTimer > 30f)
                timerText.color = Color.white;
            else if (_turnTimer > 10f)
                timerText.color = Color.yellow;
            else
                timerText.color = Color.red;
        }

        GameObject FindInParent(string name)
        {
            var parent = transform;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name)
                    return parent.GetChild(i).gameObject;
            }
            return null;
        }

        // ==================== MULLIGAN ====================
        void ShowMulliganPanel()
        {
            if (mulliganPanel == null)
            {
                FinishMulligan();
                return;
            }
            mulliganPanel.SetActive(true);
            RefreshMulliganHand();
        }

        void RefreshMulliganHand()
        {
            if (mulliganHandParent == null) return;

            foreach (var inst in _mulliganHandInstances)
                if (inst) Destroy(inst);
            _mulliganHandInstances.Clear();

            var hand = _engine.State.player1.hand;
            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                var obj = CreateHandCardUI(mulliganHandParent, card);
                _mulliganHandInstances.Add(obj);

                var btn = obj.AddComponent<Button>();
                int idx = i;
                bool selected = _mulliganSelected.Contains(idx);

                var bg = obj.GetComponent<Image>();
                if (bg != null)
                    bg.color = selected
                        ? new Color(0.4f, 0.12f, 0.12f, 0.95f)
                        : new Color(0.12f, 0.14f, 0.2f, 0.95f);

                // Add selection indicator
                if (selected)
                {
                    var indicator = new GameObject("Selected");
                    indicator.transform.SetParent(obj.transform, false);
                    var irt = indicator.AddComponent<RectTransform>();
                    irt.anchorMin = new Vector2(0.3f, 0.02f);
                    irt.anchorMax = new Vector2(0.7f, 0.12f);
                    irt.offsetMin = Vector2.zero;
                    irt.offsetMax = Vector2.zero;
                    var ibg = indicator.AddComponent<Image>();
                    ibg.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
                    var itmp = indicator.AddComponent<TextMeshProUGUI>();
                    itmp.text = "戻す";
                    itmp.fontSize = 12;
                    itmp.fontStyle = FontStyles.Bold;
                    itmp.alignment = TextAlignmentOptions.Center;
                }

                btn.onClick.AddListener(() => ToggleMulliganCard(idx));
            }
        }

        void ToggleMulliganCard(int index)
        {
            if (_mulliganSelected.Contains(index))
                _mulliganSelected.Remove(index);
            else
                _mulliganSelected.Add(index);
            RefreshMulliganHand();
        }

        void OnMulliganConfirm()
        {
            if (_mulliganSelected.Count > 0)
                _engine.PerformMulligan(PlayerSide.Player1, _mulliganSelected.ToList());
            FinishMulligan();
        }

        void OnMulliganSkip()
        {
            FinishMulligan();
        }

        void FinishMulligan()
        {
            _mulliganPhase = false;
            _mulliganSelected.Clear();
            foreach (var inst in _mulliganHandInstances)
                if (inst) Destroy(inst);
            _mulliganHandInstances.Clear();
            if (mulliganPanel) mulliganPanel.SetActive(false);

            // AI skips mulligan for simplicity
            _engine.StartTurn();
            RefreshUI();
        }

        // ==================== BLOCKER SELECTION ====================
        public void ShowBlockerSelection(BattleEngine.AttackDeclaration attack, PlayerSide attackerSide, Action<string> callback)
        {
            _pendingAttack = attack;
            _pendingAttackSide = attackerSide;
            _blockerCallback = callback;
            _mode = InteractionMode.SelectingBlocker;
            RefreshUI();
        }

        void OnSelectBlocker(FieldUnit blockerUnit)
        {
            _mode = InteractionMode.None;
            var cb = _blockerCallback;
            _blockerCallback = null;
            cb?.Invoke(blockerUnit.instanceId);
            RefreshUI();
        }

        void OnDeclineBlock()
        {
            _mode = InteractionMode.None;
            var cb = _blockerCallback;
            _blockerCallback = null;
            cb?.Invoke(null);
            RefreshUI();
        }

        // ==================== AMBUSH SELECTION ====================
        public void ShowAmbushSelection(string trigger, Action<int> callback)
        {
            _ambushTrigger = trigger;
            _ambushCallback = callback;
            _mode = InteractionMode.SelectingAmbush;
            RefreshUI();
        }

        void OnSelectAmbushCard(int handIndex)
        {
            _mode = InteractionMode.None;
            var cb = _ambushCallback;
            _ambushCallback = null;
            cb?.Invoke(handIndex);
            RefreshUI();
        }

        void OnDeclineAmbush()
        {
            _mode = InteractionMode.None;
            var cb = _ambushCallback;
            _ambushCallback = null;
            cb?.Invoke(-1);
            RefreshUI();
        }

        // ==================== UI REFRESH ====================
        void RefreshUI()
        {
            if (_engine == null || _mulliganPhase) return;
            var s = _engine.State;

            if (gaugeView) gaugeView.UpdateHP(s.player1, s.player2);

            // Dynamic battle BGM based on HP state
            SoundManager.Instance?.SetBattleHPState(s.player1.hp, s.player2.hp, s.player1.maxHp);

            if (turnText) turnText.text = $"T{s.turnTotal}";
            if (turnClock) turnClock.SetTurn(s.turnTotal);

            // Turn announcement (fade in/out on turn change)
            if (s.turnTotal != _lastKnownTurn || s.activePlayer != _lastKnownActivePlayer)
            {
                _lastKnownTurn = s.turnTotal;
                _lastKnownActivePlayer = s.activePlayer;
                string who = s.activePlayer == PlayerSide.Player1 ? "あなたのターン" : "相手のターン";
                ShowTurnAnnouncement(who);
            }

            // Keep internal references updated (hidden text)
            if (activePlayerText) activePlayerText.text = "";

            if (phaseText)
            {
                phaseText.text = _mode switch
                {
                    InteractionMode.SelectingTarget => "攻撃対象を選択",
                    InteractionMode.SelectingBlocker => "ブロッカーを選択",
                    InteractionMode.SelectingAmbush => "奇襲カードを選択",
                    _ => s.currentPhase.ToString()
                };
            }

            UpdateLeader(s.player1, p1LeaderNameText, p1LeaderPowerText, p1LeaderEvoText, p1CpText);
            UpdateLeader(s.player2, p2LeaderNameText, p2LeaderPowerText, p2LeaderEvoText, p2CpText);

            if (sharedAlgoText)
            {
                if (!MechanicUnlockManager.IsUnlocked(MechanicType.Algorithm))
                {
                    sharedAlgoText.text = $"界律: あと{MechanicUnlockManager.GamesUntilNextUnlock()}戦でアンロック";
                    sharedAlgoText.color = new Color(0.4f, 0.4f, 0.5f);
                }
                else if (s.sharedAlgo != null)
                    sharedAlgoText.text = $"界律: {s.sharedAlgo.cardData.cardName} ({(s.sharedAlgo.owner == PlayerSide.Player1 ? "P1" : "P2")})";
                else
                    sharedAlgoText.text = "界律: なし";
            }

            RefreshField(s.player1, p1FieldFront, p1FieldBack, PlayerSide.Player1);
            RefreshField(s.player2, p2FieldFront, p2FieldBack, PlayerSide.Player2);
            RefreshHand();

            if (endTurnButton)
                endTurnButton.interactable = !s.isGameOver && s.activePlayer == PlayerSide.Player1
                                             && _mode == InteractionMode.None;
            // Leader drag-attack: update P1 leader panel's DragAttackHandler
            var p1LeaderObj = FindInParent("P1LeaderPanel");
            if (p1LeaderObj != null)
            {
                var drag = p1LeaderObj.GetComponent<DragAttackHandler>();
                if (drag == null) drag = p1LeaderObj.AddComponent<DragAttackHandler>();
                drag.attackerId = "leader";
                drag.isLeader = true;
                drag.canDrag = s.activePlayer == PlayerSide.Player1 && s.player1.leader.CanAttack
                               && !s.isGameOver && _mode == InteractionMode.None;
            }

            // P2 leader as drop target
            var p2LeaderObj = FindInParent("P2LeaderPanel");
            if (p2LeaderObj != null)
            {
                var drop = p2LeaderObj.GetComponent<DropAttackTarget>();
                if (drop == null) drop = p2LeaderObj.AddComponent<DropAttackTarget>();
                drop.targetId = "leader";
                drop.isLeader = true;
                drop.onAttackReceived = OnDragAttack;
            }

            var declineBtnObj = FindInParent("DeclineBlockButton");
            if (declineBtnObj != null)
                declineBtnObj.SetActive(_mode == InteractionMode.SelectingBlocker || _mode == InteractionMode.SelectingAmbush);

            RefreshLeaderSkillButtons();
        }

        void RefreshLeaderSkillButtons()
        {
            var s = _engine.State;
            var leaderData = s.player1.leader.baseData;

            if (skillLv2Button != null)
            {
                bool canUse = _engine.CanUseLeaderSkill(PlayerSide.Player1, 2)
                              && _mode == InteractionMode.None;
                skillLv2Button.gameObject.SetActive(canUse);
                if (canUse && leaderData.leaderSkills != null && leaderData.leaderSkills.Length > 0)
                {
                    var label = skillLv2Button.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null)
                        label.text = leaderData.leaderSkills[0].name;
                }
            }

            if (skillLv3Button != null)
            {
                bool canUse = _engine.CanUseLeaderSkill(PlayerSide.Player1, 3)
                              && _mode == InteractionMode.None;
                skillLv3Button.gameObject.SetActive(canUse);
                if (canUse && leaderData.leaderSkills != null && leaderData.leaderSkills.Length > 1)
                {
                    var label = skillLv3Button.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null)
                        label.text = leaderData.leaderSkills[1].name;
                }
            }
        }

        void UpdateLeader(PlayerState p, TextMeshProUGUI nameT, TextMeshProUGUI powT, TextMeshProUGUI evoT, TextMeshProUGUI cpT)
        {
            if (nameT) nameT.text = $"Lv{p.leader.level}{(p.isFinal ? " [瀕死]" : "")}";
            if (powT) powT.text = $"{p.leader.currentPower} / {p.leader.currentWishDamage}";
            if (evoT)
            {
                if (!MechanicUnlockManager.IsUnlocked(MechanicType.LeaderEvo))
                    evoT.text = "";
                else if (p.leader.level < p.leader.baseData.levelCap)
                    evoT.text = $"EVO {p.leader.evoGauge}/{p.leader.EvoGaugeMax}";
                else
                    evoT.text = "EVO MAX";
            }
            if (cpT) cpT.text = $"CP {p.currentCP}/{p.maxCP}";

            // HP number on gauge bar
            var hpT = (nameT == p1LeaderNameText) ? p1HpText : p2HpText;
            if (hpT) hpT.text = $"{p.hp}/{p.maxHp}";

            // Set leader portrait
            if (nameT != null)
            {
                var panel = nameT.transform.parent;
                Transform portraitObj = null;
                foreach (Transform child in panel)
                {
                    if (child.name.Contains("Portrait"))
                    {
                        portraitObj = child;
                        break;
                    }
                }
                if (portraitObj != null)
                {
                    var img = portraitObj.GetComponent<Image>();
                    if (img != null)
                    {
                        var sprite = LoadLeaderSprite(p.leader.baseData.id);
                        if (sprite != null)
                        {
                            img.sprite = sprite;
                            img.color = Color.white;
                        }
                        else
                        {
                            Debug.LogWarning($"[MatchController] Leader sprite not found for {p.leader.baseData.id}");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[MatchController] Portrait object not found in panel {panel.name}");
                }
            }
        }

        Sprite LoadLeaderSprite(string leaderId)
        {
            if (_leaderSpriteCache.TryGetValue(leaderId, out var cached)) return cached;
            string key = leaderId.Replace("LDR_", "").ToLower() switch
            {
                "con_01" => "aldric",
                "whi_01" => "vael",
                "wea_01" => "hinagi",
                "ver_01" => "amara",
                "man_01" => "rahim",
                "hus_01" => "suihou",
                _ => null
            };
            if (key == null) return null;
            var sprite = Resources.Load<Sprite>($"CardIllustrations/Leaders/{key}");
            _leaderSpriteCache[leaderId] = sprite;
            return sprite;
        }

        Sprite LoadCardIllustration(string illustrationId)
        {
            if (string.IsNullOrEmpty(illustrationId)) return null;
            return Resources.Load<Sprite>($"CardIllustrations/{illustrationId}");
        }

        void RefreshField(PlayerState p, Transform frontParent, Transform backParent, PlayerSide side)
        {
            ClearFieldRow(frontParent);
            ClearFieldRow(backParent);

            for (int i = 0; i < p.field.Count; i++)
            {
                var unit = p.field[i];
                Transform row = unit.row == FieldRow.Front ? frontParent : backParent;
                if (row == null) continue;

                var obj = CreateFieldUnitUI(row, unit);
                _fieldInstances.Add(obj);

                var bg = obj.GetComponent<Image>();

                if (_mode == InteractionMode.SelectingBlocker && side == PlayerSide.Player1 && unit.CanBlock)
                {
                    // Blocker selection still uses tap
                    var btn = obj.AddComponent<Button>();
                    var captured = unit;
                    btn.onClick.AddListener(() => OnSelectBlocker(captured));
                    btn.interactable = true;
                    if (bg) bg.color = new Color(0.15f, 0.15f, 0.4f, 0.9f);
                }
                else if (side == PlayerSide.Player1 && _mode == InteractionMode.None)
                {
                    // P1 units: drag to attack
                    var drag = obj.AddComponent<DragAttackHandler>();
                    drag.attackerId = unit.instanceId;
                    drag.isLeader = false;
                    drag.canDrag = _engine.State.activePlayer == PlayerSide.Player1
                                   && unit.CanAttack && !_engine.State.isGameOver;
                    if (drag.canDrag && bg) bg.color = new Color(0.15f, 0.2f, 0.15f, 0.9f);
                }
                else if (side == PlayerSide.Player2)
                {
                    // P2 units: drop target for attacks
                    var drop = obj.AddComponent<DropAttackTarget>();
                    drop.targetId = unit.instanceId;
                    drop.isLeader = false;
                    drop.onAttackReceived = OnDragAttack;
                    if (bg) bg.color = new Color(0.4f, 0.15f, 0.15f, 0.9f);
                }
            }
        }

        void ClearFieldRow(Transform parent)
        {
            if (parent == null) return;
            for (int i = _fieldInstances.Count - 1; i >= 0; i--)
            {
                if (_fieldInstances[i] != null && _fieldInstances[i].transform.parent == parent)
                {
                    Destroy(_fieldInstances[i]);
                    _fieldInstances.RemoveAt(i);
                }
            }
        }

        GameObject CreateFieldUnitUI(Transform parent, FieldUnit unit)
        {
            var obj = new GameObject(unit.cardData.cardName);
            obj.transform.SetParent(parent, false);

            obj.AddComponent<RectTransform>();
            var bg = obj.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

            Color ac = AspectColors.GetColor(unit.cardData.aspect);
            var accentObj = new GameObject("Accent");
            accentObj.transform.SetParent(obj.transform, false);
            var acRt = accentObj.AddComponent<RectTransform>();
            acRt.anchorMin = new Vector2(0, 0.9f);
            acRt.anchorMax = new Vector2(1, 1f);
            acRt.offsetMin = Vector2.zero;
            acRt.offsetMax = Vector2.zero;
            var acImg = accentObj.AddComponent<Image>();
            acImg.color = ac;

            // Field unit illustration
            if (!string.IsNullOrEmpty(unit.cardData.illustrationId))
            {
                var illSprite = LoadCardIllustration(unit.cardData.illustrationId);
                if (illSprite != null)
                {
                    var illObj = new GameObject("Illustration");
                    illObj.transform.SetParent(obj.transform, false);
                    illObj.transform.SetAsFirstSibling();
                    var illRt = illObj.AddComponent<RectTransform>();
                    illRt.anchorMin = Vector2.zero;
                    illRt.anchorMax = Vector2.one;
                    illRt.offsetMin = Vector2.zero;
                    illRt.offsetMax = Vector2.zero;
                    var illImg = illObj.AddComponent<Image>();
                    illImg.sprite = illSprite;
                    illImg.preserveAspect = true;
                    illImg.color = Color.white;
                    illImg.raycastTarget = false;
                }
            }

            MakeTMP(obj, "Name", unit.cardData.cardName, 13,
                0f, 0.6f, 1f, 0.9f, Color.white);

            MakeTMP(obj, "Stats", $"戦力{unit.currentPower} 願撃{unit.currentWishDamage}", 11,
                0f, 0.3f, 1f, 0.6f, new Color(0.9f, 0.85f, 0.5f));

            string st = unit.status == UnitStatus.Exhausted ? "消耗" : "待機";
            if (unit.summonSick) st += " 酔";
            string kw = unit.currentKeywords.Count > 0 ? " [" + string.Join(",", unit.currentKeywords) + "]" : "";
            MakeTMP(obj, "Status", st + kw, 10,
                0f, 0f, 1f, 0.3f, new Color(0.6f, 0.6f, 0.7f));

            return obj;
        }

        void RefreshHand()
        {
            if (p1HandParent == null) return;

            foreach (var inst in _handInstances)
                if (inst) Destroy(inst);
            _handInstances.Clear();

            bool isAmbushMode = _mode == InteractionMode.SelectingAmbush;

            if (!isAmbushMode)
            {
                if (_engine.State.activePlayer != PlayerSide.Player1) return;
                if (_mode != InteractionMode.None) return;
            }

            var p = _engine.State.player1;

            for (int i = 0; i < p.hand.Count; i++)
            {
                var card = p.hand[i];
                var obj = CreateHandCardUI(p1HandParent, card);
                _handInstances.Add(obj);

                var btn = obj.AddComponent<Button>();
                int idx = i;
                var bg = obj.GetComponent<Image>();

                if (isAmbushMode)
                {
                    bool canAmbush = _engine.CanPlayAmbush(PlayerSide.Player1, idx, _ambushTrigger);
                    btn.interactable = canAmbush;
                    if (bg != null)
                        bg.color = canAmbush ? new Color(0.3f, 0.12f, 0.12f, 0.95f) : new Color(0.08f, 0.08f, 0.1f, 0.7f);
                    if (canAmbush)
                        btn.onClick.AddListener(() => OnSelectAmbushCard(idx));
                }
                else
                {
                    bool mechanicLocked = MechanicUnlockManager.IsCardTypeLocked(card.type);
                    bool canPlay = !mechanicLocked && _engine.CanPlayCard(PlayerSide.Player1, idx);
                    btn.interactable = canPlay;
                    if (bg != null)
                        bg.color = mechanicLocked ? new Color(0.15f, 0.15f, 0.15f, 0.5f)
                            : canPlay ? new Color(0.12f, 0.14f, 0.2f, 0.95f)
                            : new Color(0.08f, 0.08f, 0.1f, 0.7f);
                    if (canPlay)
                        btn.onClick.AddListener(() => { _engine.PlayCard(PlayerSide.Player1, idx); });
                }

                // Long-press: open 3D card inspector
                AddLongPressInspect(obj, card);
            }
        }

        GameObject CreateHandCardUI(Transform parent, CardData card)
        {
            var obj = new GameObject(card.cardName);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            var bg = obj.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

            Color ac = AspectColors.GetColor(card.aspect);

            var costObj = new GameObject("CostBg");
            costObj.transform.SetParent(obj.transform, false);
            var costRt = costObj.AddComponent<RectTransform>();
            costRt.anchorMin = new Vector2(0, 0.85f);
            costRt.anchorMax = new Vector2(0.22f, 1f);
            costRt.offsetMin = Vector2.zero;
            costRt.offsetMax = Vector2.zero;
            var costBg = costObj.AddComponent<Image>();
            costBg.color = ac;
            MakeTMP(costObj, "Cost", card.cpCost.ToString(), 18, 0, 0, 1, 1, Color.white, true);

            MakeTMP(obj, "Name", card.cardName, 13,
                0.24f, 0.85f, 1f, 1f, Color.white, false, TextAlignmentOptions.MidlineLeft);

            string typeStr = card.type switch
            {
                CardType.Manifest => "顕現",
                CardType.Spell => "詠術",
                CardType.Algorithm => "界律",
                _ => ""
            };
            MakeTMP(obj, "Type", $"{typeStr} [{AspectColors.GetDisplayName(card.aspect)}]", 10,
                0.02f, 0.72f, 0.98f, 0.85f, new Color(0.7f, 0.7f, 0.8f));

            if (card.type == CardType.Manifest)
            {
                MakeTMP(obj, "Power", $"戦力 {card.battlePower}", 12,
                    0.02f, 0.52f, 0.5f, 0.72f, Color.white);
                MakeTMP(obj, "WishDmg", $"願撃 {card.wishDamage}", 12,
                    0.5f, 0.52f, 0.98f, 0.72f, new Color(1f, 0.85f, 0.4f));
                if (card.keywords != null && card.keywords.Length > 0)
                    MakeTMP(obj, "KW", string.Join(" ", card.keywords), 10,
                        0.02f, 0.36f, 0.98f, 0.52f, new Color(0.4f, 0.8f, 1f));
            }
            else if (card.type == CardType.Spell)
            {
                string desc = card.effectKey switch
                {
                    "SPELL_PUSH_SMALL" => $"願力+{card.baseGaugeDelta}",
                    "SPELL_PUSH_MEDIUM" => $"願力+{card.baseGaugeDelta}",
                    "SPELL_POWER_PLUS" => $"味方戦力+{card.powerDelta}",
                    "SPELL_WISHDMG_PLUS" => $"味方願撃+{card.wishDamageDelta}",
                    "SPELL_REST" => $"敵{card.restTargets}体消耗",
                    "SPELL_REMOVE_DAMAGED" => "弱小除去",
                    _ => card.effectKey
                };
                MakeTMP(obj, "Effect", desc, 11,
                    0.02f, 0.36f, 0.98f, 0.72f, new Color(0.8f, 0.8f, 0.9f));
            }
            else if (card.type == CardType.Algorithm)
            {
                MakeTMP(obj, "Effect", "界律: 共有効果", 10,
                    0.02f, 0.36f, 0.98f, 0.72f, new Color(0.8f, 0.7f, 1f));
            }

            // Card illustration
            if (!string.IsNullOrEmpty(card.illustrationId))
            {
                var illSprite = LoadCardIllustration(card.illustrationId);
                if (illSprite != null)
                {
                    var illObj = new GameObject("Illustration");
                    illObj.transform.SetParent(obj.transform, false);
                    illObj.transform.SetAsFirstSibling();
                    var illRt = illObj.AddComponent<RectTransform>();
                    illRt.anchorMin = new Vector2(0, 0.3f);
                    illRt.anchorMax = new Vector2(1, 0.85f);
                    illRt.offsetMin = Vector2.zero;
                    illRt.offsetMax = Vector2.zero;
                    var illImg = illObj.AddComponent<Image>();
                    illImg.sprite = illSprite;
                    illImg.preserveAspect = true;
                    illImg.color = Color.white;
                    illImg.raycastTarget = false;
                }
            }

            var glow = new GameObject("Glow");
            glow.transform.SetParent(obj.transform, false);
            glow.transform.SetAsFirstSibling();
            var grt = glow.AddComponent<RectTransform>();
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(-2, -2);
            grt.offsetMax = new Vector2(2, 2);
            var gi = glow.AddComponent<Image>();
            gi.color = new Color(ac.r, ac.g, ac.b, 0.3f);

            return obj;
        }

        static void MakeTMP(GameObject parent, string name, string text, int size,
            float xMin, float yMin, float xMax, float yMax,
            Color color, bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = new Vector2(2, 0);
            rt.offsetMax = new Vector2(-2, 0);
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            var jpFont = Banganka.Game.AutoBootstrap.JapaneseFont;
            if (jpFont != null) tmp.font = jpFont;
        }

        // ==================== INTERACTIONS ====================
        // ==================== DRAG & DROP ATTACK ====================
        void OnDragAttack(string attackerId, bool attackerIsLeader, string targetId, bool targetIsLeader)
        {
            _pendingAttack = new BattleEngine.AttackDeclaration
            {
                attackerType = attackerIsLeader ? BattleEngine.AttackerType.Leader : BattleEngine.AttackerType.Unit,
                attackerInstanceId = attackerIsLeader ? null : attackerId,
                targetType = targetIsLeader ? BattleEngine.TargetType.Leader : BattleEngine.TargetType.Unit,
                targetInstanceId = targetIsLeader ? null : targetId,
            };
            _pendingAttackSide = PlayerSide.Player1;
            ExecuteAttack();
        }

        // ==================== LEGACY ATTACK (kept for AI/bot use) ====================
        void OnLeaderAttack(PlayerSide side)
        {
            _pendingAttack = new BattleEngine.AttackDeclaration
            {
                attackerType = BattleEngine.AttackerType.Leader,
            };
            _pendingAttackSide = side;
            _mode = InteractionMode.SelectingTarget;
            RefreshUI();
        }

        void OnUnitAttack(PlayerSide side, FieldUnit unit)
        {
            _pendingAttack = new BattleEngine.AttackDeclaration
            {
                attackerType = BattleEngine.AttackerType.Unit,
                attackerInstanceId = unit.instanceId,
            };
            _pendingAttackSide = side;
            _mode = InteractionMode.SelectingTarget;
            RefreshUI();
        }

        void OnSelectAttackTarget(FieldUnit targetUnit)
        {
            _pendingAttack.targetType = BattleEngine.TargetType.Unit;
            _pendingAttack.targetInstanceId = targetUnit.instanceId;
            ExecuteAttack();
        }

        public void OnAttackLeader()
        {
            _pendingAttack.targetType = BattleEngine.TargetType.Leader;
            ExecuteAttack();
        }

        void ExecuteAttack()
        {
            _mode = InteractionMode.None;
            string blockerId = null;

            if (_pendingAttackSide == PlayerSide.Player1 &&
                _pendingAttack.targetType == BattleEngine.TargetType.Leader)
            {
                bool hasGuardBreak = false;
                if (_pendingAttack.attackerType == BattleEngine.AttackerType.Unit)
                {
                    var unit = _engine.State.player1.field.Find(
                        u => u.instanceId == _pendingAttack.attackerInstanceId);
                    hasGuardBreak = unit != null && unit.currentKeywords.Contains("GuardBreak");
                }

                if (!hasGuardBreak)
                {
                    var p2Blockers = _engine.State.player2.field.Where(u => u.CanBlock).ToList();
                    if (p2Blockers.Count > 0)
                        blockerId = p2Blockers.OrderByDescending(u => u.currentPower).First().instanceId;
                }
            }

            if (_engine.CanDeclareAttack(_pendingAttackSide, _pendingAttack))
                _engine.ResolveAttack(_pendingAttackSide, _pendingAttack, blockerId);
            RefreshUI();
        }

        void OnUseLeaderSkill(int skillLevel)
        {
            if (_engine == null) return;
            if (!_engine.CanUseLeaderSkill(PlayerSide.Player1, skillLevel)) return;
            StartCoroutine(UseLeaderSkillWithCutin(skillLevel));
        }

        IEnumerator UseLeaderSkillWithCutin(int skillLevel)
        {
            var leader = _engine.State.player1.leader;
            if (cutinController != null)
                yield return cutinController.PlaySkillCutin(leader, skillLevel, PlayerSide.Player1);
            _engine.UseLeaderSkill(PlayerSide.Player1, skillLevel);
            RefreshUI();
        }

        void OnEndTurn()
        {
            _mode = InteractionMode.None;
            _engine.EndTurn();
        }

        void OnSurrender()
        {
            var result = _engine.State.activePlayer == PlayerSide.Player1
                ? MatchResult.Player2Win
                : MatchResult.Player1Win;
            OnMatchEnd(result);
        }

        // Saved replay ID for the "view replay" button
        string _lastReplayId;

        void OnMatchEnd(MatchResult result)
        {
            // Finalize replay recording
            _lastReplayId = null;
            if (_replayRecorder != null && _replayRecorder.IsRecording)
            {
                var replayData = _replayRecorder.FinishRecording(result);
                if (replayData != null)
                {
                    ReplayStorage.SaveReplay(replayData);
                    _lastReplayId = replayData.replayId;
                    Debug.Log($"[Replay] Saved: {replayData.replayId} ({replayData.commands.Count} commands)");
                }
                _replayRecorder = null;
            }

            string text = result switch
            {
                MatchResult.Player1Win => "勝利！",
                MatchResult.Player2Win => "敗北…",
                MatchResult.Draw => "引き分け",
                _ => ""
            };

            // Populate result screen details
            PopulateResultDetails(result);

            if (resultPanel && animator != null)
            {
                var rt = resultPanel.GetComponent<RectTransform>();
                if (rt != null)
                {
                    if (result == MatchResult.Player1Win)
                        StartCoroutine(animator.AnimateVictory(rt, text));
                    else
                        StartCoroutine(animator.AnimateDefeat(rt, text));
                    return;
                }
            }

            // Fallback without animation
            if (resultPanel) resultPanel.SetActive(true);
            if (resultText) resultText.text = text;
        }

        /// <summary>
        /// リザルト画面の詳細情報を設定 (SCREEN_SPEC.md §7c)
        /// </summary>
        void PopulateResultDetails(MatchResult result)
        {
            var state = _engine?.State;
            if (state == null) return;

            int p1Hp = state.player1.hp;
            int p2Hp = state.player2.hp;
            int maxHp = state.player1.maxHp > 0 ? state.player1.maxHp : 100;
            int turnCount = state.turnTotal;

            // --- 勝利タイプ (鯱鉾勝利 vs 塗り勝利) ---
            if (victoryTypeText)
            {
                if (result == MatchResult.Draw)
                {
                    victoryTypeText.text = "";
                }
                else
                {
                    // 鯱鉾勝利 = 相手HPが0 (直接KO)
                    // 塗り勝利 = ターン制限到達によるHP比較
                    bool isTurnLimit = turnCount >= BalanceConfig.TurnLimitTotal;
                    bool loserHpZero = (result == MatchResult.Player1Win && p2Hp <= 0)
                                    || (result == MatchResult.Player2Win && p1Hp <= 0);

                    if (!isTurnLimit && loserHpZero)
                    {
                        victoryTypeText.text = "鯱鉾勝利";
                        victoryTypeText.color = new Color(1f, 0.85f, 0.3f); // gold
                    }
                    else
                    {
                        victoryTypeText.text = "塗り勝利";
                        victoryTypeText.color = new Color(0.7f, 0.85f, 1f); // light blue
                    }
                }
            }

            // --- マッチID ---
            if (matchIdText)
            {
                string displayId = !string.IsNullOrEmpty(_lastReplayId) ? _lastReplayId : $"m_{DateTime.Now:yyyyMMdd_HHmmss}";
                matchIdText.text = $"Match ID: {displayId}";
            }

            // --- 最終HP表示 ---
            if (finalHpText)
            {
                finalHpText.text = $"あなた: {Mathf.Max(0, p1Hp)} HP    相手: {Mathf.Max(0, p2Hp)} HP";
            }

            // --- HP バー表示 ---
            if (resultP1HpBar)
            {
                resultP1HpBar.fillAmount = Mathf.Clamp01((float)Mathf.Max(0, p1Hp) / maxHp);
                resultP1HpBar.color = p1Hp > 0 ? new Color(0.35f, 0.76f, 0.42f) : new Color(0.7f, 0.2f, 0.2f);
            }
            if (resultP2HpBar)
            {
                resultP2HpBar.fillAmount = Mathf.Clamp01((float)Mathf.Max(0, p2Hp) / maxHp);
                resultP2HpBar.color = p2Hp > 0 ? new Color(0.69f, 0.23f, 0.23f) : new Color(0.4f, 0.15f, 0.15f);
            }

            // --- 報酬計算 ---
            // base 50 gold + 50 for winning + 10 per turn survived
            bool won = result == MatchResult.Player1Win;
            int goldEarned = 50 + (won ? 50 : 0) + turnCount * 10;
            int bpXp = goldEarned; // Battle Pass XP = gold earned

            if (rewardGoldText)
                rewardGoldText.text = $"+{goldEarned} G";

            if (rewardBpXpText)
                rewardBpXpText.text = $"+{bpXp} BP EXP";

            if (rewardMissionText)
            {
                // ミッション進捗: バトル完了ミッションを更新
                var missions = new List<string>();
                missions.Add("バトル完了 +1");
                if (won) missions.Add("勝利 +1");
                if (turnCount >= 10) missions.Add("10ターン以上生存 +1");
                rewardMissionText.text = string.Join("\n", missions);
            }

            // --- リプレイボタン ---
            if (viewReplayButton)
            {
                viewReplayButton.onClick.RemoveAllListeners();
                if (!string.IsNullOrEmpty(_lastReplayId))
                {
                    viewReplayButton.gameObject.SetActive(true);
                    viewReplayButton.onClick.AddListener(() =>
                    {
                        Debug.Log($"[Result] View replay: {_lastReplayId}");
                        var replayCtrl = gameObject.GetComponent<Banganka.UI.Replay.ReplayListController>();
                        if (replayCtrl == null)
                            replayCtrl = gameObject.AddComponent<Banganka.UI.Replay.ReplayListController>();
                        replayCtrl.ShowAndPlay(_lastReplayId);
                    });
                }
                else
                {
                    viewReplayButton.gameObject.SetActive(false);
                }
            }

            // --- もう一度ボタン ---
            if (playAgainButton)
            {
                playAgainButton.onClick.RemoveAllListeners();
                playAgainButton.onClick.AddListener(() =>
                {
                    if (screenManager) screenManager.ShowScreen(GameManager.GameScreen.Battle);
                });
            }

            // --- PlayerData に報酬反映 ---
            // RecordWin/Loss/Draw handles gold, rating, stats, and Save()
            // Additional turn-based gold bonus applied separately
            var pd = PlayerData.Instance;
            if (pd != null)
            {
                int turnBonus = turnCount * 10;
                pd.gold += turnBonus;
                pd.battlePassXp += bpXp;

                if (result == MatchResult.Player1Win)
                    pd.RecordWin();
                else if (result == MatchResult.Player2Win)
                    pd.RecordLoss();
                else
                    pd.RecordDraw();

                Debug.Log($"[Result] Rewards: +{goldEarned}G (includes base from Record*), +{bpXp} BP XP. Total gold={pd.gold}");
            }
        }

        void OnLogEntry(BattleLogEntry entry)
        {
            if (logText)
            {
                logText.text = $"[T{entry.turnNumber}] {entry.eventType}: {entry.detail}\n" + logText.text;
                if (logText.text.Length > 1500)
                    logText.text = logText.text[..1500];
            }
        }

        // ==================== TURN ANNOUNCEMENT ====================
        Coroutine _turnAnnounceCoroutine;

        void ShowTurnAnnouncement(string text)
        {
            if (turnAnnounceText == null) return;
            if (_turnAnnounceCoroutine != null) StopCoroutine(_turnAnnounceCoroutine);
            _turnAnnounceCoroutine = StartCoroutine(TurnAnnounceFade(text));
        }

        IEnumerator TurnAnnounceFade(string text)
        {
            turnAnnounceText.text = text;
            var cg = turnAnnounceText.GetComponent<CanvasGroup>();
            if (cg == null) cg = turnAnnounceText.gameObject.AddComponent<CanvasGroup>();

            // Fade in
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Clamp01(t / 0.3f);
                yield return null;
            }
            cg.alpha = 1f;

            // Hold
            yield return new WaitForSeconds(1f);

            // Fade out
            t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                cg.alpha = 1f - Mathf.Clamp01(t / 0.5f);
                yield return null;
            }
            cg.alpha = 0f;
        }

        // ==================== ANIMATION HANDLERS ====================

        void OnCardPlayedAnim(PlayerSide side, CardData card, CardType type)
        {
            // Algorithm field effect (both sides)
            if (type == CardType.Algorithm && algoFieldEffect != null)
            {
                bool isOverwrite = _engine.State.sharedAlgo != null;
                algoFieldEffect.PlayAlgorithmSet(card, isOverwrite);
            }

            if (animator == null || side != PlayerSide.Player1) return;

            // Find the last placed field unit UI for Manifest cards
            if (type == CardType.Manifest && p1FieldFront != null)
            {
                // Check front row first, then back row for the last placed unit
                Transform lastRow = p1FieldBack != null && p1FieldBack.childCount > 0 ? p1FieldBack : p1FieldFront;
                var lastChild = lastRow != null && lastRow.childCount > 0
                    ? lastRow.GetChild(lastRow.childCount - 1)
                    : null;

                if (lastChild != null)
                {
                    var rt = lastChild.GetComponent<RectTransform>();
                    if (rt != null)
                        StartCoroutine(animator.AnimateCardPlay(rt, rt.anchoredPosition));
                }
            }
        }

        void OnAttackResolvedAnim(PlayerSide side, string attackerName, string targetName, bool isDirectHit)
        {
            if (animator == null) return;

            // Find attacker and target RectTransforms in the field
            Transform attackerFront = side == PlayerSide.Player1 ? p1FieldFront : p2FieldFront;
            Transform attackerBack = side == PlayerSide.Player1 ? p1FieldBack : p2FieldBack;
            Transform targetFront = side == PlayerSide.Player1 ? p2FieldFront : p1FieldFront;
            Transform targetBack = side == PlayerSide.Player1 ? p2FieldBack : p1FieldBack;

            RectTransform attackerRT = FindChildByName(attackerFront, attackerName) ?? FindChildByName(attackerBack, attackerName);
            RectTransform targetRT = isDirectHit ? null : (FindChildByName(targetFront, targetName) ?? FindChildByName(targetBack, targetName));

            if (attackerRT != null)
                StartCoroutine(animator.AnimateAttack(attackerRT, targetRT, isDirectHit));
        }

        void OnUnitDestroyedAnim(PlayerSide side, string unitName)
        {
            if (animator == null) return;

            Transform front = side == PlayerSide.Player1 ? p1FieldFront : p2FieldFront;
            Transform back = side == PlayerSide.Player1 ? p1FieldBack : p2FieldBack;

            RectTransform rt = FindChildByName(front, unitName) ?? FindChildByName(back, unitName);
            if (rt != null)
                StartCoroutine(animator.AnimateUnitExit(rt));
        }

        void OnHpDamagedAnim(PlayerSide side, int damage)
        {
            if (animator == null) return;

            var player = _engine.State.GetPlayer(side);
            var leaderText = side == PlayerSide.Player1 ? p1LeaderNameText : p2LeaderNameText;
            if (leaderText != null)
            {
                var rt = leaderText.GetComponent<RectTransform>();
                if (rt != null)
                    StartCoroutine(animator.AnimateHpDamage(rt, damage, player.maxHp));
            }
        }

        void OnLeaderLevelUpAnim(PlayerSide side, int newLevel)
        {
            var player = _engine.State.GetPlayer(side);

            // Play level-up cutin
            if (cutinController != null)
                StartCoroutine(cutinController.PlayLevelUpCutin(player.leader, newLevel));

            // Also play the local flash on leader panel
            if (animator != null)
            {
                var leaderText = side == PlayerSide.Player1 ? p1LeaderNameText : p2LeaderNameText;
                if (leaderText != null)
                {
                    var rt = leaderText.GetComponent<RectTransform>();
                    if (rt != null)
                        StartCoroutine(animator.AnimateLeaderLevelUp(rt, newLevel));
                }
            }
        }

        void OnWishTriggerAnim(PlayerSide side, WishCardSlot slot)
        {
            string cardName = slot.card?.cardName ?? "願力カード";
            var player = _engine.State.GetPlayer(side);
            Aspect aspect = player.leader.baseData.keyAspect;

            // Play cutin-style wish banner
            if (cutinController != null)
                StartCoroutine(cutinController.PlayWishTriggerCutin(slot.threshold, cardName, aspect));
        }

        void AddLongPressInspect(GameObject cardObj, CardData card)
        {
            if (card3DInspector == null) return;
            var trigger = cardObj.GetComponent<EventTrigger>();
            if (trigger == null) trigger = cardObj.AddComponent<EventTrigger>();

            var holdCoroutine = new Coroutine[1];
            var captured = card;

            // PointerDown: start hold timer
            var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            downEntry.callback.AddListener(_ => { holdCoroutine[0] = StartCoroutine(LongPressRoutine(captured)); });
            trigger.triggers.Add(downEntry);

            // PointerUp / PointerExit: cancel hold
            var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            upEntry.callback.AddListener(_ => { if (holdCoroutine[0] != null) { StopCoroutine(holdCoroutine[0]); holdCoroutine[0] = null; } });
            trigger.triggers.Add(upEntry);

            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener(_ => { if (holdCoroutine[0] != null) { StopCoroutine(holdCoroutine[0]); holdCoroutine[0] = null; } });
            trigger.triggers.Add(exitEntry);
        }

        IEnumerator LongPressRoutine(CardData card)
        {
            yield return new WaitForSeconds(0.5f);
            card3DInspector?.ShowCard(card);
        }

        RectTransform FindChildByName(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name)
                    return parent.GetChild(i).GetComponent<RectTransform>();
            }
            return null;
        }
    }
}
