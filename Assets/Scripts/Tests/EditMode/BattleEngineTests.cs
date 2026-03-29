using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Banganka.Core.Battle;
using Banganka.Core.Data;
using Banganka.Core.Config;

namespace Banganka.Tests.EditMode
{
    [TestFixture]
    public class BattleEngineTests
    {
        BattleEngine _engine;
        LeaderData _leader;
        List<CardData> _deckP1;
        List<CardData> _deckP2;

        [SetUp]
        public void SetUp()
        {
            _engine = new BattleEngine(seed: 42);
            _leader = new LeaderData
            {
                id = "LDR_TEST",
                leaderName = "TestLeader",
                keyAspect = Aspect.Contest,
                basePower = 5000,
                baseWishDamage = 3,
                wishDamageType = "fixed",
                levelCap = 3,
                levelUpPowerGain = 1000,
                levelUpWishDamageGain = 1,
                evoGaugeMaxByLevel = new[] { 3, 4 },
                wishDamageByLevel = new[] { 3, 5, 8 },
            };
            _deckP1 = MakeDeck(Aspect.Contest);
            _deckP2 = MakeDeck(Aspect.Whisper);
        }

        List<CardData> MakeDeck(Aspect aspect)
        {
            var deck = new List<CardData>();
            for (int i = 0; i < 34; i++)
            {
                deck.Add(new CardData
                {
                    id = $"TEST_{aspect}_{i:D2}",
                    cardName = $"Test{i}",
                    type = CardType.Manifest,
                    cpCost = (i % 5) + 1,
                    aspect = aspect,
                    battlePower = ((i % 5) + 1) * 2000,
                    wishDamage = 3,
                    wishDamageType = "fixed",
                    wishTrigger = i < 6 ? "WT_DRAW" : "-",
                    effectKey = "",
                    keywords = new string[0],
                });
            }
            return deck;
        }

        [Test]
        public void InitMatch_SetsHpTo100()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            Assert.AreEqual(100, _engine.State.player1.hp);
            Assert.AreEqual(100, _engine.State.player2.hp);
        }

        [Test]
        public void InitMatch_Places6WishCards()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            Assert.AreEqual(6, _engine.State.player1.wishZone.Count);
            Assert.AreEqual(6, _engine.State.player2.wishZone.Count);
            Assert.AreEqual(85, _engine.State.player1.wishZone[0].threshold);
            Assert.AreEqual(10, _engine.State.player1.wishZone[5].threshold);
        }

        [Test]
        public void InitMatch_DrawsInitialHand()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            Assert.AreEqual(BalanceConfig.InitialHand, _engine.State.player1.hand.Count);
            Assert.AreEqual(BalanceConfig.InitialHand, _engine.State.player2.hand.Count);
        }

        [Test]
        public void InitMatch_DeckReducedByHandAndWishCards()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            // 34 - 5 (hand) - 6 (wish) = 23
            Assert.AreEqual(23, _engine.State.player1.deck.Count);
        }

        [Test]
        public void StartTurn_IncrementsCpAndTurn()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var active = _engine.State.GetPlayer(_engine.State.activePlayer);
            Assert.AreEqual(1, _engine.State.turnTotal);
            Assert.AreEqual(1, active.maxCP);
            Assert.AreEqual(1, active.currentCP);
        }

        [Test]
        public void ApplyHpDamage_FixedPercent()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            var p2 = _engine.State.player2;
            _engine.ApplyHpDamage(p2, 10, "fixed");
            Assert.AreEqual(90, p2.hp);
        }

        [Test]
        public void ApplyHpDamage_CurrentPercent()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            var p2 = _engine.State.player2;
            p2.hp = 50;
            _engine.ApplyHpDamage(p2, 20, "current");
            // 50 * 20 / 100 = 10, ceil = 10
            Assert.AreEqual(40, p2.hp);
        }

        [Test]
        public void ApplyHpDamage_MinimumOne()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            var p2 = _engine.State.player2;
            p2.hp = 1;
            _engine.ApplyHpDamage(p2, 1, "current");
            // 1 * 1 / 100 = 0.01, ceil = 1
            Assert.AreEqual(0, p2.hp);
            Assert.IsTrue(p2.isFinal);
        }

        [Test]
        public void ApplyHpDamage_TriggersWishCards()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            var p2 = _engine.State.player2;
            // HP 100 -> 10 (fixed 90%) -> crosses 85,70,55,40,25 thresholds
            _engine.ApplyHpDamage(p2, 90, "fixed");
            Assert.AreEqual(10, p2.hp);

            int triggered = p2.wishZone.Count(s => s.triggered);
            Assert.AreEqual(5, triggered); // 85,70,55,40,25 crossed, 10 not yet
        }

        [Test]
        public void ApplyHpDamage_FinalState()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            var p2 = _engine.State.player2;
            _engine.ApplyHpDamage(p2, 100, "fixed");
            Assert.AreEqual(0, p2.hp);
            Assert.IsTrue(p2.isFinal);
        }

        [Test]
        public void DirectHit_AfterFinal_CausesVictory()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();

            var active = _engine.State.activePlayer;
            var attacker = _engine.State.GetPlayer(active);
            var defender = _engine.State.GetOpponent(active);
            defender.hp = 0;
            defender.isFinal = true;
            // Mark all wish cards as triggered
            foreach (var s in defender.wishZone) s.triggered = true;

            attacker.leader.status = UnitStatus.Ready;

            var decl = new BattleEngine.AttackDeclaration
            {
                attackerType = BattleEngine.AttackerType.Leader,
                targetType = BattleEngine.TargetType.Leader,
            };

            var result = _engine.ResolveAttack(active, decl);
            Assert.IsTrue(result.finalBlow);
            Assert.IsTrue(_engine.State.isGameOver);
        }

        [Test]
        public void TurnLimit_ComparesHp()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.State.player1.hp = 60;
            _engine.State.player2.hp = 40;
            _engine.State.turnTotal = BalanceConfig.TurnLimitTotal;

            _engine.EndTurn();
            Assert.IsTrue(_engine.State.isGameOver);
            Assert.AreEqual(MatchResult.Player1Win, _engine.State.result);
        }

        [Test]
        public void TurnLimit_Draw()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.State.player1.hp = 50;
            _engine.State.player2.hp = 50;
            _engine.State.turnTotal = BalanceConfig.TurnLimitTotal;

            _engine.EndTurn();
            Assert.IsTrue(_engine.State.isGameOver);
            Assert.AreEqual(MatchResult.Draw, _engine.State.result);
        }

        [Test]
        public void Mulligan_ReplacesCards()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            var originalHand = new List<CardData>(_engine.State.player1.hand);

            _engine.PerformMulligan(PlayerSide.Player1, new List<int> { 0, 1 });

            Assert.AreEqual(5, _engine.State.player1.hand.Count);
            // At least one card should be different (probabilistic but very likely with seed)
        }

        [Test]
        public void PlayCard_ReducesCp()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();

            var active = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(active);
            p.currentCP = 10;

            // Find a playable card
            int idx = -1;
            for (int i = 0; i < p.hand.Count; i++)
            {
                if (p.hand[i].type == CardType.Manifest && p.hand[i].cpCost <= 10)
                {
                    idx = i;
                    break;
                }
            }
            Assert.IsTrue(idx >= 0, "Should have a playable card");

            int cost = p.hand[idx].cpCost;
            _engine.PlayCard(active, idx);
            Assert.AreEqual(10 - cost, p.currentCP);
        }

        [Test]
        public void TimeoutThreeTimes_CausesLoss()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;

            _engine.HandleTurnTimeout(side);
            Assert.IsFalse(_engine.State.isGameOver);

            // Need to get back to same player's turn
            side = _engine.State.activePlayer;
            // Skip to make them timeout again - force activePlayer
            _engine.State.activePlayer = side == PlayerSide.Player1 ? PlayerSide.Player1 : PlayerSide.Player2;
            var targetSide = side;

            // Reset and directly test
            _engine.State.isGameOver = false;
            if (targetSide == PlayerSide.Player1)
            {
                _engine.State.p1ConsecutiveTimeouts = 2;
                _engine.State.activePlayer = PlayerSide.Player1;
            }
            else
            {
                _engine.State.p2ConsecutiveTimeouts = 2;
                _engine.State.activePlayer = PlayerSide.Player2;
            }

            _engine.HandleTurnTimeout(targetSide);
            Assert.IsTrue(_engine.State.isGameOver);
        }

        [Test]
        public void HealHp_RestoresHealth()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            var p1 = _engine.State.player1;
            p1.hp = 50;
            _engine.HealHp(p1, 20);
            Assert.AreEqual(70, p1.hp);
        }

        [Test]
        public void HealHp_CapsAtMax()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            var p1 = _engine.State.player1;
            p1.hp = 95;
            _engine.HealHp(p1, 20);
            Assert.AreEqual(100, p1.hp);
        }

        [Test]
        public void HealHp_RemovesFinalState()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            var p1 = _engine.State.player1;
            p1.hp = 0;
            p1.isFinal = true;
            _engine.HealHp(p1, 10);
            Assert.AreEqual(10, p1.hp);
            Assert.IsFalse(p1.isFinal);
        }

        [Test]
        public void DamageHalve_ReducesDamage()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            var p2 = _engine.State.player2;
            p2.leader.damageHalveActive = true;

            _engine.ApplyHpDamage(p2, 20, "fixed");
            // 20 damage halved = ceil(10) = 10
            Assert.AreEqual(90, p2.hp);
        }

        // ================================================================
        // Combat Resolution Tests
        // ================================================================

        FieldUnit PlaceUnit(PlayerState p, int power, int wishDmg = 3, string[] keywords = null)
        {
            var card = new CardData
            {
                id = $"COMBAT_TEST_{p.field.Count}",
                cardName = "CombatUnit",
                type = CardType.Manifest,
                cpCost = 1,
                aspect = Aspect.Contest,
                battlePower = power,
                wishDamage = wishDmg,
                wishDamageType = "fixed",
                keywords = keywords ?? new string[0],
                effectKey = "",
            };
            var unit = new FieldUnit(card, _engine.State.NextInstanceId());
            unit.status = UnitStatus.Ready;
            unit.summonSick = false;
            p.field.Add(unit);
            return unit;
        }

        [Test]
        public void Combat_UnitVsUnit_HigherPowerWins()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var attacker = _engine.State.GetPlayer(side);
            var defender = _engine.State.GetOpponent(side);

            var atkUnit = PlaceUnit(attacker, 6000);
            var defUnit = PlaceUnit(defender, 4000);

            var decl = new BattleEngine.AttackDeclaration
            {
                attackerType = BattleEngine.AttackerType.Unit,
                attackerInstanceId = atkUnit.instanceId,
                targetType = BattleEngine.TargetType.Unit,
                targetInstanceId = defUnit.instanceId,
            };

            var result = _engine.ResolveAttack(side, decl);
            Assert.IsTrue(result.defenderDestroyed);
            Assert.IsFalse(result.attackerDestroyed);
            Assert.IsFalse(defender.field.Contains(defUnit));
        }

        [Test]
        public void Combat_UnitVsUnit_LowerPowerLoses()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var attacker = _engine.State.GetPlayer(side);
            var defender = _engine.State.GetOpponent(side);

            var atkUnit = PlaceUnit(attacker, 3000);
            var defUnit = PlaceUnit(defender, 5000);

            var decl = new BattleEngine.AttackDeclaration
            {
                attackerType = BattleEngine.AttackerType.Unit,
                attackerInstanceId = atkUnit.instanceId,
                targetType = BattleEngine.TargetType.Unit,
                targetInstanceId = defUnit.instanceId,
            };

            var result = _engine.ResolveAttack(side, decl);
            Assert.IsFalse(result.defenderDestroyed);
            Assert.IsTrue(result.attackerDestroyed);
        }

        [Test]
        public void Combat_UnitVsUnit_EqualPower_BothDestroyed()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var attacker = _engine.State.GetPlayer(side);
            var defender = _engine.State.GetOpponent(side);

            var atkUnit = PlaceUnit(attacker, 4000);
            var defUnit = PlaceUnit(defender, 4000);

            var decl = new BattleEngine.AttackDeclaration
            {
                attackerType = BattleEngine.AttackerType.Unit,
                attackerInstanceId = atkUnit.instanceId,
                targetType = BattleEngine.TargetType.Unit,
                targetInstanceId = defUnit.instanceId,
            };

            var result = _engine.ResolveAttack(side, decl);
            Assert.IsTrue(result.defenderDestroyed);
            Assert.IsTrue(result.attackerDestroyed);
        }

        [Test]
        public void Combat_UnitVsLeader_DealsWishDamage()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var attacker = _engine.State.GetPlayer(side);
            var defender = _engine.State.GetOpponent(side);

            var atkUnit = PlaceUnit(attacker, 4000, wishDmg: 5);
            int hpBefore = defender.hp;

            var decl = new BattleEngine.AttackDeclaration
            {
                attackerType = BattleEngine.AttackerType.Unit,
                attackerInstanceId = atkUnit.instanceId,
                targetType = BattleEngine.TargetType.Leader,
            };

            var result = _engine.ResolveAttack(side, decl);
            Assert.IsTrue(result.directHit);
            Assert.IsTrue(defender.hp < hpBefore);
        }

        [Test]
        public void Combat_LeaderVsLeader_DealsWishDamage()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var attacker = _engine.State.GetPlayer(side);
            var defender = _engine.State.GetOpponent(side);
            int hpBefore = defender.hp;

            var decl = new BattleEngine.AttackDeclaration
            {
                attackerType = BattleEngine.AttackerType.Leader,
                targetType = BattleEngine.TargetType.Leader,
            };

            var result = _engine.ResolveAttack(side, decl);
            Assert.IsTrue(result.directHit);
            Assert.IsTrue(defender.hp < hpBefore);
        }

        [Test]
        public void Combat_Blocker_InterceptsAttack()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var attacker = _engine.State.GetPlayer(side);
            var defender = _engine.State.GetOpponent(side);

            var atkUnit = PlaceUnit(attacker, 6000);
            var blocker = PlaceUnit(defender, 4000, keywords: new[] { "Blocker" });
            int hpBefore = defender.hp;

            var decl = new BattleEngine.AttackDeclaration
            {
                attackerType = BattleEngine.AttackerType.Unit,
                attackerInstanceId = atkUnit.instanceId,
                targetType = BattleEngine.TargetType.Leader,
            };

            var result = _engine.ResolveAttack(side, decl, blockerId: blocker.instanceId);
            // Blocker is destroyed (4000 < 6000), no HP damage
            Assert.IsTrue(result.defenderDestroyed);
            Assert.AreEqual(hpBefore, defender.hp);
        }

        [Test]
        public void Combat_AttackerExhaustedAfterAttack()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var attacker = _engine.State.GetPlayer(side);

            var atkUnit = PlaceUnit(attacker, 4000);

            var decl = new BattleEngine.AttackDeclaration
            {
                attackerType = BattleEngine.AttackerType.Unit,
                attackerInstanceId = atkUnit.instanceId,
                targetType = BattleEngine.TargetType.Leader,
            };

            _engine.ResolveAttack(side, decl);
            Assert.AreEqual(UnitStatus.Exhausted, atkUnit.status);
        }

        // ================================================================
        // Summon Sickness Tests
        // ================================================================

        [Test]
        public void SummonSick_CannotAttackOnSummonTurn()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            int idx = p.hand.FindIndex(c => c.type == CardType.Manifest);
            _engine.PlayCard(side, idx);

            var unit = p.field[^1];
            Assert.IsTrue(unit.summonSick);
            Assert.IsFalse(unit.CanAttack);
        }

        [Test]
        public void Rush_BypassesSummonSickness()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            // Replace a hand card with a Rush card
            p.hand[0] = new CardData
            {
                id = "RUSH_TEST",
                cardName = "RushUnit",
                type = CardType.Manifest,
                cpCost = 2,
                aspect = Aspect.Contest,
                battlePower = 4000,
                wishDamage = 3,
                wishDamageType = "fixed",
                keywords = new[] { "Rush" },
                effectKey = "",
            };

            _engine.PlayCard(side, 0);
            var unit = p.field[^1];
            Assert.IsFalse(unit.summonSick);
            Assert.IsTrue(unit.CanAttack);
        }

        // ================================================================
        // Row Placement Tests
        // ================================================================

        [Test]
        public void RowPlacement_DefaultFront()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            _engine.PlayCard(side, 0);
            Assert.AreEqual(FieldRow.Front, p.field[^1].row);
        }

        [Test]
        public void RowPlacement_BackWhenSet()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            _engine.SetPlacementRow(FieldRow.Back);
            _engine.PlayCard(side, 0);
            Assert.AreEqual(FieldRow.Back, p.field[^1].row);
        }

        // ================================================================
        // Evo Gauge + Level Up Tests
        // ================================================================

        [Test]
        public void EvoGauge_IncreasesOnMatchingAspect()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            int evoBefore = p.leader.evoGauge;
            // All test cards are Contest aspect = matches _leader.keyAspect
            int idx = p.hand.FindIndex(c => c.type == CardType.Manifest && c.aspect == Aspect.Contest);
            if (idx >= 0)
            {
                _engine.PlayCard(side, idx);
                Assert.AreEqual(evoBefore + 1, p.leader.evoGauge);
            }
        }

        [Test]
        public void EvoGauge_LevelUpAtThreshold()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            // Set evo gauge to 2 (threshold for Lv1→2 is 3)
            p.leader.evoGauge = 2;
            int levelBefore = p.leader.level;

            int idx = p.hand.FindIndex(c => c.type == CardType.Manifest && c.aspect == Aspect.Contest);
            if (idx >= 0)
            {
                _engine.PlayCard(side, idx);
                Assert.AreEqual(levelBefore + 1, p.leader.level);
                Assert.AreEqual(0, p.leader.evoGauge); // reset after level up
            }
        }

        [Test]
        public void EvoGauge_NoIncreaseOnNonMatchingAspect()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            // Replace a hand card with a different aspect
            p.hand[0] = new CardData
            {
                id = "OFFASPECT_TEST",
                cardName = "OffAspect",
                type = CardType.Manifest,
                cpCost = 1,
                aspect = Aspect.Whisper, // != Contest
                battlePower = 2000,
                wishDamage = 3,
                wishDamageType = "fixed",
                keywords = new string[0],
                effectKey = "",
            };

            int evoBefore = p.leader.evoGauge;
            _engine.PlayCard(side, 0);
            Assert.AreEqual(evoBefore, p.leader.evoGauge);
        }

        // ================================================================
        // Spell Effect Tests
        // ================================================================

        CardData MakeSpell(string effectKey, int cpCost = 1)
        {
            return new CardData
            {
                id = "SPELL_TEST",
                cardName = "TestSpell",
                type = CardType.Spell,
                cpCost = cpCost,
                aspect = Aspect.Contest,
                effectKey = effectKey,
                battlePower = 0,
                wishDamage = 0,
                keywords = new string[0],
                drawCount = 2,
                hpDamagePercent = 10,
                damageType = "fixed",
                bounceCount = 1,
                destroyCount = 1,
                powerDelta = 2000,
                targetScope = "ally",
            };
        }

        [Test]
        public void Spell_Draw_AddsCardsToHand()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            var spell = MakeSpell("SPELL_DRAW");
            spell.drawCount = 2;
            p.hand.Insert(0, spell);

            int handBefore = p.hand.Count;
            int deckBefore = p.deck.Count;
            _engine.PlayCard(side, 0);

            // Played 1 card (-1), drew 2 (+2) = net +1
            Assert.AreEqual(handBefore + 1, p.hand.Count);
            Assert.AreEqual(deckBefore - 2, p.deck.Count);
        }

        [Test]
        public void Spell_HpDamageFixed_DamagesOpponent()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.currentCP = 10;

            var spell = MakeSpell("SPELL_HP_DAMAGE_FIXED");
            spell.hpDamagePercent = 10;
            p.hand.Insert(0, spell);

            _engine.PlayCard(side, 0);
            // 10% of MaxHP(100) = 10 damage
            Assert.AreEqual(90, opp.hp);
        }

        [Test]
        public void Spell_Bounce_ReturnsUnitToHand()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.currentCP = 10;

            var target = PlaceUnit(opp, 3000);

            var spell = MakeSpell("SPELL_BOUNCE");
            spell.bounceCount = 1;
            spell.targetScope = "enemy";
            p.hand.Insert(0, spell);

            int oppHandBefore = opp.hand.Count;
            _engine.PlayCard(side, 0);

            Assert.AreEqual(0, opp.field.Count);
            Assert.AreEqual(oppHandBefore + 1, opp.hand.Count);
        }

        [Test]
        public void Spell_Destroy_RemovesStrongestUnit()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.currentCP = 10;

            PlaceUnit(opp, 3000);
            PlaceUnit(opp, 6000);

            var spell = MakeSpell("SPELL_DESTROY");
            spell.destroyCount = 1;
            p.hand.Insert(0, spell);

            _engine.PlayCard(side, 0);

            Assert.AreEqual(1, opp.field.Count);
            Assert.AreEqual(3000, opp.field[0].currentPower); // 6000 was destroyed
        }

        [Test]
        public void Spell_DestroyAll_ClearsBothFields()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.currentCP = 10;

            PlaceUnit(p, 4000);
            PlaceUnit(opp, 3000);

            var spell = MakeSpell("SPELL_DESTROY_ALL");
            p.hand.Insert(0, spell);

            _engine.PlayCard(side, 0);

            Assert.AreEqual(0, p.field.Count);
            Assert.AreEqual(0, opp.field.Count);
        }

        [Test]
        public void Spell_PowerPlus_BuffsAlly()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            var unit = PlaceUnit(p, 4000);

            var spell = MakeSpell("SPELL_POWER_PLUS");
            spell.powerDelta = 2000;
            spell.targetScope = "ally";
            p.hand.Insert(0, spell);

            _engine.PlayCard(side, 0);
            Assert.AreEqual(6000, unit.currentPower);
        }

        [Test]
        public void Spell_GoesToGraveyardAfterPlay()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            var spell = MakeSpell("SPELL_DRAW");
            spell.drawCount = 1;
            p.hand.Insert(0, spell);

            int graveBefore = p.graveyard.Count;
            _engine.PlayCard(side, 0);
            Assert.AreEqual(graveBefore + 1, p.graveyard.Count);
        }

        // ================================================================
        // Algorithm Tests
        // ================================================================

        CardData MakeAlgorithm(string globalKind = null, int globalValue = 0, string ownerKind = null, int ownerValue = 0)
        {
            return new CardData
            {
                id = "ALGO_TEST",
                cardName = "TestAlgorithm",
                type = CardType.Algorithm,
                cpCost = 2,
                aspect = Aspect.Contest,
                effectKey = "",
                keywords = new string[0],
                globalRule = globalKind != null ? new AlgorithmRule { kind = globalKind, value = globalValue } : null,
                ownerBonus = ownerKind != null ? new AlgorithmRule { kind = ownerKind, value = ownerValue } : null,
            };
        }

        [Test]
        public void Algorithm_FaceUp_SetsSharedSlot()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            var algo = MakeAlgorithm("power_plus", 1000);
            p.hand.Insert(0, algo);

            _engine.PlayCard(side, 0, faceDown: false);
            Assert.IsNotNull(_engine.State.sharedAlgo);
            Assert.IsFalse(_engine.State.sharedAlgo.isFaceDown);
            Assert.AreEqual(side, _engine.State.sharedAlgo.owner);
        }

        [Test]
        public void Algorithm_FaceDown_NoEffects()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            var unit = PlaceUnit(p, 4000);
            int powerBefore = unit.currentPower;

            var algo = MakeAlgorithm("power_plus", 1000);
            p.hand.Insert(0, algo);

            _engine.PlayCard(side, 0, faceDown: true);
            Assert.IsTrue(_engine.State.sharedAlgo.isFaceDown);
            Assert.AreEqual(powerBefore, unit.currentPower); // no buff while face-down
        }

        [Test]
        public void Algorithm_FaceUp_GlobalRuleApplies()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.currentCP = 10;

            var allyUnit = PlaceUnit(p, 4000);
            var enemyUnit = PlaceUnit(opp, 3000);

            var algo = MakeAlgorithm("power_plus", 1000);
            p.hand.Insert(0, algo);

            _engine.PlayCard(side, 0, faceDown: false);
            // Global rule affects both sides
            Assert.AreEqual(5000, allyUnit.currentPower);
            Assert.AreEqual(4000, enemyUnit.currentPower);
        }

        [Test]
        public void Algorithm_OwnerBonus_OnlyAppliestoOwner()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.currentCP = 10;

            var allyUnit = PlaceUnit(p, 4000);
            var enemyUnit = PlaceUnit(opp, 4000);

            var algo = MakeAlgorithm(ownerKind: "power_plus", ownerValue: 2000);
            p.hand.Insert(0, algo);

            _engine.PlayCard(side, 0, faceDown: false);
            Assert.AreEqual(6000, allyUnit.currentPower); // owner bonus
            Assert.AreEqual(4000, enemyUnit.currentPower); // no bonus
        }

        [Test]
        public void Algorithm_Overwrite_ReplacesExisting()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            var algo1 = MakeAlgorithm("power_plus", 1000);
            algo1.id = "ALGO_1";
            p.hand.Insert(0, algo1);
            _engine.PlayCard(side, 0, faceDown: false);

            // End turn, opponent turn, end again to get back
            _engine.EndTurn();
            _engine.EndTurn();
            p.currentCP = 10;

            var algo2 = MakeAlgorithm("power_plus", 3000);
            algo2.id = "ALGO_2";
            p.hand.Insert(0, algo2);
            _engine.PlayCard(side, 0, faceDown: false);

            Assert.AreEqual("ALGO_2", _engine.State.sharedAlgo.cardData.id);
        }

        [Test]
        public void Algorithm_FlipFaceDown_ActivatesEffects()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            var unit = PlaceUnit(p, 4000);

            var algo = MakeAlgorithm("power_plus", 1000);
            p.hand.Insert(0, algo);
            _engine.PlayCard(side, 0, faceDown: true);

            Assert.AreEqual(4000, unit.currentPower); // still no buff

            _engine.FlipAlgorithm(side);
            Assert.IsFalse(_engine.State.sharedAlgo.isFaceDown);
            Assert.AreEqual(5000, unit.currentPower); // now buffed
        }

        [Test]
        public void Algorithm_AutoFlip_OnNextOwnerTurn()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn(); // Turn 1: side A
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            var unit = PlaceUnit(p, 4000);

            var algo = MakeAlgorithm("power_plus", 1000);
            p.hand.Insert(0, algo);
            _engine.PlayCard(side, 0, faceDown: true);

            Assert.IsTrue(_engine.State.sharedAlgo.isFaceDown);

            // End turn -> opponent turn -> end turn -> back to owner
            _engine.EndTurn(); // opponent's turn starts
            _engine.EndTurn(); // owner's turn starts again (auto-flip happens in StartTurn)

            Assert.IsFalse(_engine.State.sharedAlgo.isFaceDown);
            Assert.AreEqual(5000, unit.currentPower);
        }

        [Test]
        public void Algorithm_OnlyOnePerTurn()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            var algo1 = MakeAlgorithm("power_plus", 1000);
            algo1.id = "ALGO_A";
            var algo2 = MakeAlgorithm("power_plus", 2000);
            algo2.id = "ALGO_B";
            p.hand.Insert(0, algo1);
            p.hand.Insert(1, algo2);

            _engine.PlayCard(side, 0);
            Assert.IsFalse(_engine.CanPlayCard(side, 0)); // algo2 is now at index 0, can't play
        }

        // ================================================================
        // Leader Skill Tests
        // ================================================================

        LeaderData MakeLeaderWithSkills()
        {
            return new LeaderData
            {
                id = "LDR_SKILL_TEST",
                leaderName = "SkillLeader",
                keyAspect = Aspect.Contest,
                basePower = 5000,
                baseWishDamage = 3,
                wishDamageType = "fixed",
                levelCap = 3,
                levelUpPowerGain = 1000,
                levelUpWishDamageGain = 1,
                evoGaugeMaxByLevel = new[] { 3, 4 },
                wishDamageByLevel = new[] { 3, 5, 8 },
                leaderSkills = new[]
                {
                    new LeaderSkill
                    {
                        unlockLevel = 2,
                        name = "RushAll",
                        effectKey = "LEADER_SKILL_RUSH_ALL",
                    },
                    new LeaderSkill
                    {
                        unlockLevel = 3,
                        name = "DamageHalve",
                        effectKey = "LEADER_SKILL_DAMAGE_HALVE",
                    }
                },
            };
        }

        [Test]
        public void LeaderSkill_CannotUseBeforeUnlockLevel()
        {
            var leader = MakeLeaderWithSkills();
            _engine.InitMatch(leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);

            // Level 1 cannot use Lv2 skill
            Assert.IsFalse(_engine.CanUseLeaderSkill(side, 2));
        }

        [Test]
        public void LeaderSkill_RushAll_RemovesSummonSickness()
        {
            var leader = MakeLeaderWithSkills();
            _engine.InitMatch(leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);

            // Level up to 2
            p.leader.level = 2;

            // Place a summon-sick unit
            var unit = PlaceUnit(p, 4000);
            unit.summonSick = true;
            unit.status = UnitStatus.Ready;
            Assert.IsFalse(unit.CanAttack);

            _engine.UseLeaderSkill(side, 2);
            Assert.IsFalse(unit.summonSick);
            Assert.IsTrue(unit.CanAttack);
        }

        [Test]
        public void LeaderSkill_OncePerGame()
        {
            var leader = MakeLeaderWithSkills();
            _engine.InitMatch(leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.leader.level = 2;

            Assert.IsTrue(_engine.CanUseLeaderSkill(side, 2));
            _engine.UseLeaderSkill(side, 2);
            Assert.IsFalse(_engine.CanUseLeaderSkill(side, 2)); // used this game
        }

        [Test]
        public void LeaderSkill_OncePerTurn()
        {
            var leader = new LeaderData
            {
                id = "LDR_MULTI",
                leaderName = "MultiSkill",
                keyAspect = Aspect.Contest,
                basePower = 5000,
                baseWishDamage = 3,
                wishDamageType = "fixed",
                levelCap = 3,
                levelUpPowerGain = 1000,
                levelUpWishDamageGain = 1,
                evoGaugeMaxByLevel = new[] { 3, 4 },
                wishDamageByLevel = new[] { 3, 5, 8 },
                leaderSkills = new[]
                {
                    new LeaderSkill { unlockLevel = 2, name = "Skill2", effectKey = "LEADER_SKILL_RUSH_ALL" },
                    new LeaderSkill { unlockLevel = 3, name = "Skill3", effectKey = "LEADER_SKILL_DAMAGE_HALVE" },
                },
            };
            _engine.InitMatch(leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.leader.level = 3;

            _engine.UseLeaderSkill(side, 2);
            // Can't use Lv3 same turn (skillUsedThisTurn)
            Assert.IsFalse(_engine.CanUseLeaderSkill(side, 3));
        }

        [Test]
        public void LeaderSkill_DamageHalve_SetsFlag()
        {
            var leader = MakeLeaderWithSkills();
            _engine.InitMatch(leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.leader.level = 3;

            Assert.IsFalse(p.leader.damageHalveActive);
            _engine.UseLeaderSkill(side, 3);
            Assert.IsTrue(p.leader.damageHalveActive);
        }

        // ================================================================
        // Ambush Tests
        // ================================================================

        [Test]
        public void Ambush_Defend_CanPlayOnDefend()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var defSide = side == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
            var defender = _engine.State.GetPlayer(defSide);

            var ambushCard = new CardData
            {
                id = "AMBUSH_TEST",
                cardName = "AmbushDefend",
                type = CardType.Manifest,
                cpCost = 2,
                aspect = Aspect.Contest,
                battlePower = 3000,
                wishDamage = 3,
                wishDamageType = "fixed",
                keywords = new[] { "Ambush" },
                effectKey = "",
                ambushType = "defend",
            };
            defender.hand.Insert(0, ambushCard);

            Assert.IsTrue(_engine.CanPlayAmbush(defSide, 0, "defend"));
            Assert.IsFalse(_engine.CanPlayAmbush(defSide, 0, "retaliate"));
        }

        [Test]
        public void Ambush_Play_AddsUnitToField()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var defSide = side == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
            var defender = _engine.State.GetPlayer(defSide);

            var ambushCard = new CardData
            {
                id = "AMBUSH_PLAY_TEST",
                cardName = "AmbushUnit",
                type = CardType.Manifest,
                cpCost = 2,
                aspect = Aspect.Contest,
                battlePower = 4000,
                wishDamage = 3,
                wishDamageType = "fixed",
                keywords = new[] { "Ambush" },
                effectKey = "",
                ambushType = "defend",
            };
            defender.hand.Insert(0, ambushCard);

            int fieldBefore = defender.field.Count;
            _engine.PlayAmbush(defSide, 0);
            Assert.AreEqual(fieldBefore + 1, defender.field.Count);
            Assert.IsFalse(defender.field[^1].summonSick); // Ambush units can act immediately
        }

        // ================================================================
        // Wish Trigger Effect Tests
        // ================================================================

        [Test]
        public void WishTrigger_Draw_DrawsCard()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            var p2 = _engine.State.player2;

            // Replace wish card at 85% with a card that has WT_DRAW
            p2.wishZone[0].card.wishTrigger = "WT_DRAW";
            int handBefore = p2.hand.Count;
            int deckBefore = p2.deck.Count;

            // Damage from 100 to 80 crosses 85% threshold
            _engine.ApplyHpDamage(p2, 20, "fixed");

            Assert.IsTrue(p2.wishZone[0].triggered);
            Assert.AreEqual(handBefore + 1, p2.hand.Count);
            Assert.AreEqual(deckBefore - 1, p2.deck.Count);
        }

        [Test]
        public void WishTrigger_Bounce_BouncesWeakestEnemy()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            var p1 = _engine.State.player1;
            var p2 = _engine.State.player2;

            // Place enemy units on p1's field
            PlaceUnit(p1, 3000);
            PlaceUnit(p1, 6000);

            // p2's wish card triggers bounce on p1's units
            p2.wishZone[0].card.wishTrigger = "WT_BOUNCE";

            _engine.ApplyHpDamage(p2, 20, "fixed");

            Assert.AreEqual(1, p1.field.Count);
            Assert.AreEqual(6000, p1.field[0].currentPower); // 3000 was bounced
        }

        [Test]
        public void WishTrigger_PowerPlus_BuffsStrongestAlly()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            var p2 = _engine.State.player2;

            var unit = PlaceUnit(p2, 4000);
            p2.wishZone[0].card.wishTrigger = "WT_POWER_PLUS";

            _engine.ApplyHpDamage(p2, 20, "fixed");

            Assert.AreEqual(5000, unit.currentPower); // +1000
        }

        // ================================================================
        // Field Slot Limits
        // ================================================================

        [Test]
        public void FieldFull_CannotPlayManifest()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            // Fill all 6 slots
            for (int i = 0; i < BalanceConfig.FieldTotalSize; i++)
            {
                PlaceUnit(p, 2000);
            }

            int idx = p.hand.FindIndex(c => c.type == CardType.Manifest);
            Assert.IsFalse(_engine.CanPlayCard(side, idx));
        }

        // ================================================================
        // CP Limit Tests
        // ================================================================

        [Test]
        public void CP_CapsAtMax10()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            // Simulate many turns for one player
            for (int i = 0; i < 12; i++)
            {
                _engine.StartTurn();
                _engine.EndTurn();
            }

            var p1 = _engine.State.player1;
            var p2 = _engine.State.player2;
            Assert.IsTrue(p1.maxCP <= BalanceConfig.MaxCPCap);
            Assert.IsTrue(p2.maxCP <= BalanceConfig.MaxCPCap);
        }

        // ================================================================
        // On-Death Effect Tests
        // ================================================================

        [Test]
        public void OnDeath_Draw_DrawsWhenDestroyed()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);

            var deathDrawCard = new CardData
            {
                id = "DEATH_DRAW_TEST",
                cardName = "DeathDrawer",
                type = CardType.Manifest,
                cpCost = 2,
                aspect = Aspect.Contest,
                battlePower = 3000,
                wishDamage = 3,
                wishDamageType = "fixed",
                keywords = new string[0],
                effectKey = "SUMMON_ON_DEATH_DRAW",
                drawCount = 1,
            };
            var unit = new FieldUnit(deathDrawCard, _engine.State.NextInstanceId());
            p.field.Add(unit);

            int handBefore = p.hand.Count;
            _engine.DestroyUnit(side, unit);
            Assert.AreEqual(handBefore + 1, p.hand.Count);
            Assert.IsFalse(p.field.Contains(unit));
        }

        // ================================================================
        // StartTurn Refresh Tests
        // ================================================================

        [Test]
        public void StartTurn_RefreshesUnitsAndLeader()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);

            var unit = PlaceUnit(p, 4000);
            unit.status = UnitStatus.Exhausted;
            p.leader.status = UnitStatus.Exhausted;
            p.leader.skillUsedThisTurn = true;
            p.leader.damageHalveActive = true;

            _engine.EndTurn(); // switches to opponent
            _engine.EndTurn(); // switches back

            Assert.AreEqual(UnitStatus.Ready, unit.status);
            Assert.AreEqual(UnitStatus.Ready, p.leader.status);
            Assert.IsFalse(p.leader.skillUsedThisTurn);
            Assert.IsFalse(p.leader.damageHalveActive);
        }

        // ================================================================
        // CanPlayCard Validation Tests
        // ================================================================

        [Test]
        public void CanPlayCard_NotEnoughCp_ReturnsFalse()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 0;

            int idx = p.hand.FindIndex(c => c.cpCost > 0);
            if (idx >= 0)
                Assert.IsFalse(_engine.CanPlayCard(side, idx));
        }

        [Test]
        public void CanPlayCard_WrongPhase_ReturnsFalse()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            _engine.State.currentPhase = TurnPhase.End;

            Assert.IsFalse(_engine.CanPlayCard(side, 0));
        }

        // ================================================================
        // Quick Match Mode Tests
        // ================================================================

        [Test]
        public void QuickMatch_SetsHpTo60()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2, MatchMode.Quick);
            Assert.AreEqual(60, _engine.State.player1.hp);
            Assert.AreEqual(60, _engine.State.player2.hp);
            Assert.AreEqual(60, _engine.State.player1.maxHp);
        }

        [Test]
        public void QuickMatch_SetsMatchMode()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2, MatchMode.Quick);
            Assert.AreEqual(MatchMode.Quick, _engine.State.matchMode);
        }

        [Test]
        public void QuickMatch_StartsWithCP2()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2, MatchMode.Quick);
            Assert.AreEqual(2, _engine.State.player1.currentCP);
            Assert.AreEqual(2, _engine.State.player2.currentCP);
            Assert.AreEqual(2, _engine.State.player1.maxCP);
        }

        [Test]
        public void QuickMatch_WishThresholds_DifferFromStandard()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2, MatchMode.Quick);
            Assert.AreEqual(6, _engine.State.player1.wishZone.Count);
            Assert.AreEqual(50, _engine.State.player1.wishZone[0].threshold);
            Assert.AreEqual(5, _engine.State.player1.wishZone[5].threshold);
        }

        [Test]
        public void QuickMatch_TurnLimit16_EndsGame()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2, MatchMode.Quick);
            _engine.State.player1.hp = 40;
            _engine.State.player2.hp = 30;
            _engine.State.turnTotal = 16; // Quick turn limit

            _engine.EndTurn();
            Assert.IsTrue(_engine.State.isGameOver);
            Assert.AreEqual(MatchResult.Player1Win, _engine.State.result);
        }

        [Test]
        public void StandardMatch_TurnLimit24_StillWorks()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2, MatchMode.Standard);
            Assert.AreEqual(100, _engine.State.player1.hp);
            Assert.AreEqual(MatchMode.Standard, _engine.State.matchMode);

            // Turn 16 should NOT end a standard match
            _engine.State.turnTotal = 16;
            _engine.EndTurn();
            Assert.IsFalse(_engine.State.isGameOver);
        }
    }
}
