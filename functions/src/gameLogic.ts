/**
 * Banganka — Server-side game logic
 * GAME_DESIGN.md rules, NETWORK_SPEC.md §4.2 command validation
 */

import {
  GameState, PrivateState, FieldUnit, GameCommand, SharedAlgo,
  CardMaster, LeaderMaster, LeaderState, WishSlot, GameConstants, ErrorCodes,
  MatchMode, getMatchConstants,
} from "./types";

type PlayerNum = 1 | 2;

interface CommandResult {
  success: boolean;
  error?: string;
  stateUpdated: boolean;
  matchEnded?: boolean;
  winner?: "P1" | "P2" | "Draw";
  reason?: string;
  logEntries: LogEntry[];
}

interface LogEntry {
  event: string;
  data: Record<string, unknown>;
  timestamp: number;
}

let instanceCounter = 0;
function nextInstanceId(): string {
  return `srv_${++instanceCounter}_${Date.now()}`;
}

// ====================================================================
// Command Dispatcher
// ====================================================================

export function processCommand(
  state: GameState,
  priv: PrivateState,
  cmd: GameCommand,
  cards: Map<string, CardMaster>,
  leaders: Map<string, LeaderMaster>,
  playerNum: PlayerNum,
): CommandResult {
  const logs: LogEntry[] = [];
  const now = Date.now();

  // Sequence number validation (SECURITY_SPEC.md §3.2 — replay attack prevention)
  if (cmd.seq !== undefined) {
    const lastSeq = playerNum === 1 ? (state.lastSeqP1 ?? 0) : (state.lastSeqP2 ?? 0);
    if (cmd.seq <= lastSeq) {
      return { success: false, error: "ERR_REPLAY_ATTACK", stateUpdated: false, logEntries: [] };
    }
    if (playerNum === 1) state.lastSeqP1 = cmd.seq;
    else state.lastSeqP2 = cmd.seq;
  }

  // Active player check (EndTurn always allowed for active player)
  if (state.activePlayer !== playerNum) {
    // DeclareBlock, PlayAmbush, and Mulligan are special — allowed for non-active player
    if (cmd.type !== "DeclareBlock" && cmd.type !== "PlayAmbush" && cmd.type !== "Mulligan" && cmd.type !== "FlipAlgorithm") {
      return { success: false, error: ErrorCodes.NOT_YOUR_TURN, stateUpdated: false, logEntries: [] };
    }
  }

  if (state.isGameOver) {
    return { success: false, error: "MATCH_ALREADY_ENDED", stateUpdated: false, logEntries: [] };
  }

  let result: CommandResult;

  switch (cmd.type) {
    case "Mulligan":
      result = handleMulligan(state, priv, cmd, playerNum, logs, now);
      break;
    case "PlayManifest":
      result = handlePlayManifest(state, priv, cmd, cards, playerNum, logs, now);
      break;
    case "PlaySpell":
      result = handlePlaySpell(state, priv, cmd, cards, playerNum, logs, now);
      break;
    case "PlayAlgorithm":
      result = handlePlayAlgorithm(state, priv, cmd, cards, playerNum, logs, now);
      break;
    case "FlipAlgorithm":
      result = handleFlipAlgorithm(state, cmd, playerNum, logs, now);
      break;
    case "PlayAmbush":
      result = handlePlayAmbush(state, priv, cmd, cards, playerNum, logs, now);
      break;
    case "DeclareAttack":
      result = handleDeclareAttack(state, priv, cmd, cards, playerNum, logs, now);
      break;
    case "DeclareBlock":
      result = handleDeclareBlock(state, cmd, playerNum, logs, now, cards, priv);
      break;
    case "UseLeaderSkill":
      result = handleUseLeaderSkill(state, priv, cmd, leaders, playerNum, logs, now);
      break;
    case "EndTurn":
      result = handleEndTurn(state, priv, cards, playerNum, logs, now);
      break;
    default:
      return { success: false, error: "UNKNOWN_COMMAND", stateUpdated: false, logEntries: [] };
  }

  // Check victory conditions after any state change
  if (result.stateUpdated && !result.matchEnded) {
    const victory = checkVictoryConditions(state);
    if (victory) {
      state.isGameOver = true;
      result.matchEnded = true;
      result.winner = victory.winner;
      result.reason = victory.reason;
      logs.push({ event: "match_end", data: { winner: victory.winner, reason: victory.reason }, timestamp: now });
    }
  }

  return result;
}

// ====================================================================
// Mulligan (GAME_DESIGN.md §9.3)
// ====================================================================

function handleMulligan(
  state: GameState, priv: PrivateState, cmd: GameCommand,
  player: PlayerNum, logs: LogEntry[], now: number,
): CommandResult {
  const returnIndices = cmd.payload.returnIndices as number[] | undefined;
  if (!returnIndices || !Array.isArray(returnIndices)) {
    return fail("ERR_INVALID_MULLIGAN");
  }

  const hand = player === 1 ? priv.handP1 : priv.handP2;
  const deck = player === 1 ? priv.deckP1 : priv.deckP2;

  // Validate indices
  for (const idx of returnIndices) {
    if (idx < 0 || idx >= hand.length) {
      return fail("ERR_INVALID_MULLIGAN_INDEX");
    }
  }

  // Remove duplicates and sort descending for safe removal
  const uniqueIndices = [...new Set(returnIndices)].sort((a, b) => b - a);

  // Return selected cards to deck
  const returned: string[] = [];
  for (const idx of uniqueIndices) {
    returned.push(hand[idx]);
    hand.splice(idx, 1);
  }
  deck.push(...returned);

  // Shuffle deck
  for (let i = deck.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [deck[i], deck[j]] = [deck[j], deck[i]];
  }

  // Draw same number of cards
  for (let i = 0; i < returned.length && deck.length > 0; i++) {
    hand.push(deck.shift()!);
  }

  if (player === 1) state.handCountP1 = hand.length;
  else state.handCountP2 = hand.length;

  logs.push({ event: "mulligan", data: { player, returned: returned.length }, timestamp: now });

  return { success: true, stateUpdated: true, logEntries: logs };
}

// ====================================================================
// DeclareBlock (GAME_DESIGN.md §10.5)
// ====================================================================

function handleDeclareBlock(
  state: GameState, cmd: GameCommand,
  player: PlayerNum, logs: LogEntry[], now: number,
  cards?: Map<string, CardMaster>, priv?: PrivateState,
): CommandResult {
  // Block is declared by the defending player (not active player)
  const blockerId = cmd.payload.blockerId as string;
  const attackerId = cmd.payload.attackerId as string;

  if (!blockerId || !attackerId) return fail(ErrorCodes.INVALID_BLOCK);

  const field = player === 1 ? state.fieldP1 : state.fieldP2;
  const blocker = field.find(u => u.instanceId === blockerId);

  if (!blocker) return fail("ERR_BLOCKER_NOT_FOUND");
  if (blocker.exhausted) return fail(ErrorCodes.EXHAUSTED);
  if (!blocker.keywords.includes("Blocker")) return fail("ERR_NOT_BLOCKER");

  // Find the attacker on opponent's field
  const oppField = player === 1 ? state.fieldP2 : state.fieldP1;
  const attacker = oppField.find(u => u.instanceId === attackerId);
  if (!attacker) return fail("ERR_ATTACKER_NOT_FOUND");

  // M3: GuardBreak check — attacker with GuardBreak bypasses blockers (already handled in DeclareAttack)
  // DeclareBlock should reject if attacker has GuardBreak or Rush
  if (attacker.keywords.includes("GuardBreak") || attacker.keywords.includes("Rush")) {
    return fail("ERR_GUARD_BREAK");
  }

  // Resolve combat: blocker vs attacker
  const atkPow = attacker.power;
  const defPow = blocker.power;

  const blockerDestroyed = atkPow >= defPow;
  const attackerDestroyed = defPow >= atkPow;

  if (blockerDestroyed) {
    // Blocker destroyed
    const idx = field.findIndex(u => u.instanceId === blockerId);
    if (idx >= 0) {
      const destroyedUnit = field[idx];
      field.splice(idx, 1);
      if (cards && priv) {
        resolveOnDeathEffect(state, priv, player, destroyedUnit, cards);
      }
    }
    logs.push({ event: "unit_destroyed", data: { player, targetId: blockerId }, timestamp: now });
  }
  if (attackerDestroyed) {
    // Attacker destroyed
    const oppPlayer: PlayerNum = player === 1 ? 2 : 1;
    const idx = oppField.findIndex(u => u.instanceId === attackerId);
    if (idx >= 0) {
      const destroyedUnit = oppField[idx];
      oppField.splice(idx, 1);
      if (cards && priv) {
        resolveOnDeathEffect(state, priv, oppPlayer, destroyedUnit, cards);
      }
    }
    logs.push({ event: "unit_destroyed", data: { player: player === 1 ? 2 : 1, targetId: attackerId }, timestamp: now });
  }

  // Only exhaust blocker if it survived
  if (!blockerDestroyed) {
    blocker.exhausted = true;
  }

  logs.push({ event: "block_declared", data: { player, blockerId, attackerId }, timestamp: now });

  return { success: true, stateUpdated: true, logEntries: logs };
}

// ====================================================================
// PlayManifest
// ====================================================================

function handlePlayManifest(
  state: GameState, priv: PrivateState, cmd: GameCommand,
  cards: Map<string, CardMaster>, player: PlayerNum,
  logs: LogEntry[], now: number,
): CommandResult {
  if (state.currentPhase !== "Main") {
    return fail(ErrorCodes.INVALID_PHASE);
  }

  const cardId = cmd.payload.cardId as string;
  const hand = player === 1 ? priv.handP1 : priv.handP2;
  const handIdx = hand.indexOf(cardId);
  if (handIdx < 0) return fail(ErrorCodes.CARD_NOT_IN_HAND);

  const card = cards.get(cardId);
  if (!card || card.type !== "Manifest") return fail("ERR_NOT_MANIFEST");

  const cp = player === 1 ? state.cpP1 : state.cpP2;
  if (cp < card.cpCost) return fail(ErrorCodes.INSUFFICIENT_CP);

  const field = player === 1 ? state.fieldP1 : state.fieldP2;
  if (field.length >= GameConstants.MAX_FIELD_SIZE) return fail(ErrorCodes.FIELD_FULL);

  // Execute
  hand.splice(handIdx, 1);
  if (player === 1) { state.cpP1 -= card.cpCost; state.handCountP1--; }
  else { state.cpP2 -= card.cpCost; state.handCountP2--; }

  const row = (cmd.payload.row === "Back" ? "Back" : "Front") as "Front" | "Back";
  const unit: FieldUnit = {
    instanceId: nextInstanceId(),
    cardId: card.id,
    row,
    power: card.battlePower,
    wishDamage: card.wishDamage,
    keywords: [...(card.keywords || [])],
    exhausted: false,
    summonSick: !(card.keywords?.includes("Rush")),
  };
  field.push(unit);

  // Evo gauge (matching aspect)
  const evoLevelUp = addEvoGauge(state, player, card);
  if (evoLevelUp > 0) {
    logs.push({ event: "leader_evo_level_up", data: { player, newLevel: evoLevelUp }, timestamp: now });
  }

  // On-play effects
  applyOnPlayEffects(state, priv, card, player, cards);

  logs.push({ event: "play_manifest", data: { player, cardId, instanceId: unit.instanceId }, timestamp: now });

  return { success: true, stateUpdated: true, logEntries: logs };
}

// ====================================================================
// PlaySpell
// ====================================================================

function handlePlaySpell(
  state: GameState, priv: PrivateState, cmd: GameCommand,
  cards: Map<string, CardMaster>, player: PlayerNum,
  logs: LogEntry[], now: number,
): CommandResult {
  if (state.currentPhase !== "Main") return fail(ErrorCodes.INVALID_PHASE);

  const cardId = cmd.payload.cardId as string;
  const hand = player === 1 ? priv.handP1 : priv.handP2;
  const handIdx = hand.indexOf(cardId);
  if (handIdx < 0) return fail(ErrorCodes.CARD_NOT_IN_HAND);

  const card = cards.get(cardId);
  if (!card || card.type !== "Spell") return fail("ERR_NOT_SPELL");

  const cp = player === 1 ? state.cpP1 : state.cpP2;
  if (cp < card.cpCost) return fail(ErrorCodes.INSUFFICIENT_CP);

  // Execute
  hand.splice(handIdx, 1);
  if (player === 1) { state.cpP1 -= card.cpCost; state.handCountP1--; }
  else { state.cpP2 -= card.cpCost; state.handCountP2--; }

  addEvoGauge(state, player, card);
  applySpellEffect(state, priv, card, player, cards);

  logs.push({ event: "play_spell", data: { player, cardId, effectKey: card.effectKey }, timestamp: now });

  return { success: true, stateUpdated: true, logEntries: logs };
}

// ====================================================================
// PlayAlgorithm
// ====================================================================

function handlePlayAlgorithm(
  state: GameState, priv: PrivateState, cmd: GameCommand,
  cards: Map<string, CardMaster>, player: PlayerNum,
  logs: LogEntry[], now: number,
): CommandResult {
  if (state.currentPhase !== "Main") return fail(ErrorCodes.INVALID_PHASE);
  if (state.algorithmPlayedThisTurn) return fail(ErrorCodes.ALGO_ALREADY_PLAYED);

  const cardId = cmd.payload.cardId as string;
  const hand = player === 1 ? priv.handP1 : priv.handP2;
  const handIdx = hand.indexOf(cardId);
  if (handIdx < 0) return fail(ErrorCodes.CARD_NOT_IN_HAND);

  const card = cards.get(cardId);
  if (!card || card.type !== "Algorithm") return fail("ERR_NOT_ALGORITHM");

  const cp = player === 1 ? state.cpP1 : state.cpP2;
  if (cp < card.cpCost) return fail(ErrorCodes.INSUFFICIENT_CP);

  // Execute
  hand.splice(handIdx, 1);
  if (player === 1) { state.cpP1 -= card.cpCost; state.handCountP1--; }
  else { state.cpP2 -= card.cpCost; state.handCountP2--; }

  const faceDown = cmd.payload.faceDown !== false; // default face-down unless explicitly false
  state.sharedAlgo = { cardId: card.id, owner: player, faceDown, setTurn: state.turnTotal };
  state.algorithmPlayedThisTurn = true;

  addEvoGauge(state, player, card);

  logs.push({ event: "play_algorithm", data: { player, cardId }, timestamp: now });

  return { success: true, stateUpdated: true, logEntries: logs };
}

// ====================================================================
// FlipAlgorithm (GAME_DESIGN.md §12 — manual face-up flip by owner)
// ====================================================================

function handleFlipAlgorithm(
  state: GameState, cmd: GameCommand,
  player: PlayerNum, logs: LogEntry[], now: number,
): CommandResult {
  if (!state.sharedAlgo) return fail("ERR_NO_ALGORITHM");
  if (state.sharedAlgo.owner !== player) return fail("ERR_NOT_ALGO_OWNER");
  if (!state.sharedAlgo.faceDown) return fail("ERR_ALREADY_FACE_UP");

  state.sharedAlgo.faceDown = false;
  logs.push({ event: "algorithm_flip", data: { cardId: state.sharedAlgo.cardId, owner: player, manual: true }, timestamp: now });

  return { success: true, stateUpdated: true, logEntries: logs };
}

// ====================================================================
// PlayAmbush (GAME_DESIGN.md §5.7)
// ====================================================================

function handlePlayAmbush(
  state: GameState, priv: PrivateState, cmd: GameCommand,
  cards: Map<string, CardMaster>, player: PlayerNum,
  logs: LogEntry[], now: number,
): CommandResult {
  // Ambush is played on the OPPONENT's turn (interrupting)
  const cardId = cmd.payload.cardId as string;
  const ambushTrigger = cmd.payload.trigger as string; // "defend" | "retaliate"
  const hand = player === 1 ? priv.handP1 : priv.handP2;
  const handIdx = hand.indexOf(cardId);
  if (handIdx < 0) return fail(ErrorCodes.CARD_NOT_IN_HAND);

  const card = cards.get(cardId);
  if (!card || card.type !== "Manifest") return fail("ERR_NOT_MANIFEST");
  if (!card.keywords || !card.keywords.includes("Ambush")) return fail("ERR_NOT_AMBUSH");

  const field = player === 1 ? state.fieldP1 : state.fieldP2;
  if (field.length >= GameConstants.MAX_FIELD_SIZE) return fail(ErrorCodes.FIELD_FULL);

  // Ambush doesn't cost CP, but requires discarding 1 extra card from hand (hand cost)
  if (hand.length < 2) return fail("ERR_INSUFFICIENT_HAND"); // need the ambush card + 1 discard

  // Execute: remove from hand
  hand.splice(handIdx, 1);
  if (player === 1) state.handCountP1--;
  else state.handCountP2--;

  // Discard 1 card as ambush cost (remove last card as simplified logic)
  const discarded = hand.pop()!;
  if (player === 1) state.handCountP1--;
  else state.handCountP2--;

  // Place unit on field (no summoning sickness for ambush units)
  const unit: FieldUnit = {
    instanceId: nextInstanceId(),
    cardId: card.id,
    row: "Front",
    power: card.battlePower,
    wishDamage: card.wishDamage,
    keywords: [...(card.keywords || [])],
    exhausted: false,
    summonSick: false, // Ambush units can act immediately
  };
  field.push(unit);

  addEvoGauge(state, player, card);

  // Resolve ambush on-play effects (C2)
  applyOnPlayEffects(state, priv, card, player, cards);

  logs.push({
    event: "play_ambush",
    data: { player, cardId, trigger: ambushTrigger, discarded },
    timestamp: now,
  });

  return { success: true, stateUpdated: true, logEntries: logs };
}

// ====================================================================
// DeclareAttack
// ====================================================================

function handleDeclareAttack(
  state: GameState, priv: PrivateState, cmd: GameCommand,
  cards: Map<string, CardMaster>, player: PlayerNum,
  logs: LogEntry[], now: number,
): CommandResult {
  if (state.currentPhase !== "Main" && state.currentPhase !== "Combat") {
    return fail(ErrorCodes.INVALID_PHASE);
  }
  const attackerId = cmd.payload.attackerId as string;
  const targetType = cmd.payload.targetType as "leader" | "unit";
  const targetId = cmd.payload.targetId as string | undefined;

  const field = player === 1 ? state.fieldP1 : state.fieldP2;
  const leader = player === 1 ? state.leaderP1 : state.leaderP2;
  const oppField = player === 1 ? state.fieldP2 : state.fieldP1;
  const oppLeader = player === 1 ? state.leaderP2 : state.leaderP1;

  // Find attacker (unit or leader)
  let attackerPower: number;
  let attackerWishDmg: number;
  let isLeaderAttack = false;

  if (attackerId === "leader") {
    if (leader.exhausted) return fail(ErrorCodes.EXHAUSTED);
    attackerPower = leader.power;
    attackerWishDmg = leader.wishDamage;
    isLeaderAttack = true;
  } else {
    const unit = field.find(u => u.instanceId === attackerId);
    if (!unit) return fail("ERR_ATTACKER_NOT_FOUND");
    if (unit.exhausted) return fail(ErrorCodes.EXHAUSTED);
    if (unit.summonSick) return fail(ErrorCodes.SUMMON_SICK);
    attackerPower = unit.power;
    attackerWishDmg = unit.wishDamage;
  }

  if (targetType === "leader") {
    // Check for GuardBreak or Rush (both bypass Blockers)
    const attackerUnit = isLeaderAttack ? null : field.find(u => u.instanceId === attackerId);
    const hasGuardBreak = isLeaderAttack
      ? leader.keywords.includes("GuardBreak")
      : attackerUnit?.keywords.includes("GuardBreak") ?? false;
    const hasRush = isLeaderAttack
      ? false
      : attackerUnit?.keywords.includes("Rush") ?? false;

    const blockers = oppField.filter(u => u.keywords.includes("Blocker") && !u.exhausted);
    let blockerId: string | null = null;

    if (!hasGuardBreak && !hasRush && blockers.length > 0) {
      // Blocker intercepts — server auto-selects strongest (client uses DeclareBlock)
      blockers.sort((a, b) => b.power - a.power);
      blockerId = blockers[0].instanceId;
    }

    if (blockerId) {
      // Combat vs blocker
      const blocker = oppField.find(u => u.instanceId === blockerId)!;
      resolveCombat(state, attackerId, isLeaderAttack, player, blocker, field, oppField, leader, logs, now, cards, priv);
    } else {
      // Direct hit to leader
      applyWishDamage(state, player === 1 ? 2 : 1, attackerWishDmg);

      // Algorithm bonus (direct_hit_plus)
      if (state.sharedAlgo) {
        const algoBonus = getAlgoBonus(state.sharedAlgo, "direct_hit_plus", player, cards);
        if (algoBonus > 0) {
          applyWishDamage(state, player === 1 ? 2 : 1, algoBonus);
        }
      }

      logs.push({ event: "direct_hit", data: { player, attackerId, damage: attackerWishDmg }, timestamp: now });
    }
  } else if (targetType === "unit" && targetId) {
    // Attack specific unit
    const target = oppField.find(u => u.instanceId === targetId);
    if (!target) return fail("ERR_TARGET_NOT_FOUND");

    resolveCombat(state, attackerId, isLeaderAttack, player, target, field, oppField, leader, logs, now, cards, priv);
  } else {
    return fail("ERR_INVALID_TARGET_TYPE");
  }

  // Exhaust attacker
  if (isLeaderAttack) {
    leader.exhausted = true;
  } else {
    const attacker = field.find(u => u.instanceId === attackerId);
    if (attacker) attacker.exhausted = true;
  }

  // Check wish thresholds after damage
  checkWishThresholds(state, priv, 1, cards);
  checkWishThresholds(state, priv, 2, cards);

  return { success: true, stateUpdated: true, logEntries: logs };
}

function resolveCombat(
  state: GameState, attackerId: string, isLeaderAttack: boolean,
  attackerPlayer: PlayerNum, defender: FieldUnit,
  attackerField: FieldUnit[], defenderField: FieldUnit[],
  attackerLeader: { power: number; wishDamage: number },
  logs: LogEntry[], now: number,
  cards?: Map<string, CardMaster>,
  priv?: PrivateState,
): void {
  const atkPower = isLeaderAttack
    ? attackerLeader.power
    : attackerField.find(u => u.instanceId === attackerId)?.power ?? 0;

  // Apply damage halve if defending leader has it active
  const defPlayer: PlayerNum = attackerPlayer === 1 ? 2 : 1;
  const defLeader = defPlayer === 1 ? state.leaderP1 : state.leaderP2;

  const defenderDestroyed = atkPower >= defender.power;
  const attackerDestroyed = defender.power >= atkPower && !isLeaderAttack;

  if (defenderDestroyed) {
    // Defender destroyed
    const idx = defenderField.findIndex(u => u.instanceId === defender.instanceId);
    if (idx >= 0) {
      defenderField.splice(idx, 1);
      // On-death effects (C1)
      if (cards && priv) {
        resolveOnDeathEffect(state, priv, defPlayer, defender, cards);
      }
    }
    logs.push({ event: "unit_destroyed", data: { player: attackerPlayer, targetId: defender.instanceId }, timestamp: now });

    // Surviving attacker deals wish damage to defending leader
    if (!attackerDestroyed) {
      const attacker = isLeaderAttack ? null : attackerField.find(u => u.instanceId === attackerId);
      const wishDmg = isLeaderAttack
        ? attackerLeader.wishDamage
        : (attacker?.wishDamage ?? 0);
      if (wishDmg > 0) {
        applyWishDamage(state, defPlayer, wishDmg);
        logs.push({ event: "combat_wish_damage", data: { player: attackerPlayer, targetPlayer: defPlayer, damage: wishDmg }, timestamp: now });
      }
    }
  }

  if (attackerDestroyed) {
    // Attacker destroyed
    const idx = attackerField.findIndex(u => u.instanceId === attackerId);
    if (idx >= 0) {
      const destroyedUnit = attackerField[idx];
      attackerField.splice(idx, 1);
      // On-death effects (C1)
      if (cards && priv) {
        resolveOnDeathEffect(state, priv, attackerPlayer, destroyedUnit, cards);
      }
    }
    logs.push({ event: "unit_destroyed", data: { player: attackerPlayer, targetId: attackerId }, timestamp: now });
  }
}

// ====================================================================
// UseLeaderSkill
// ====================================================================

function handleUseLeaderSkill(
  state: GameState, priv: PrivateState, cmd: GameCommand,
  leaders: Map<string, LeaderMaster>, player: PlayerNum,
  logs: LogEntry[], now: number,
): CommandResult {
  if (state.currentPhase !== "Main" && state.currentPhase !== "Combat") {
    return fail(ErrorCodes.INVALID_PHASE);
  }
  const skillLevel = cmd.payload.skillLevel as number;
  if (skillLevel !== 2 && skillLevel !== 3) return fail("ERR_INVALID_SKILL_LEVEL");

  const leader = player === 1 ? state.leaderP1 : state.leaderP2;

  if (leader.level < skillLevel) return fail(ErrorCodes.SKILL_LEVEL_TOO_LOW);

  if (skillLevel === 2) {
    if (leader.skillUsedLv2) return fail(ErrorCodes.SKILL_ALREADY_USED);
    if (leader.skillUsedThisTurnLv2) return fail(ErrorCodes.SKILL_ALREADY_USED);
    leader.skillUsedLv2 = true;
    leader.skillUsedThisTurnLv2 = true;
  } else if (skillLevel === 3) {
    if (leader.skillUsedLv3) return fail(ErrorCodes.SKILL_ALREADY_USED);
    if (leader.skillUsedThisTurnLv3) return fail(ErrorCodes.SKILL_ALREADY_USED);
    leader.skillUsedLv3 = true;
    leader.skillUsedThisTurnLv3 = true;
  }

  // Resolve effect based on effectKey from leader master data
  const leaderAspect = leader.aspect;
  // Find matching leader master to get skill definition
  let skillDef: { effectKey: string; effectValue: number; targetCount?: number } | undefined;
  for (const lm of leaders.values()) {
    if (lm.aspect === leaderAspect) {
      skillDef = lm.leaderSkills.find(s => s.level === skillLevel);
      break;
    }
  }

  if (skillDef) {
    resolveLeaderSkillEffect(state, priv, player, skillDef.effectKey, skillDef.effectValue, skillDef.targetCount, logs, now);
  }

  logs.push({ event: "leader_skill", data: { player, skillLevel }, timestamp: now });

  return { success: true, stateUpdated: true, logEntries: logs };
}

// ====================================================================
// EndTurn
// ====================================================================

function handleEndTurn(
  state: GameState, priv: PrivateState, cards: Map<string, CardMaster>,
  player: PlayerNum, logs: LogEntry[], now: number,
): CommandResult {
  if (state.activePlayer !== player) return fail(ErrorCodes.NOT_YOUR_TURN);

  // Switch active player
  state.activePlayer = state.activePlayer === 1 ? 2 : 1;
  state.turnTotal++;
  state.currentPhase = "Main";
  state.algorithmPlayedThisTurn = false;

  const nextPlayer = state.activePlayer;

  // Increase max CP (capped at 10)
  if (nextPlayer === 1) {
    state.maxCpP1 = Math.min(state.maxCpP1 + 1, GameConstants.MAX_CP);
    state.cpP1 = state.maxCpP1;
  } else {
    state.maxCpP2 = Math.min(state.maxCpP2 + 1, GameConstants.MAX_CP);
    state.cpP2 = state.maxCpP2;
  }

  // Draw card
  drawCard(state, priv, nextPlayer);

  // Ready all units (clear exhausted + summon sickness)
  const field = nextPlayer === 1 ? state.fieldP1 : state.fieldP2;
  for (const unit of field) {
    unit.exhausted = false;
    unit.summonSick = false;
  }

  // Ready leader
  const leader = nextPlayer === 1 ? state.leaderP1 : state.leaderP2;
  leader.exhausted = false;
  leader.skillUsedThisTurnLv2 = false;
  leader.skillUsedThisTurnLv3 = false;
  leader.damageHalveActive = false; // Reset damage halve each turn

  // Auto-flip face-down algorithm after 1 turn (GAME_DESIGN.md §12)
  if (state.sharedAlgo && state.sharedAlgo.faceDown && state.turnTotal > state.sharedAlgo.setTurn) {
    state.sharedAlgo.faceDown = false;
    logs.push({ event: "algorithm_flip", data: { cardId: state.sharedAlgo.cardId, owner: state.sharedAlgo.owner }, timestamp: now });
  }

  // Turn limit check (mode-aware)
  const mc = getMatchConstants(state.matchMode ?? "standard");
  if (state.turnTotal >= mc.TURN_LIMIT) {
    state.isGameOver = true;
    const hp1 = state.hpP1;
    const hp2 = state.hpP2;
    const winner = hp1 > hp2 ? "P1" : hp2 > hp1 ? "P2" : "Draw";
    logs.push({ event: "turn_limit", data: { turnTotal: state.turnTotal, winner }, timestamp: now });
    return {
      success: true, stateUpdated: true, matchEnded: true,
      winner: winner as "P1" | "P2" | "Draw", reason: "turn_limit", logEntries: logs,
    };
  }

  logs.push({ event: "end_turn", data: { nextPlayer, turn: state.turnTotal }, timestamp: now });

  return { success: true, stateUpdated: true, logEntries: logs };
}

// ====================================================================
// Helper Functions
// ====================================================================

function fail(error: string): CommandResult {
  return { success: false, error, stateUpdated: false, logEntries: [] };
}

/** Exported for handleTimeout in index.ts */
export { drawCard as drawCardHelper };

function drawCard(state: GameState, priv: PrivateState, player: PlayerNum): void {
  const deck = player === 1 ? priv.deckP1 : priv.deckP2;
  const hand = player === 1 ? priv.handP1 : priv.handP2;

  if (deck.length === 0) {
    // Deck-out: take 5 wish damage as penalty
    applyWishDamage(state, player, 5);
    return;
  }

  // L1: Max hand size enforcement — silently skip draw
  if (hand.length >= GameConstants.MAX_HAND_SIZE) return;

  const cardId = deck.shift()!;
  hand.push(cardId);

  if (player === 1) state.handCountP1 = hand.length;
  else state.handCountP2 = hand.length;
}

function addEvoGauge(
  state: GameState, player: PlayerNum,
  card: CardMaster,
): number {
  const leader = player === 1 ? state.leaderP1 : state.leaderP2;
  // Only matching aspect cards contribute to evo gauge
  if (card.aspect !== leader.aspect) return 0;
  // Already at max level — no more gauge accumulation
  if (leader.level >= 3) return 0;
  leader.evoGauge++;

  // Level up check (M4: returns new level for logging)
  if (leader.level === 1 && leader.evoGauge >= GameConstants.EVO_GAUGE_LV2) {
    leader.level = 2;
    leader.evoGauge = 0;
    leader.evoMax = GameConstants.EVO_GAUGE_LV3;
    leader.power += 1000;
    leader.wishDamage += 1;
    return 2; // signal level-up
  } else if (leader.level === 2 && leader.evoGauge >= GameConstants.EVO_GAUGE_LV3) {
    leader.level = 3;
    leader.evoGauge = 0;
    leader.power += 1000;
    leader.wishDamage += 1;
    return 3; // signal level-up
  }
  return 0;
}

function applyWishDamage(state: GameState, targetPlayer: PlayerNum, damage: number): void {
  // C5: Apply damage halve if active
  const leader = targetPlayer === 1 ? state.leaderP1 : state.leaderP2;
  let finalDamage = damage;
  if (leader.damageHalveActive) {
    finalDamage = Math.floor(damage / 2);
  }
  if (targetPlayer === 1) {
    state.hpP1 = Math.max(0, state.hpP1 - finalDamage);
  } else {
    state.hpP2 = Math.max(0, state.hpP2 - finalDamage);
  }
}

function checkWishThresholds(
  state: GameState, priv: PrivateState, player: PlayerNum,
  cards?: Map<string, CardMaster>,
): void {
  const hp = player === 1 ? state.hpP1 : state.hpP2;
  const wishZone = player === 1 ? priv.wishZoneP1 : priv.wishZoneP2;
  const hand = player === 1 ? priv.handP1 : priv.handP2;
  const oppPlayer: PlayerNum = player === 1 ? 2 : 1;

  for (const slot of wishZone) {
    if (slot.triggered) continue;
    // H1: threshold is a percentage of maxHp (works for HP=100 and HP=30 tutorial)
    const thresholdHp = Math.ceil(state.maxHp * slot.threshold / 100);
    if (hp <= thresholdHp) {
      slot.triggered = true;

      // Resolve wish trigger effect (§5.5) — card goes to graveyard, not hand
      const card = cards?.get(slot.cardId);
      if (card?.wishTrigger && card.wishTrigger !== "-") {
        resolveWishTrigger(state, priv, player, oppPlayer, card.wishTrigger);
      }
    }
  }
}

function resolveWishTrigger(
  state: GameState, priv: PrivateState,
  player: PlayerNum, oppPlayer: PlayerNum,
  trigger: string,
): void {
  switch (trigger) {
    case "WT_DRAW":
      drawCard(state, priv, player);
      break;

    case "WT_BOUNCE": {
      // Bounce weakest opponent unit to hand
      const oppField = oppPlayer === 1 ? state.fieldP1 : state.fieldP2;
      const oppHand = oppPlayer === 1 ? priv.handP1 : priv.handP2;
      if (oppField.length > 0) {
        oppField.sort((a, b) => a.power - b.power);
        const bounced = oppField.shift()!;
        oppHand.push(bounced.cardId);
        if (oppPlayer === 1) state.handCountP1 = oppHand.length;
        else state.handCountP2 = oppHand.length;
      }
      break;
    }

    case "WT_POWER_PLUS": {
      // Buff strongest ally unit +1000
      const allyField = player === 1 ? state.fieldP1 : state.fieldP2;
      if (allyField.length > 0) {
        allyField.sort((a, b) => b.power - a.power);
        allyField[0].power += 1000;
      }
      break;
    }

    case "WT_BLOCKER": {
      // Grant Blocker to first non-Blocker ally unit
      const field = player === 1 ? state.fieldP1 : state.fieldP2;
      const target = field.find(u => !u.keywords.includes("Blocker"));
      if (target) {
        target.keywords.push("Blocker");
      }
      break;
    }
  }
}

function applySpellEffect(
  state: GameState, priv: PrivateState,
  card: CardMaster, player: PlayerNum,
  cards?: Map<string, CardMaster>,
): void {
  const oppPlayer: PlayerNum = player === 1 ? 2 : 1;
  const allyField = player === 1 ? state.fieldP1 : state.fieldP2;
  const oppField = player === 1 ? state.fieldP2 : state.fieldP1;

  let bonusDamage = 0;
  if (state.sharedAlgo) {
    bonusDamage = getAlgoBonus(state.sharedAlgo, "spell_hp_damage", player, cards, card);
  }

  switch (card.effectKey) {
    case "SPELL_PUSH_SMALL":
    case "SPELL_PUSH_MEDIUM":
      applyWishDamage(state, oppPlayer, (card.baseGaugeDelta ?? 0) + bonusDamage);
      break;

    case "SPELL_POWER_PLUS":
      if (card.powerDelta && allyField.length > 0) {
        // Default: buff strongest ally. Use restTargets > 1 for "all_ally" scope.
        if ((card.restTargets ?? 1) >= allyField.length) {
          for (const unit of allyField) unit.power += card.powerDelta;
        } else {
          const sorted = [...allyField].sort((a, b) => b.power - a.power);
          for (let i = 0; i < Math.min(card.restTargets ?? 1, sorted.length); i++) {
            sorted[i].power += card.powerDelta;
          }
        }
      }
      break;

    case "SPELL_WISHDMG_PLUS": {
      if (card.wishDamageDelta && allyField.length > 0) {
        // Buff strongest unit
        const sorted = [...allyField].sort((a, b) => b.power - a.power);
        sorted[0].wishDamage += card.wishDamageDelta;
      }
      break;
    }

    case "SPELL_REST": {
      const targets = card.restTargets ?? 1;
      const candidates = oppField.filter(u => !u.exhausted);
      candidates.sort((a, b) => b.power - a.power);
      for (let i = 0; i < Math.min(targets, candidates.length); i++) {
        candidates[i].exhausted = true;
      }
      break;
    }

    case "SPELL_REMOVE_DAMAGED": {
      const threshold = card.removeCondition ?? card.battlePower ?? 3000;
      for (let i = oppField.length - 1; i >= 0; i--) {
        if (oppField[i].power <= threshold) {
          const destroyed = oppField.splice(i, 1)[0];
          if (cards) resolveOnDeathEffect(state, priv, oppPlayer, destroyed, cards);
        }
      }
      break;
    }

    case "SPELL_HP_DAMAGE_CURRENT": {
      // H6: Use card's hpDamagePercent field, fallback to 10%
      const hp = player === 1 ? state.hpP1 : state.hpP2;
      const pct = (card.hpDamagePercent ?? 10) / 100;
      const damage = Math.floor(hp * pct) + bonusDamage;
      applyWishDamage(state, oppPlayer, damage);
      break;
    }

    case "SPELL_SLOT_LOCK": {
      // Reduce opponent's max field size (remove weakest unit if at capacity)
      if (oppField.length >= GameConstants.MAX_FIELD_SIZE) {
        oppField.sort((a, b) => a.power - b.power);
        const destroyed = oppField.shift()!;
        if (cards) resolveOnDeathEffect(state, priv, oppPlayer, destroyed, cards);
      }
      break;
    }

    case "SPELL_DRAW": {
      const drawCount = card.restTargets ?? 1; // reuse field for draw count
      for (let i = 0; i < drawCount; i++) drawCard(state, priv, player);
      break;
    }

    case "SPELL_BOUNCE": {
      if (oppField.length > 0) {
        oppField.sort((a, b) => a.power - b.power);
        const bounced = oppField.shift()!;
        const oppHand = player === 1 ? priv.handP2 : priv.handP1;
        oppHand.push(bounced.cardId);
        if (player === 1) state.handCountP2++;
        else state.handCountP1++;
      }
      break;
    }

    case "SPELL_DESTROY": {
      if (oppField.length > 0) {
        const threshold = card.removeCondition ?? card.battlePower ?? 5000;
        for (let i = oppField.length - 1; i >= 0; i--) {
          if (oppField[i].power <= threshold) {
            const destroyed = oppField.splice(i, 1)[0];
            if (cards) resolveOnDeathEffect(state, priv, oppPlayer, destroyed, cards);
            break;
          }
        }
      }
      break;
    }

    case "SPELL_DESTROY_ALL": {
      const destroyedAll = oppField.splice(0, oppField.length);
      if (cards) {
        for (const destroyed of destroyedAll) {
          resolveOnDeathEffect(state, priv, oppPlayer, destroyed, cards);
        }
      }
      break;
    }

    case "SPELL_SEARCH_ASPECT": {
      // Search deck for card of same aspect and add to hand
      const deck = player === 1 ? priv.deckP1 : priv.deckP2;
      const hand = player === 1 ? priv.handP1 : priv.handP2;
      if (cards && deck.length > 0) {
        const searchCount = card.searchCount ?? 1;
        let found = 0;
        for (let i = 0; i < deck.length && found < searchCount && hand.length < GameConstants.MAX_HAND_SIZE; i++) {
          const c = cards.get(deck[i]);
          if (c && c.aspect === (card.searchAspect ?? card.aspect)) {
            hand.push(deck.splice(i, 1)[0]);
            found++;
            i--;
          }
        }
        // H7: Shuffle deck after search to maintain randomness
        shuffleArray(deck);
        if (player === 1) state.handCountP1 = hand.length;
        else state.handCountP2 = hand.length;
      }
      break;
    }

    case "SPELL_SEARCH_TYPE": {
      // Search deck for card of specified type and add to hand
      const deck2 = player === 1 ? priv.deckP1 : priv.deckP2;
      const hand2 = player === 1 ? priv.handP1 : priv.handP2;
      if (cards && card.searchType && deck2.length > 0) {
        const searchCount2 = card.searchCount ?? 1;
        let found2 = 0;
        for (let i = 0; i < deck2.length && found2 < searchCount2 && hand2.length < GameConstants.MAX_HAND_SIZE; i++) {
          const c = cards.get(deck2[i]);
          if (c && c.type === card.searchType) {
            hand2.push(deck2.splice(i, 1)[0]);
            found2++;
            i--;
          }
        }
        // H7: Shuffle deck after search
        shuffleArray(deck2);
        if (player === 1) state.handCountP1 = hand2.length;
        else state.handCountP2 = hand2.length;
      }
      break;
    }

    case "SPELL_EMOTION_DESTROY": {
      // Destroy opponent unit with matching emotionTag
      if (card.emotionTag && cards) {
        for (let i = oppField.length - 1; i >= 0; i--) {
          const unitCard = cards.get(oppField[i].cardId);
          if (unitCard && unitCard.emotionTag === card.emotionTag) {
            const destroyed = oppField.splice(i, 1)[0];
            resolveOnDeathEffect(state, priv, oppPlayer, destroyed, cards);
            break;
          }
        }
      }
      break;
    }

    case "SPELL_HEAL": {
      const healAmt = card.baseGaugeDelta ?? 5;
      if (player === 1) {
        state.hpP1 = Math.min(state.hpP1 + healAmt, state.maxHp);
      } else {
        state.hpP2 = Math.min(state.hpP2 + healAmt, state.maxHp);
      }
      break;
    }

    case "SPELL_HP_DAMAGE_FIXED": {
      const pctFixed = card.hpDamagePercent ?? card.baseGaugeDelta ?? 10;
      const damage = Math.floor((state.maxHp * pctFixed) / 100) + bonusDamage;
      applyWishDamage(state, oppPlayer, damage);
      break;
    }
  }
}

/** Normalize effectKey to handle both ON_PLAY_* and SUMMON_ON_PLAY_* naming (C6) */
function normalizeEffectKey(key: string): string {
  if (key.startsWith("SUMMON_ON_PLAY_")) return key.slice(7); // SUMMON_ON_PLAY_X → ON_PLAY_X
  if (key.startsWith("SUMMON_ON_DEATH_")) return key.slice(7); // SUMMON_ON_DEATH_X → ON_DEATH_X
  return key;
}

function applyOnPlayEffects(
  state: GameState, priv: PrivateState,
  card: CardMaster, player: PlayerNum,
  cards?: Map<string, CardMaster>,
): void {
  if (!card.effectKey) return;

  const key = normalizeEffectKey(card.effectKey);
  const oppPlayer: PlayerNum = player === 1 ? 2 : 1;

  switch (key) {
    case "ON_PLAY_DRAW":
      drawCard(state, priv, player);
      break;
    case "ON_PLAY_HP_DAMAGE": {
      const dmg = card.baseGaugeDelta ?? 2;
      applyWishDamage(state, oppPlayer, dmg);
      break;
    }
    case "ON_PLAY_BUFF_ALLY": {
      const field = player === 1 ? state.fieldP1 : state.fieldP2;
      // Exclude the unit that was just played (compare by cardId, last element in field)
      const allies = field.filter(u => u.cardId !== card.id);
      if (allies.length > 0 && card.powerDelta) {
        allies[0].power += card.powerDelta;
      }
      break;
    }
    case "ON_PLAY_DESTROY": {
      const oppField = player === 1 ? state.fieldP2 : state.fieldP1;
      const threshold = card.removeCondition ?? card.battlePower ?? 3000;
      for (let i = oppField.length - 1; i >= 0; i--) {
        if (oppField[i].power <= threshold) {
          const destroyed = oppField.splice(i, 1)[0];
          if (cards) resolveOnDeathEffect(state, priv, oppPlayer, destroyed, cards);
          break;
        }
      }
      break;
    }
    case "ON_PLAY_BOUNCE": {
      const oppField = player === 1 ? state.fieldP2 : state.fieldP1;
      const oppHand = player === 1 ? priv.handP2 : priv.handP1;
      if (oppField.length > 0) {
        oppField.sort((a, b) => a.power - b.power);
        const bounced = oppField.shift()!;
        oppHand.push(bounced.cardId);
        if (player === 1) state.handCountP2++;
        else state.handCountP1++;
      }
      break;
    }
    case "ON_PLAY_SLOT_LOCK": {
      // Lock opponent's field slot — remove weakest if at capacity (C7)
      const oppField = player === 1 ? state.fieldP2 : state.fieldP1;
      if (oppField.length >= GameConstants.MAX_FIELD_SIZE) {
        oppField.sort((a, b) => a.power - b.power);
        const destroyed = oppField.shift()!;
        if (cards) resolveOnDeathEffect(state, priv, oppPlayer, destroyed, cards);
      }
      break;
    }
    case "ON_PLAY_EMOTION_BUFF": {
      const field = player === 1 ? state.fieldP1 : state.fieldP2;
      if (card.emotionTag) {
        for (const unit of field) {
          const unitCard = cards?.get(unit.cardId);
          if (unitCard && unitCard.emotionTag === card.emotionTag) {
            unit.power += card.powerDelta ?? 1000;
          }
        }
      }
      break;
    }
    case "ON_PLAY_EMOTION_DRAW": {
      const field = player === 1 ? state.fieldP1 : state.fieldP2;
      if (card.emotionTag) {
        const hasMatch = field.some(u => {
          const unitCard = cards?.get(u.cardId);
          return unitCard && unitCard.emotionTag === card.emotionTag;
        });
        if (hasMatch) {
          drawCard(state, priv, player);
        }
      }
      break;
    }
  }
}

// ====================================================================
// On-Death Effects (C1 — SUMMON_ON_DEATH_DRAW, SUMMON_ON_DEATH_HP_DAMAGE)
// ====================================================================

function resolveOnDeathEffect(
  state: GameState, priv: PrivateState,
  ownerPlayer: PlayerNum, destroyedUnit: FieldUnit,
  cards: Map<string, CardMaster>,
): void {
  const card = cards.get(destroyedUnit.cardId);
  if (!card) return;

  const effectKey = card.onDeathEffect ?? "";
  if (!effectKey) return;

  const key = normalizeEffectKey(effectKey);
  const oppPlayer: PlayerNum = ownerPlayer === 1 ? 2 : 1;

  switch (key) {
    case "ON_DEATH_DRAW":
      drawCard(state, priv, ownerPlayer);
      break;
    case "ON_DEATH_HP_DAMAGE": {
      const dmg = card.baseGaugeDelta ?? 3;
      applyWishDamage(state, oppPlayer, dmg);
      break;
    }
  }
}

function matchesAlgoCondition(condition: string, triggerCard?: CardMaster): boolean {
  if (!condition || condition === "") return true; // no condition = always applies
  if (!triggerCard) return true; // no context card to check against, allow

  if (condition.startsWith("aspect:")) {
    return triggerCard.aspect === condition.slice(7);
  }
  if (condition.startsWith("keyword:")) {
    return triggerCard.keywords?.includes(condition.slice(8)) ?? false;
  }
  if (condition.startsWith("cpCost<=")) {
    return triggerCard.cpCost <= parseInt(condition.slice(8), 10);
  }
  if (condition.startsWith("cpCost>=")) {
    return triggerCard.cpCost >= parseInt(condition.slice(8), 10);
  }
  if (condition.startsWith("type:")) {
    return triggerCard.type === condition.slice(5);
  }
  return true; // unknown condition format — allow
}

function resolveLeaderSkillEffect(
  state: GameState, priv: PrivateState, player: PlayerNum,
  effectKey: string, effectValue: number, targetCount: number | undefined,
  logs: LogEntry[], now: number,
): void {
  const field = player === 1 ? state.fieldP1 : state.fieldP2;
  const oppField = player === 1 ? state.fieldP2 : state.fieldP1;

  switch (effectKey) {
    case "LEADER_SKILL_BUFF":
    case "LEADER_SKILL_POWER_BUFF_GUARDBREAK":
      // Buff all ally units by effectValue power
      for (const unit of field) {
        unit.power += effectValue;
      }
      break;

    case "LEADER_SKILL_DRAW": {
      const count = targetCount ?? effectValue;
      for (let i = 0; i < count; i++) drawCard(state, priv, player);
      break;
    }

    case "LEADER_SKILL_RUSH_ALL":
      // Grant rush to all ally units (clear summon sickness)
      for (const unit of field) {
        unit.summonSick = false;
        if (!unit.keywords.includes("Rush")) unit.keywords.push("Rush");
      }
      break;

    case "LEADER_SKILL_ATTACK_SEAL": {
      // Exhaust N opponent units
      const count2 = targetCount ?? 1;
      const candidates = oppField.filter(u => !u.exhausted);
      candidates.sort((a, b) => b.power - a.power);
      for (let i = 0; i < Math.min(count2, candidates.length); i++) {
        candidates[i].exhausted = true;
      }
      break;
    }

    case "LEADER_SKILL_DAMAGE_HALVE": {
      // C5: Set flag to halve incoming wish damage until next turn
      const myLeader = player === 1 ? state.leaderP1 : state.leaderP2;
      myLeader.damageHalveActive = true;
      break;
    }

    case "LEADER_SKILL_HAND_DISCARD": {
      // Opponent discards random cards
      const oppHand = player === 1 ? priv.handP2 : priv.handP1;
      const discardCount = Math.min(targetCount ?? 1, oppHand.length);
      for (let i = 0; i < discardCount; i++) {
        const idx = Math.floor(Math.random() * oppHand.length);
        oppHand.splice(idx, 1);
      }
      if (player === 1) state.handCountP2 = oppHand.length;
      else state.handCountP1 = oppHand.length;
      break;
    }

    case "LEADER_SKILL_MASS_BUFF_HEAL": {
      // C4: Buff all ally units AND heal HP
      for (const unit of field) {
        unit.power += effectValue;
      }
      // Heal HP (restore effectValue as HP, capped at maxHp)
      const healAmount = effectValue;
      if (player === 1) {
        state.hpP1 = Math.min(state.hpP1 + healAmount, state.maxHp);
      } else {
        state.hpP2 = Math.min(state.hpP2 + healAmount, state.maxHp);
      }
      break;
    }

    case "LEADER_SKILL_DISMANTLE_HALVE_DRAW": {
      // C3: Destroy weakest ally unit, halve its power, draw cards
      if (field.length > 0) {
        field.sort((a, b) => a.power - b.power);
        field.shift();
        const drawCount2 = targetCount ?? 2;
        for (let i = 0; i < drawCount2; i++) drawCard(state, priv, player);
      }
      break;
    }

    case "LEADER_SKILL_GRAVE_TO_HAND": {
      // C3: Return cards from graveyard to hand (simplified: draw from deck)
      const drawCount3 = targetCount ?? 1;
      for (let i = 0; i < drawCount3; i++) drawCard(state, priv, player);
      break;
    }

    case "LEADER_SKILL_DISMANTLE_ALL_RUIN_DAMAGE": {
      // C3: Destroy all own units, deal wish damage per unit destroyed
      const destroyed = field.length;
      field.length = 0;
      const oppP: PlayerNum = player === 1 ? 2 : 1;
      applyWishDamage(state, oppP, destroyed * (effectValue || 3));
      break;
    }

    case "LEADER_SKILL_GRAVE_TO_FIELD": {
      // C3: Summon unit from graveyard to field (simplified: draw + summon from deck)
      const deck = player === 1 ? priv.deckP1 : priv.deckP2;
      if (deck.length > 0 && field.length < GameConstants.MAX_FIELD_SIZE) {
        const cardId = deck.shift()!;
        field.push({
          instanceId: nextInstanceId(),
          cardId,
          row: "Front",
          power: effectValue || 3000,
          wishDamage: 3,
          keywords: [],
          exhausted: false,
          summonSick: true,
        });
      }
      break;
    }

    case "LEADER_SKILL_SACRIFICE_DAMAGE": {
      // C3: Sacrifice own units to deal damage
      const unitCount = field.length;
      if (unitCount > 0) {
        field.length = 0;
        const oppP2: PlayerNum = player === 1 ? 2 : 1;
        applyWishDamage(state, oppP2, unitCount * (effectValue || 5));
      }
      break;
    }

    default:
      // Unknown effect — log but don't fail
      logs.push({ event: "unknown_leader_skill", data: { effectKey }, timestamp: now });
      break;
  }
}

function getAlgoBonus(
  algo: SharedAlgo, bonusKind: string, player: PlayerNum,
  cards?: Map<string, CardMaster>, triggerCard?: CardMaster,
): number {
  if (!cards) return 0;
  if (algo.faceDown) return 0; // face-down algorithms provide no bonus
  const card = cards.get(algo.cardId);
  if (!card) return 0;

  let total = 0;
  // Check globalRule (applies to all players)
  if (card.globalRule && card.globalRule.kind === bonusKind && matchesAlgoCondition(card.globalRule.condition, triggerCard)) {
    total += card.globalRule.value;
  }
  // Check ownerBonus (only for the algo owner)
  if (card.ownerBonus && card.ownerBonus.kind === bonusKind && algo.owner === player && matchesAlgoCondition(card.ownerBonus.condition, triggerCard)) {
    total += card.ownerBonus.value;
  }
  return total;
}

function checkVictoryConditions(state: GameState): { winner: "P1" | "P2" | "Draw"; reason: string } | null {
  if (state.hpP1 <= 0 && state.hpP2 <= 0) {
    return { winner: "Draw", reason: "simultaneous_ko" };
  }
  if (state.hpP1 <= 0) {
    // Check for nuri_victory (塗り勝利: HP reaches exactly 0 through wish damage)
    return { winner: "P2", reason: state.hpP2 > 50 ? "shachihoko_victory" : "nuri_victory" };
  }
  if (state.hpP2 <= 0) {
    return { winner: "P1", reason: state.hpP1 > 50 ? "shachihoko_victory" : "nuri_victory" };
  }
  return null;
}

// ====================================================================
// Match Initialization
// ====================================================================

export function initializeGameState(
  leaderP1: LeaderMaster,
  leaderP2: LeaderMaster,
  deckP1: string[],
  deckP2: string[],
  mode: MatchMode = "standard",
): { state: GameState; priv: PrivateState } {
  const mc = getMatchConstants(mode);

  // Shuffle decks
  const shuffledDeck1 = shuffleArray([...deckP1]);
  const shuffledDeck2 = shuffleArray([...deckP2]);

  // Draw initial hands
  const handP1 = shuffledDeck1.splice(0, mc.INITIAL_HAND);
  const handP2 = shuffledDeck2.splice(0, mc.INITIAL_HAND);

  // Wish zone (6 cards from remaining deck at thresholds)
  const wishZoneP1 = createWishZone(shuffledDeck1, mc.WISH_THRESHOLDS);
  const wishZoneP2 = createWishZone(shuffledDeck2, mc.WISH_THRESHOLDS);

  // Random first player
  const firstPlayer: PlayerNum = Math.random() < 0.5 ? 1 : 2;

  const state: GameState = {
    turnTotal: 0,
    activePlayer: firstPlayer,
    currentPhase: "Start",
    hpP1: mc.MAX_HP,
    hpP2: mc.MAX_HP,
    maxHp: mc.MAX_HP,
    cpP1: mc.START_CP,
    cpP2: mc.START_CP,
    maxCpP1: mc.START_CP,
    maxCpP2: mc.START_CP,
    fieldP1: [],
    fieldP2: [],
    sharedAlgo: null,
    leaderP1: createLeaderState(leaderP1),
    leaderP2: createLeaderState(leaderP2),
    handCountP1: handP1.length,
    handCountP2: handP2.length,
    isGameOver: false,
    algorithmPlayedThisTurn: false,
    lastSeqP1: 0,
    lastSeqP2: 0,
    matchMode: mode,
  };

  const priv: PrivateState = {
    deckP1: shuffledDeck1,
    deckP2: shuffledDeck2,
    handP1,
    handP2,
    wishZoneP1,
    wishZoneP2,
  };

  return { state, priv };
}

function createLeaderState(leader: LeaderMaster): LeaderState {
  return {
    aspect: leader.aspect,
    level: 1,
    evoGauge: 0,
    evoMax: GameConstants.EVO_GAUGE_LV2,
    power: leader.basePower,
    wishDamage: leader.baseWishDamage,
    exhausted: false,
    keywords: [],
    skillUsedLv2: false,
    skillUsedLv3: false,
    skillUsedThisTurnLv2: false,
    skillUsedThisTurnLv3: false,
    damageHalveActive: false,
  };
}

function createWishZone(deck: string[], thresholds: readonly number[] = GameConstants.WISH_THRESHOLDS): WishSlot[] {
  const slots: WishSlot[] = [];
  for (const threshold of thresholds) {
    if (deck.length > 0) {
      slots.push({ threshold, cardId: deck.shift()!, triggered: false });
    }
  }
  return slots;
}

function shuffleArray<T>(arr: T[]): T[] {
  // Use crypto for fair, unpredictable shuffling (IAP game)
  const crypto = require("crypto") as typeof import("crypto");
  const randomBytes = crypto.randomBytes(arr.length * 4);
  for (let i = arr.length - 1; i > 0; i--) {
    const j = randomBytes.readUInt32BE(i * 4) % (i + 1);
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}
