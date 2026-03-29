using System;

namespace Banganka.Core.Data
{
    public enum CardType
    {
        Manifest,   // 顕現
        Spell,      // 詠術
        Algorithm   // 界律
    }

    [Serializable]
    public class CardData
    {
        public string id;
        public string cardName;
        public CardType type;
        public int cpCost;
        public Aspect aspect;
        public string effectKey;
        public string flavorText;
        public string rarity;          // "C", "R", "SR", "SSR"
        public bool hasFoil;
        public string wishTrigger;     // "-", "WT_DRAW", "WT_BOUNCE", "WT_POWER_PLUS", "WT_BLOCKER"
        public string emotionTag;      // "Thirst","Atonement","Grace","Longing","Resign","Pride"
        public string illustrationId;  // Resources path key (e.g. "aldric", "MAN_AKE_01")

        // 顕現 only
        public int battlePower;
        public int wishDamage;         // percentage value (3 = 固定3%, 5 = 現在5% etc.)
        public string wishDamageType;  // "fixed" or "current"
        public string[] keywords;

        // 詠術 / OnPlay params
        public int baseGaugeDelta;     // legacy (SPELL_PUSH_SMALL/MEDIUM)
        public int wishDamageDelta;    // SPELL_WISHDMG_PLUS
        public int powerDelta;         // SPELL_POWER_PLUS, SUMMON_ON_PLAY_BUFF_ALLY
        public int restTargets;        // SPELL_REST
        public string removeCondition; // SPELL_REMOVE_DAMAGED, SUMMON_ON_PLAY_DESTROY
        public string targetScope;     // "ally","enemy","enemy_manifest","all","all_ally"
        public int drawCount;          // SPELL_DRAW, SUMMON_ON_PLAY_DRAW
        public int hpDamagePercent;    // HP damage effects
        public string damageType;      // "fixed" or "current"
        public int bounceCount;        // SPELL_BOUNCE
        public string searchAspect;    // SPELL_SEARCH_ASPECT
        public string searchType;      // SPELL_SEARCH_TYPE
        public int searchCount;        // search count
        public int destroyCount;       // SPELL_DESTROY

        // 奇襲 (Ambush) params
        public string ambushType;      // "defend" or "retaliate"
        public int selfPowerDelta;     // SUMMON_AMBUSH_DEFEND_BUFF

        // 情相参照 params
        public string emotionMatch;    // "self" for same emotion as this card
        public int emotionThreshold;   // required count of matching emotion units

        // スロット封印 params
        public int lockCount;          // SPELL_SLOT_LOCK, SUMMON_ON_PLAY_SLOT_LOCK

        // 界律 only
        public AlgorithmRule globalRule;
        public AlgorithmRule ownerBonus;

        public bool HasKeyword(string kw)
        {
            if (keywords == null) return false;
            foreach (var k in keywords)
                if (k == kw) return true;
            return false;
        }
    }

    [Serializable]
    public class AlgorithmRule
    {
        public string kind;     // e.g. "spell_gauge_plus", "power_plus", "grant_rush", "wish_damage_plus"
        public int value;
        public string condition; // e.g. "aspect:Contest", "keyword:Blocker", "cpCost<=2"
    }
}
