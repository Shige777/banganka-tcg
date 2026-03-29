using System;
using System.Collections.Generic;
using System.Linq;

namespace Banganka.Core.Economy
{
    public enum MissionType { Daily, Weekly }
    public enum MissionGoal { WinBattles, PlayCards, DealDamage, UseLeaderSkill, PlaySpells, CompleteBattles }

    [Serializable]
    public class Mission
    {
        public string id;
        public string description;
        public MissionType type;
        public MissionGoal goal;
        public int targetCount;
        public int currentCount;
        public int goldReward;
        public int xpReward; // Battle Pass XP
        public bool claimed;

        public bool IsComplete => currentCount >= targetCount;
        public float Progress => targetCount > 0 ? (float)currentCount / targetCount : 0;
    }

    /// <summary>
    /// デイリー/ウィークリーミッション (MONETIZATION_DESIGN.md)
    /// </summary>
    public static class MissionSystem
    {
        static List<Mission> _activeMissions = new();

        public static IReadOnlyList<Mission> ActiveMissions => _activeMissions;
        public static event Action OnMissionUpdated;

        public static void GenerateDailyMissions()
        {
            _activeMissions.RemoveAll(m => m.type == MissionType.Daily);

            _activeMissions.Add(new Mission
            {
                id = "daily_win_1",
                description = "バトルに1回勝利する",
                type = MissionType.Daily,
                goal = MissionGoal.WinBattles,
                targetCount = 1,
                goldReward = 50,
                xpReward = 20,
            });

            _activeMissions.Add(new Mission
            {
                id = "daily_play_3",
                description = "カードを10枚プレイする",
                type = MissionType.Daily,
                goal = MissionGoal.PlayCards,
                targetCount = 10,
                goldReward = 30,
                xpReward = 15,
            });

            _activeMissions.Add(new Mission
            {
                id = "daily_battle_2",
                description = "バトルを2回完了する",
                type = MissionType.Daily,
                goal = MissionGoal.CompleteBattles,
                targetCount = 2,
                goldReward = 40,
                xpReward = 15,
            });

            OnMissionUpdated?.Invoke();
        }

        public static void GenerateWeeklyMissions()
        {
            _activeMissions.RemoveAll(m => m.type == MissionType.Weekly);

            _activeMissions.Add(new Mission
            {
                id = "weekly_win_5",
                description = "バトルに5回勝利する",
                type = MissionType.Weekly,
                goal = MissionGoal.WinBattles,
                targetCount = 5,
                goldReward = 200,
                xpReward = 50,
            });

            _activeMissions.Add(new Mission
            {
                id = "weekly_skill",
                description = "リーダースキルを3回使用する",
                type = MissionType.Weekly,
                goal = MissionGoal.UseLeaderSkill,
                targetCount = 3,
                goldReward = 150,
                xpReward = 40,
            });

            _activeMissions.Add(new Mission
            {
                id = "weekly_spell",
                description = "詠術を15枚プレイする",
                type = MissionType.Weekly,
                goal = MissionGoal.PlaySpells,
                targetCount = 15,
                goldReward = 150,
                xpReward = 40,
            });

            OnMissionUpdated?.Invoke();
        }

        public static void ReportProgress(MissionGoal goal, int amount = 1)
        {
            bool updated = false;
            foreach (var m in _activeMissions)
            {
                if (m.goal == goal && !m.IsComplete)
                {
                    m.currentCount = Math.Min(m.currentCount + amount, m.targetCount);
                    updated = true;
                }
            }
            if (updated) OnMissionUpdated?.Invoke();
        }

        public static bool ClaimReward(string missionId)
        {
            var mission = _activeMissions.FirstOrDefault(m => m.id == missionId);
            if (mission == null || !mission.IsComplete || mission.claimed) return false;

            mission.claimed = true;
            CurrencyManager.AddGold(mission.goldReward);
            BattlePassSystem.AddXp(mission.xpReward);

            OnMissionUpdated?.Invoke();
            return true;
        }

        public static int UnclaimedCount =>
            _activeMissions.Count(m => m.IsComplete && !m.claimed);
    }
}
