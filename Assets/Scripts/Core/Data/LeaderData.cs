using System;

namespace Banganka.Core.Data
{
    [Serializable]
    public class LeaderSkill
    {
        public int unlockLevel;
        public string name;
        public string effectKey;
        public string description;

        // Skill params (各スキル固有)
        public int powerDelta;
        public string grantKeyword;
        public int drawCount;
        public int dismantleCount;
        public float powerMultiplier;  // e.g. 0.5 for halve
        public float damageMultiplier; // e.g. 0.5 for halve damage
        public int duration;           // turns
        public int targetCount;
        public string targetScope;     // "ally","enemy","all"
        public string source;          // "graveyard","field"
        public string destination;     // "hand","field"
        public int healHP;
        public string damageFormula;   // "ruinCount*2", "power*0.10"
    }

    [Serializable]
    public class LeaderData
    {
        public string id;
        public string leaderName;
        public Aspect keyAspect;
        public int basePower;
        public int baseWishDamage;
        public string wishDamageType; // "fixed" or "current"
        public int levelCap;
        public int[] evoGaugeMaxByLevel;        // index 0 = Lv1->2, index 1 = Lv2->3
        public int levelUpPowerGain;
        public int levelUpWishDamageGain;       // legacy (uniform gain)
        public int[] wishDamageByLevel;         // exact wish damage at each level: [Lv1, Lv2, Lv3]
        public string[] grantedKeywordsByLevel; // index 0 = Lv2 keyword, index 1 = Lv3 keyword
        public LeaderSkill[] leaderSkills;      // [0] = Lv2 skill, [1] = Lv3 skill
    }
}
