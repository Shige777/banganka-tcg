using System;
using Banganka.Core.Data;

namespace Banganka.Core.Game
{
    /// <summary>
    /// 新規プレイヤー向けメカニクス段階的解放。
    /// PlayerData.totalGames に基づいて解放状態を管理。
    /// GAME_DESIGN.md — 新規プレイヤー体験改善。
    /// </summary>
    public enum MechanicType
    {
        Summon,         // 顕現召喚 — always unlocked
        Attack,         // 攻撃 — always unlocked
        Block,          // ブロック — always unlocked
        EndTurn,        // ターンエンド — always unlocked
        Incantation,    // 詠術 — unlock at 1 game
        CpManagement,   // CP管理 — unlock at 1 game
        Algorithm,      // 界律 — unlock at 4 games
        LeaderEvo,      // 願主進化 — unlock at 4 games
        Ambush,         // 奇襲 — unlock at 6 games
        AspectSynergy,  // 情相シナジー — unlock at 6 games
    }

    public static class MechanicUnlockManager
    {
        // Unlock thresholds by total games played
        const int ThresholdSpell = 1;
        const int ThresholdAlgorithm = 4;
        const int ThresholdAdvanced = 6;
        const int ThresholdFull = 9;

        /// <summary>全メカニクスが解放済みか</summary>
        public static bool AllUnlocked => GetTotalGames() >= ThresholdFull;

        public static bool IsUnlocked(MechanicType mechanic)
        {
            int games = GetTotalGames();
            return mechanic switch
            {
                MechanicType.Summon => true,
                MechanicType.Attack => true,
                MechanicType.Block => true,
                MechanicType.EndTurn => true,
                MechanicType.Incantation => games >= ThresholdSpell,
                MechanicType.CpManagement => games >= ThresholdSpell,
                MechanicType.Algorithm => games >= ThresholdAlgorithm,
                MechanicType.LeaderEvo => games >= ThresholdAlgorithm,
                MechanicType.Ambush => games >= ThresholdAdvanced,
                MechanicType.AspectSynergy => games >= ThresholdAdvanced,
                _ => true,
            };
        }

        /// <summary>次のアンロックまでの残り戦数 (全解放済みなら0)</summary>
        public static int GamesUntilNextUnlock()
        {
            int games = GetTotalGames();
            if (games < ThresholdSpell) return ThresholdSpell - games;
            if (games < ThresholdAlgorithm) return ThresholdAlgorithm - games;
            if (games < ThresholdAdvanced) return ThresholdAdvanced - games;
            if (games < ThresholdFull) return ThresholdFull - games;
            return 0;
        }

        /// <summary>Nal(ナビキャラ)のアンロック台詞</summary>
        public static string GetNalMessage()
        {
            int games = GetTotalGames();
            if (games < ThresholdSpell)
                return null; // チュートリアル中
            if (games < ThresholdAlgorithm)
                return "詠術カードを使ってみよう！";
            if (games < ThresholdAdvanced)
                return "界律を置くとフィールドが変わるよ！";
            if (games < ThresholdFull)
                return "上級テクニックを伝授するよ！";
            return "君はもう一人前だ！";
        }

        /// <summary>CardType に対応するメカニクスがロックされているか</summary>
        public static bool IsCardTypeLocked(CardType type)
        {
            return type switch
            {
                CardType.Spell => !IsUnlocked(MechanicType.Incantation),
                CardType.Algorithm => !IsUnlocked(MechanicType.Algorithm),
                _ => false,
            };
        }

        static int GetTotalGames()
        {
            if (_overrideTotalGames.HasValue) return _overrideTotalGames.Value;
            return PlayerData.Instance?.totalGames ?? 0;
        }

        // For testing — allows injecting a custom game count
        static int? _overrideTotalGames;
        public static void SetTotalGamesOverride(int? games) => _overrideTotalGames = games;

        static MechanicUnlockManager()
        {
            // Reset static override
        }
    }
}
