using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Banganka.Core.Battle;
using Banganka.Core.Data;
using Banganka.Core.Config;

namespace Banganka.Tests.EditMode
{
    [TestFixture]
    public class EffectResolverTests
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
                    effectKey = "",
                    keywords = new string[0],
                });
            }
            return deck;
        }

        void InitAndStart()
        {
            _engine.InitMatch(_leader, _deckP1, _deckP2);
            _engine.StartTurn();
        }

        FieldUnit PlaceUnit(PlayerState p, int power, string effectKey = "", int drawCount = 0)
        {
            var card = new CardData
            {
                id = $"EFF_TEST_{p.field.Count}",
                cardName = "EffectUnit",
                type = CardType.Manifest,
                cpCost = 1,
                aspect = Aspect.Contest,
                battlePower = power,
                wishDamage = 3,
                wishDamageType = "fixed",
                keywords = new string[0],
                effectKey = effectKey,
                drawCount = drawCount,
            };
            var unit = new FieldUnit(card, _engine.State.NextInstanceId());
            unit.status = UnitStatus.Ready;
            unit.summonSick = false;
            p.field.Add(unit);
            return unit;
        }

        CardData MakeSpell(string effectKey, int cpCost = 1)
        {
            return new CardData
            {
                id = "SPELL_EFF_TEST",
                cardName = "TestSpell",
                type = CardType.Spell,
                cpCost = cpCost,
                aspect = Aspect.Contest,
                effectKey = effectKey,
                keywords = new string[0],
            };
        }

        // ================================================================
        // Spell: SPELL_REST
        // ================================================================

        [Test]
        public void SpellRest_ExhaustsEnemyUnits()
        {
            InitAndStart();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.currentCP = 10;

            var enemy1 = PlaceUnit(opp, 3000);
            var enemy2 = PlaceUnit(opp, 5000);

            var spell = MakeSpell("SPELL_REST");
            spell.restTargets = 1;
            spell.targetScope = "enemy";
            p.hand.Insert(0, spell);

            _engine.PlayCard(side, 0);
            // Should exhaust 1 ready unit (first in list)
            int exhausted = opp.field.Count(u => u.status == UnitStatus.Exhausted);
            Assert.AreEqual(1, exhausted);
        }

        // ================================================================
        // Spell: SPELL_REMOVE_DAMAGED
        // ================================================================

        [Test]
        public void SpellRemoveDamaged_RemovesWeakUnits()
        {
            InitAndStart();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.currentCP = 10;

            PlaceUnit(opp, 2000);
            PlaceUnit(opp, 6000);

            var spell = MakeSpell("SPELL_REMOVE_DAMAGED");
            spell.removeCondition = "power<=3000";
            p.hand.Insert(0, spell);

            _engine.PlayCard(side, 0);
            Assert.AreEqual(1, opp.field.Count);
            Assert.AreEqual(6000, opp.field[0].currentPower);
        }

        // ================================================================
        // Spell: SPELL_WISHDMG_PLUS
        // ================================================================

        [Test]
        public void SpellWishDmgPlus_BuffsStrongestUnit()
        {
            InitAndStart();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            var unit = PlaceUnit(p, 4000);
            unit.currentWishDamage = 3;

            var spell = MakeSpell("SPELL_WISHDMG_PLUS");
            spell.wishDamageDelta = 2;
            p.hand.Insert(0, spell);

            _engine.PlayCard(side, 0);
            Assert.AreEqual(5, unit.currentWishDamage);
        }

        // ================================================================
        // Spell: SPELL_POWER_PLUS (all_ally scope)
        // ================================================================

        [Test]
        public void SpellPowerPlus_AllAlly_BuffsAllUnits()
        {
            InitAndStart();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            var unit1 = PlaceUnit(p, 3000);
            var unit2 = PlaceUnit(p, 5000);

            var spell = MakeSpell("SPELL_POWER_PLUS");
            spell.powerDelta = 1000;
            spell.targetScope = "all_ally";
            p.hand.Insert(0, spell);

            _engine.PlayCard(side, 0);
            Assert.AreEqual(4000, unit1.currentPower);
            Assert.AreEqual(6000, unit2.currentPower);
        }

        // ================================================================
        // Spell: SPELL_SLOT_LOCK
        // ================================================================

        [Test]
        public void SpellSlotLock_ReducesOpponentSlots()
        {
            InitAndStart();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.currentCP = 10;

            Assert.AreEqual(0, opp.lockedSlots);

            var spell = MakeSpell("SPELL_SLOT_LOCK");
            spell.lockCount = 2;
            p.hand.Insert(0, spell);

            _engine.PlayCard(side, 0);
            Assert.AreEqual(2, opp.lockedSlots);
            Assert.AreEqual(BalanceConfig.FieldTotalSize - 2, opp.AvailableSlots);
        }

        // ================================================================
        // Spell: SPELL_HP_DAMAGE_CURRENT
        // ================================================================

        [Test]
        public void SpellHpDamageCurrent_BasedOnCurrentHp()
        {
            InitAndStart();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.currentCP = 10;
            opp.hp = 50;

            var spell = MakeSpell("SPELL_HP_DAMAGE_CURRENT");
            spell.hpDamagePercent = 20;
            p.hand.Insert(0, spell);

            _engine.PlayCard(side, 0);
            // 50 * 20/100 = 10 damage
            Assert.AreEqual(40, opp.hp);
        }

        // ================================================================
        // Leader Skill: LEADER_SKILL_ATTACK_SEAL
        // ================================================================

        [Test]
        public void LeaderSkill_AttackSeal_ExhaustsEnemyUnits()
        {
            var leader = new LeaderData
            {
                id = "LDR_SEAL",
                leaderName = "SealLeader",
                keyAspect = Aspect.Whisper,
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
                        name = "AttackSeal",
                        effectKey = "LEADER_SKILL_ATTACK_SEAL",
                        targetCount = 1,
                    },
                    new LeaderSkill
                    {
                        unlockLevel = 3,
                        name = "Draw",
                        effectKey = "LEADER_SKILL_DRAW",
                        drawCount = 2,
                    }
                },
            };

            _engine.InitMatch(leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.leader.level = 2;

            var enemy = PlaceUnit(opp, 5000);
            Assert.AreEqual(UnitStatus.Ready, enemy.status);

            _engine.UseLeaderSkill(side, 2);
            Assert.AreEqual(UnitStatus.Exhausted, enemy.status);
        }

        // ================================================================
        // Leader Skill: LEADER_SKILL_DRAW
        // ================================================================

        [Test]
        public void LeaderSkill_Draw_DrawsCards()
        {
            var leader = new LeaderData
            {
                id = "LDR_DRAW",
                leaderName = "DrawLeader",
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
                        name = "Draw2",
                        effectKey = "LEADER_SKILL_DRAW",
                        drawCount = 2,
                    },
                    new LeaderSkill
                    {
                        unlockLevel = 3,
                        name = "Dummy",
                        effectKey = "LEADER_SKILL_DAMAGE_HALVE",
                    }
                },
            };

            _engine.InitMatch(leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.leader.level = 2;

            int handBefore = p.hand.Count;
            _engine.UseLeaderSkill(side, 2);
            Assert.AreEqual(handBefore + 2, p.hand.Count);
        }

        // ================================================================
        // Leader Skill: LEADER_SKILL_GRAVE_TO_HAND
        // ================================================================

        [Test]
        public void LeaderSkill_GraveToHand_ReturnsManifest()
        {
            var leader = new LeaderData
            {
                id = "LDR_GRAVE",
                leaderName = "GraveLeader",
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
                        name = "Recover",
                        effectKey = "LEADER_SKILL_GRAVE_TO_HAND",
                        targetCount = 1,
                    },
                    new LeaderSkill
                    {
                        unlockLevel = 3,
                        name = "Dummy",
                        effectKey = "LEADER_SKILL_DAMAGE_HALVE",
                    }
                },
            };

            _engine.InitMatch(leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.leader.level = 2;

            // Put a manifest card in graveyard
            var gravCard = new CardData
            {
                id = "GRAVE_CARD",
                cardName = "GraveManifest",
                type = CardType.Manifest,
                aspect = Aspect.Contest,
                battlePower = 4000,
            };
            p.graveyard.Add(gravCard);

            int handBefore = p.hand.Count;
            _engine.UseLeaderSkill(side, 2);
            Assert.AreEqual(handBefore + 1, p.hand.Count);
            Assert.AreEqual(0, p.graveyard.Count(c => c.id == "GRAVE_CARD"));
        }

        // ================================================================
        // Leader Skill: LEADER_SKILL_POWER_BUFF_GUARDBREAK
        // ================================================================

        [Test]
        public void LeaderSkill_PowerBuffGuardBreak_BuffsAndGrantsKeyword()
        {
            var leader = new LeaderData
            {
                id = "LDR_BUFF",
                leaderName = "BuffLeader",
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
                        name = "PowerBuff",
                        effectKey = "LEADER_SKILL_POWER_BUFF_GUARDBREAK",
                        powerDelta = 3000,
                        grantKeyword = "GuardBreak",
                    },
                    new LeaderSkill
                    {
                        unlockLevel = 3,
                        name = "Dummy",
                        effectKey = "LEADER_SKILL_DAMAGE_HALVE",
                    }
                },
            };

            _engine.InitMatch(leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.leader.level = 2;

            var unit = PlaceUnit(p, 4000);

            _engine.UseLeaderSkill(side, 2);
            Assert.AreEqual(7000, unit.currentPower); // +3000
            Assert.IsTrue(unit.currentKeywords.Contains("GuardBreak"));
        }

        // ================================================================
        // Leader Skill: LEADER_SKILL_MASS_BUFF_HEAL
        // ================================================================

        [Test]
        public void LeaderSkill_MassBuffHeal_BuffsAndHeals()
        {
            var leader = new LeaderData
            {
                id = "LDR_MASSBUFF",
                leaderName = "MassBuffLeader",
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
                        name = "Dummy",
                        effectKey = "LEADER_SKILL_RUSH_ALL",
                    },
                    new LeaderSkill
                    {
                        unlockLevel = 3,
                        name = "MassBuffHeal",
                        effectKey = "LEADER_SKILL_MASS_BUFF_HEAL",
                        powerDelta = 2000,
                        healHP = 10,
                    }
                },
            };

            _engine.InitMatch(leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.leader.level = 3;
            p.hp = 80;

            var unit1 = PlaceUnit(p, 3000);
            var unit2 = PlaceUnit(p, 5000);

            _engine.UseLeaderSkill(side, 3);
            Assert.AreEqual(5000, unit1.currentPower); // +2000
            Assert.AreEqual(7000, unit2.currentPower); // +2000
            Assert.AreEqual(90, p.hp); // +10 heal
        }

        // ================================================================
        // Leader Skill: LEADER_SKILL_SACRIFICE_DAMAGE
        // ================================================================

        [Test]
        public void LeaderSkill_SacrificeDamage_SacrificesAndDamages()
        {
            var leader = new LeaderData
            {
                id = "LDR_SAC",
                leaderName = "SacLeader",
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
                        name = "Dummy",
                        effectKey = "LEADER_SKILL_RUSH_ALL",
                    },
                    new LeaderSkill
                    {
                        unlockLevel = 3,
                        name = "Sacrifice",
                        effectKey = "LEADER_SKILL_SACRIFICE_DAMAGE",
                    }
                },
            };

            _engine.InitMatch(leader, _deckP1, _deckP2);
            _engine.StartTurn();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.leader.level = 3;

            PlaceUnit(p, 5000); // will be sacrificed (weakest = only one)

            int oppHpBefore = opp.hp;
            _engine.UseLeaderSkill(side, 3);

            Assert.AreEqual(0, p.field.Count); // unit sacrificed
            Assert.IsTrue(opp.hp < oppHpBefore); // damage dealt (5000/1000 = 5% fixed)
        }

        // ================================================================
        // OnPlay Effect: SUMMON_ON_PLAY_DRAW
        // ================================================================

        [Test]
        public void OnPlayDraw_DrawsOnSummon()
        {
            InitAndStart();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            var card = new CardData
            {
                id = "ONPLAY_DRAW",
                cardName = "DrawOnPlay",
                type = CardType.Manifest,
                cpCost = 2,
                aspect = Aspect.Contest,
                battlePower = 3000,
                wishDamage = 3,
                wishDamageType = "fixed",
                keywords = new string[0],
                effectKey = "SUMMON_ON_PLAY_DRAW",
                drawCount = 1,
            };
            p.hand.Insert(0, card);

            int handBefore = p.hand.Count;
            int deckBefore = p.deck.Count;
            _engine.PlayCard(side, 0);

            // Played 1 card (-1), drew 1 (+1) = net 0, but unit placed on field
            Assert.AreEqual(handBefore - 1 + 1, p.hand.Count);
            Assert.AreEqual(deckBefore - 1, p.deck.Count);
        }

        // ================================================================
        // OnPlay Effect: SUMMON_ON_PLAY_HP_DAMAGE
        // ================================================================

        [Test]
        public void OnPlayHpDamage_DamagesOpponent()
        {
            InitAndStart();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.currentCP = 10;

            var card = new CardData
            {
                id = "ONPLAY_DMG",
                cardName = "DamageOnPlay",
                type = CardType.Manifest,
                cpCost = 3,
                aspect = Aspect.Contest,
                battlePower = 4000,
                wishDamage = 3,
                wishDamageType = "fixed",
                keywords = new string[0],
                effectKey = "SUMMON_ON_PLAY_HP_DAMAGE",
                hpDamagePercent = 5,
                damageType = "fixed",
            };
            p.hand.Insert(0, card);

            _engine.PlayCard(side, 0);
            // 5% of MaxHP(100) = 5 damage
            Assert.AreEqual(95, opp.hp);
        }

        // ================================================================
        // OnPlay Effect: SUMMON_ON_PLAY_BUFF_ALLY
        // ================================================================

        [Test]
        public void OnPlayBuffAlly_BuffsExistingAlly()
        {
            InitAndStart();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            var existing = PlaceUnit(p, 3000);

            var card = new CardData
            {
                id = "ONPLAY_BUFF",
                cardName = "BuffOnPlay",
                type = CardType.Manifest,
                cpCost = 3,
                aspect = Aspect.Contest,
                battlePower = 4000,
                wishDamage = 3,
                wishDamageType = "fixed",
                keywords = new string[0],
                effectKey = "SUMMON_ON_PLAY_BUFF_ALLY",
                powerDelta = 2000,
            };
            p.hand.Insert(0, card);

            _engine.PlayCard(side, 0);
            Assert.AreEqual(5000, existing.currentPower); // +2000
        }

        // ================================================================
        // OnPlay Effect: SUMMON_ON_PLAY_DESTROY
        // ================================================================

        [Test]
        public void OnPlayDestroy_RemovesWeakEnemies()
        {
            InitAndStart();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.currentCP = 10;

            PlaceUnit(opp, 2000);
            PlaceUnit(opp, 6000);

            var card = new CardData
            {
                id = "ONPLAY_DESTROY",
                cardName = "DestroyOnPlay",
                type = CardType.Manifest,
                cpCost = 4,
                aspect = Aspect.Contest,
                battlePower = 5000,
                wishDamage = 3,
                wishDamageType = "fixed",
                keywords = new string[0],
                effectKey = "SUMMON_ON_PLAY_DESTROY",
                removeCondition = "power<=3000",
            };
            p.hand.Insert(0, card);

            _engine.PlayCard(side, 0);
            Assert.AreEqual(1, opp.field.Count);
            Assert.AreEqual(6000, opp.field[0].currentPower);
        }

        // ================================================================
        // Algorithm: spell_hp_damage bonus
        // ================================================================

        [Test]
        public void Algorithm_SpellHpDamageBonus_AddsExtraDamage()
        {
            InitAndStart();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);
            p.currentCP = 10;

            // Place algorithm with spell_hp_damage global rule
            _engine.State.sharedAlgo = new SharedAlgorithm
            {
                cardData = new CardData
                {
                    id = "ALGO_SPELL_BONUS",
                    globalRule = new AlgorithmRule { kind = "spell_hp_damage", value = 3 },
                },
                owner = side,
                isFaceDown = false,
            };

            var spell = MakeSpell("SPELL_HP_DAMAGE_FIXED");
            spell.hpDamagePercent = 5;
            p.hand.Insert(0, spell);

            _engine.PlayCard(side, 0);
            // 5% spell damage + 3% algo bonus = 8% total
            // 5 + 3 = 8 damage
            Assert.AreEqual(92, opp.hp);
        }

        // ================================================================
        // Algorithm: direct_hit_plus bonus
        // ================================================================

        [Test]
        public void Algorithm_DirectHitPlus_IncreasesWishDamage()
        {
            InitAndStart();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            var opp = _engine.State.GetOpponent(side);

            // Place algorithm with direct_hit_plus
            _engine.State.sharedAlgo = new SharedAlgorithm
            {
                cardData = new CardData
                {
                    id = "ALGO_DIRECT",
                    globalRule = new AlgorithmRule { kind = "direct_hit_plus", value = 2 },
                },
                owner = side,
                isFaceDown = false,
            };

            var unit = PlaceUnit(p, 4000);
            unit.currentWishDamage = 3; // fixed 3%

            int hpBefore = opp.hp;
            var decl = new BattleEngine.AttackDeclaration
            {
                attackerType = BattleEngine.AttackerType.Unit,
                attackerInstanceId = unit.instanceId,
                targetType = BattleEngine.TargetType.Leader,
            };

            var result = _engine.ResolveAttack(side, decl);
            Assert.IsTrue(result.directHit);
            // 3% base + 2% algo bonus = 5% of 100 = 5 damage
            Assert.AreEqual(hpBefore - 5, opp.hp);
        }

        // ================================================================
        // Unknown effectKey throws
        // ================================================================

        [Test]
        public void UnknownSpellEffectKey_Throws()
        {
            InitAndStart();
            var side = _engine.State.activePlayer;
            var p = _engine.State.GetPlayer(side);
            p.currentCP = 10;

            var spell = MakeSpell("SPELL_NONEXISTENT");
            p.hand.Insert(0, spell);

            Assert.Throws<System.InvalidOperationException>(() =>
                _engine.PlayCard(side, 0));
        }
    }
}
