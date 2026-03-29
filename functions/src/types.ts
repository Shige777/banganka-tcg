/**
 * Banganka — Server-side type definitions
 * BACKEND_DESIGN.md §3, NETWORK_SPEC.md §3
 */

// ====================================================================
// RTDB Match State
// ====================================================================

export interface MatchMeta {
  player1Uid: string;
  player2Uid: string;
  status: "mulligan" | "active" | "finished";
  createdAt: number;
  winner: "P1" | "P2" | "Draw" | null;
}

export interface FieldUnit {
  instanceId: string;
  cardId: string;
  row: "Front" | "Back";
  power: number;
  wishDamage: number;
  keywords: string[];
  exhausted: boolean;
  summonSick: boolean;
}

export interface LeaderState {
  aspect: string;
  level: number;
  evoGauge: number;
  evoMax: number;
  power: number;
  wishDamage: number;
  wishDamageType?: string;
  exhausted: boolean;
  keywords: string[];
  skillUsedLv2: boolean;
  skillUsedLv3: boolean;
  skillUsedThisTurnLv2: boolean;
  skillUsedThisTurnLv3: boolean;
  damageHalveActive: boolean;
}

export interface SharedAlgo {
  cardId: string;
  owner: 1 | 2;
  faceDown: boolean;
  setTurn: number;
}

export interface GameState {
  turnTotal: number;
  activePlayer: 1 | 2;
  currentPhase: "Start" | "Draw" | "Main" | "Combat" | "End";
  hpP1: number;
  hpP2: number;
  maxHp: number;
  cpP1: number;
  cpP2: number;
  maxCpP1: number;
  maxCpP2: number;
  fieldP1: FieldUnit[];
  fieldP2: FieldUnit[];
  sharedAlgo: SharedAlgo | null;
  leaderP1: LeaderState;
  leaderP2: LeaderState;
  handCountP1: number;
  handCountP2: number;
  isGameOver: boolean;
  algorithmPlayedThisTurn: boolean;
  /** Last processed sequence number per player (SECURITY_SPEC.md §3.2) */
  lastSeqP1?: number;
  lastSeqP2?: number;
  /** Match mode: "standard" or "quick" */
  matchMode?: "standard" | "quick";
}

export interface PrivateState {
  deckP1: string[];
  deckP2: string[];
  handP1: string[];
  handP2: string[];
  wishZoneP1: WishSlot[];
  wishZoneP2: WishSlot[];
}

export interface WishSlot {
  threshold: number;
  cardId: string;
  triggered: boolean;
}

export interface TimerState {
  turnStartedAt: number;
  turnTimeLimit: number;
  timeoutCountP1: number;
  timeoutCountP2: number;
}

// ====================================================================
// Commands (NETWORK_SPEC.md §4.1)
// ====================================================================

export type CommandType =
  | "Mulligan"
  | "PlayManifest"
  | "PlaySpell"
  | "PlayAlgorithm"
  | "PlayAmbush"
  | "FlipAlgorithm"
  | "DeclareAttack"
  | "DeclareBlock"
  | "UseLeaderSkill"
  | "EndTurn";

export interface GameCommand {
  type: CommandType;
  payload: Record<string, unknown>;
  playerUid: string;
  timestamp: number;
  /** Sequence number for replay attack prevention (SECURITY_SPEC.md §3.2) */
  seq?: number;
}

// ====================================================================
// Card Master Data
// ====================================================================

export interface CardMaster {
  id: string;
  cardName: string;
  type: "Manifest" | "Spell" | "Algorithm";
  aspect: string;
  rarity: string;
  cpCost: number;
  battlePower: number;
  wishDamage: number;
  keywords: string[];
  effectKey: string;
  // Spell fields
  baseGaugeDelta?: number;
  powerDelta?: number;
  wishDamageDelta?: number;
  restTargets?: number;
  searchAspect?: string;
  searchType?: "Manifest" | "Spell" | "Algorithm";
  searchCount?: number;
  // Algorithm fields
  globalRule?: AlgorithmRule;
  ownerBonus?: AlgorithmRule;
  // Emotion tag (情相シナジー)
  emotionTag?: string;
  // Ambush
  ambushType?: "defend" | "retaliate";
  // Wish trigger effect
  wishTrigger?: string;
  // On-death effect
  onDeathEffect?: string;
  // HP damage percent (for SPELL_HP_DAMAGE_CURRENT / FIXED)
  hpDamagePercent?: number;
  // Removal condition threshold
  removeCondition?: number;
  // Additional fields for client-server parity
  drawCount?: number;
  targetScope?: string;     // "single" | "all_ally" | "all_enemy" | "weakest" | "strongest"
  bounceCount?: number;
  healAmount?: number;
  description?: string;
  flavorText?: string;
}

export interface AlgorithmRule {
  kind: string;      // e.g. "spell_gauge_plus", "power_plus", "grant_rush", "wish_damage_plus"
  value: number;
  condition: string; // e.g. "aspect:Contest", "keyword:Blocker", "cpCost<=2", "" for no condition
}

export interface LeaderMaster {
  id: string;
  name: string;
  aspect: string;
  basePower: number;
  baseWishDamage: number;
  levelCap: number;
  leaderSkills: LeaderSkillDef[];
}

export interface LeaderSkillDef {
  name: string;
  level: number;
  effectKey: string;
  effectValue: number;
  targetCount?: number;
}

// ====================================================================
// Firestore Documents
// ====================================================================

export interface FirestoreRoom {
  hostUid: string;
  guestUid: string | null;
  hostDeckId: string;
  guestDeckId: string | null;
  status: "waiting" | "ready" | "started" | "expired";
  createdAt: number;
  matchId: string | null;
  eventId?: string;
  matchMode?: MatchMode;
}

export interface FirestoreMatch {
  player1Uid: string;
  player2Uid: string;
  winner: "P1" | "P2" | "Draw" | null;
  reason: string | null;
  turnCount: number;
  duration: number;
  startedAt: number;
  endedAt: number | null;
  player1DeckSnapshot: string[];
  player2DeckSnapshot: string[];
}

export interface UserStats {
  totalGames: number;
  wins: number;
  losses: number;
  draws: number;
  winStreak: number;
  maxWinStreak: number;
  rating: number;
}

// ====================================================================
// Error codes (NETWORK_SPEC.md §10)
// ====================================================================

export const ErrorCodes = {
  NOT_YOUR_TURN: "ERR_NOT_YOUR_TURN",
  INVALID_PHASE: "ERR_INVALID_PHASE",
  INSUFFICIENT_CP: "ERR_INSUFFICIENT_CP",
  FIELD_FULL: "ERR_FIELD_FULL",
  CARD_NOT_IN_HAND: "ERR_CARD_NOT_IN_HAND",
  EXHAUSTED: "ERR_EXHAUSTED",
  SUMMON_SICK: "ERR_SUMMON_SICK",
  INVALID_BLOCK: "ERR_INVALID_BLOCK",
  MATCH_NOT_FOUND: "ERR_MATCH_NOT_FOUND",
  ROOM_FULL: "ERR_ROOM_FULL",
  ROOM_NOT_FOUND: "ERR_ROOM_NOT_FOUND",
  TIMEOUT: "ERR_TIMEOUT",
  ATTACKER_NOT_FOUND: "ERR_ATTACKER_NOT_FOUND",
  ALGO_ALREADY_PLAYED: "ERR_ALGO_ALREADY_PLAYED",
  SKILL_ALREADY_USED: "ERR_SKILL_ALREADY_USED",
  SKILL_LEVEL_TOO_LOW: "ERR_SKILL_LEVEL_TOO_LOW",
} as const;

// ====================================================================
// Constants (GAME_DESIGN.md)
// ====================================================================

export const GameConstants = {
  MAX_HP: 100,
  MAX_CP: 10,
  DECK_SIZE: 34,
  INITIAL_HAND: 5,
  TURN_LIMIT: 24,
  TURN_TIMER_SECONDS: 60,
  DISCONNECT_GRACE_SECONDS: 60,
  MAX_FIELD_SIZE: 5,
  MAX_HAND_SIZE: 10,
  CONSECUTIVE_TIMEOUT_LIMIT: 3,
  WISH_THRESHOLDS: [85, 70, 55, 40, 25, 10] as readonly number[],
  EVO_GAUGE_LV2: 3,
  EVO_GAUGE_LV3: 4,
  // Rewards (MONETIZATION_DESIGN.md §2.2)
  GOLD_WIN: 50,
  GOLD_LOSE: 20,
  GOLD_WIN_BOT: 25,
  GOLD_LOSE_BOT: 5,
  // Rate limits (SECURITY_SPEC.md §5)
  ROOM_EXPIRY_MINUTES: 5,
} as const;

export type MatchMode = "standard" | "quick";

/** Mode-aware constants — Quick Match uses reduced HP/turns/timer */
export function getMatchConstants(mode: MatchMode = "standard") {
  if (mode === "quick") {
    return {
      ...GameConstants,
      MAX_HP: 60,
      TURN_LIMIT: 16,
      TURN_TIMER_SECONDS: 30,
      START_CP: 2,
      WISH_THRESHOLDS: [50, 40, 30, 20, 10, 5] as readonly number[],
    };
  }
  return { ...GameConstants, START_CP: 0 };
}
