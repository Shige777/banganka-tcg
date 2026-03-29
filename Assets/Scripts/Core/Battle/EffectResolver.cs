using System;
using System.Collections.Generic;
using System.Linq;
using Banganka.Core.Config;
using Banganka.Core.Data;

namespace Banganka.Core.Battle
{
    /// <summary>
    /// GAME_DESIGN.md §11 の全EffectKeyを処理する。
    /// unknownゼロ設計: 未知のEffectKeyはハードエラー。
    /// HP damage calls go through BattleEngine.ApplyHpDamage for wish card trigger resolution.
    /// </summary>
    public static class EffectResolver
    {
        // ====================================================================
        // §11.1b 詠術 EffectKey
        // ====================================================================
        public static void ResolveSpell(BattleState state, PlayerSide caster, CardData spell, BattleEngine engine)
        {
            switch (spell.effectKey)
            {
                case "SPELL_PUSH_SMALL":
                case "SPELL_PUSH_MEDIUM":
                    // Legacy: convert gauge delta to fixed HP damage
                    ResolveHpDamage(state, caster, spell.baseGaugeDelta, "fixed", engine);
                    break;

                case "SPELL_HP_DAMAGE_FIXED":
                    ResolveHpDamage(state, caster, spell.hpDamagePercent, "fixed", engine);
                    break;
                case "SPELL_HP_DAMAGE_CURRENT":
                    ResolveHpDamage(state, caster, spell.hpDamagePercent, "current", engine);
                    break;

                case "SPELL_POWER_PLUS":
                    ResolvePowerPlus(state, caster, spell);
                    break;
                case "SPELL_WISHDMG_PLUS":
                    ResolveWishDmgPlus(state, caster, spell);
                    break;

                case "SPELL_REST":
                    ResolveRest(state, caster, spell);
                    break;
                case "SPELL_REMOVE_DAMAGED":
                    ResolveRemoveDamaged(state, caster, spell);
                    break;

                case "SPELL_DRAW":
                    ResolveDraw(state, caster, spell.drawCount);
                    break;
                case "SPELL_SEARCH_ASPECT":
                    ResolveSearchAspect(state, caster, spell);
                    break;
                case "SPELL_SEARCH_TYPE":
                    ResolveSearchType(state, caster, spell);
                    break;

                case "SPELL_BOUNCE":
                    ResolveBounce(state, caster, spell);
                    break;
                case "SPELL_DESTROY":
                    ResolveDestroy(state, caster, spell);
                    break;
                case "SPELL_DESTROY_ALL":
                    ResolveDestroyAll(state, caster);
                    break;

                case "SPELL_EMOTION_DESTROY":
                    ResolveEmotionDestroy(state, caster, spell);
                    break;

                case "SPELL_SLOT_LOCK":
                    ResolveSlotLock(state, caster, spell);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown spell EffectKey: {spell.effectKey}");
            }
        }

        // ====================================================================
        // §11.1 顕現 OnPlay effects
        // ====================================================================
        public static void ResolveOnPlayDraw(BattleState state, PlayerSide caster, int count)
        {
            ResolveDraw(state, caster, count);
        }

        public static void ResolveOnPlayHpDamage(BattleState state, PlayerSide caster, CardData card, BattleEngine engine)
        {
            ResolveHpDamage(state, caster, card.hpDamagePercent, card.damageType ?? "fixed", engine);
        }

        public static void ResolveOnPlayBuffAlly(BattleState state, PlayerSide caster, CardData card)
        {
            var p = state.GetPlayer(caster);
            var summoned = p.field.Count > 0 ? p.field[^1] : null;
            var target = p.field
                .Where(u => u != summoned)
                .OrderByDescending(u => u.currentPower)
                .FirstOrDefault();
            if (target != null)
                target.currentPower += card.powerDelta;
        }

        public static void ResolveOnPlayDestroy(BattleState state, PlayerSide caster, CardData card)
        {
            var opponent = state.GetOpponent(caster);
            if (string.IsNullOrEmpty(card.removeCondition)) return;

            if (card.removeCondition.StartsWith("power<="))
            {
                int threshold = int.Parse(card.removeCondition.Substring(7));
                var targets = opponent.field.Where(u => u.currentPower <= threshold).ToList();
                foreach (var t in targets)
                {
                    opponent.field.Remove(t);
                    opponent.graveyard.Add(t.cardData);
                }
            }
        }

        public static void ResolveOnPlayBounce(BattleState state, PlayerSide caster, int count)
        {
            var opponent = state.GetOpponent(caster);
            int actual = Math.Min(count, opponent.field.Count);
            var targets = opponent.field.OrderBy(u => u.currentPower).Take(actual).ToList();
            foreach (var t in targets)
            {
                opponent.field.Remove(t);
                opponent.hand.Add(t.cardData);
            }
        }

        // ====================================================================
        // §11.1 顕現 OnDeath effects
        // ====================================================================
        public static void ResolveOnDeathDraw(BattleState state, PlayerSide owner, CardData card)
        {
            int count = card.drawCount > 0 ? card.drawCount : 1;
            ResolveDraw(state, owner, count);
        }

        public static void ResolveOnDeathHpDamage(BattleState state, PlayerSide owner, CardData card, BattleEngine engine)
        {
            ResolveHpDamage(state, owner, card.hpDamagePercent, card.damageType ?? "fixed", engine);
        }

        // ====================================================================
        // §11.1d 情相参照 OnPlay effects
        // ====================================================================
        public static void ResolveOnPlayEmotionBuff(BattleState state, PlayerSide caster, CardData card)
        {
            var p = state.GetPlayer(caster);
            string emotion = card.emotionTag;
            int delta = card.powerDelta > 0 ? card.powerDelta : 500;

            foreach (var u in p.field)
            {
                if (u.cardData.emotionTag == emotion)
                    u.currentPower += delta;
            }
        }

        public static void ResolveOnPlayEmotionDraw(BattleState state, PlayerSide caster, CardData card)
        {
            var p = state.GetPlayer(caster);
            string emotion = card.emotionTag;
            int threshold = card.emotionThreshold > 0 ? card.emotionThreshold : 2;

            int count = p.field.Count(u => u.cardData.emotionTag == emotion);
            if (count >= threshold)
            {
                int drawCount = card.drawCount > 0 ? card.drawCount : 1;
                ResolveDraw(state, caster, drawCount);
            }
        }

        // ====================================================================
        // §11.1e スロット封印 OnPlay effects
        // ====================================================================
        public static void ResolveOnPlaySlotLock(BattleState state, PlayerSide caster, CardData card)
        {
            int lockCount = card.lockCount > 0 ? card.lockCount : 1;
            ResolveSlotLockInternal(state, caster, lockCount);
        }

        // ====================================================================
        // §11.1c 願主スキル EffectKey
        // ====================================================================
        public static void ResolveLeaderSkill(BattleState state, PlayerSide caster, LeaderSkill skill, BattleEngine engine)
        {
            switch (skill.effectKey)
            {
                case "LEADER_SKILL_RUSH_ALL":
                    ResolveLeaderRushAll(state, caster);
                    break;
                case "LEADER_SKILL_ATTACK_SEAL":
                    ResolveLeaderAttackSeal(state, caster, skill);
                    break;
                case "LEADER_SKILL_DRAW":
                    ResolveDraw(state, caster, skill.drawCount > 0 ? skill.drawCount : 2);
                    break;
                case "LEADER_SKILL_DISMANTLE_HALVE_DRAW":
                    ResolveLeaderDismantleHalveDraw(state, caster, skill);
                    break;
                case "LEADER_SKILL_GRAVE_TO_HAND":
                    ResolveLeaderGraveToHand(state, caster, skill);
                    break;
                case "LEADER_SKILL_DAMAGE_HALVE":
                    ResolveLeaderDamageHalve(state, caster);
                    break;

                case "LEADER_SKILL_POWER_BUFF_GUARDBREAK":
                    ResolveLeaderPowerBuffGuardBreak(state, caster, skill);
                    break;
                case "LEADER_SKILL_HAND_DISCARD":
                    ResolveLeaderHandDiscard(state, caster, skill);
                    break;
                case "LEADER_SKILL_MASS_BUFF_HEAL":
                    ResolveLeaderMassBuffHeal(state, caster, skill, engine);
                    break;
                case "LEADER_SKILL_DISMANTLE_ALL_RUIN_DAMAGE":
                    ResolveLeaderDismantleAllRuinDamage(state, caster, engine);
                    break;
                case "LEADER_SKILL_GRAVE_TO_FIELD":
                    ResolveLeaderGraveToField(state, caster, skill);
                    break;
                case "LEADER_SKILL_SACRIFICE_DAMAGE":
                    ResolveLeaderSacrificeDamage(state, caster, engine);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown leader skill EffectKey: {skill.effectKey}");
            }
        }

        // ====================================================================
        // Internal resolvers
        // ====================================================================

        static void ResolveHpDamage(BattleState state, PlayerSide caster, int percent, string damageType, BattleEngine engine)
        {
            var target = state.GetOpponent(caster);
            engine.ApplyHpDamage(target, percent, damageType);
        }

        static void ResolvePowerPlus(BattleState state, PlayerSide caster, CardData spell)
        {
            var p = state.GetPlayer(caster);
            var scope = spell.targetScope ?? "ally";

            if (scope == "ally" || scope == "all_ally")
            {
                if (scope == "all_ally")
                {
                    foreach (var u in p.field)
                        u.currentPower += spell.powerDelta;
                }
                else
                {
                    var target = p.field.OrderByDescending(u => u.currentPower).FirstOrDefault();
                    if (target != null)
                        target.currentPower += spell.powerDelta;
                }
            }
        }

        static void ResolveWishDmgPlus(BattleState state, PlayerSide caster, CardData spell)
        {
            var p = state.GetPlayer(caster);
            var target = p.field.OrderByDescending(u => u.currentWishDamage).FirstOrDefault();
            if (target != null)
                target.currentWishDamage += spell.wishDamageDelta;
        }

        static void ResolveRest(BattleState state, PlayerSide caster, CardData spell)
        {
            var scope = spell.targetScope ?? "enemy";
            if (scope != "enemy" && scope != "enemy_manifest") return;

            var targetPlayer = state.GetOpponent(caster);
            var candidates = targetPlayer.field.Where(u => u.status == UnitStatus.Ready).ToList();
            int count = Math.Min(spell.restTargets, candidates.Count);
            for (int i = 0; i < count; i++)
                candidates[i].status = UnitStatus.Exhausted;
        }

        static void ResolveRemoveDamaged(BattleState state, PlayerSide caster, CardData spell)
        {
            var opponent = state.GetOpponent(caster);
            if (string.IsNullOrEmpty(spell.removeCondition)) return;

            if (spell.removeCondition.StartsWith("power<="))
            {
                int threshold = int.Parse(spell.removeCondition.Substring(7));
                var targets = opponent.field.Where(u => u.currentPower <= threshold).ToList();
                foreach (var t in targets)
                {
                    opponent.field.Remove(t);
                    opponent.graveyard.Add(t.cardData);
                }
            }
        }

        static void ResolveDraw(BattleState state, PlayerSide caster, int count)
        {
            var p = state.GetPlayer(caster);
            int actual = Math.Min(count, p.deck.Count);
            for (int i = 0; i < actual; i++)
            {
                var card = p.deck[0];
                p.deck.RemoveAt(0);
                p.hand.Add(card);
            }
        }

        static void ResolveSearchAspect(BattleState state, PlayerSide caster, CardData spell)
        {
            var p = state.GetPlayer(caster);
            if (string.IsNullOrEmpty(spell.searchAspect)) return;
            if (!Enum.TryParse<Aspect>(spell.searchAspect, out var targetAspect)) return;

            int count = Math.Max(1, spell.searchCount);
            int found = 0;
            for (int i = 0; i < p.deck.Count && found < count; i++)
            {
                if (p.deck[i].aspect == targetAspect)
                {
                    var card = p.deck[i];
                    p.deck.RemoveAt(i);
                    p.hand.Add(card);
                    found++;
                    i--;
                }
            }
            ShuffleDeck(p);
        }

        static void ResolveSearchType(BattleState state, PlayerSide caster, CardData spell)
        {
            var p = state.GetPlayer(caster);
            if (string.IsNullOrEmpty(spell.searchType)) return;

            CardType targetType;
            switch (spell.searchType)
            {
                case "Manifest": targetType = CardType.Manifest; break;
                case "Spell": targetType = CardType.Spell; break;
                case "Algorithm": targetType = CardType.Algorithm; break;
                default: return;
            }

            int count = Math.Max(1, spell.searchCount);
            int found = 0;
            for (int i = 0; i < p.deck.Count && found < count; i++)
            {
                if (p.deck[i].type == targetType)
                {
                    var card = p.deck[i];
                    p.deck.RemoveAt(i);
                    p.hand.Add(card);
                    found++;
                    i--;
                }
            }
            ShuffleDeck(p);
        }

        static void ResolveBounce(BattleState state, PlayerSide caster, CardData spell)
        {
            var scope = spell.targetScope ?? "enemy";
            int count = Math.Max(1, spell.bounceCount);

            if (scope == "enemy")
            {
                var opponent = state.GetOpponent(caster);
                int actual = Math.Min(count, opponent.field.Count);
                var targets = opponent.field.OrderBy(u => u.currentPower).Take(actual).ToList();
                foreach (var t in targets)
                {
                    opponent.field.Remove(t);
                    opponent.hand.Add(t.cardData);
                }
            }
        }

        static void ResolveDestroy(BattleState state, PlayerSide caster, CardData spell)
        {
            var opponent = state.GetOpponent(caster);
            int count = Math.Max(1, spell.destroyCount);
            int actual = Math.Min(count, opponent.field.Count);
            var targets = opponent.field.OrderByDescending(u => u.currentPower).Take(actual).ToList();
            foreach (var t in targets)
            {
                opponent.field.Remove(t);
                opponent.graveyard.Add(t.cardData);
            }
        }

        static void ResolveDestroyAll(BattleState state, PlayerSide caster)
        {
            foreach (var side in new[] { PlayerSide.Player1, PlayerSide.Player2 })
            {
                var p = state.GetPlayer(side);
                foreach (var u in p.field.ToList())
                    p.graveyard.Add(u.cardData);
                p.field.Clear();
            }
        }

        static void ResolveEmotionDestroy(BattleState state, PlayerSide caster, CardData spell)
        {
            var opponent = state.GetOpponent(caster);
            string emotion = spell.emotionTag;
            int count = spell.destroyCount > 0 ? spell.destroyCount : 1;

            var targets = opponent.field
                .Where(u => u.cardData.emotionTag == emotion)
                .OrderByDescending(u => u.currentPower)
                .Take(count).ToList();
            foreach (var t in targets)
            {
                opponent.field.Remove(t);
                opponent.graveyard.Add(t.cardData);
            }
        }

        static void ResolveSlotLock(BattleState state, PlayerSide caster, CardData spell)
        {
            int lockCount = spell.lockCount > 0 ? spell.lockCount : 1;
            ResolveSlotLockInternal(state, caster, lockCount);
        }

        static void ResolveSlotLockInternal(BattleState state, PlayerSide caster, int lockCount)
        {
            var opponent = state.GetOpponent(caster);
            opponent.lockedSlots = Math.Min(
                opponent.lockedSlots + lockCount,
                BalanceConfig.FieldTotalSize
            );
        }

        // ====================================================================
        // §11.1c 願主スキル resolvers
        // ====================================================================

        static void ResolveLeaderRushAll(BattleState state, PlayerSide caster)
        {
            var p = state.GetPlayer(caster);
            foreach (var u in p.field)
            {
                if (!u.currentKeywords.Contains("Rush"))
                    u.currentKeywords.Add("Rush");
                u.summonSick = false;
                u.status = UnitStatus.Ready;
            }
        }

        static void ResolveLeaderAttackSeal(BattleState state, PlayerSide caster, LeaderSkill skill)
        {
            var opponent = state.GetOpponent(caster);
            int count = skill.targetCount > 0 ? skill.targetCount : 1;
            var targets = opponent.field
                .Where(u => u.status == UnitStatus.Ready)
                .OrderByDescending(u => u.currentPower)
                .Take(count).ToList();
            foreach (var t in targets)
                t.status = UnitStatus.Exhausted;
        }

        static void ResolveLeaderDismantleHalveDraw(BattleState state, PlayerSide caster, LeaderSkill skill)
        {
            var p = state.GetPlayer(caster);
            var opponent = state.GetOpponent(caster);

            var sacrifice = p.field.OrderBy(u => u.currentPower).FirstOrDefault();
            if (sacrifice != null)
            {
                p.field.Remove(sacrifice);
                p.graveyard.Add(sacrifice.cardData);
            }

            var target = opponent.field.OrderByDescending(u => u.currentPower).FirstOrDefault();
            if (target != null)
                target.currentPower = (int)(target.currentPower * (skill.powerMultiplier > 0 ? skill.powerMultiplier : 0.5f));

            int draw = skill.drawCount > 0 ? skill.drawCount : 1;
            ResolveDraw(state, caster, draw);
        }

        static void ResolveLeaderGraveToHand(BattleState state, PlayerSide caster, LeaderSkill skill)
        {
            var p = state.GetPlayer(caster);
            int count = skill.targetCount > 0 ? skill.targetCount : 1;
            int found = 0;
            for (int i = p.graveyard.Count - 1; i >= 0 && found < count; i--)
            {
                if (p.graveyard[i].type == CardType.Manifest)
                {
                    p.hand.Add(p.graveyard[i]);
                    p.graveyard.RemoveAt(i);
                    found++;
                }
            }
        }

        static void ResolveLeaderDamageHalve(BattleState state, PlayerSide caster)
        {
            var p = state.GetPlayer(caster);
            p.leader.damageHalveActive = true;
        }

        static void ResolveLeaderPowerBuffGuardBreak(BattleState state, PlayerSide caster, LeaderSkill skill)
        {
            var p = state.GetPlayer(caster);
            int delta = skill.powerDelta > 0 ? skill.powerDelta : 3000;
            foreach (var u in p.field)
            {
                u.currentPower += delta;
                if (!string.IsNullOrEmpty(skill.grantKeyword) && !u.currentKeywords.Contains(skill.grantKeyword))
                    u.currentKeywords.Add(skill.grantKeyword);
            }
        }

        static void ResolveLeaderHandDiscard(BattleState state, PlayerSide caster, LeaderSkill skill)
        {
            var opponent = state.GetOpponent(caster);
            int count = skill.targetCount > 0 ? skill.targetCount : 2;
            var rng = new System.Random();

            for (int i = 0; i < count && opponent.hand.Count > 0; i++)
            {
                int idx = rng.Next(opponent.hand.Count);
                var card = opponent.hand[idx];
                opponent.hand.RemoveAt(idx);
                opponent.graveyard.Add(card);
            }
        }

        // 灯凪 Lv3: 自軍顕現全体の戦力+2000（永続）＋自HP10回復
        static void ResolveLeaderMassBuffHeal(BattleState state, PlayerSide caster, LeaderSkill skill, BattleEngine engine)
        {
            var p = state.GetPlayer(caster);
            int delta = skill.powerDelta > 0 ? skill.powerDelta : 2000;
            foreach (var u in p.field)
                u.currentPower += delta;

            int healAmount = skill.healHP > 0 ? skill.healHP : 10;
            engine.HealHp(p, healAmount);
        }

        // Amara Lv3: 自軍顕現を全て解体し、墓地枚数×固定2%ダメージ
        static void ResolveLeaderDismantleAllRuinDamage(BattleState state, PlayerSide caster, BattleEngine engine)
        {
            var p = state.GetPlayer(caster);

            foreach (var u in p.field.ToList())
                p.graveyard.Add(u.cardData);
            p.field.Clear();

            int ruinCount = p.graveyard.Count;
            int damage = ruinCount * 2; // 墓地枚数×固定2%
            var target = state.GetOpponent(caster);
            engine.ApplyHpDamage(target, damage, "fixed");
        }

        static void ResolveLeaderGraveToField(BattleState state, PlayerSide caster, LeaderSkill skill)
        {
            var p = state.GetPlayer(caster);
            if (p.AvailableSlots <= 0) return;

            int count = skill.targetCount > 0 ? skill.targetCount : 1;
            int found = 0;
            for (int i = p.graveyard.Count - 1; i >= 0 && found < count; i--)
            {
                if (p.graveyard[i].type == CardType.Manifest)
                {
                    var card = p.graveyard[i];
                    p.graveyard.RemoveAt(i);
                    var unit = new FieldUnit(card, state.NextInstanceId());
                    unit.row = FieldRow.Front;
                    unit.summonSick = false;
                    unit.status = UnitStatus.Ready;
                    p.field.Add(unit);
                    found++;
                }
            }
        }

        // 崔鋒 Lv3: 自軍顕現1体を退場させ、戦力の10%を固定%ダメージ
        static void ResolveLeaderSacrificeDamage(BattleState state, PlayerSide caster, BattleEngine engine)
        {
            var p = state.GetPlayer(caster);
            var sacrifice = p.field.OrderBy(u => u.currentPower).FirstOrDefault();
            if (sacrifice == null) return;

            int power = sacrifice.currentPower;
            p.field.Remove(sacrifice);
            p.graveyard.Add(sacrifice.cardData);

            int damage = Math.Max(1, power / 1000); // power 5000 → 5% damage
            var target = state.GetOpponent(caster);
            engine.ApplyHpDamage(target, damage, "fixed");
        }

        // ====================================================================
        // Utility
        // ====================================================================
        static void ShuffleDeck(PlayerState p)
        {
            var rng = new System.Random();
            var deck = p.deck;
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
        }
    }
}
