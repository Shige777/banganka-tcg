using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Banganka.Core.Battle;
using Banganka.Core.Data;
using Banganka.Core.Config;

namespace Banganka.Tests.EditMode
{
    /// <summary>
    /// L2 Integration Tests — BattleEngine + SimpleAI full game simulation
    /// TEST_PLAN.md §L2
    /// </summary>
    [TestFixture]
    public class IntegrationTests
    {
        LeaderData _leader;
        List<CardData> _deck;

        [SetUp]
        public void SetUp()
        {
            _leader = new LeaderData
            {
                id = "LDR_INT_TEST",
                leaderName = "IntTestLeader",
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
                    new LeaderSkill { unlockLevel = 2, name = "RushAll", effectKey = "LEADER_SKILL_RUSH_ALL" },
                    new LeaderSkill { unlockLevel = 3, name = "DamageHalve", effectKey = "LEADER_SKILL_DAMAGE_HALVE" },
                },
            };
            _deck = MakeDeck(Aspect.Contest);
        }

        List<CardData> MakeDeck(Aspect aspect)
        {
            var deck = new List<CardData>();
            for (int i = 0; i < 34; i++)
            {
                var type = i < 24 ? CardType.Manifest : (i < 30 ? CardType.Spell : CardType.Algorithm);
                deck.Add(new CardData
                {
                    id = $"INT_{aspect}_{i:D2}",
                    cardName = $"IntTest{i}",
                    type = type,
                    cpCost = (i % 5) + 1,
                    aspect = aspect,
                    battlePower = type == CardType.Manifest ? ((i % 5) + 1) * 2000 : 0,
                    wishDamage = 3,
                    wishDamageType = "fixed",
                    wishTrigger = i < 6 ? "WT_DRAW" : "-",
                    effectKey = type == CardType.Spell ? "SPELL_DRAW" : "",
                    keywords = new string[0],
                    drawCount = type == CardType.Spell ? 1 : 0,
                });
            }
            return deck;
        }

        MatchResult SimulateGame(int seed, BotDifficulty diff = BotDifficulty.Normal)
        {
            var engine = new BattleEngine(seed);
            engine.InitMatch(_leader, _leader, new List<CardData>(_deck), new List<CardData>(_deck));
            var ai1 = new SimpleAI(engine, PlayerSide.Player1, diff);
            var ai2 = new SimpleAI(engine, PlayerSide.Player2, diff);
            engine.StartTurn();

            int maxIter = BalanceConfig.TurnLimitTotal * 2 + 10;
            int iter = 0;
            while (!engine.State.isGameOver && iter < maxIter)
            {
                iter++;
                if (engine.State.activePlayer == PlayerSide.Player1)
                    ai1.PlayTurn();
                else
                    ai2.PlayTurn();
                if (!engine.State.isGameOver)
                    engine.EndTurn();
            }
            return engine.State.result;
        }

        // ================================================================
        // Full Game Simulation
        // ================================================================

        [Test]
        public void FullGame_CompletesWithoutException()
        {
            // Run 10 games with different seeds
            for (int i = 0; i < 10; i++)
            {
                Assert.DoesNotThrow(() => SimulateGame(i * 31));
            }
        }

        [Test]
        public void FullGame_AlwaysHasResult()
        {
            for (int i = 0; i < 10; i++)
            {
                var result = SimulateGame(i * 17);
                Assert.IsTrue(
                    result == MatchResult.Player1Win ||
                    result == MatchResult.Player2Win ||
                    result == MatchResult.Draw,
                    $"Game {i} had result: {result}");
            }
        }

        [Test]
        public void FullGame_HpNeverNegative()
        {
            var engine = new BattleEngine(42);
            engine.InitMatch(_leader, _leader, new List<CardData>(_deck), new List<CardData>(_deck));
            var ai1 = new SimpleAI(engine, PlayerSide.Player1, BotDifficulty.Normal);
            var ai2 = new SimpleAI(engine, PlayerSide.Player2, BotDifficulty.Normal);
            engine.StartTurn();

            int maxIter = BalanceConfig.TurnLimitTotal * 2 + 10;
            int iter = 0;
            while (!engine.State.isGameOver && iter < maxIter)
            {
                iter++;
                if (engine.State.activePlayer == PlayerSide.Player1)
                    ai1.PlayTurn();
                else
                    ai2.PlayTurn();
                Assert.IsTrue(engine.State.player1.hp >= 0, $"P1 HP was {engine.State.player1.hp} at iter {iter}");
                Assert.IsTrue(engine.State.player2.hp >= 0, $"P2 HP was {engine.State.player2.hp} at iter {iter}");
                if (!engine.State.isGameOver)
                    engine.EndTurn();
            }
        }

        [Test]
        public void FullGame_TurnCountWithinLimits()
        {
            var engine = new BattleEngine(99);
            engine.InitMatch(_leader, _leader, new List<CardData>(_deck), new List<CardData>(_deck));
            var ai1 = new SimpleAI(engine, PlayerSide.Player1, BotDifficulty.Normal);
            var ai2 = new SimpleAI(engine, PlayerSide.Player2, BotDifficulty.Normal);
            engine.StartTurn();

            int maxIter = BalanceConfig.TurnLimitTotal * 2 + 10;
            int iter = 0;
            while (!engine.State.isGameOver && iter < maxIter)
            {
                iter++;
                if (engine.State.activePlayer == PlayerSide.Player1) ai1.PlayTurn();
                else ai2.PlayTurn();
                if (!engine.State.isGameOver) engine.EndTurn();
            }

            Assert.IsTrue(engine.State.turnTotal <= BalanceConfig.TurnLimitTotal + 1,
                $"Turn count {engine.State.turnTotal} exceeded limit {BalanceConfig.TurnLimitTotal}");
        }

        [Test]
        public void FullGame_FieldNeverExceedsMax()
        {
            var engine = new BattleEngine(77);
            engine.InitMatch(_leader, _leader, new List<CardData>(_deck), new List<CardData>(_deck));
            var ai1 = new SimpleAI(engine, PlayerSide.Player1, BotDifficulty.Normal);
            var ai2 = new SimpleAI(engine, PlayerSide.Player2, BotDifficulty.Normal);
            engine.StartTurn();

            int maxIter = BalanceConfig.TurnLimitTotal * 2 + 10;
            int iter = 0;
            while (!engine.State.isGameOver && iter < maxIter)
            {
                iter++;
                if (engine.State.activePlayer == PlayerSide.Player1) ai1.PlayTurn();
                else ai2.PlayTurn();

                Assert.IsTrue(engine.State.player1.field.Count <= BalanceConfig.FieldTotalSize,
                    $"P1 field had {engine.State.player1.field.Count} units");
                Assert.IsTrue(engine.State.player2.field.Count <= BalanceConfig.FieldTotalSize,
                    $"P2 field had {engine.State.player2.field.Count} units");

                if (!engine.State.isGameOver) engine.EndTurn();
            }
        }

        [Test]
        public void FullGame_CpNeverExceedsMax()
        {
            var engine = new BattleEngine(55);
            engine.InitMatch(_leader, _leader, new List<CardData>(_deck), new List<CardData>(_deck));
            var ai1 = new SimpleAI(engine, PlayerSide.Player1, BotDifficulty.Normal);
            var ai2 = new SimpleAI(engine, PlayerSide.Player2, BotDifficulty.Normal);
            engine.StartTurn();

            int maxIter = BalanceConfig.TurnLimitTotal * 2 + 10;
            int iter = 0;
            while (!engine.State.isGameOver && iter < maxIter)
            {
                iter++;
                if (engine.State.activePlayer == PlayerSide.Player1) ai1.PlayTurn();
                else ai2.PlayTurn();

                Assert.IsTrue(engine.State.player1.maxCP <= BalanceConfig.MaxCPCap,
                    $"P1 maxCP was {engine.State.player1.maxCP}");
                Assert.IsTrue(engine.State.player2.maxCP <= BalanceConfig.MaxCPCap,
                    $"P2 maxCP was {engine.State.player2.maxCP}");

                if (!engine.State.isGameOver) engine.EndTurn();
            }
        }

        // ================================================================
        // AI Difficulty Tests
        // ================================================================

        [Test]
        public void AI_Easy_CompletesGames()
        {
            for (int i = 0; i < 5; i++)
                Assert.DoesNotThrow(() => SimulateGame(i, BotDifficulty.Easy));
        }

        [Test]
        public void AI_Hard_CompletesGames()
        {
            for (int i = 0; i < 5; i++)
                Assert.DoesNotThrow(() => SimulateGame(i, BotDifficulty.Hard));
        }

        // ================================================================
        // L5 Balance Smoke Test (quick 100-game sample)
        // ================================================================

        [Test]
        public void Balance_MirrorMatch_FirstPlayerNotDominant()
        {
            int p1Wins = 0, p2Wins = 0;
            const int games = 100;

            for (int i = 0; i < games; i++)
            {
                var result = SimulateGame(i * 7);
                if (result == MatchResult.Player1Win) p1Wins++;
                else if (result == MatchResult.Player2Win) p2Wins++;
            }

            int totalDecisive = p1Wins + p2Wins;
            if (totalDecisive == 0) return; // all draws is fine

            float p1Rate = (float)p1Wins / totalDecisive * 100f;
            // Warn if outside 35-65% (loose check for smoke test)
            Assert.IsTrue(p1Rate >= 35f && p1Rate <= 65f,
                $"First-player win rate {p1Rate:F1}% outside acceptable 35-65% range " +
                $"(P1: {p1Wins}, P2: {p2Wins}, Draws: {games - totalDecisive})");
        }

        [Test]
        public void Balance_AverageGameLength_Reasonable()
        {
            int totalTurns = 0;
            const int games = 50;

            for (int i = 0; i < games; i++)
            {
                var engine = new BattleEngine(i * 13);
                engine.InitMatch(_leader, _leader, new List<CardData>(_deck), new List<CardData>(_deck));
                var ai1 = new SimpleAI(engine, PlayerSide.Player1, BotDifficulty.Normal);
                var ai2 = new SimpleAI(engine, PlayerSide.Player2, BotDifficulty.Normal);
                engine.StartTurn();

                int maxIter = BalanceConfig.TurnLimitTotal * 2 + 10;
                int iter = 0;
                while (!engine.State.isGameOver && iter < maxIter)
                {
                    iter++;
                    if (engine.State.activePlayer == PlayerSide.Player1) ai1.PlayTurn();
                    else ai2.PlayTurn();
                    if (!engine.State.isGameOver) engine.EndTurn();
                }
                totalTurns += engine.State.turnTotal;
            }

            float avg = (float)totalTurns / games;
            // Games should average between 6-24 turns
            Assert.IsTrue(avg >= 4f && avg <= BalanceConfig.TurnLimitTotal,
                $"Average game length {avg:F1} turns outside expected range");
        }
    }
}
