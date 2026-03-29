using System.Collections;
using System.Linq;
using UnityEngine;
using Banganka.Core.Battle;
using Banganka.Game;

namespace Banganka.UI.Battle
{
    public class AIMatchController : MonoBehaviour
    {
        SimpleAI _ai;
        BattleEngine _engine;
        MatchController _matchController;

        [SerializeField] float aiThinkDelay = 1.0f;
        bool _processing;

        void OnEnable()
        {
            _engine = GameManager.Instance?.BattleEngine;
            if (_engine == null) return;

            _ai = GameManager.Instance?.BotAI ?? new SimpleAI(_engine, PlayerSide.Player2);
            _matchController = GetComponent<MatchController>();
            _engine.OnStateChanged += CheckAITurn;
        }

        void OnDisable()
        {
            if (_engine != null)
                _engine.OnStateChanged -= CheckAITurn;
        }

        void CheckAITurn()
        {
            if (_engine == null || _engine.State.isGameOver || _processing) return;
            if (_engine.State.activePlayer == PlayerSide.Player2 &&
                _engine.State.currentPhase == TurnPhase.Main)
            {
                StartCoroutine(DoAITurnCoroutine());
            }
        }

        IEnumerator DoAITurnCoroutine()
        {
            _processing = true;
            yield return new WaitForSeconds(aiThinkDelay);

            if (_engine == null || _engine.State.isGameOver ||
                _engine.State.activePlayer != PlayerSide.Player2)
            {
                _processing = false;
                yield break;
            }

            // Play cards
            _ai.PlayCardsGreedily();
            yield return new WaitForSeconds(0.5f);

            // Plan and execute attacks one by one
            var attacks = _ai.PlanAttacks();
            foreach (var atk in attacks)
            {
                if (_engine.State.isGameOver) break;
                if (!_engine.CanDeclareAttack(PlayerSide.Player2, atk)) continue;

                // When AI targets P1's leader, let player choose blocker
                if (atk.targetType == BattleEngine.TargetType.Leader &&
                    _matchController != null)
                {
                    var p1Blockers = _engine.State.player1.field.Where(u => u.CanBlock).ToList();
                    if (p1Blockers.Count > 0)
                    {
                        string selectedBlockerId = null;
                        bool decided = false;

                        _matchController.ShowBlockerSelection(atk, PlayerSide.Player2, id =>
                        {
                            selectedBlockerId = id;
                            decided = true;
                        });

                        while (!decided) yield return null;

                        _engine.ResolveAttack(PlayerSide.Player2, atk, selectedBlockerId);
                    }
                    else
                    {
                        _engine.ResolveAttack(PlayerSide.Player2, atk);
                    }
                }
                else
                {
                    _engine.ResolveAttack(PlayerSide.Player2, atk);
                }

                yield return new WaitForSeconds(0.5f);
            }

            if (!_engine.State.isGameOver)
                _engine.EndTurn();

            _processing = false;
        }
    }
}
