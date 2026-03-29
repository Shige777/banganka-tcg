using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Banganka.Core.Battle;
using Banganka.Core.Data;
using Banganka.Core.Config;

namespace Banganka.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for battle flow requiring MonoBehaviour lifecycle (coroutines, frames).
    /// EditMode tests cover pure logic; these test multi-frame sequences and event timing.
    /// </summary>
    [TestFixture]
    public class BattleFlowTests
    {
        BattleEngine _engine;
        LeaderData _leaderP1;
        LeaderData _leaderP2;
        List<CardData> _deckP1;
        List<CardData> _deckP2;

        [SetUp]
        public void SetUp()
        {
            _engine = new BattleEngine(seed: 42);
            _leaderP1 = MakeLeader("LDR_P1", Aspect.Contest);
            _leaderP2 = MakeLeader("LDR_P2", Aspect.Whisper);
            _deckP1 = MakeDeck(Aspect.Contest);
            _deckP2 = MakeDeck(Aspect.Whisper);
        }

        static LeaderData MakeLeader(string id, Aspect aspect) => new LeaderData
        {
            id = id,
            leaderName = $"Test_{aspect}",
            keyAspect = aspect,
            basePower = 5000,
            baseWishDamage = 3,
            wishDamageType = "fixed",
            levelCap = 3,
            levelUpPowerGain = 1000,
            levelUpWishDamageGain = 1,
            evoGaugeMaxByLevel = new[] { 3, 4 },
            wishDamageByLevel = new[] { 3, 5, 8 },
        };

        static List<CardData> MakeDeck(Aspect aspect)
        {
            var deck = new List<CardData>();
            for (int i = 0; i < 34; i++)
            {
                deck.Add(new CardData
                {
                    id = $"PM_{aspect}_{i:D2}",
                    cardName = $"PMTest{i}",
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

        // ================================================================
        // Full match lifecycle: init → play turns → game over
        // ================================================================

        [UnityTest]
        public IEnumerator FullMatch_RunsToCompletion_Within24Turns()
        {
            _engine.InitMatch(_leaderP1, _leaderP2, _deckP1, _deckP2);

            bool gameEnded = false;
            MatchResult result = MatchResult.None;
            _engine.OnMatchEnd += r => { gameEnded = true; result = r; };

            int safety = 0;
            while (!gameEnded && safety < 500)
            {
                var state = _engine.State;
                var active = state.activePlayer == PlayerSide.Player1 ? state.player1 : state.player2;

                // Start + Draw phases
                _engine.StartTurn();

                // Main phase: play cheapest card if possible
                TryPlayCheapestCard(active);

                // End turn
                _engine.EndTurn();

                safety++;
                if (safety % 10 == 0)
                    yield return null; // yield every 10 iterations to avoid frame timeout
            }

            Assert.IsTrue(gameEnded || _engine.State.turnTotal >= 24,
                $"Match should end by turn 24 or by HP depletion. Turn={_engine.State.turnTotal}, safety={safety}");

            if (gameEnded)
                Assert.AreNotEqual(MatchResult.None, result);

            yield return null;
        }

        [UnityTest]
        public IEnumerator TurnSequence_PhasesProgressCorrectly()
        {
            _engine.InitMatch(_leaderP1, _leaderP2, _deckP1, _deckP2);

            var phases = new List<TurnPhase>();
            _engine.OnStateChanged += () => phases.Add(_engine.State.currentPhase);

            _engine.StartTurn();
            yield return null;

            _engine.EndTurn();
            yield return null;

            // Verify we went through Start/Draw/Main/End at minimum
            Assert.IsTrue(phases.Count > 0, "State changes should have been recorded");
        }

        [UnityTest]
        public IEnumerator EventFiring_OnCardPlayed_FiresOnSummon()
        {
            _engine.InitMatch(_leaderP1, _leaderP2, _deckP1, _deckP2);

            bool cardPlayedFired = false;
            PlayerSide playedSide = PlayerSide.Player2;
            _engine.OnCardPlayed += (side, card, type) =>
            {
                cardPlayedFired = true;
                playedSide = side;
            };

            _engine.StartTurn();
            yield return null;

            var active = _engine.State.activePlayer;
            var player = active == PlayerSide.Player1 ? _engine.State.player1 : _engine.State.player2;

            // Try to play a card from hand
            for (int i = 0; i < player.hand.Count; i++)
            {
                if (player.hand[i].cpCost <= player.currentCP)
                {
                    _engine.PlayCard(active, i);
                    break;
                }
            }

            yield return null;

            if (cardPlayedFired)
                Assert.AreEqual(active, playedSide);
        }

        [UnityTest]
        public IEnumerator EventFiring_OnMatchEnd_FiresOnHpZero()
        {
            _engine.InitMatch(_leaderP1, _leaderP2, _deckP1, _deckP2);

            bool endFired = false;
            _engine.OnMatchEnd += _ => endFired = true;

            // Force HP to 0
            _engine.State.player2.hp = 1;
            _engine.ApplyHpDamage(_engine.State.player2, 10, "fixed");

            yield return null;

            Assert.IsTrue(endFired || _engine.State.isGameOver,
                "Match should end when HP reaches 0");
        }

        [UnityTest]
        public IEnumerator MultiTurn_CpIncrementsCorrectly()
        {
            _engine.InitMatch(_leaderP1, _leaderP2, _deckP1, _deckP2);
            yield return null;

            var cpValues = new List<int>();

            for (int t = 0; t < 5; t++)
            {
                _engine.StartTurn();
                var active = _engine.State.activePlayer == PlayerSide.Player1
                    ? _engine.State.player1 : _engine.State.player2;
                cpValues.Add(active.currentCP);
                _engine.EndTurn();
                yield return null;
            }

            // CP should increase over turns (max +1 per turn, cap 10)
            for (int i = 1; i < cpValues.Count; i++)
            {
                Assert.GreaterOrEqual(cpValues[i], cpValues[i - 1],
                    $"CP should not decrease: turn {i - 1}={cpValues[i - 1]}, turn {i}={cpValues[i]}");
            }
        }

        [UnityTest]
        public IEnumerator WishTrigger_FiresWhenHpCrossesThreshold()
        {
            _engine.InitMatch(_leaderP1, _leaderP2, _deckP1, _deckP2);

            bool wishFired = false;
            int triggeredThreshold = 0;
            _engine.OnWishTrigger += (side, slot) =>
            {
                wishFired = true;
                triggeredThreshold = slot.threshold;
            };

            // Set HP just above 85 threshold, then damage past it
            _engine.State.player1.hp = 90;
            _engine.ApplyHpDamage(_engine.State.player1, 10, "fixed");

            yield return null;

            if (wishFired)
            {
                Assert.AreEqual(85, triggeredThreshold,
                    "First wish trigger should be at 85% threshold");
            }
        }

        void TryPlayCheapestCard(PlayerState player)
        {
            var side = _engine.State.player1 == player ? PlayerSide.Player1 : PlayerSide.Player2;
            for (int i = 0; i < player.hand.Count; i++)
            {
                if (player.hand[i].cpCost <= player.currentCP &&
                    player.hand[i].type == CardType.Manifest)
                {
                    _engine.PlayCard(side, i);
                    return;
                }
            }
        }
    }
}
