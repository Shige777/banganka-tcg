using System;
using System.Collections.Generic;
using System.Linq;
using Banganka.Core.Config;
using Banganka.Core.Data;

namespace Banganka.Core.Battle
{
    public class BattleEngine
    {
        public BattleState State { get; private set; }
        public event Action<BattleLogEntry> OnLog;
        public event Action<MatchResult> OnMatchEnd;
        public event Action<PlayerSide, WishCardSlot> OnWishTrigger;
        public event Action OnStateChanged;

        // Animation events
        public event Action<PlayerSide, CardData, CardType> OnCardPlayed;
        public event Action<PlayerSide, string, string, bool> OnAttackResolved; // side, attackerName, targetName, isDirectHit
        public event Action<PlayerSide, string> OnUnitDestroyed; // side, unitName
        public event Action<PlayerSide, int> OnHpDamaged; // side, damage
        public event Action<PlayerSide, int> OnLeaderLevelUp; // side, newLevel

        readonly System.Random _rng;
        bool _algorithmPlayedThisTurn;

        public BattleEngine(int? seed = null)
        {
            _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        // ====================================================================
        // §9.2 Match Initialization
        // ====================================================================

        public void InitMatch(LeaderData leaderP1, LeaderData leaderP2, List<CardData> deckP1, List<CardData> deckP2,
            MatchMode mode = MatchMode.Standard)
        {
            var modeParams = MatchModeConfig.Get(mode);

            State = new BattleState
            {
                turnTotal = 0,
                activePlayer = _rng.Next(2) == 0 ? PlayerSide.Player1 : PlayerSide.Player2,
                currentPhase = TurnPhase.Start,
                isGameOver = false,
                result = MatchResult.None,
                matchMode = mode,
                player1 = CreatePlayer(PlayerSide.Player1, leaderP1, deckP1, modeParams),
                player2 = CreatePlayer(PlayerSide.Player2, leaderP2, deckP2, modeParams),
            };

            // Step 4: Shuffle + draw initial hand
            ShuffleDeck(State.player1);
            ShuffleDeck(State.player2);

            // v0.5.5: 後攻補正 — 後攻プレイヤーは+1枚多く引く
            var secondPlayer = State.activePlayer == PlayerSide.Player1 ? State.player2 : State.player1;
            var firstPlayer = State.activePlayer == PlayerSide.Player1 ? State.player1 : State.player2;
            DrawCards(firstPlayer, BalanceConfig.InitialHand);
            DrawCards(secondPlayer, BalanceConfig.InitialHand + BalanceConfig.SecondPlayerExtraCards);

            // Quick Match: 初期CPを付与
            if (modeParams.startCP > 0)
            {
                State.player1.currentCP = modeParams.startCP;
                State.player1.maxCP = modeParams.startCP;
                State.player2.currentCP = modeParams.startCP;
                State.player2.maxCP = modeParams.startCP;
            }

            // Step 5: HP + Wish Card Initialization (before mulligan)
            InitializeHpAndWishCards(State.player1, modeParams);
            InitializeHpAndWishCards(State.player2, modeParams);
        }

        public void InitMatch(LeaderData leaderData, List<CardData> deckP1, List<CardData> deckP2,
            MatchMode mode = MatchMode.Standard)
        {
            InitMatch(leaderData, leaderData, deckP1, deckP2, mode);
        }

        /// <summary>
        /// チュートリアル用初期化: HP=customHp、Player1先攻、固定ドロー順
        /// TUTORIAL_FLOW.md §3.1
        /// </summary>
        public void InitTutorialMatch(LeaderData leaderP1, LeaderData leaderP2,
            List<CardData> deckP1, List<CardData> deckP2, int customHp,
            string[] fixedDrawOrder = null)
        {
            State = new BattleState
            {
                turnTotal = 0,
                activePlayer = PlayerSide.Player1, // Tutorial: Player1 always first
                currentPhase = TurnPhase.Start,
                isGameOver = false,
                result = MatchResult.None,
                player1 = CreatePlayer(PlayerSide.Player1, leaderP1, deckP1),
                player2 = CreatePlayer(PlayerSide.Player2, leaderP2, deckP2),
            };

            // Override HP
            State.player1.hp = customHp;
            State.player1.maxHp = customHp;
            State.player2.hp = customHp;
            State.player2.maxHp = customHp;

            // Fixed draw order for player1 (tutorial scripted)
            if (fixedDrawOrder != null)
            {
                var ordered = new List<CardData>();
                foreach (var cardId in fixedDrawOrder)
                {
                    int idx = State.player1.deck.FindIndex(c => c.id == cardId);
                    if (idx >= 0)
                    {
                        ordered.Add(State.player1.deck[idx]);
                        State.player1.deck.RemoveAt(idx);
                    }
                }
                // Put fixed-order cards on top, remaining shuffled below
                ShuffleDeck(State.player1);
                State.player1.deck.InsertRange(0, ordered);
            }
            else
            {
                ShuffleDeck(State.player1);
            }
            ShuffleDeck(State.player2);

            DrawCards(State.player1, BalanceConfig.InitialHand);
            DrawCards(State.player2, BalanceConfig.InitialHand);

            InitializeHpAndWishCards(State.player1);
            InitializeHpAndWishCards(State.player2);
        }

        PlayerState CreatePlayer(PlayerSide side, LeaderData leader, List<CardData> deckCards,
            MatchParams? modeParams = null)
        {
            var mp = modeParams ?? MatchModeConfig.Get(MatchMode.Standard);
            return new PlayerState
            {
                side = side,
                leader = new LeaderState(leader),
                deck = new List<CardData>(deckCards),
                maxCP = 0,
                currentCP = 0,
                hp = mp.hp,
                maxHp = mp.hp,
                isFinal = false,
            };
        }

        /// <summary>
        /// §9.2 Step 5: デッキ上から6枚を願力ゾーンの閾値に配置
        /// </summary>
        void InitializeHpAndWishCards(PlayerState p, MatchParams? modeParams = null)
        {
            var mp = modeParams ?? MatchModeConfig.Get(MatchMode.Standard);
            p.wishZone.Clear();
            int slotCount = System.Math.Min(mp.wishThresholds.Length, BalanceConfig.WishCardSlotCount);
            for (int i = 0; i < slotCount && p.deck.Count > 0; i++)
            {
                var card = p.deck[0];
                p.deck.RemoveAt(0);
                p.wishZone.Add(new WishCardSlot(mp.wishThresholds[i], card));
            }
            Log("WISH_ZONE_INIT", $"{p.side}: {p.wishZone.Count} wish cards placed");
        }

        void ShuffleDeck(PlayerState p)
        {
            var deck = p.deck;
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
        }

        void DrawCards(PlayerState p, int count)
        {
            for (int i = 0; i < count && p.deck.Count > 0; i++)
            {
                var card = p.deck[0];
                p.deck.RemoveAt(0);
                p.hand.Add(card);
            }
        }

        // ====================================================================
        // §9.3 Mulligan
        // ====================================================================

        public void PerformMulligan(PlayerSide side, List<int> handIndices)
        {
            var p = State.GetPlayer(side);
            var toReturn = new List<CardData>();
            foreach (int idx in handIndices.OrderByDescending(x => x))
            {
                toReturn.Add(p.hand[idx]);
                p.hand.RemoveAt(idx);
            }
            foreach (var c in toReturn) p.deck.Add(c);
            ShuffleDeck(p);
            DrawCards(p, toReturn.Count);
        }

        // ====================================================================
        // §10.2 Turn Flow
        // ====================================================================

        public void StartTurn()
        {
            if (State.isGameOver) return;
            State.turnTotal++;
            State.currentPhase = TurnPhase.Start;
            _algorithmPlayedThisTurn = false;

            var p = State.GetPlayer(State.activePlayer);

            // Refresh units and leader
            p.leader.status = UnitStatus.Ready;
            p.leader.skillUsedThisTurn = false;
            p.leader.damageHalveActive = false;
            foreach (var u in p.field)
            {
                u.status = UnitStatus.Ready;
                u.summonSick = false;
            }

            // CP update
            if (p.maxCP < BalanceConfig.MaxCPCap) p.maxCP++;
            p.currentCP = p.maxCP;

            // Draw phase
            State.currentPhase = TurnPhase.Draw;
            DrawCards(p, BalanceConfig.DrawPerTurn);

            State.currentPhase = TurnPhase.Main;

            // §5.4.1: Auto-flip face-down algorithm at start of owner's main phase
            AutoFlipFaceDownAlgorithm(State.activePlayer);

            OnStateChanged?.Invoke();
        }

        // ====================================================================
        // Card Play
        // ====================================================================

        public bool CanPlayCard(PlayerSide side, int handIndex)
        {
            if (State.isGameOver || State.activePlayer != side) return false;
            if (State.currentPhase != TurnPhase.Main) return false;
            var p = State.GetPlayer(side);
            if (handIndex < 0 || handIndex >= p.hand.Count) return false;
            var card = p.hand[handIndex];
            if (card.cpCost > p.currentCP) return false;
            if (card.type == CardType.Manifest && p.AvailableSlots <= 0)
                return false;
            // §5.4.1: Only 1 algorithm play per turn (face-up or face-down)
            if (card.type == CardType.Algorithm && _algorithmPlayedThisTurn)
                return false;
            return true;
        }

        public void PlayCard(PlayerSide side, int handIndex, bool faceDown = false)
        {
            if (!CanPlayCard(side, handIndex))
                throw new InvalidOperationException("Cannot play this card.");

            var p = State.GetPlayer(side);
            var card = p.hand[handIndex];

            // §5.4.1: Face-down is only valid for Algorithm cards
            if (faceDown && card.type != CardType.Algorithm)
                throw new InvalidOperationException("Only Algorithm cards can be played face-down.");

            p.currentCP -= card.cpCost;
            p.hand.RemoveAt(handIndex);

            switch (card.type)
            {
                case CardType.Manifest:
                    PlayManifest(p, card);
                    break;
                case CardType.Spell:
                    PlaySpell(p, card);
                    break;
                case CardType.Algorithm:
                    PlayAlgorithm(p, card, faceDown);
                    break;
            }

            OnCardPlayed?.Invoke(side, card, card.type);
            OnStateChanged?.Invoke();
        }

        FieldRow _nextPlacementRow = FieldRow.Front;

        /// <summary>
        /// 次に召喚する顕現の配置列を指定する。PlayCard前に呼ぶ。
        /// </summary>
        public void SetPlacementRow(FieldRow row) => _nextPlacementRow = row;

        void PlayManifest(PlayerState p, CardData card)
        {
            var unit = new FieldUnit(card, State.NextInstanceId());
            unit.row = _nextPlacementRow;
            _nextPlacementRow = FieldRow.Front; // reset default
            ApplyAlgorithmToUnit(unit, p.side);
            p.field.Add(unit);

            Log("PLAY_SHOW", $"{card.cardName} ({card.id}) by {p.side}");

            ResolveOnPlayEffect(p, card);
        }

        void PlaySpell(PlayerState p, CardData card)
        {
            Log("PLAY_SHOW", $"{card.cardName} ({card.id}) by {p.side}");
            EffectResolver.ResolveSpell(State, p.side, card, this);

            // Algorithm spell bonus (face-down algorithms have no active effects)
            if (State.sharedAlgo != null && !State.sharedAlgo.isFaceDown)
            {
                var algo = State.sharedAlgo.cardData;
                if (algo.globalRule != null && algo.globalRule.kind == "spell_hp_damage")
                {
                    int bonus = algo.globalRule.value;
                    ApplyHpDamage(State.GetOpponent(p.side), bonus, "fixed");
                    Log("ALGO_SPELL_BONUS", $"Algorithm global spell bonus: {bonus} HP damage");
                }
                if (State.sharedAlgo.owner == p.side && algo.ownerBonus != null && algo.ownerBonus.kind == "spell_hp_damage")
                {
                    int bonus = algo.ownerBonus.value;
                    ApplyHpDamage(State.GetOpponent(p.side), bonus, "fixed");
                    Log("ALGO_SPELL_BONUS", $"Algorithm owner spell bonus: {bonus} HP damage");
                }
            }

            p.graveyard.Add(card);
        }

        void PlayAlgorithm(PlayerState p, CardData card, bool faceDown = false)
        {
            // §5.4.1: If a face-down algorithm is in the shared slot and a new one overwrites it,
            // the face-down card goes to its owner's graveyard (effects never activate)
            if (State.sharedAlgo != null && State.sharedAlgo.isFaceDown)
            {
                var prevOwner = State.GetPlayer(State.sharedAlgo.owner);
                prevOwner.graveyard.Add(State.sharedAlgo.cardData);
                Log("ALGO_FACEDOWN_OVERWRITTEN",
                    $"Face-down algorithm by {State.sharedAlgo.owner} overwritten and sent to graveyard");
            }

            _algorithmPlayedThisTurn = true;

            if (faceDown)
            {
                PlayAlgorithmFaceDown(p, card);
            }
            else
            {
                State.sharedAlgo = new SharedAlgorithm
                {
                    cardData = card,
                    owner = p.side,
                    isFaceDown = false,
                    setTurn = 0
                };
                Log("SET_SHARED_ALGO", $"{card.cardName} set by {p.side}");

                ReapplyAlgorithmToAllUnits();
                OnStateChanged?.Invoke();
            }
        }

        /// <summary>
        /// §5.4.1: Place algorithm face-down. CP is paid but no effects activate.
        /// Opponent sees "face-down algorithm" but not which card.
        /// </summary>
        void PlayAlgorithmFaceDown(PlayerState p, CardData card)
        {
            State.sharedAlgo = new SharedAlgorithm
            {
                cardData = card,
                owner = p.side,
                isFaceDown = true,
                setTurn = State.turnTotal
            };
            Log("SET_SHARED_ALGO_FACEDOWN", $"Face-down algorithm set by {p.side}");

            // No effect activation — do not call ReapplyAlgorithmToAllUnits
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// §5.4.1: Check if the given side can flip a face-down algorithm.
        /// Must be own main phase, shared algo must be face-down, and owned by this side.
        /// </summary>
        public bool CanFlipAlgorithm(PlayerSide side)
        {
            if (State.isGameOver || State.activePlayer != side) return false;
            if (State.currentPhase != TurnPhase.Main) return false;
            if (State.sharedAlgo == null) return false;
            if (!State.sharedAlgo.isFaceDown) return false;
            if (State.sharedAlgo.owner != side) return false;
            return true;
        }

        /// <summary>
        /// §5.4.1: Flip face-down algorithm face-up. Free action (no CP cost).
        /// Effects activate at this moment. Does not count as an algorithm play.
        /// </summary>
        public void FlipAlgorithm(PlayerSide side)
        {
            if (!CanFlipAlgorithm(side))
                throw new InvalidOperationException("Cannot flip algorithm.");

            State.sharedAlgo.isFaceDown = false;
            Log("FLIP_ALGO", $"{State.sharedAlgo.cardData.cardName} flipped face-up by {side}");

            ReapplyAlgorithmToAllUnits();
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// §5.4.1: Auto-flip face-down algorithm at the start of owner's next main phase.
        /// Max 1 turn face-down.
        /// </summary>
        void AutoFlipFaceDownAlgorithm(PlayerSide side)
        {
            if (State.sharedAlgo == null) return;
            if (!State.sharedAlgo.isFaceDown) return;
            if (State.sharedAlgo.owner != side) return;

            State.sharedAlgo.isFaceDown = false;
            Log("AUTO_FLIP_ALGO",
                $"{State.sharedAlgo.cardData.cardName} auto-flipped face-up for {side} (was set turn {State.sharedAlgo.setTurn})");

            ReapplyAlgorithmToAllUnits();
        }

        void ApplyAlgorithmToUnit(FieldUnit unit, PlayerSide owner)
        {
            if (State.sharedAlgo == null) return;
            if (State.sharedAlgo.isFaceDown) return; // face-down algorithms have no active effects
            var algo = State.sharedAlgo.cardData;

            ApplyAlgoRule(algo.globalRule, unit, owner, false);
            if (State.sharedAlgo.owner == owner)
                ApplyAlgoRule(algo.ownerBonus, unit, owner, true);
        }

        void ApplyAlgoRule(AlgorithmRule rule, FieldUnit unit, PlayerSide owner, bool isOwnerBonus)
        {
            if (rule == null) return;
            if (!MatchesCondition(rule.condition, unit.cardData, unit)) return;

            switch (rule.kind)
            {
                case "power_plus":
                    unit.currentPower += rule.value;
                    break;
                case "grant_rush":
                    if (!unit.currentKeywords.Contains("Rush"))
                        unit.currentKeywords.Add("Rush");
                    if (unit.currentKeywords.Contains("Rush"))
                        unit.summonSick = false;
                    break;
                case "wish_damage_plus":
                    unit.currentWishDamage += rule.value;
                    break;
            }
        }

        bool MatchesCondition(string condition, CardData card, FieldUnit unit)
        {
            if (string.IsNullOrEmpty(condition)) return true;
            if (condition.StartsWith("aspect:"))
            {
                string asp = condition.Substring(7);
                return card.aspect.ToString() == asp;
            }
            if (condition.StartsWith("keyword:"))
            {
                string kw = condition.Substring(8);
                return unit.currentKeywords.Contains(kw);
            }
            if (condition.StartsWith("cpCost<="))
            {
                int val = int.Parse(condition.Substring(8));
                return card.cpCost <= val;
            }
            return true;
        }

        void ReapplyAlgorithmToAllUnits()
        {
            foreach (var u in State.player1.field)
            {
                u.currentPower = u.cardData.battlePower;
                u.currentWishDamage = u.cardData.wishDamage;
                u.currentKeywords = new List<string>(u.cardData.keywords ?? Array.Empty<string>());
                ApplyAlgorithmToUnit(u, PlayerSide.Player1);
            }
            foreach (var u in State.player2.field)
            {
                u.currentPower = u.cardData.battlePower;
                u.currentWishDamage = u.cardData.wishDamage;
                u.currentKeywords = new List<string>(u.cardData.keywords ?? Array.Empty<string>());
                ApplyAlgorithmToUnit(u, PlayerSide.Player2);
            }
        }

        void ResolveOnPlayEffect(PlayerState p, CardData card)
        {
            // Rush keyword — bypass summoning sickness, ready to attack immediately
            if (card.HasKeyword("Rush"))
            {
                var unit = p.field[^1];
                unit.summonSick = false;
                unit.status = UnitStatus.Ready;
            }

            switch (card.effectKey)
            {
                case "SUMMON_ON_PLAY_DRAW":
                    EffectResolver.ResolveOnPlayDraw(State, p.side, card.drawCount > 0 ? card.drawCount : 1);
                    Log("ON_PLAY_EFFECT", $"{card.cardName}: draw {card.drawCount}");
                    break;

                case "SUMMON_ON_PLAY_HP_DAMAGE":
                    EffectResolver.ResolveOnPlayHpDamage(State, p.side, card, this);
                    Log("ON_PLAY_EFFECT", $"{card.cardName}: HP damage {card.hpDamagePercent}% ({card.damageType})");
                    break;

                case "SUMMON_ON_PLAY_BUFF_ALLY":
                    EffectResolver.ResolveOnPlayBuffAlly(State, p.side, card);
                    Log("ON_PLAY_EFFECT", $"{card.cardName}: buff ally +{card.powerDelta}");
                    break;

                case "SUMMON_ON_PLAY_DESTROY":
                    EffectResolver.ResolveOnPlayDestroy(State, p.side, card);
                    Log("ON_PLAY_EFFECT", $"{card.cardName}: destroy ({card.removeCondition})");
                    break;

                case "SUMMON_ON_PLAY_BOUNCE":
                    EffectResolver.ResolveOnPlayBounce(State, p.side, card.bounceCount > 0 ? card.bounceCount : 1);
                    Log("ON_PLAY_EFFECT", $"{card.cardName}: bounce {card.bounceCount}");
                    break;

                case "SUMMON_ON_PLAY_EMOTION_BUFF":
                    EffectResolver.ResolveOnPlayEmotionBuff(State, p.side, card);
                    Log("ON_PLAY_EFFECT", $"{card.cardName}: emotion buff ({card.emotionTag})");
                    break;
                case "SUMMON_ON_PLAY_EMOTION_DRAW":
                    EffectResolver.ResolveOnPlayEmotionDraw(State, p.side, card);
                    Log("ON_PLAY_EFFECT", $"{card.cardName}: emotion draw ({card.emotionTag})");
                    break;

                case "SUMMON_ON_PLAY_SLOT_LOCK":
                    EffectResolver.ResolveOnPlaySlotLock(State, p.side, card);
                    Log("ON_PLAY_EFFECT", $"{card.cardName}: slot lock");
                    break;
            }
        }

        /// <summary>
        /// 願力カード閾値を踏んだ時に願成ゲージを加算する。
        /// 「願いは、追い詰められるほど強くなる」コンセプトの実現。
        /// </summary>
        void ResolveWishThresholdEvoGain(PlayerState p)
        {
            if (p.leader.level >= p.leader.baseData.levelCap) return;

            p.leader.evoGauge += BalanceConfig.WishThresholdEvoGain;
            Log("LEADER_EVO_GAIN", $"+{BalanceConfig.WishThresholdEvoGain} for {p.side} (wishThreshold crossed)");
            CheckLevelUp(p);
        }

        /// <summary>
        /// 願成ゲージがレベルアップ閾値に到達しているかチェックし、到達していればレベルアップ処理を行う。
        /// </summary>
        void CheckLevelUp(PlayerState p)
        {
            if (p.leader.evoGauge >= p.leader.EvoGaugeMax && p.leader.level < p.leader.baseData.levelCap)
            {
                p.leader.level++;
                p.leader.evoGauge = 0;
                p.leader.currentPower += p.leader.baseData.levelUpPowerGain;

                if (p.leader.baseData.wishDamageByLevel != null &&
                    p.leader.level - 1 < p.leader.baseData.wishDamageByLevel.Length)
                {
                    p.leader.currentWishDamage = p.leader.baseData.wishDamageByLevel[p.leader.level - 1];
                }
                else
                {
                    p.leader.currentWishDamage += p.leader.baseData.levelUpWishDamageGain;
                }

                int lvIdx = p.leader.level - 2;
                if (p.leader.baseData.grantedKeywordsByLevel != null &&
                    lvIdx < p.leader.baseData.grantedKeywordsByLevel.Length &&
                    !string.IsNullOrEmpty(p.leader.baseData.grantedKeywordsByLevel[lvIdx]))
                {
                    p.leader.keywords.Add(p.leader.baseData.grantedKeywordsByLevel[lvIdx]);
                }

                Log("LEADER_LEVEL_UP", $"{p.side} -> Lv{p.leader.level}");
                OnLeaderLevelUp?.Invoke(p.side, p.leader.level);
            }
        }

        // ====================================================================
        // §4.1 HP Damage + Wish Card Trigger System
        // ====================================================================

        /// <summary>
        /// HPにダメージを与え、閾値を超えた願力カードを発動する。
        /// §10.5: 固定X% = MaxHP × X / 100 (切り上げ、最低1)
        ///         現在Y% = 現在HP × Y / 100 (切り上げ、最低1)
        /// </summary>
        public void ApplyHpDamage(PlayerState target, int percent, string damageType)
        {
            if (target.isFinal) return; // Already at 0

            int damage;
            if (damageType == "current")
            {
                damage = Math.Max(1, (int)Math.Ceiling(target.hp * percent / 100.0));
            }
            else // "fixed"
            {
                damage = Math.Max(1, (int)Math.Ceiling(target.maxHp * percent / 100.0));
            }

            // Apply damage halve (崔鋒 Lv2)
            if (target.leader.damageHalveActive)
            {
                damage = Math.Max(1, (int)Math.Ceiling(damage / 2.0));
                Log("DAMAGE_HALVE", $"{target.side}: damage halved to {damage}");
            }

            int hpBefore = target.hp;
            target.hp = Math.Max(0, target.hp - damage);

            Log("HP_DAMAGE", $"{target.side}: {hpBefore} -> {target.hp} ({damage} damage, {damageType} {percent}%)");

            // Check wish card thresholds crossed (highest to lowest)
            ResolveWishCardTriggers(target, hpBefore);

            // Check Final state
            if (target.hp <= 0 && !target.isFinal)
            {
                target.isFinal = true;
                Log("FINAL_STATE", $"{target.side} is now Final (HP=0)");
            }
        }

        /// <summary>
        /// HP回復。MaxHPを超えない。
        /// </summary>
        public void HealHp(PlayerState target, int amount)
        {
            int hpBefore = target.hp;
            target.hp = Math.Min(target.maxHp, target.hp + amount);

            if (target.isFinal && target.hp > 0)
                target.isFinal = false;

            Log("HP_HEAL", $"{target.side}: {hpBefore} -> {target.hp} (+{amount})");
        }

        /// <summary>
        /// §4.1: HPが閾値を下回ったとき、その位置の願力カードを上から順に発動→墓地
        /// </summary>
        void ResolveWishCardTriggers(PlayerState target, int hpBefore)
        {
            foreach (var slot in target.wishZone)
            {
                if (slot.triggered) continue;
                if (slot.card == null) continue;

                // Check if HP crossed this threshold
                // threshold=85 means the card triggers when HP drops below 85
                if (hpBefore >= slot.threshold && target.hp < slot.threshold)
                {
                    slot.triggered = true;
                    var card = slot.card;

                    Log("WISH_TRIGGER", $"{target.side}: wish card at {slot.threshold}% triggered ({card.cardName})");
                    OnWishTrigger?.Invoke(target.side, slot);

                    // 願いは、追い詰められるほど強くなる — 閾値踏みで願成ゲージ加算
                    ResolveWishThresholdEvoGain(target);

                    // Resolve wish trigger effect if card has a WishTrigger (not "-" or empty)
                    if (!string.IsNullOrEmpty(card.wishTrigger) && card.wishTrigger != "-")
                    {
                        ResolveWishTriggerEffect(target, card);
                    }

                    // Card goes to graveyard
                    target.graveyard.Add(card);
                    slot.card = null;
                }
            }
        }

        /// <summary>
        /// §5.5 Wish Trigger Effects: WT_DRAW, WT_BOUNCE, WT_POWER_PLUS, WT_BLOCKER
        /// </summary>
        void ResolveWishTriggerEffect(PlayerState owner, CardData card)
        {
            switch (card.wishTrigger)
            {
                case "WT_DRAW":
                    DrawCards(owner, 1);
                    Log("WISH_EFFECT", $"{owner.side}: WT_DRAW -> draw 1");
                    break;

                case "WT_BOUNCE":
                    var opponent = State.GetOpponent(owner.side);
                    if (opponent.field.Count > 0)
                    {
                        var target = opponent.field.OrderBy(u => u.currentPower).First();
                        opponent.field.Remove(target);
                        opponent.hand.Add(target.cardData);
                        Log("WISH_EFFECT", $"{owner.side}: WT_BOUNCE -> bounced {target.cardData.cardName}");
                    }
                    break;

                case "WT_POWER_PLUS":
                    if (owner.field.Count > 0)
                    {
                        var ally = owner.field.OrderByDescending(u => u.currentPower).First();
                        ally.currentPower += 1000;
                        Log("WISH_EFFECT", $"{owner.side}: WT_POWER_PLUS -> {ally.cardData.cardName} +1000");
                    }
                    break;

                case "WT_BLOCKER":
                    var nonBlocker = owner.field.FirstOrDefault(u => !u.currentKeywords.Contains("Blocker"));
                    if (nonBlocker != null)
                    {
                        nonBlocker.currentKeywords.Add("Blocker");
                        Log("WISH_EFFECT", $"{owner.side}: WT_BLOCKER -> {nonBlocker.cardData.cardName} gained Blocker");
                    }
                    break;
            }
        }

        // ====================================================================
        // §10.5 Combat
        // ====================================================================

        public void EnterCombatPhase()
        {
            if (State.currentPhase != TurnPhase.Main) return;
            State.currentPhase = TurnPhase.Combat;
            OnStateChanged?.Invoke();
        }

        public enum AttackerType { Leader, Unit }
        public enum TargetType { Leader, Unit }

        public struct AttackDeclaration
        {
            public AttackerType attackerType;
            public string attackerInstanceId;
            public TargetType targetType;
            public string targetInstanceId;
        }

        public bool CanDeclareAttack(PlayerSide side, AttackDeclaration decl)
        {
            if (State.isGameOver || State.activePlayer != side) return false;
            if (State.currentPhase != TurnPhase.Combat && State.currentPhase != TurnPhase.Main) return false;

            if (decl.attackerType == AttackerType.Leader)
            {
                return State.GetPlayer(side).leader.CanAttack;
            }
            else
            {
                var unit = FindUnit(State.GetPlayer(side), decl.attackerInstanceId);
                return unit != null && unit.CanAttack;
            }
        }

        public struct CombatResult
        {
            public bool attackerDestroyed;
            public bool defenderDestroyed;
            public int hpDamage;
            public bool directHit;
            public bool finalBlow; // 鯱鉾勝利: opponent was Final and got hit
        }

        public CombatResult ResolveAttack(PlayerSide side, AttackDeclaration decl, string blockerId = null)
        {
            var result = new CombatResult();
            var attacker = State.GetPlayer(side);
            var defender = State.GetOpponent(side);

            int attackPower, attackWishDmg;
            string attackerName;
            string attackWishDmgType = "fixed";

            if (decl.attackerType == AttackerType.Leader)
            {
                attackPower = attacker.leader.currentPower;
                attackWishDmg = attacker.leader.currentWishDamage;
                attackWishDmgType = attacker.leader.wishDamageType ?? "fixed";
                attacker.leader.status = UnitStatus.Exhausted;
                attackerName = "Leader";
            }
            else
            {
                var unit = FindUnit(attacker, decl.attackerInstanceId);
                attackPower = unit.currentPower;
                attackWishDmg = unit.currentWishDamage;
                attackWishDmgType = unit.cardData.wishDamageType ?? "fixed";
                unit.status = UnitStatus.Exhausted;
                attackerName = unit.cardData.cardName;
            }

            Log("ATTACK_DECLARED", $"{attackerName} -> {decl.targetType}");
            string targetName = decl.targetType == TargetType.Leader ? "Leader" : "";
            if (decl.targetType == TargetType.Unit)
            {
                var tgt = FindUnit(defender, decl.targetInstanceId);
                targetName = tgt?.cardData.cardName ?? "Unit";
            }

            // Check for blocker
            bool blocked = false;
            FieldUnit blockerUnit = null;
            if (!string.IsNullOrEmpty(blockerId))
            {
                if (blockerId == "leader" && defender.leader.CanBlock)
                {
                    blocked = true;
                    Log("BLOCK_DECLARED", "Leader blocks");
                }
                else
                {
                    blockerUnit = FindUnit(defender, blockerId);
                    if (blockerUnit != null && blockerUnit.CanBlock)
                    {
                        blocked = true;
                        Log("BLOCK_DECLARED", $"{blockerUnit.cardData.cardName} blocks");
                    }
                }
            }

            if (blocked)
            {
                // Combat vs blocker — no HP damage
                int defPower;
                if (blockerUnit != null)
                {
                    defPower = blockerUnit.currentPower;
                    ResolvePowerComparison(attackPower, defPower, side, decl, blockerUnit, ref result);
                }
                else
                {
                    defPower = defender.leader.currentPower;
                    if (attackPower < defPower || attackPower == defPower)
                        DestroyAttacker(side, decl, ref result);
                }
                Log("BATTLE_RESOLVED", $"Blocked. AtkPow={attackPower}");
            }
            else if (decl.targetType == TargetType.Leader)
            {
                // §10.5: Direct hit to wish master
                result.directHit = true;

                // §8.2: If defender is already Final → 鯱鉾勝利
                if (defender.isFinal)
                {
                    result.finalBlow = true;
                    Log("FINAL_BLOW", $"{attackerName} delivers final blow to {defender.side}");
                    var winResult = side == PlayerSide.Player1 ? MatchResult.Player1Win : MatchResult.Player2Win;
                    EndMatch(winResult);
                    OnStateChanged?.Invoke();
                    return result;
                }

                int wishDmg = attackWishDmg;

                // Algorithm direct hit bonus (face-down algorithms have no active effects)
                if (State.sharedAlgo != null && !State.sharedAlgo.isFaceDown)
                {
                    var algo = State.sharedAlgo.cardData;
                    if (algo.globalRule != null && algo.globalRule.kind == "direct_hit_plus")
                        wishDmg += algo.globalRule.value;
                    if (State.sharedAlgo.owner == side && algo.ownerBonus != null && algo.ownerBonus.kind == "direct_hit_plus")
                        wishDmg += algo.ownerBonus.value;
                }

                // Apply HP damage to defender
                ApplyHpDamage(defender, wishDmg, attackWishDmgType);
                result.hpDamage = wishDmg;

                Log("DIRECT_HIT", $"{attackerName} deals {wishDmg}% ({attackWishDmgType}) wish damage to {defender.side}");

                // Check if this direct hit caused Final + was already pending
                // (Final is set inside ApplyHpDamage, but 鯱鉾勝利 requires the NEXT hit after Final)
            }
            else
            {
                // Attack vs target unit
                var targetUnit = FindUnit(defender, decl.targetInstanceId);
                if (targetUnit != null)
                {
                    int defPower = targetUnit.currentPower;
                    ResolvePowerComparison(attackPower, defPower, side, decl, targetUnit, ref result);
                    Log("BATTLE_RESOLVED", $"AtkPow={attackPower} vs DefPow={defPower}");
                }
            }

            OnAttackResolved?.Invoke(side, attackerName, targetName, result.directHit);
            if (result.directHit && result.hpDamage > 0)
                OnHpDamaged?.Invoke(side == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1, result.hpDamage);
            OnStateChanged?.Invoke();
            return result;
        }

        void ResolvePowerComparison(int atkPow, int defPow, PlayerSide side, AttackDeclaration decl, FieldUnit defUnit, ref CombatResult res)
        {
            var defenderSide = side == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;

            if (atkPow > defPow)
            {
                DestroyUnit(defenderSide, defUnit);
                res.defenderDestroyed = true;
            }
            else if (atkPow < defPow)
            {
                DestroyAttacker(side, decl, ref res);
            }
            else
            {
                DestroyUnit(defenderSide, defUnit);
                res.defenderDestroyed = true;
                DestroyAttacker(side, decl, ref res);
            }
        }

        void DestroyAttacker(PlayerSide side, AttackDeclaration decl, ref CombatResult res)
        {
            if (decl.attackerType == AttackerType.Unit)
            {
                var attacker = State.GetPlayer(side);
                var unit = FindUnit(attacker, decl.attackerInstanceId);
                if (unit != null)
                    DestroyUnit(side, unit);
            }
            res.attackerDestroyed = true;
        }

        /// <summary>
        /// ユニットを退場させ、OnDeath効果を発動する (§11.1 退場時効果)
        /// </summary>
        public void DestroyUnit(PlayerSide ownerSide, FieldUnit unit)
        {
            var owner = State.GetPlayer(ownerSide);
            owner.field.Remove(unit);
            owner.graveyard.Add(unit.cardData);
            OnUnitDestroyed?.Invoke(ownerSide, unit.cardData.cardName);

            switch (unit.cardData.effectKey)
            {
                case "SUMMON_ON_DEATH_DRAW":
                    EffectResolver.ResolveOnDeathDraw(State, ownerSide, unit.cardData);
                    Log("ON_DEATH_EFFECT", $"{unit.cardData.cardName}: draw");
                    break;
                case "SUMMON_ON_DEATH_HP_DAMAGE":
                    EffectResolver.ResolveOnDeathHpDamage(State, ownerSide, unit.cardData, this);
                    Log("ON_DEATH_EFFECT", $"{unit.cardData.cardName}: HP damage");
                    break;
            }
        }

        FieldUnit FindUnit(PlayerState p, string instanceId)
        {
            return p.field.Find(u => u.instanceId == instanceId);
        }

        // ====================================================================
        // 願成チャージ (CP1消費で願成+1)
        // 「願いは、追い詰められるほど強くなる」— 能動的な成長手段
        // ====================================================================

        public bool CanChargeEvo(PlayerSide side)
        {
            if (State.isGameOver || State.activePlayer != side) return false;
            if (State.currentPhase != TurnPhase.Main) return false;
            var p = State.GetPlayer(side);
            if (p.currentCP < 1) return false;
            if (p.leader.level >= p.leader.baseData.levelCap) return false;
            return true;
        }

        public void ChargeEvo(PlayerSide side)
        {
            if (!CanChargeEvo(side))
                throw new InvalidOperationException("Cannot charge evo.");

            var p = State.GetPlayer(side);
            p.currentCP -= 1;
            p.leader.evoGauge++;
            Log("EVO_CHARGE", $"{side}: CP1 spent, evoGauge -> {p.leader.evoGauge}");
            CheckLevelUp(p);
            OnStateChanged?.Invoke();
        }

        // ====================================================================
        // §11.1c Leader Skill
        // ====================================================================

        public bool CanUseLeaderSkill(PlayerSide side, int skillLevel)
        {
            if (State.isGameOver || State.activePlayer != side) return false;
            if (State.currentPhase != TurnPhase.Main && State.currentPhase != TurnPhase.Combat) return false;
            return State.GetPlayer(side).leader.CanUseSkill(skillLevel);
        }

        public void UseLeaderSkill(PlayerSide side, int skillLevel)
        {
            if (!CanUseLeaderSkill(side, skillLevel))
                throw new InvalidOperationException($"Cannot use leader skill Lv{skillLevel}.");

            var p = State.GetPlayer(side);
            int idx = skillLevel - 2;
            var skill = p.leader.baseData.leaderSkills[idx];

            EffectResolver.ResolveLeaderSkill(State, side, skill, this);

            p.leader.skillUsedThisGame[idx] = true;
            p.leader.skillUsedThisTurn = true;

            Log("LEADER_SKILL", $"{p.leader.baseData.leaderName} Lv{skillLevel}: {skill.name}");
            OnStateChanged?.Invoke();
        }

        // ====================================================================
        // §11.1 Ambush
        // ====================================================================

        public bool CanPlayAmbush(PlayerSide side, int handIndex, string trigger)
        {
            var p = State.GetPlayer(side);
            if (handIndex < 0 || handIndex >= p.hand.Count) return false;
            var card = p.hand[handIndex];
            if (!card.HasKeyword("Ambush")) return false;
            if (p.AvailableSlots <= 0) return false;

            var ambushType = card.ambushType;
            if (trigger == "defend" && ambushType == "defend") return true;
            if (trigger == "retaliate" && ambushType == "retaliate") return true;
            return false;
        }

        public void PlayAmbush(PlayerSide side, int handIndex)
        {
            var p = State.GetPlayer(side);
            var card = p.hand[handIndex];
            p.hand.RemoveAt(handIndex);

            var unit = new FieldUnit(card, State.NextInstanceId());
            unit.row = FieldRow.Front;
            unit.summonSick = false;
            unit.status = UnitStatus.Ready;
            ApplyAlgorithmToUnit(unit, side);
            p.field.Add(unit);

            Log("AMBUSH_PLAY", $"{card.cardName} ({card.id}) by {side} [{card.ambushType}]");

            switch (card.effectKey)
            {
                case "SUMMON_AMBUSH_DEFEND_BUFF":
                    unit.currentPower += card.selfPowerDelta;
                    Log("AMBUSH_EFFECT", $"{card.cardName}: self power +{card.selfPowerDelta}");
                    break;
                case "SUMMON_AMBUSH_RETALIATE_DRAW":
                    EffectResolver.ResolveOnPlayDraw(State, side, card.drawCount > 0 ? card.drawCount : 1);
                    Log("AMBUSH_EFFECT", $"{card.cardName}: draw {card.drawCount}");
                    break;
                case "SUMMON_AMBUSH_RETALIATE_DAMAGE":
                    EffectResolver.ResolveOnPlayHpDamage(State, side, card, this);
                    Log("AMBUSH_EFFECT", $"{card.cardName}: HP damage {card.hpDamagePercent}%");
                    break;
            }

            OnStateChanged?.Invoke();
        }

        // ====================================================================
        // §10.1a Turn Timer / Timeout
        // ====================================================================

        public void HandleTurnTimeout(PlayerSide side)
        {
            if (State.isGameOver) return;
            if (State.activePlayer != side) return;

            if (side == PlayerSide.Player1)
                State.p1ConsecutiveTimeouts++;
            else
                State.p2ConsecutiveTimeouts++;

            int count = side == PlayerSide.Player1
                ? State.p1ConsecutiveTimeouts
                : State.p2ConsecutiveTimeouts;

            Log("TURN_TIMEOUT", $"{side} timeout #{count}");

            if (count >= BalanceConfig.ConsecutiveTimeoutLimit)
            {
                Log("AUTO_LOSE", $"{side} loses by {BalanceConfig.ConsecutiveTimeoutLimit} consecutive timeouts");
                EndMatch(side == PlayerSide.Player1 ? MatchResult.Player2Win : MatchResult.Player1Win);
                return;
            }

            EndTurn();
        }

        void ResetTimeoutStreak(PlayerSide side)
        {
            if (side == PlayerSide.Player1)
                State.p1ConsecutiveTimeouts = 0;
            else
                State.p2ConsecutiveTimeouts = 0;
        }

        // ====================================================================
        // §8 Victory Conditions + End Turn
        // ====================================================================

        public void EndTurn()
        {
            if (State.isGameOver) return;

            ResetTimeoutStreak(State.activePlayer);

            State.currentPhase = TurnPhase.End;
            Log("END_TURN", $"{State.activePlayer}");

            // Turn limit check (§8.4: 塗り勝利) — モード別ターン上限
            int turnLimit = MatchModeConfig.Get(State.matchMode).turnLimit;
            if (State.turnTotal >= turnLimit)
            {
                ResolveTurnLimit();
                return;
            }

            // Switch active player
            State.activePlayer = State.activePlayer == PlayerSide.Player1
                ? PlayerSide.Player2
                : PlayerSide.Player1;

            StartTurn();
        }

        /// <summary>
        /// §8.4 塗り勝利: 24ターン経過時の残りHP比較
        /// </summary>
        void ResolveTurnLimit()
        {
            int hp1 = State.player1.hp;
            int hp2 = State.player2.hp;

            Log("TURN_LIMIT", $"Turn limit reached. P1 HP={hp1}, P2 HP={hp2}");

            if (hp1 > hp2)
                EndMatch(MatchResult.Player1Win);
            else if (hp2 > hp1)
                EndMatch(MatchResult.Player2Win);
            else
                EndMatch(MatchResult.Draw);
        }

        void EndMatch(MatchResult result)
        {
            State.isGameOver = true;
            State.result = result;
            Log("MATCH_END", result.ToString());
            OnMatchEnd?.Invoke(result);
            OnStateChanged?.Invoke();
        }

        public void Log(string eventType, string detail)
        {
            var entry = new BattleLogEntry(eventType, detail, State.turnTotal);
            State.log.Add(entry);
            OnLog?.Invoke(entry);
        }
    }
}
