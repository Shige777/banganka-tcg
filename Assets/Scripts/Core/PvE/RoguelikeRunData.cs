using System;
using System.Collections.Generic;
using Banganka.Core.Data;

namespace Banganka.Core.PvE
{
    /// <summary>
    /// PvEローグライクモードのラン状態データ。
    /// 1ラン = 8ステージ構成。各ステージでBot対戦後、報酬選択。
    /// Slay the Spire / Monster Train 型の構造。
    /// </summary>
    [Serializable]
    public class RoguelikeRunData
    {
        public string runId;
        public string leaderId;
        public int currentStage;      // 0-based, 0-7
        public int maxStage;          // 通常8
        public int hp;                // ランHP（全ステージ共通、回復なし）
        public int maxHp;
        public int gold;              // ラン内ゴールド（カード購入用）
        public List<string> deckCards = new(); // 現在のデッキ
        public List<string> artifacts = new(); // 取得済みアーティファクト
        public List<StageResult> completedStages = new();
        public bool isActive;
        public string startedAt;      // ISO 8601

        public bool IsComplete => currentStage >= maxStage;
        public bool IsAlive => hp > 0;
    }

    [Serializable]
    public class StageResult
    {
        public int stage;
        public bool won;
        public int hpLost;
        public string rewardChosen; // cardId or artifactId
    }

    /// <summary>ステージ定義</summary>
    [Serializable]
    public class RoguelikeStage
    {
        public int stageIndex;
        public string enemyName;
        public string enemyDeckProfile; // Bot AIのデッキプロファイル
        public Aspect enemyAspect;
        public DifficultyTier difficulty;
        public int bonusGold;           // 勝利報酬ゴールド
    }

    public enum DifficultyTier
    {
        Easy,     // ステージ1-2
        Normal,   // ステージ3-5
        Hard,     // ステージ6-7
        Boss      // ステージ8
    }

    /// <summary>ステージクリア後の報酬選択肢</summary>
    [Serializable]
    public class StageReward
    {
        public RewardType type;
        public string cardId;          // カード報酬の場合
        public string artifactId;      // アーティファクト報酬の場合
        public int goldAmount;         // ゴールド報酬の場合
        public int healAmount;         // HP回復の場合
        public string displayName;
        public string description;
    }

    public enum RewardType
    {
        Card,
        Artifact,
        Gold,
        Heal,
        RemoveCard
    }

    /// <summary>
    /// アーティファクト（ラン中のパッシブ効果）。
    /// 各ステージで1つ選択肢に出る可能性あり。
    /// </summary>
    [Serializable]
    public class ArtifactData
    {
        public string id;
        public string displayName;
        public string description;
        public string effectKey;  // "start_cp_plus_1", "draw_plus_1", "hp_plus_10", etc.
        public int effectValue;
        public string rarity;    // "C","R","SR"
    }
}
