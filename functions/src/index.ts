/**
 * Banganka Cloud Functions v2
 * Server-authoritative game logic (BACKEND_DESIGN.md, NETWORK_SPEC.md)
 */

import { onCall, HttpsError } from "firebase-functions/v2/https";
import { onValueWritten } from "firebase-functions/v2/database";
import { onSchedule } from "firebase-functions/v2/scheduler";
import * as admin from "firebase-admin";
import {
  GameState, PrivateState, TimerState, MatchMeta,
  FirestoreRoom, FirestoreMatch, UserStats,
  CardMaster, LeaderMaster, GameCommand, GameConstants,
  MatchMode, getMatchConstants,
} from "./types";
import { processCommand, initializeGameState, drawCardHelper } from "./gameLogic";
import {
  submitMatchResult as rankSubmitMatchResult,
  getRanking as rankGetRanking,
  getPlayerRank as rankGetPlayerRank,
  resetSeason as rankResetSeason,
} from "./rankSystem";

// Billing Guard — 予算超過時に自動停止
export { billingGuard } from "./billingGuard";

// IAP Receipt Verification (MONETIZATION_DESIGN.md §2.3)
export { verifyReceipt } from "./receiptVerifier";

admin.initializeApp();
const db = admin.firestore();
const rtdb = admin.database();

// ====================================================================
// Card Master Cache (loaded once at cold start)
// ====================================================================

let cardMasterCache: Map<string, CardMaster> | null = null;
let leaderMasterCache: Map<string, LeaderMaster> | null = null;
let cacheTimestamp = 0;
const CACHE_TTL_MS = 5 * 60 * 1000; // 5 minutes

function isCacheStale(): boolean {
  return Date.now() - cacheTimestamp > CACHE_TTL_MS;
}

async function getCardMaster(): Promise<Map<string, CardMaster>> {
  if (cardMasterCache && !isCacheStale()) return cardMasterCache;
  const snap = await db.collection("cardMaster").get();
  const map = new Map<string, CardMaster>();
  snap.forEach(doc => map.set(doc.id, doc.data() as CardMaster));
  cardMasterCache = map;
  cacheTimestamp = Date.now();
  return map;
}

async function getLeaderMaster(): Promise<Map<string, LeaderMaster>> {
  if (leaderMasterCache && !isCacheStale()) return leaderMasterCache;
  const snap = await db.collection("leaderMaster").get();
  const map = new Map<string, LeaderMaster>();
  snap.forEach(doc => map.set(doc.id, doc.data() as LeaderMaster));
  leaderMasterCache = map;
  cacheTimestamp = Date.now();
  return map;
}

// ====================================================================
// Room Management (BACKEND_DESIGN.md §4.1)
// ====================================================================

export const createRoom = onCall(async (request) => {
  const uid = request.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Must be signed in");

  const { deckId, mode } = request.data as { deckId: string; mode?: MatchMode };
  if (!deckId) throw new HttpsError("invalid-argument", "deckId required");
  const matchMode: MatchMode = mode === "quick" ? "quick" : "standard";

  // Validate deck ownership and legality
  await validateDeck(uid, deckId);

  const roomId = generateRoomId();
  const room: FirestoreRoom = {
    hostUid: uid,
    guestUid: null,
    hostDeckId: deckId,
    guestDeckId: null,
    status: "waiting",
    createdAt: Date.now(),
    matchId: null,
    matchMode,
  };

  await db.collection("rooms").doc(roomId).set(room);
  console.log(`[createRoom] Room ${roomId} created by ${uid}`);

  return { roomId };
});

export const joinRoom = onCall(async (request) => {
  const uid = request.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Must be signed in");

  const { roomId, deckId } = request.data as { roomId: string; deckId: string };
  if (!roomId) throw new HttpsError("invalid-argument", "roomId required");
  if (!deckId) throw new HttpsError("invalid-argument", "deckId required");

  // Validate deck ownership and legality
  await validateDeck(uid, deckId);

  const roomRef = db.collection("rooms").doc(roomId);

  await db.runTransaction(async (tx) => {
    const roomSnap = await tx.get(roomRef);
    if (!roomSnap.exists) throw new HttpsError("not-found", "Room not found");

    const room = roomSnap.data() as FirestoreRoom;
    if (room.status !== "waiting") throw new HttpsError("failed-precondition", "Room is not waiting");
    if (room.guestUid) throw new HttpsError("failed-precondition", "Room is full");
    if (room.hostUid === uid) throw new HttpsError("failed-precondition", "Cannot join own room");

    tx.update(roomRef, {
      guestUid: uid,
      guestDeckId: deckId,
      status: "ready",
    });
  });

  console.log(`[joinRoom] ${uid} joined room ${roomId}`);
  return { success: true };
});

export const startMatch = onCall(async (request) => {
  const uid = request.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Must be signed in");

  const { roomId } = request.data as { roomId: string };
  if (!roomId || typeof roomId !== "string") {
    throw new HttpsError("invalid-argument", "roomId required");
  }
  const roomRef = db.collection("rooms").doc(roomId);

  // Use transaction to prevent race condition on room status
  const { room, hostDeck, guestDeck, hostLeaderId, guestLeaderId } = await db.runTransaction(async (tx) => {
    const roomSnap = await tx.get(roomRef);
    if (!roomSnap.exists) throw new HttpsError("not-found", "Room not found");

    const roomData = roomSnap.data() as FirestoreRoom;
    if (roomData.hostUid !== uid) throw new HttpsError("permission-denied", "Only host can start");
    if (roomData.status !== "ready") throw new HttpsError("failed-precondition", "Room not ready");
    if (!roomData.guestUid || !roomData.guestDeckId) throw new HttpsError("failed-precondition", "Guest not ready");

    // Load decks within transaction to ensure consistency
    const [hostDeckSnap, guestDeckSnap] = await Promise.all([
      tx.get(db.doc(`users/${roomData.hostUid}/decks/${roomData.hostDeckId}`)),
      tx.get(db.doc(`users/${roomData.guestUid}/decks/${roomData.guestDeckId}`)),
    ]);

    const hDeck = hostDeckSnap.data()?.cardIds as string[] ?? [];
    const gDeck = guestDeckSnap.data()?.cardIds as string[] ?? [];

    // Mark room as started atomically
    tx.update(roomRef, { status: "started" });

    return {
      room: roomData,
      hostDeck: hDeck,
      guestDeck: gDeck,
      hostLeaderId: hostDeckSnap.data()?.leaderId as string,
      guestLeaderId: guestDeckSnap.data()?.leaderId as string,
    };
  });

  if (hostDeck.length !== GameConstants.DECK_SIZE) {
    throw new HttpsError("failed-precondition", `Host deck must be ${GameConstants.DECK_SIZE} cards`);
  }
  if (guestDeck.length !== GameConstants.DECK_SIZE) {
    throw new HttpsError("failed-precondition", `Guest deck must be ${GameConstants.DECK_SIZE} cards`);
  }

  // Validate all cards exist in cardMaster and respect 3-copy limit
  const cards = await getCardMaster();
  validateDeckCards(hostDeck, cards, "Host");
  validateDeckCards(guestDeck, cards, "Guest");

  // Validate event-specific restrictions if applicable
  if (room.eventId) {
    await validateDeckForEvent(hostDeck, room.eventId, cards);
    await validateDeckForEvent(guestDeck, room.eventId, cards);
  }

  // Load leader data
  const leaders = await getLeaderMaster();
  const leaderP1 = leaders.get(hostLeaderId);
  const leaderP2 = leaders.get(guestLeaderId);
  if (!leaderP1 || !leaderP2) throw new HttpsError("not-found", "Leader not found");

  // Initialize game (mode-aware)
  const roomMode: MatchMode = (room as any).matchMode ?? "standard";
  const mc = getMatchConstants(roomMode);
  const { state, priv } = initializeGameState(leaderP1, leaderP2, hostDeck, guestDeck, roomMode);
  const matchId = `match_${Date.now()}_${roomId}`;

  const meta: MatchMeta = {
    player1Uid: room.hostUid,
    player2Uid: room.guestUid!,
    status: "active",
    createdAt: Date.now(),
    winner: null,
  };

  const timers: TimerState = {
    turnStartedAt: Date.now(),
    turnTimeLimit: mc.TURN_TIMER_SECONDS,
    timeoutCountP1: 0,
    timeoutCountP2: 0,
  };

  // Write RTDB + Firestore match atomically (Firestore first, then RTDB)
  const firestoreMatch: FirestoreMatch = {
    player1Uid: room.hostUid,
    player2Uid: room.guestUid!,
    winner: null,
    reason: null,
    turnCount: 0,
    duration: 0,
    startedAt: Date.now(),
    endedAt: null,
    player1DeckSnapshot: hostDeck,
    player2DeckSnapshot: guestDeck,
  };
  await db.collection("matches").doc(matchId).set(firestoreMatch);

  // M1: Write to RTDB — if this fails, clean up Firestore record
  const matchRef = rtdb.ref(`matches/${matchId}`);
  try {
    await matchRef.set({
      meta,
      state,
      private: priv,
      timers,
      commands: {},
      log: [],
    });
  } catch (err) {
    // Rollback Firestore match record
    await db.collection("matches").doc(matchId).delete().catch(() => {});
    throw new HttpsError("internal", "Failed to initialize match in RTDB");
  }

  // Update room with matchId
  await roomRef.update({ matchId });

  console.log(`[startMatch] Match ${matchId} started: ${room.hostUid} vs ${room.guestUid}`);
  return { matchId };
});

// ====================================================================
// Battle Command Processing (NETWORK_SPEC.md §4.2)
// ====================================================================

export const processGameCommand = onValueWritten(
  "/matches/{matchId}/commands/{pushId}",
  async (event) => {
    const matchId = event.params.matchId;
    const command = event.data.after.val() as GameCommand | null;
    if (!command) return;

    // Validate command structure
    if (!command.type || typeof command.type !== "string" ||
        !command.playerUid || typeof command.playerUid !== "string") {
      console.warn(`[processGameCommand] ${matchId}: Invalid command structure`);
      return;
    }

    const matchRef = rtdb.ref(`matches/${matchId}`);

    // Load current state
    const [metaSnap, stateSnap, privSnap] = await Promise.all([
      matchRef.child("meta").get(),
      matchRef.child("state").get(),
      matchRef.child("private").get(),
    ]);

    const meta = metaSnap.val() as MatchMeta | null;
    const state = stateSnap.val() as GameState | null;
    const priv = privSnap.val() as PrivateState | null;

    if (!meta || !state || !priv) {
      console.error(`[processGameCommand] Match ${matchId}: state not found`);
      return;
    }

    // RTDB may convert arrays to objects with numeric keys — normalize
    state.fieldP1 = normalizeArray(state.fieldP1);
    state.fieldP2 = normalizeArray(state.fieldP2);
    // Normalize nested arrays within FieldUnits (keywords)
    for (const unit of state.fieldP1) unit.keywords = normalizeArray(unit.keywords);
    for (const unit of state.fieldP2) unit.keywords = normalizeArray(unit.keywords);
    // Normalize leader keywords
    if (state.leaderP1) state.leaderP1.keywords = normalizeArray(state.leaderP1.keywords);
    if (state.leaderP2) state.leaderP2.keywords = normalizeArray(state.leaderP2.keywords);
    priv.deckP1 = normalizeArray(priv.deckP1);
    priv.deckP2 = normalizeArray(priv.deckP2);
    priv.handP1 = normalizeArray(priv.handP1);
    priv.handP2 = normalizeArray(priv.handP2);
    priv.wishZoneP1 = normalizeArray(priv.wishZoneP1);
    priv.wishZoneP2 = normalizeArray(priv.wishZoneP2);

    if (meta.status === "finished") return;

    // Determine player number
    let playerNum: 1 | 2;
    if (command.playerUid === meta.player1Uid) playerNum = 1;
    else if (command.playerUid === meta.player2Uid) playerNum = 2;
    else {
      console.warn(`[processGameCommand] Unknown player: ${command.playerUid}`);
      return;
    }

    // Load card master data
    const cards = await getCardMaster();
    const leaders = await getLeaderMaster();

    // Process command
    const result = processCommand(state, priv, command, cards, leaders, playerNum);

    if (!result.success) {
      // Write error back to command node
      await matchRef.child(`commands/${event.params.pushId}/error`).set(result.error);
      console.log(`[processGameCommand] ${matchId}: Command rejected: ${result.error}`);
      return;
    }

    // Write updated state
    const updates: Record<string, unknown> = {};

    if (result.stateUpdated) {
      updates["state"] = state;
      updates["private"] = priv;
      updates["timers/turnStartedAt"] = Date.now();
    }

    if (result.matchEnded) {
      updates["meta/status"] = "finished";
      updates["meta/winner"] = result.winner;

      // Idempotency: use Firestore transaction to atomically check + update + grant rewards
      const isBot = meta.player2Uid.startsWith("bot_");
      const matchDocRef = db.collection("matches").doc(matchId);
      await db.runTransaction(async (tx) => {
        const existingMatch = await tx.get(matchDocRef);
        const alreadyEnded = existingMatch.exists && existingMatch.data()?.endedAt != null;

        tx.update(matchDocRef, {
          winner: result.winner,
          reason: result.reason,
          turnCount: state.turnTotal,
          endedAt: Date.now(),
          duration: Math.floor((Date.now() - meta.createdAt) / 1000),
        });

        // Award gold only if not already granted (checked inside transaction)
        if (!alreadyEnded) {
          // Note: awardMatchRewards uses its own transaction, so we flag here
          return { shouldAward: true };
        }
        return { shouldAward: false };
      }).then(async (txResult) => {
        if (txResult && (txResult as { shouldAward: boolean }).shouldAward) {
          try {
            await awardMatchRewards(meta, result.winner!, isBot);
          } catch (rewardErr) {
            console.error(`[processGameCommand] Failed to award rewards for match ${matchId}:`, rewardErr);
            // Match is already marked as finished — log for manual intervention
          }
        }
      });
    }

    // Append log entries as part of the update batch
    if (result.logEntries.length > 0) {
      for (const entry of result.logEntries) {
        const pushId = matchRef.child("log").push().key;
        if (pushId) updates[`log/${pushId}`] = entry;
      }
    }

    await matchRef.update(updates);
    console.log(`[processGameCommand] ${matchId}: ${command.type} processed`);
  }
);

// ====================================================================
// Turn Timeout Check (NETWORK_SPEC.md §6)
// ====================================================================

export const checkTurnTimeout = onSchedule("every 1 minutes", async () => {
  const matchesRef = rtdb.ref("matches");
  // M2: Limit query to prevent performance issues with many active matches
  const snap = await matchesRef.orderByChild("meta/status").equalTo("active").limitToFirst(200).get();

  if (!snap.exists()) return;

  const now = Date.now();
  const promises: Promise<void>[] = [];

  snap.forEach(matchSnap => {
    const matchId = matchSnap.key!;
    const timers = matchSnap.child("timers").val() as TimerState | null;
    const state = matchSnap.child("state").val() as GameState | null;
    const meta = matchSnap.child("meta").val() as MatchMeta | null;

    if (!timers || !state || !meta || state.isGameOver) return;

    const elapsed = (now - timers.turnStartedAt) / 1000;
    if (elapsed >= timers.turnTimeLimit) {
      promises.push(handleTimeout(matchId, state, meta, timers));
    }
  });

  await Promise.all(promises);
});

async function handleTimeout(
  matchId: string, state: GameState, meta: MatchMeta, timers: TimerState,
): Promise<void> {
  const matchRef = rtdb.ref(`matches/${matchId}`);
  const activePlayer = state.activePlayer;

  // Increment timeout counter
  if (activePlayer === 1) timers.timeoutCountP1++;
  else timers.timeoutCountP2++;

  const timeoutCount = activePlayer === 1 ? timers.timeoutCountP1 : timers.timeoutCountP2;

  if (timeoutCount >= GameConstants.CONSECUTIVE_TIMEOUT_LIMIT) {
    // Auto-lose after 3 consecutive timeouts
    const winner = activePlayer === 1 ? "P2" : "P1";
    await matchRef.update({
      "meta/status": "finished",
      "meta/winner": winner,
      "state/isGameOver": true,
      timers,
    });
    // Idempotency: check if match already finalized before granting rewards
    const existingMatch = await db.collection("matches").doc(matchId).get();
    const alreadyEnded = existingMatch.exists && existingMatch.data()?.endedAt != null;

    await db.collection("matches").doc(matchId).update({
      winner,
      reason: "timeout",
      turnCount: state.turnTotal,
      endedAt: Date.now(),
    });
    if (!alreadyEnded) {
      await awardMatchRewards(meta, winner, false);
    }
    console.log(`[checkTurnTimeout] ${matchId}: ${winner} wins by timeout (${timeoutCount} consecutive)`);
  } else {
    // Auto end turn — replicate EndTurn logic
    state.activePlayer = state.activePlayer === 1 ? 2 : 1;
    state.turnTotal++;
    state.currentPhase = "Main";
    state.algorithmPlayedThisTurn = false;
    timers.turnStartedAt = Date.now();

    const nextPlayer = state.activePlayer;

    // Increase max CP
    if (nextPlayer === 1) {
      state.maxCpP1 = Math.min(state.maxCpP1 + 1, GameConstants.MAX_CP);
      state.cpP1 = state.maxCpP1;
    } else {
      state.maxCpP2 = Math.min(state.maxCpP2 + 1, GameConstants.MAX_CP);
      state.cpP2 = state.maxCpP2;
    }

    // Normalize RTDB arrays in state
    state.fieldP1 = normalizeArray(state.fieldP1);
    state.fieldP2 = normalizeArray(state.fieldP2);
    for (const unit of state.fieldP1) unit.keywords = normalizeArray(unit.keywords);
    for (const unit of state.fieldP2) unit.keywords = normalizeArray(unit.keywords);
    if (state.leaderP1) state.leaderP1.keywords = normalizeArray(state.leaderP1.keywords);
    if (state.leaderP2) state.leaderP2.keywords = normalizeArray(state.leaderP2.keywords);

    // Draw card for next player
    const privSnap = await matchRef.child("private").get();
    const priv = privSnap.val() as PrivateState | null;
    if (priv) {
      priv.deckP1 = normalizeArray(priv.deckP1);
      priv.deckP2 = normalizeArray(priv.deckP2);
      priv.handP1 = normalizeArray(priv.handP1);
      priv.handP2 = normalizeArray(priv.handP2);
      priv.wishZoneP1 = normalizeArray(priv.wishZoneP1);
      priv.wishZoneP2 = normalizeArray(priv.wishZoneP2);
      drawCardHelper(state, priv, nextPlayer);
    }

    // Ready all units
    const field = nextPlayer === 1 ? state.fieldP1 : state.fieldP2;
    if (Array.isArray(field)) {
      for (const unit of field) {
        unit.exhausted = false;
        unit.summonSick = false;
      }
    }

    // Ready leader
    const leader = nextPlayer === 1 ? state.leaderP1 : state.leaderP2;
    if (leader) {
      leader.exhausted = false;
      leader.skillUsedThisTurnLv2 = false;
      leader.skillUsedThisTurnLv3 = false;
      leader.damageHalveActive = false; // C4: Reset damage halve
    }

    // Auto-flip algorithm
    if (state.sharedAlgo && state.sharedAlgo.faceDown && state.turnTotal > state.sharedAlgo.setTurn) {
      state.sharedAlgo.faceDown = false;
    }

    // Turn limit check (mode-aware)
    const mcTimeout = getMatchConstants((state.matchMode as MatchMode) ?? "standard");
    if (state.turnTotal >= mcTimeout.TURN_LIMIT) {
      const hp1 = state.hpP1;
      const hp2 = state.hpP2;
      const winner = hp1 > hp2 ? "P1" : hp2 > hp1 ? "P2" : "Draw";
      state.isGameOver = true;
      const updates: Record<string, unknown> = {
        state,
        timers,
        "meta/status": "finished",
        "meta/winner": winner,
      };
      if (priv) updates["private"] = priv;
      await matchRef.update(updates);
      // Idempotency: check before granting rewards
      const existingMatch2 = await db.collection("matches").doc(matchId).get();
      const alreadyEnded2 = existingMatch2.exists && existingMatch2.data()?.endedAt != null;

      await db.collection("matches").doc(matchId).update({
        winner,
        reason: "turn_limit",
        turnCount: state.turnTotal,
        endedAt: Date.now(),
      });
      if (!alreadyEnded2) {
        await awardMatchRewards(meta, winner as "P1" | "P2" | "Draw", false);
      }
      console.log(`[checkTurnTimeout] ${matchId}: ${winner} wins by turn limit`);
      return;
    }

    const updates: Record<string, unknown> = { state, timers };
    if (priv) updates["private"] = priv;
    await matchRef.update(updates);
    console.log(`[checkTurnTimeout] ${matchId}: Auto end turn (timeout #${timeoutCount})`);
  }
}

// ====================================================================
// Bot Match (AI_BOT_SPEC.md)
// ====================================================================

export const createBotMatch = onCall(async (request) => {
  const uid = request.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Must be signed in");

  const { difficulty, deckId, mode } = request.data as { difficulty: string; deckId: string; mode?: MatchMode };
  if (!deckId) throw new HttpsError("invalid-argument", "deckId required");
  const matchMode: MatchMode = mode === "quick" ? "quick" : "standard";
  const validDifficulties = ["easy", "normal", "hard"];
  if (!difficulty || !validDifficulties.includes(difficulty)) {
    throw new HttpsError("invalid-argument", `difficulty must be one of: ${validDifficulties.join(", ")}`);
  }

  // Load player deck
  const deckSnap = await db.doc(`users/${uid}/decks/${deckId}`).get();
  if (!deckSnap.exists) throw new HttpsError("not-found", "Deck not found");
  const playerDeck = deckSnap.data()?.cardIds as string[] ?? [];
  const playerLeaderId = deckSnap.data()?.leaderId as string;

  if (playerDeck.length !== GameConstants.DECK_SIZE) {
    throw new HttpsError("failed-precondition", `Deck must be ${GameConstants.DECK_SIZE} cards`);
  }

  // Select bot deck (random preset different from player's aspect)
  const leaders = await getLeaderMaster();
  const playerLeader = leaders.get(playerLeaderId);
  if (!playerLeader) throw new HttpsError("not-found", "Player leader not found");

  // Pick bot leader (different aspect)
  let botLeader: LeaderMaster | undefined;
  for (const [, leader] of leaders) {
    if (leader.aspect !== playerLeader.aspect) {
      botLeader = leader;
      break;
    }
  }
  if (!botLeader) botLeader = leaders.values().next().value;

  // Bot uses a starter deck (card IDs from cardMaster)
  const cards = await getCardMaster();
  const botDeck = buildBotDeck(cards, botLeader!.aspect);

  const mcBot = getMatchConstants(matchMode);
  const { state, priv } = initializeGameState(playerLeader, botLeader!, playerDeck, botDeck, matchMode);
  const matchId = `bot_${difficulty ?? "normal"}_${Date.now()}`;

  const meta: MatchMeta = {
    player1Uid: uid,
    player2Uid: `bot_${difficulty ?? "normal"}`,
    status: "active",
    createdAt: Date.now(),
    winner: null,
  };

  const timers: TimerState = {
    turnStartedAt: Date.now(),
    turnTimeLimit: mcBot.TURN_TIMER_SECONDS,
    timeoutCountP1: 0,
    timeoutCountP2: 0,
  };

  // H4: Write Firestore match record for bot matches too
  const firestoreMatch: FirestoreMatch = {
    player1Uid: uid,
    player2Uid: `bot_${difficulty ?? "normal"}`,
    winner: null,
    reason: null,
    turnCount: 0,
    duration: 0,
    startedAt: Date.now(),
    endedAt: null,
    player1DeckSnapshot: playerDeck,
    player2DeckSnapshot: botDeck,
  };
  await db.collection("matches").doc(matchId).set(firestoreMatch);

  await rtdb.ref(`matches/${matchId}`).set({
    meta, state, private: priv, timers, commands: {}, log: [],
  });

  console.log(`[createBotMatch] Bot match ${matchId} created for ${uid}`);
  return { matchId };
});

function buildBotDeck(cards: Map<string, CardMaster>, aspect: string): string[] {
  const deck: string[] = [];
  const aspectCards = Array.from(cards.values()).filter(c => c.aspect === aspect);
  const genericCards = Array.from(cards.values()).filter(c => c.aspect === "generic" || c.aspect === "");

  // Fill with aspect cards (up to 3 copies each)
  for (const card of aspectCards) {
    const copies = Math.min(3, GameConstants.DECK_SIZE - deck.length);
    for (let i = 0; i < copies; i++) deck.push(card.id);
    if (deck.length >= GameConstants.DECK_SIZE) break;
  }

  // Fill remaining with generic
  for (const card of genericCards) {
    if (deck.length >= GameConstants.DECK_SIZE) break;
    const copies = Math.min(3, GameConstants.DECK_SIZE - deck.length);
    for (let i = 0; i < copies; i++) deck.push(card.id);
  }

  // Pad with random cards if not enough (with safety limit to prevent infinite loop)
  const allCards = Array.from(cards.values());
  let padAttempts = 0;
  const maxPadAttempts = allCards.length * 3 + 100;
  while (deck.length < GameConstants.DECK_SIZE && allCards.length > 0 && padAttempts < maxPadAttempts) {
    padAttempts++;
    const card = allCards[Math.floor(Math.random() * allCards.length)];
    const count = deck.filter(id => id === card.id).length;
    if (count < 3) deck.push(card.id);
  }

  return deck.slice(0, GameConstants.DECK_SIZE);
}

// ====================================================================
// Match End Processing
// ====================================================================

export const onMatchEnd = onCall(async (request) => {
  const uid = request.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Must be signed in");

  const { matchId } = request.data as { matchId: string };

  // Verify match exists and user is participant
  const matchSnap = await db.collection("matches").doc(matchId).get();
  if (!matchSnap.exists) throw new HttpsError("not-found", "Match not found");

  const match = matchSnap.data() as FirestoreMatch;
  if (match.player1Uid !== uid && match.player2Uid !== uid) {
    throw new HttpsError("permission-denied", "Not a participant");
  }

  // Already finalized — no double processing
  if (match.endedAt) {
    return { success: true, winner: match.winner };
  }

  // Read authoritative winner from RTDB (set by processGameCommand)
  const metaSnap = await rtdb.ref(`matches/${matchId}/meta`).get();
  const rtdbMeta = metaSnap.val() as MatchMeta | null;

  if (!rtdbMeta || rtdbMeta.status !== "finished" || !rtdbMeta.winner) {
    throw new HttpsError("failed-precondition", "Match has not ended on server");
  }

  const winner = rtdbMeta.winner;

  // Only update Firestore record — rewards already granted by processGameCommand
  await db.collection("matches").doc(matchId).update({
    winner,
    reason: "server_confirmed",
    turnCount: match.turnCount,
    endedAt: Date.now(),
    duration: Math.floor((Date.now() - match.startedAt) / 1000),
  });

  return { success: true, winner };
});

async function awardMatchRewards(
  meta: MatchMeta, winner: "P1" | "P2" | "Draw", isBot: boolean,
): Promise<void> {
  const goldWin = isBot ? GameConstants.GOLD_WIN_BOT : GameConstants.GOLD_WIN;
  const goldLose = isBot ? GameConstants.GOLD_LOSE_BOT : GameConstants.GOLD_LOSE;

  const updates: Promise<void>[] = [];

  // Player 1
  if (!meta.player1Uid.startsWith("bot_")) {
    const p1Gold = winner === "P1" ? goldWin : winner === "Draw" ? Math.floor(goldWin / 2) : goldLose;
    updates.push(updatePlayerStats(meta.player1Uid, winner === "P1" ? "win" : winner === "Draw" ? "draw" : "loss", p1Gold));
  }

  // Player 2
  if (!meta.player2Uid.startsWith("bot_")) {
    const p2Gold = winner === "P2" ? goldWin : winner === "Draw" ? Math.floor(goldWin / 2) : goldLose;
    updates.push(updatePlayerStats(meta.player2Uid, winner === "P2" ? "win" : winner === "Draw" ? "draw" : "loss", p2Gold));
  }

  await Promise.all(updates);
}

async function updatePlayerStats(uid: string, result: "win" | "loss" | "draw", gold: number): Promise<void> {
  const statsRef = db.doc(`users/${uid}/stats/battle`);
  const currencyRef = db.doc(`users/${uid}`);

  await db.runTransaction(async (tx) => {
    const statsSnap = await tx.get(statsRef);
    const stats: UserStats = statsSnap.exists ? statsSnap.data() as UserStats : {
      totalGames: 0, wins: 0, losses: 0, draws: 0,
      winStreak: 0, maxWinStreak: 0, rating: 1000,
    };

    stats.totalGames++;
    if (result === "win") {
      stats.wins++;
      stats.winStreak++;
      stats.maxWinStreak = Math.max(stats.maxWinStreak, stats.winStreak);
      stats.rating += 25;
    } else if (result === "loss") {
      stats.losses++;
      stats.winStreak = 0;
      stats.rating = Math.max(0, stats.rating - 15);
    } else {
      stats.draws++;
      stats.winStreak = 0;
    }

    tx.set(statsRef, stats, { merge: true });
    tx.set(currencyRef, {
      "currency.gold": admin.firestore.FieldValue.increment(gold),
    }, { merge: true });
  });
}

// ====================================================================
// Economy (MONETIZATION_DESIGN.md)
// ====================================================================

export const purchaseItem = onCall(async (request) => {
  const uid = request.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Must be signed in");

  const { itemId, quantity } = request.data as { itemId: string; quantity?: number };
  if (!itemId) throw new HttpsError("invalid-argument", "itemId required");
  const qty = quantity ?? 1;
  if (!Number.isInteger(qty) || qty < 1 || qty > 99) {
    throw new HttpsError("invalid-argument", "quantity must be between 1 and 99");
  }

  // Load shop item
  const itemSnap = await db.collection("shopItems").doc(itemId).get();
  if (!itemSnap.exists) throw new HttpsError("not-found", "Item not found");
  const item = itemSnap.data()!;

  if (!item.isActive) throw new HttpsError("failed-precondition", "Item not available");

  const priceAmount = item.priceAmount;
  if (typeof priceAmount !== "number" || priceAmount <= 0) {
    throw new HttpsError("internal", "Invalid item price configuration");
  }
  const totalCost = priceAmount * qty;
  const priceType = item.priceType as string;
  // Validate priceType is a known currency type to prevent field injection
  if (priceType !== "gold" && priceType !== "premium") {
    throw new HttpsError("internal", "Invalid item price type configuration");
  }

  // Deduct currency atomically
  const userRef = db.doc(`users/${uid}`);
  await db.runTransaction(async (tx) => {
    const userSnap = await tx.get(userRef);
    if (!userSnap.exists) throw new HttpsError("not-found", "User not found");

    const currency = userSnap.data()?.currency ?? {};
    const balance = currency[priceType] ?? 0;

    if (balance < totalCost) {
      throw new HttpsError("failed-precondition", `Insufficient ${priceType}: need ${totalCost}, have ${balance}`);
    }

    tx.update(userRef, {
      [`currency.${priceType}`]: admin.firestore.FieldValue.increment(-totalCost),
    });

    // Grant items — use increment to batch writes per card (avoid 500 write limit)
    const contents = item.contents as Array<{ cardId: string; count: number }> | undefined;
    if (contents) {
      for (const entry of contents) {
        const totalCount = entry.count * qty;
        const cardRef = db.doc(`users/${uid}/cards/${entry.cardId}`);
        tx.set(cardRef, {
          cardId: entry.cardId,
          count: admin.firestore.FieldValue.increment(totalCount),
          obtainedAt: admin.firestore.FieldValue.serverTimestamp(),
          isNew: true,
        }, { merge: true });
      }
    }
  });

  console.log(`[purchaseItem] ${uid} bought ${qty}x ${itemId}`);
  return { success: true };
});

// ====================================================================
// Cleanup (BACKEND_DESIGN.md §4.1)
// ====================================================================

export const cleanupRooms = onSchedule("every 1 minutes", async () => {
  const cutoff = Date.now() - GameConstants.ROOM_EXPIRY_MINUTES * 60 * 1000;

  // First: mark old waiting/ready rooms as expired
  for (const status of ["waiting", "ready"] as const) {
    const snap = await db.collection("rooms")
      .where("status", "==", status)
      .where("createdAt", "<", cutoff)
      .limit(100)
      .get();

    if (!snap.empty) {
      const expireBatch = db.batch();
      snap.forEach(doc => expireBatch.update(doc.ref, { status: "expired" }));
      await expireBatch.commit();
      console.log(`[cleanupRooms] Marked ${snap.size} ${status} rooms as expired`);
    }
  }

  // Then: delete old expired rooms (already marked or previously expired)
  const expiredSnap = await db.collection("rooms")
    .where("status", "==", "expired")
    .where("createdAt", "<", cutoff)
    .limit(100)
    .get();

  if (!expiredSnap.empty) {
    const deleteBatch = db.batch();
    expiredSnap.forEach(doc => deleteBatch.delete(doc.ref));
    await deleteBatch.commit();
    console.log(`[cleanupRooms] Deleted ${expiredSnap.size} expired rooms`);
  }
});

// ====================================================================
// Account Deletion
// ====================================================================

export const deleteAccount = onCall(async (request) => {
  const uid = request.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Must be signed in");

  // Delete subcollections (batch limited to 500 ops)
  const subcollections = ["cards", "decks", "stats", "devices"];
  for (const sub of subcollections) {
    let snap = await db.collection(`users/${uid}/${sub}`).limit(500).get();
    while (!snap.empty) {
      const batch = db.batch();
      snap.forEach(doc => batch.delete(doc.ref));
      await batch.commit();
      if (snap.size < 500) break;
      snap = await db.collection(`users/${uid}/${sub}`).limit(500).get();
    }
  }

  // Delete receipt records associated with this user
  let receiptSnap = await db.collection("receipts").where("uid", "==", uid).limit(500).get();
  while (!receiptSnap.empty) {
    const batch = db.batch();
    receiptSnap.forEach(doc => batch.delete(doc.ref));
    await batch.commit();
    if (receiptSnap.size < 500) break;
    receiptSnap = await db.collection("receipts").where("uid", "==", uid).limit(500).get();
  }

  // Delete user document
  await db.doc(`users/${uid}`).delete();

  // Delete auth account
  await admin.auth().deleteUser(uid);

  console.log(`[deleteAccount] Account ${uid} deleted`);
  return { success: true };
});

// ====================================================================
// Ranked Match System (GAME_DESIGN.md §14)
// ====================================================================

/**
 * submitMatchResult — ランク対戦結果を提出して ELO を更新
 *
 * リクエスト:
 *   matchId: string
 *   player1Uid: string
 *   player2Uid: string
 *   winnerId: "P1" | "P2" | "Draw"
 *
 * レスポンス:
 *   { player1: { eloChange, newRating, newRank }, player2: { ... } }
 */
export const submitMatchResult = onCall(async (request) => {
  const uid = request.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Must be signed in");

  const { matchId, player1Uid, player2Uid, winnerId } = request.data as {
    matchId: string;
    player1Uid: string;
    player2Uid: string;
    winnerId: "P1" | "P2" | "Draw";
  };

  if (!matchId || !player1Uid || !player2Uid || !winnerId) {
    throw new HttpsError("invalid-argument", "matchId, player1Uid, player2Uid, winnerId required");
  }

  if (!["P1", "P2", "Draw"].includes(winnerId)) {
    throw new HttpsError("invalid-argument", "winnerId must be P1, P2, or Draw");
  }

  // 呼び出し元がマッチの参加者であることを確認
  if (uid !== player1Uid && uid !== player2Uid) {
    throw new HttpsError("permission-denied", "Not a participant in this match");
  }

  try {
    const result = await rankSubmitMatchResult(matchId, player1Uid, player2Uid, winnerId);
    console.log(`[submitMatchResult] Success: ${matchId}`);
    return result;
  } catch (err) {
    console.error(`[submitMatchResult] Error:`, err);
    throw new HttpsError("internal", "Failed to submit match result");
  }
});

/**
 * getRanking — トップ100リーダーボードを取得
 *
 * レスポンス:
 *   { players: [{ uid, rating, rank, wins, gamesPlayed, displayName }, ...] }
 */
export const getRanking = onCall(async (request) => {
  if (!request.auth) throw new HttpsError("unauthenticated", "Must be signed in");

  const limit = (request.data?.limit as number | undefined) || 100;
  if (limit < 1 || limit > 1000) {
    throw new HttpsError("invalid-argument", "limit must be between 1 and 1000");
  }

  try {
    const players = await rankGetRanking(limit);
    console.log(`[getRanking] Returned ${players.length} players`);
    return { players };
  } catch (err) {
    console.error(`[getRanking] Error:`, err);
    throw new HttpsError("internal", "Failed to fetch ranking");
  }
});

/**
 * getPlayerRank — プレイヤーの現在ランク情報を取得
 *
 * リクエスト:
 *   uid?: string (省略時は自分)
 *
 * レスポンス:
 *   { rating, rank, stars, wins, losses, gamesPlayed, highestRating, ... }
 */
export const getPlayerRank = onCall(async (request) => {
  const uid = request.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Must be signed in");

  const targetUid = (request.data?.uid as string | undefined) || uid;

  try {
    const rankData = await rankGetPlayerRank(targetUid);
    if (!rankData) {
      throw new HttpsError("not-found", `No rank data for ${targetUid}`);
    }
    console.log(`[getPlayerRank] Fetched for ${targetUid}`);
    return rankData;
  } catch (err) {
    console.error(`[getPlayerRank] Error:`, err);
    throw new HttpsError("internal", "Failed to fetch player rank");
  }
});

/**
 * resetSeason — 月次ランクシーズンリセット (スケジュール実行, 毎月1日 00:00 UTC)
 *
 * 処理:
 *   1. 前シーズンのプレイヤーを列挙
 *   2. ランクを一段階降格
 *   3. シーズン報酬ゴールドを付与
 *   4. 新シーズンのランクドキュメントを初期化
 */
export const resetSeasonScheduled = onSchedule("0 0 1 * *", async (event) => {
  // 全プレイヤーのシーズンリセットを実行
  try {
    const result = await rankResetSeason();
    console.log(`[resetSeasonScheduled] Completed: processed ${result.processedCount} players`);
  } catch (err) {
    console.error(`[resetSeasonScheduled] Error:`, err);
    // スケジュール実行エラーはログするがエラーを投げない
  }
});

// ====================================================================
// Helpers
// ====================================================================

// ====================================================================
// Deck Validation (SECURITY_SPEC.md §2.1)
// ====================================================================

async function validateDeck(uid: string, deckId: string): Promise<void> {
  const deckSnap = await db.doc(`users/${uid}/decks/${deckId}`).get();
  if (!deckSnap.exists) {
    throw new HttpsError("not-found", "Deck not found");
  }
  const deckData = deckSnap.data()!;
  const cardIds = deckData.cardIds as string[] | undefined;
  if (!cardIds || cardIds.length !== GameConstants.DECK_SIZE) {
    throw new HttpsError("failed-precondition", `Deck must contain exactly ${GameConstants.DECK_SIZE} cards`);
  }
  if (!deckData.leaderId) {
    throw new HttpsError("failed-precondition", "Deck must have a leader");
  }
}

async function validateDeckForEvent(
  deckCardIds: string[], eventId: string, cards: Map<string, CardMaster>,
): Promise<void> {
  if (!eventId) return;

  const eventSnap = await db.collection("events").doc(eventId).get();
  if (!eventSnap.exists) return;

  const eventData = eventSnap.data()!;
  const rules = eventData.rules as { maxRarity?: string; specialRules?: string[] } | undefined;
  if (!rules) return;

  const rarityOrder = ["C", "R", "SR", "SSR"];
  if (rules.maxRarity) {
    const maxIdx = rarityOrder.indexOf(rules.maxRarity);
    for (const cardId of deckCardIds) {
      const card = cards.get(cardId);
      if (!card) continue;
      const cardIdx = rarityOrder.indexOf(card.rarity);
      if (cardIdx > maxIdx) {
        throw new HttpsError("failed-precondition",
          `Card ${cardId} rarity ${card.rarity} exceeds event limit ${rules.maxRarity}`);
      }
    }
  }

  if (rules.specialRules?.includes("no_algorithm")) {
    for (const cardId of deckCardIds) {
      const card = cards.get(cardId);
      if (card?.type === "Algorithm") {
        throw new HttpsError("failed-precondition",
          `Algorithm cards not allowed in this event`);
      }
    }
  }
}

function validateDeckCards(
  cardIds: string[], cards: Map<string, CardMaster>, label: string,
): void {
  const counts = new Map<string, number>();
  for (const id of cardIds) {
    if (!cards.has(id)) {
      throw new HttpsError("failed-precondition", `${label}: unknown card ${id}`);
    }
    counts.set(id, (counts.get(id) ?? 0) + 1);
  }
  for (const [id, count] of counts) {
    if (count > 3) {
      throw new HttpsError("failed-precondition", `${label}: card ${id} exceeds 3-copy limit (${count})`);
    }
  }
}

/** Firebase RTDB may convert arrays to objects with numeric keys. This normalizes them back. */
function normalizeArray<T>(val: T[] | Record<string, T> | null | undefined): T[] {
  if (Array.isArray(val)) return val;
  if (val && typeof val === "object") return Object.values(val);
  return [];
}

function generateRoomId(): string {
  const chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  // Use crypto.randomBytes for secure room IDs
  const bytes = require("crypto").randomBytes(8);
  let result = "";
  for (let i = 0; i < 8; i++) {
    result += chars.charAt(bytes[i] % chars.length);
  }
  return result;
}
