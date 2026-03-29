using System;
using System.Collections.Generic;
using System.Linq;
using Banganka.Core.Data;
using Banganka.Core.Config;
using Banganka.Core.Game;

namespace Banganka.Core.Battle
{
    public enum BotDifficulty { Easy, Normal, Hard }

    /// <summary>
    /// AI Bot with 3 difficulty levels (AI_BOT_SPEC.md).
    /// Easy=見習い, Normal=挑戦者, Hard=達人
    /// </summary>
    public class SimpleAI
    {
        readonly BattleEngine _engine;
        readonly PlayerSide _side;
        readonly BotDifficulty _difficulty;
        readonly System.Random _rng;

        public SimpleAI(BattleEngine engine, PlayerSide side, BotDifficulty difficulty = BotDifficulty.Normal)
        {
            _engine = engine;
            _side = side;
            _difficulty = difficulty;
            _rng = new System.Random();
        }

        // Backward compat
        public SimpleAI(BattleEngine engine, PlayerSide side) : this(engine, side, BotDifficulty.Normal) { }

        public void PlayTurn()
        {
            if (_engine.State.isGameOver) return;
            if (_engine.State.activePlayer != _side) return;

            // Leader skill check (Normal/Hard only, requires LeaderEvo unlocked)
            if (_difficulty != BotDifficulty.Easy && MechanicUnlockManager.IsUnlocked(MechanicType.LeaderEvo))
                TryUseLeaderSkill();

            PlayCardsGreedily();

            // 余ったCPで願成チャージ（Normal/Hard only）
            if (_difficulty != BotDifficulty.Easy)
                TryChargeEvo();

            var attacks = PlanAttacks();
            foreach (var decl in attacks)
            {
                if (_engine.CanDeclareAttack(_side, decl))
                    _engine.ResolveAttack(_side, decl, ChooseBlockerForDefense(decl));
            }

            _engine.EndTurn();
        }

        public List<int> ChooseMulliganCards()
        {
            var p = _engine.State.GetPlayer(_side);
            var toMulligan = new List<int>();

            switch (_difficulty)
            {
                case BotDifficulty.Easy:
                    // Random 0-2 cards
                    for (int i = 0; i < p.hand.Count; i++)
                        if (_rng.NextDouble() < 0.3) toMulligan.Add(i);
                    break;

                case BotDifficulty.Normal:
                    // Mulligan high-cost cards
                    for (int i = 0; i < p.hand.Count; i++)
                        if (p.hand[i].cpCost >= 5) toMulligan.Add(i);
                    break;

                case BotDifficulty.Hard:
                    // Optimize for cost curve: keep 1-3 cost, mulligan rest
                    for (int i = 0; i < p.hand.Count; i++)
                        if (p.hand[i].cpCost > 3) toMulligan.Add(i);
                    break;
            }

            return toMulligan;
        }

        void TryUseLeaderSkill()
        {
            var p = _engine.State.GetPlayer(_side);

            // Try Lv3 skill first (more powerful)
            for (int lvl = 3; lvl >= 2; lvl--)
            {
                if (!_engine.CanUseLeaderSkill(_side, lvl)) continue;

                if (_difficulty == BotDifficulty.Hard)
                {
                    // Use skills strategically
                    if (ShouldUseSkill(lvl))
                    {
                        _engine.UseLeaderSkill(_side, lvl);
                        return;
                    }
                }
                else
                {
                    // Normal: use when available with some randomness
                    if (_rng.NextDouble() < 0.7)
                    {
                        _engine.UseLeaderSkill(_side, lvl);
                        return;
                    }
                }
            }
        }

        bool ShouldUseSkill(int skillLevel)
        {
            var p = _engine.State.GetPlayer(_side);
            var opp = _engine.State.GetOpponent(_side);

            // Use offensive Lv3 skills when opponent is low HP
            if (skillLevel == 3 && opp.hp <= 30) return true;

            // Use defensive Lv2 skills when we're low HP
            if (skillLevel == 2 && p.hp <= 40) return true;

            // Use buff skills when we have units
            if (p.field.Count >= 2) return true;

            return false;
        }

        void TryChargeEvo()
        {
            var p = _engine.State.GetPlayer(_side);

            // Hard: レベルアップまであと1〜2ptの時だけCPを使う
            // Normal: 余りCPがあれば1回だけチャージ
            if (_difficulty == BotDifficulty.Hard)
            {
                int remaining = p.leader.EvoGaugeMax - p.leader.evoGauge;
                while (remaining <= 2 && _engine.CanChargeEvo(_side))
                {
                    _engine.ChargeEvo(_side);
                    remaining = p.leader.EvoGaugeMax - p.leader.evoGauge;
                }
            }
            else
            {
                // Normal: 1回だけ
                if (_engine.CanChargeEvo(_side))
                    _engine.ChargeEvo(_side);
            }
        }

        public void PlayCardsGreedily()
        {
            var p = _engine.State.GetPlayer(_side);

            if (_difficulty == BotDifficulty.Hard)
                PlayCardsOptimal(p);
            else
                PlayCardsSimple(p);
        }

        void PlayCardsSimple(PlayerState p)
        {
            bool played = true;
            while (played)
            {
                played = false;

                // Easy: random playable card
                if (_difficulty == BotDifficulty.Easy)
                {
                    var playable = new List<int>();
                    for (int i = 0; i < p.hand.Count; i++)
                        if (_engine.CanPlayCard(_side, i) && !MechanicUnlockManager.IsCardTypeLocked(p.hand[i].type))
                            playable.Add(i);

                    if (playable.Count > 0)
                    {
                        int idx = playable[_rng.Next(playable.Count)];
                        SetRowForCard(p.hand[idx]);
                        _engine.PlayCard(_side, idx);
                        played = true;
                    }
                }
                else
                {
                    // Normal: play highest cost first (skip locked card types)
                    int bestIdx = -1;
                    int bestCost = -1;
                    for (int i = 0; i < p.hand.Count; i++)
                    {
                        if (_engine.CanPlayCard(_side, i) && !MechanicUnlockManager.IsCardTypeLocked(p.hand[i].type)
                            && p.hand[i].cpCost > bestCost)
                        {
                            bestIdx = i;
                            bestCost = p.hand[i].cpCost;
                        }
                    }
                    if (bestIdx >= 0)
                    {
                        SetRowForCard(p.hand[bestIdx]);
                        _engine.PlayCard(_side, bestIdx);
                        played = true;
                    }
                }
            }
        }

        void PlayCardsOptimal(PlayerState p)
        {
            // Hard: maximize CP usage across multiple cards
            var playable = new List<(int idx, CardData card)>();
            for (int i = 0; i < p.hand.Count; i++)
                if (_engine.CanPlayCard(_side, i) && !MechanicUnlockManager.IsCardTypeLocked(p.hand[i].type))
                    playable.Add((i, p.hand[i]));

            // Sort by value score
            playable.Sort((a, b) => ScoreCard(b.card).CompareTo(ScoreCard(a.card)));

            foreach (var (idx, card) in playable)
            {
                // Re-check since hand indices shift
                int actualIdx = p.hand.IndexOf(card);
                if (actualIdx >= 0 && _engine.CanPlayCard(_side, actualIdx))
                {
                    SetRowForCard(card);
                    _engine.PlayCard(_side, actualIdx);
                }
            }
        }

        void SetRowForCard(CardData card)
        {
            // Place Blockers in front, others based on available space
            if (card.HasKeyword("Blocker"))
            {
                _engine.SetPlacementRow(FieldRow.Front);
            }
            else
            {
                var p = _engine.State.GetPlayer(_side);
                _engine.SetPlacementRow(p.HasBackSpace ? FieldRow.Back : FieldRow.Front);
            }
        }

        int ScoreCard(CardData card)
        {
            int score = 0;

            if (card.type == CardType.Manifest)
            {
                score += card.battlePower / 1000;
                score += card.wishDamage * 2;
                if (card.HasKeyword("Rush")) score += 3;
                if (card.HasKeyword("Blocker")) score += 2;
                if (card.HasKeyword("GuardBreak")) score += 2;
            }
            else if (card.type == CardType.Spell)
            {
                score += card.hpDamagePercent;
                if (card.effectKey.Contains("DESTROY")) score += 5;
                if (card.effectKey.Contains("DRAW")) score += 3;
            }
            else if (card.type == CardType.Algorithm)
            {
                score += 4;
            }

            // Tempo bonus: efficient CP usage
            if (card.cpCost > 0)
                score += (score * 10 / card.cpCost);

            return score;
        }

        public List<BattleEngine.AttackDeclaration> PlanAttacks()
        {
            var attacks = new List<BattleEngine.AttackDeclaration>();
            var p = _engine.State.GetPlayer(_side);
            var opponent = _engine.State.GetOpponent(_side);

            var readyUnits = p.field.Where(u => u.CanAttack).ToList();

            if (_difficulty == BotDifficulty.Hard)
                PlanAttacksOptimal(readyUnits, opponent, attacks);
            else
                PlanAttacksSimple(readyUnits, opponent, attacks, p);

            return attacks;
        }

        void PlanAttacksSimple(List<FieldUnit> readyUnits, PlayerState opponent,
            List<BattleEngine.AttackDeclaration> attacks, PlayerState p)
        {
            foreach (var unit in readyUnits)
            {
                var decl = new BattleEngine.AttackDeclaration
                {
                    attackerType = BattleEngine.AttackerType.Unit,
                    attackerInstanceId = unit.instanceId,
                };

                if (_difficulty == BotDifficulty.Easy)
                {
                    // Easy: random target
                    if (_rng.NextDouble() < 0.5 && opponent.field.Count > 0)
                    {
                        var target = opponent.field[_rng.Next(opponent.field.Count)];
                        decl.targetType = BattleEngine.TargetType.Unit;
                        decl.targetInstanceId = target.instanceId;
                    }
                    else
                    {
                        decl.targetType = BattleEngine.TargetType.Leader;
                    }
                }
                else
                {
                    // Normal: kill weak units or go face
                    var weakTarget = opponent.field
                        .Where(u => u.currentPower <= unit.currentPower)
                        .OrderBy(u => u.currentPower)
                        .FirstOrDefault();

                    if (weakTarget != null)
                    {
                        decl.targetType = BattleEngine.TargetType.Unit;
                        decl.targetInstanceId = weakTarget.instanceId;
                    }
                    else
                    {
                        decl.targetType = BattleEngine.TargetType.Leader;
                    }
                }

                attacks.Add(decl);
            }

            if (p.leader.CanAttack)
            {
                attacks.Add(new BattleEngine.AttackDeclaration
                {
                    attackerType = BattleEngine.AttackerType.Leader,
                    targetType = BattleEngine.TargetType.Leader,
                });
            }
        }

        void PlanAttacksOptimal(List<FieldUnit> readyUnits, PlayerState opponent,
            List<BattleEngine.AttackDeclaration> attacks)
        {
            var p = _engine.State.GetPlayer(_side);

            // Prioritize: clear threats first, then go face for lethal
            bool canLethal = CanCalculateLethal(readyUnits, opponent, p);

            if (canLethal || opponent.isFinal)
            {
                // All face for lethal
                foreach (var unit in readyUnits)
                {
                    attacks.Add(new BattleEngine.AttackDeclaration
                    {
                        attackerType = BattleEngine.AttackerType.Unit,
                        attackerInstanceId = unit.instanceId,
                        targetType = BattleEngine.TargetType.Leader,
                    });
                }
            }
            else
            {
                // Trade favorably, then face with remainder
                var remainingUnits = new List<FieldUnit>(readyUnits);
                var opponentUnits = new List<FieldUnit>(opponent.field);

                // Find favorable trades
                foreach (var unit in readyUnits.OrderByDescending(u => u.currentPower))
                {
                    var killable = opponentUnits
                        .Where(u => u.currentPower <= unit.currentPower && u.currentPower >= unit.currentPower * 0.5)
                        .OrderByDescending(u => u.currentPower)
                        .FirstOrDefault();

                    if (killable != null)
                    {
                        attacks.Add(new BattleEngine.AttackDeclaration
                        {
                            attackerType = BattleEngine.AttackerType.Unit,
                            attackerInstanceId = unit.instanceId,
                            targetType = BattleEngine.TargetType.Unit,
                            targetInstanceId = killable.instanceId,
                        });
                        remainingUnits.Remove(unit);
                        opponentUnits.Remove(killable);
                    }
                }

                // Remaining units go face
                foreach (var unit in remainingUnits)
                {
                    attacks.Add(new BattleEngine.AttackDeclaration
                    {
                        attackerType = BattleEngine.AttackerType.Unit,
                        attackerInstanceId = unit.instanceId,
                        targetType = BattleEngine.TargetType.Leader,
                    });
                }
            }

            if (p.leader.CanAttack)
            {
                attacks.Add(new BattleEngine.AttackDeclaration
                {
                    attackerType = BattleEngine.AttackerType.Leader,
                    targetType = BattleEngine.TargetType.Leader,
                });
            }
        }

        bool CanCalculateLethal(List<FieldUnit> readyUnits, PlayerState opponent, PlayerState self)
        {
            if (opponent.isFinal) return true;

            int totalWishDmg = 0;
            foreach (var u in readyUnits)
                totalWishDmg += u.currentWishDamage;
            totalWishDmg += self.leader.CanAttack ? self.leader.currentWishDamage : 0;

            // Rough estimate: can we bring opponent to 0?
            return totalWishDmg >= opponent.hp;
        }

        public string ChooseBlocker()
        {
            var p = _engine.State.GetPlayer(_side);
            var blockers = p.field.Where(u => u.CanBlock).ToList();

            if (blockers.Count == 0)
                return p.leader.CanBlock ? "leader" : null;

            if (_difficulty == BotDifficulty.Easy)
            {
                // Easy: 50% chance to not block at all
                if (_rng.NextDouble() < 0.5) return null;
                return blockers[_rng.Next(blockers.Count)].instanceId;
            }

            // Normal/Hard: block with strongest blocker
            return blockers.OrderByDescending(u => u.currentPower).First().instanceId;
        }

        string ChooseBlockerForDefense(BattleEngine.AttackDeclaration attackDecl)
        {
            // AI doesn't block its own attacks
            return null;
        }
    }
}
