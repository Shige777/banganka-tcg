/**
 * ランクマッチ ELO レーティングシステム
 * GAME_DESIGN.md §14 実装
 *
 * 26段階ランクティアと月次シーズンを管理。
 */

import * as admin from "firebase-admin";
import { HttpsError } from "firebase-functions/v2/https";

const db = admin.firestore();

// ====================================================================
// 型定義
// ====================================================================

export interface RankedMatchResult {
  matchId: string;
  player1Uid: string;
  player2Uid: string;
  winnerId: "P1" | "P2" | "Draw";
  matchedAt: number;
  finishedAt: number;
}

export interface RankData {
  rating: number;
  rank: string; // e.g., "bronze_5", "gold_3", "wish_master"
  stars: number;
  wins: number;
  losses: number;
  streak: number; // 連勝数 (負けるとリセット)
  seasonId: string;
  gamesPlayed: number;
  highestRating: number;
}

export interface PlayerRankSnapshot {
  uid: string;
  rating: number;
  rank: string;
  stars: number;
  wins: number;
  losses: number;
  streak: number;
  gamesPlayed: number;
  displayName: string;
}

// ====================================================================
// ランク定義 (§14.1)
// ====================================================================

const RANK_TIERS = [
  "bronze_5", "bronze_4", "bronze_3", "bronze_2", "bronze_1",
  "silver_5", "silver_4", "silver_3", "silver_2", "silver_1",
  "gold_5", "gold_4", "gold_3", "gold_2", "gold_1",
  "platinum_5", "platinum_4", "platinum_3", "platinum_2", "platinum_1",
  "diamond_5", "diamond_4", "diamond_3", "diamond_2", "diamond_1",
  "wish_master",
];

// ELO rating thresholds for each rank
const RANK_THRESHOLDS: Record<string, { minRating: number; starsToPromote: number }> = {
  "bronze_5": { minRating: 0, starsToPromote: 3 },
  "bronze_4": { minRating: 150, starsToPromote: 3 },
  "bronze_3": { minRating: 300, starsToPromote: 3 },
  "bronze_2": { minRating: 450, starsToPromote: 3 },
  "bronze_1": { minRating: 600, starsToPromote: 3 },
  "silver_5": { minRating: 750, starsToPromote: 3 },
  "silver_4": { minRating: 900, starsToPromote: 3 },
  "silver_3": { minRating: 1050, starsToPromote: 3 },
  "silver_2": { minRating: 1200, starsToPromote: 3 },
  "silver_1": { minRating: 1350, starsToPromote: 4 },
  "gold_5": { minRating: 1500, starsToPromote: 4 },
  "gold_4": { minRating: 1650, starsToPromote: 4 },
  "gold_3": { minRating: 1800, starsToPromote: 4 },
  "gold_2": { minRating: 1950, starsToPromote: 4 },
  "gold_1": { minRating: 2100, starsToPromote: 4 },
  "platinum_5": { minRating: 2250, starsToPromote: 4 },
  "platinum_4": { minRating: 2400, starsToPromote: 4 },
  "platinum_3": { minRating: 2550, starsToPromote: 4 },
  "platinum_2": { minRating: 2700, starsToPromote: 4 },
  "platinum_1": { minRating: 2850, starsToPromote: 5 },
  "diamond_5": { minRating: 3000, starsToPromote: 5 },
  "diamond_4": { minRating: 3150, starsToPromote: 5 },
  "diamond_3": { minRating: 3300, starsToPromote: 5 },
  "diamond_2": { minRating: 3450, starsToPromote: 5 },
  "diamond_1": { minRating: 3600, starsToPromote: 5 },
  "wish_master": { minRating: 3750, starsToPromote: 0 },
};

// ====================================================================
// 定数 (§14, GAME_DESIGN.md)
// ====================================================================

const INITIAL_RATING = 1200;
const ELO_K_FACTOR = 32; // Normal K-factor
const ELO_K_FACTOR_NEW = 32; // K-factor for new players (<30 games)
const ELO_K_FACTOR_HIGH = 16; // K-factor for 2000+ rating
const ELO_DIVISOR = 400;

const SEASON_RESET_SCHEDULE = "0 0 1 * *"; // 毎月1日 00:00 UTC → JST 09:00

// ====================================================================
// ヘルパー関数
// ====================================================================

/**
 * 現在のシーズンIDを取得 (YYYY-MM形式, JST基準)
 */
export function getCurrentSeasonId(): string {
  // JST (UTC+9) で月初を基準にする
  const now = new Date();
  const jstOffset = 9 * 60 * 60 * 1000;
  const jstNow = new Date(now.getTime() + jstOffset);

  const year = jstNow.getUTCFullYear();
  const month = String(jstNow.getUTCMonth() + 1).padStart(2, "0");
  return `${year}-${month}`;
}

/**
 * K-factorを決定 (ELO rating / games playedに応じて調整)
 */
function getKFactor(rating: number, gamesPlayed: number): number {
  if (gamesPlayed < 30) return ELO_K_FACTOR_NEW;
  if (rating >= 2000) return ELO_K_FACTOR_HIGH;
  return ELO_K_FACTOR;
}

/**
 * ELO期待勝率を計算
 */
function getExpectedWinRate(playerRating: number, opponentRating: number): number {
  return 1 / (1 + Math.pow(10, (opponentRating - playerRating) / ELO_DIVISOR));
}

/**
 * ELO rating変動を計算
 */
function calculateEloChange(playerRating: number, opponentRating: number, isWin: boolean, kFactor: number): number {
  const expected = getExpectedWinRate(playerRating, opponentRating);
  const actual = isWin ? 1 : 0;
  return Math.round(kFactor * (actual - expected));
}

/**
 * ratingからrank stringを判定
 */
function getRankFromRating(rating: number): string {
  // Wish Masterまたはそれ以上
  if (rating >= RANK_THRESHOLDS["wish_master"].minRating) {
    return "wish_master";
  }

  // 降順で探索 (最も高いマッチするランク)
  for (let i = RANK_TIERS.length - 2; i >= 0; i--) {
    const tier = RANK_TIERS[i];
    if (rating >= RANK_THRESHOLDS[tier].minRating) {
      return tier;
    }
  }

  return "bronze_5";
}

/**
 * 昇格・降格をチェックしてstarと rankを更新
 */
function updateRankAndStars(
  rating: number,
  currentRank: string,
  currentStars: number,
  isWin: boolean,
): { newRank: string; newStars: number } {
  let newRank = getRankFromRating(rating);
  let newStars = currentStars;

  // 勝利でスター増加
  if (isWin) {
    newStars++;
    const starsToPromote = RANK_THRESHOLDS[newRank].starsToPromote;

    // 昇格
    if (starsToPromote > 0 && newStars >= starsToPromote && newRank !== "wish_master") {
      const currentIndex = RANK_TIERS.indexOf(newRank);
      if (currentIndex < RANK_TIERS.length - 1) {
        newRank = RANK_TIERS[currentIndex + 1];
        newStars = 0;
      }
    }
  } else {
    // 敗北でスター減少 (bronze/silverは保護)
    if (!newRank.startsWith("bronze") && !newRank.startsWith("silver")) {
      newStars--;

      // 降格
      if (newStars < 0) {
        const currentIndex = RANK_TIERS.indexOf(newRank);
        if (currentIndex > 0) {
          newRank = RANK_TIERS[currentIndex - 1];
          newStars = RANK_THRESHOLDS[newRank].starsToPromote - 1;
        } else {
          newStars = 0;
        }
      }
    }
  }

  return { newRank, newStars };
}

// ====================================================================
// Public API: submitMatchResult
// ====================================================================

/**
 * 対戦結果を提出し、両プレイヤーのELO・スター・ランクを更新
 *
 * @param matchId - マッチID
 * @param player1Uid - プレイヤー1 UID
 * @param player2Uid - プレイヤー2 UID
 * @param winnerId - "P1" | "P2" | "Draw"
 * @returns { player1Delta, player2Delta, ... }
 */
export async function submitMatchResult(
  matchId: string,
  player1Uid: string,
  player2Uid: string,
  winnerId: "P1" | "P2" | "Draw"
): Promise<{
  player1: { eloChange: number; newRating: number; newRank: string };
  player2: { eloChange: number; newRating: number; newRank: string };
}> {
  // 両プレイヤーのランクデータを取得
  const p1Ref = db.collection("users").doc(player1Uid).collection("ranked").doc("current");
  const p2Ref = db.collection("users").doc(player2Uid).collection("ranked").doc("current");

  const [p1Snap, p2Snap] = await Promise.all([p1Ref.get(), p2Ref.get()]);

  const p1Data = p1Snap.data() as RankData | undefined;
  const p2Data = p2Snap.data() as RankData | undefined;

  // 初期化 (新規プレイヤー)
  const p1 = p1Data || {
    rating: INITIAL_RATING,
    rank: getRankFromRating(INITIAL_RATING),
    stars: 0,
    wins: 0,
    losses: 0,
    streak: 0,
    seasonId: getCurrentSeasonId(),
    gamesPlayed: 0,
    highestRating: INITIAL_RATING,
  };

  const p2 = p2Data || {
    rating: INITIAL_RATING,
    rank: getRankFromRating(INITIAL_RATING),
    stars: 0,
    wins: 0,
    losses: 0,
    streak: 0,
    seasonId: getCurrentSeasonId(),
    gamesPlayed: 0,
    highestRating: INITIAL_RATING,
  };

  // ELO変動計算
  const p1KFactor = getKFactor(p1.rating, p1.gamesPlayed);
  const p2KFactor = getKFactor(p2.rating, p2.gamesPlayed);

  const p1IsWin = winnerId === "P1";
  const p2IsWin = winnerId === "P2";
  const isDraw = winnerId === "Draw";

  const p1EloChange = isDraw
    ? 0
    : calculateEloChange(p1.rating, p2.rating, p1IsWin, p1KFactor);
  const p2EloChange = isDraw
    ? 0
    : calculateEloChange(p2.rating, p1.rating, p2IsWin, p2KFactor);

  // 新しいrating計算
  p1.rating = Math.max(0, p1.rating + p1EloChange);
  p2.rating = Math.max(0, p2.rating + p2EloChange);

  // ランク・スターを更新
  const p1Update = updateRankAndStars(p1.rating, p1.rank, p1.stars, p1IsWin);
  p1.rank = p1Update.newRank;
  p1.stars = p1Update.newStars;

  const p2Update = updateRankAndStars(p2.rating, p2.rank, p2.stars, p2IsWin);
  p2.rank = p2Update.newRank;
  p2.stars = p2Update.newStars;

  // Win/Loss/Streak を更新
  if (p1IsWin) {
    p1.wins++;
    p1.streak++;
  } else if (isDraw) {
    // Draw時は streak リセット
    p1.streak = 0;
  } else {
    p1.losses++;
    p1.streak = 0;
  }

  if (p2IsWin) {
    p2.wins++;
    p2.streak++;
  } else if (isDraw) {
    p2.streak = 0;
  } else {
    p2.losses++;
    p2.streak = 0;
  }

  p1.gamesPlayed++;
  p2.gamesPlayed++;

  // Highest Rating を更新
  p1.highestRating = Math.max(p1.highestRating, p1.rating);
  p2.highestRating = Math.max(p2.highestRating, p2.rating);

  // Firestore に保存
  await Promise.all([
    p1Ref.set(p1, { merge: true }),
    p2Ref.set(p2, { merge: true }),
  ]);

  // Season leaderboard も更新
  const seasonId = getCurrentSeasonId();
  const leaderRef = db.collection("seasons").doc(seasonId).collection("rankings");
  await Promise.all([
    leaderRef.doc(player1Uid).set({
      uid: player1Uid,
      rating: p1.rating,
      rank: p1.rank,
      wins: p1.wins,
      gamesPlayed: p1.gamesPlayed,
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true }),
    leaderRef.doc(player2Uid).set({
      uid: player2Uid,
      rating: p2.rating,
      rank: p2.rank,
      wins: p2.wins,
      gamesPlayed: p2.gamesPlayed,
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true }),
  ]);

  console.log(`[submitMatchResult] Match ${matchId}: ${winnerId}, P1 rating ${p1.rating} (${p1EloChange:+d}), P2 rating ${p2.rating} (${p2EloChange:+d})`);

  return {
    player1: {
      eloChange: p1EloChange,
      newRating: p1.rating,
      newRank: p1.rank,
    },
    player2: {
      eloChange: p2EloChange,
      newRating: p2.rating,
      newRank: p2.rank,
    },
  };
}

// ====================================================================
// Public API: getRanking (Top 100 Leaderboard)
// ====================================================================

export async function getRanking(limit: number = 100): Promise<PlayerRankSnapshot[]> {
  const seasonId = getCurrentSeasonId();
  const snap = await db
    .collection("seasons")
    .doc(seasonId)
    .collection("rankings")
    .orderBy("rating", "desc")
    .limit(limit)
    .get();

  const results: PlayerRankSnapshot[] = [];
  for (const doc of snap.docs) {
    const data = doc.data();

    // ユーザーの表示名を取得
    let displayName = "Unknown";
    try {
      const userSnap = await db.collection("users").doc(doc.id).get();
      const userData = userSnap.data();
      if (userData && "displayName" in userData) {
        displayName = userData.displayName;
      }
    } catch (err) {
      console.warn(`Failed to fetch displayName for ${doc.id}:`, err);
    }

    results.push({
      uid: doc.id,
      rating: data.rating || 0,
      rank: data.rank || "bronze_5",
      stars: data.stars || 0,
      wins: data.wins || 0,
      losses: 0,
      streak: 0,
      gamesPlayed: data.gamesPlayed || 0,
      displayName,
    });
  }

  return results;
}

// ====================================================================
// Public API: getPlayerRank
// ====================================================================

export async function getPlayerRank(uid: string): Promise<RankData | null> {
  const snap = await db
    .collection("users")
    .doc(uid)
    .collection("ranked")
    .doc("current")
    .get();

  return (snap.data() as RankData) || null;
}

// ====================================================================
// Public API: resetSeason (Scheduled Cloud Function)
// ====================================================================

/**
 * 月次シーズンリセット (毎月1日 00:00 UTC に実行)
 * MVP: 簡易版 — ランク一段階降格 + ゴールド報酬
 */
export async function resetSeason(): Promise<{ processedCount: number }> {
  const prevSeasonId = getPreviousSeasonId();
  const currentSeasonId = getCurrentSeasonId();

  console.log(`[resetSeason] Starting season reset: ${prevSeasonId} → ${currentSeasonId}`);

  // 前シーズン終了時点のプレイヤー全員を列挙
  const prevRankings = await db
    .collection("seasons")
    .doc(prevSeasonId)
    .collection("rankings")
    .get();

  let processedCount = 0;

  for (const rankDoc of prevRankings.docs) {
    const uid = rankDoc.id;
    const prevRankData = rankDoc.data();

    // 新シーズンの初期ランクを決定（一段階降格）
    let newRank = applySeasonDemotion(prevRankData.rank || "bronze_5");

    // シーズン報酬を計算 (§14.3)
    const reward = calculateSeasonReward(prevRankData.rank || "bronze_5");

    // 新シーズンのランクドキュメントを作成
    const newRankData: RankData = {
      rating: INITIAL_RATING, // リセット（ソフトリセット）
      rank: newRank,
      stars: 0,
      wins: 0,
      losses: 0,
      streak: 0,
      seasonId: currentSeasonId,
      gamesPlayed: 0,
      highestRating: INITIAL_RATING,
    };

    // Firestore に保存
    await db.collection("users").doc(uid).collection("ranked").doc("current").set(newRankData);

    // 新シーズンのランキングドキュメントも作成
    await db
      .collection("seasons")
      .doc(currentSeasonId)
      .collection("rankings")
      .doc(uid)
      .set({
        uid,
        rating: INITIAL_RATING,
        rank: newRank,
        wins: 0,
        gamesPlayed: 0,
        updatedAt: admin.firestore.FieldValue.serverTimestamp(),
      });

    // シーズン報酬をユーザーの currency に追加 (MONETIZATION_DESIGN.md)
    await db.collection("users").doc(uid).update({
      gold: admin.firestore.FieldValue.increment(reward),
    });

    processedCount++;

    if (processedCount % 100 === 0) {
      console.log(`[resetSeason] Processed ${processedCount} players...`);
    }
  }

  console.log(`[resetSeason] Completed: ${processedCount} players reset`);
  return { processedCount };
}

/**
 * 前シーズンのIDを取得 (YYYY-MM形式)
 */
function getPreviousSeasonId(): string {
  const now = new Date();
  const jstOffset = 9 * 60 * 60 * 1000;
  const jstNow = new Date(now.getTime() + jstOffset);

  let year = jstNow.getUTCFullYear();
  let month = jstNow.getUTCMonth(); // 0-11

  if (month === 0) {
    year--;
    month = 12;
  }

  const monthStr = String(month).padStart(2, "0");
  return `${year}-${monthStr}`;
}

/**
 * ランクを一段階降格 (§14.3)
 * 金3→銀5, 白金3→金5, 宝石3→白金5, 願主→宝石5
 */
function applySeasonDemotion(currentRank: string): string {
  const demotionMap: Record<string, string> = {
    "wish_master": "diamond_5",
    "diamond_1": "platinum_5",
    "diamond_2": "platinum_5",
    "diamond_3": "platinum_5",
    "diamond_4": "platinum_5",
    "diamond_5": "platinum_5",
    "platinum_1": "gold_5",
    "platinum_2": "gold_5",
    "platinum_3": "gold_5",
    "platinum_4": "gold_5",
    "platinum_5": "gold_5",
    "gold_1": "silver_5",
    "gold_2": "silver_5",
    "gold_3": "silver_5",
    "gold_4": "silver_5",
    "gold_5": "silver_5",
  };

  return demotionMap[currentRank] || currentRank; // Bronze/Silver は変動なし
}

/**
 * シーズン報酬額を計算 (§14.3)
 */
function calculateSeasonReward(rank: string): number {
  const rewardMap: Record<string, number> = {
    "wish_master": 5000,
    "diamond_1": 2000,
    "diamond_2": 2000,
    "diamond_3": 2000,
    "diamond_4": 2000,
    "diamond_5": 2000,
    "platinum_1": 1000,
    "platinum_2": 1000,
    "platinum_3": 1000,
    "platinum_4": 1000,
    "platinum_5": 1000,
    "gold_1": 500,
    "gold_2": 500,
    "gold_3": 500,
    "gold_4": 500,
    "gold_5": 500,
    "silver_1": 200,
    "silver_2": 200,
    "silver_3": 200,
    "silver_4": 200,
    "silver_5": 200,
    "bronze_1": 100,
    "bronze_2": 100,
    "bronze_3": 100,
    "bronze_4": 100,
    "bronze_5": 100,
  };

  return rewardMap[rank] || 0;
}
