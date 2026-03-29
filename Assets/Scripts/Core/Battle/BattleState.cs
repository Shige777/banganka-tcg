using System;
using System.Collections.Generic;
using System.Linq;
using Banganka.Core.Data;
using Banganka.Core.Config;

namespace Banganka.Core.Battle
{
    public enum PlayerSide { Player1, Player2 }
    public enum UnitStatus { Ready, Exhausted }
    public enum TurnPhase { Start, Draw, Main, Combat, End }
    public enum MatchResult { None, Player1Win, Player2Win, Draw }
    public enum FieldRow { Front, Back }

    [Serializable]
    public class FieldUnit
    {
        public string instanceId;
        public CardData cardData;
        public int currentPower;
        public int currentWishDamage;
        public FieldRow row;
        public UnitStatus status;
        public bool summonSick;
        public List<string> currentKeywords;

        public FieldUnit(CardData data, string instId)
        {
            instanceId = instId;
            cardData = data;
            currentPower = data.battlePower;
            currentWishDamage = data.wishDamage;
            status = UnitStatus.Exhausted;
            summonSick = true;
            currentKeywords = new List<string>(data.keywords ?? Array.Empty<string>());
        }

        public bool CanAttack => status == UnitStatus.Ready && !summonSick;
        public bool CanBlock => status == UnitStatus.Ready && !summonSick && currentKeywords.Contains("Blocker");
    }

    /// <summary>
    /// 願力カードスロット (HP閾値に配置)
    /// </summary>
    [Serializable]
    public class WishCardSlot
    {
        public int threshold; // 85, 70, 55, 40, 25, 10
        public CardData card; // null if already triggered
        public bool triggered;

        public WishCardSlot(int threshold, CardData card)
        {
            this.threshold = threshold;
            this.card = card;
            triggered = false;
        }
    }

    [Serializable]
    public class LeaderState
    {
        public LeaderData baseData;
        public int level;
        public int evoGauge;
        public int currentPower;
        public int currentWishDamage;
        public string wishDamageType; // "fixed" or "current"
        public UnitStatus status;
        public List<string> keywords;

        // 願主スキル使用済みフラグ (1ゲーム1回制限)
        public bool[] skillUsedThisGame;
        // 願主スキル使用済みフラグ (1ターン1回制限)
        public bool skillUsedThisTurn;
        // 崔鋒Lv2「不退転の構え」: ダメージ半減フラグ (1ターン)
        public bool damageHalveActive;

        public LeaderState(LeaderData data)
        {
            baseData = data;
            level = 1;
            evoGauge = 0;
            currentPower = data.basePower;
            currentWishDamage = data.baseWishDamage;
            wishDamageType = data.wishDamageType ?? "fixed";
            status = UnitStatus.Ready;
            keywords = new List<string>();
            skillUsedThisGame = new bool[2]; // [0]=Lv2 skill, [1]=Lv3 skill
            skillUsedThisTurn = false;
        }

        public bool CanAttack => status == UnitStatus.Ready;
        public bool CanBlock => status == UnitStatus.Ready && keywords.Contains("Blocker");

        public bool CanUseSkill(int skillLevel)
        {
            if (baseData.leaderSkills == null) return false;
            int idx = skillLevel - 2;
            if (idx < 0 || idx >= baseData.leaderSkills.Length) return false;
            if (level < baseData.leaderSkills[idx].unlockLevel) return false;
            if (skillUsedThisGame[idx]) return false;
            if (skillUsedThisTurn) return false;
            return true;
        }

        public int EvoGaugeMax
        {
            get
            {
                if (level >= baseData.levelCap) return int.MaxValue;
                int idx = level - 1;
                if (idx < baseData.evoGaugeMaxByLevel.Length)
                    return baseData.evoGaugeMaxByLevel[idx];
                return int.MaxValue;
            }
        }
    }

    [Serializable]
    public class PlayerState
    {
        public PlayerSide side;
        public LeaderState leader;
        public List<FieldUnit> field = new();
        public List<CardData> hand = new();
        public List<CardData> deck = new();
        public List<CardData> graveyard = new();
        public int maxCP;
        public int currentCP;
        public int lockedSlots;

        // HP System (GAME_DESIGN.md §4.1)
        public int hp;
        public int maxHp;
        public bool isFinal; // HP=0 → Final (瀕死) status
        public List<WishCardSlot> wishZone = new();

        public int FieldCount => field.Count;
        public int FrontCount => field.Count(u => u.row == FieldRow.Front);
        public int BackCount => field.Count(u => u.row == FieldRow.Back);
        public bool HasFrontSpace => FrontCount < BalanceConfig.FieldFrontSize;
        public bool HasBackSpace => BackCount < BalanceConfig.FieldBackSize;
        public int AvailableSlots => BalanceConfig.FieldTotalSize - lockedSlots - field.Count;
    }

    [Serializable]
    public class SharedAlgorithm
    {
        public CardData cardData;
        public PlayerSide owner;
        public bool isFaceDown;
        public int setTurn; // turn number when placed face-down
    }

    [Serializable]
    public class BattleState
    {
        public int turnTotal;
        public PlayerSide activePlayer;
        public TurnPhase currentPhase;
        public PlayerState player1;
        public PlayerState player2;
        public SharedAlgorithm sharedAlgo;
        public bool isGameOver;
        public MatchResult result;
        public MatchMode matchMode = MatchMode.Standard;
        public List<BattleLogEntry> log = new();

        // ターンタイマー: 連続タイムアウト回数 (GAME_DESIGN.md §10.1a)
        public int p1ConsecutiveTimeouts;
        public int p2ConsecutiveTimeouts;

        // Sequence numbers for replay attack prevention (SECURITY_SPEC.md §3.2)
        public int lastSeqP1;
        public int lastSeqP2;

        int _instanceCounter;

        public string NextInstanceId() => $"inst_{_instanceCounter++}";

        public PlayerState GetPlayer(PlayerSide side) =>
            side == PlayerSide.Player1 ? player1 : player2;

        public PlayerState GetOpponent(PlayerSide side) =>
            side == PlayerSide.Player1 ? player2 : player1;
    }

    [Serializable]
    public class BattleLogEntry
    {
        public string eventType;
        public string detail;
        public int turnNumber;

        public BattleLogEntry(string type, string detail, int turn)
        {
            eventType = type;
            this.detail = detail;
            turnNumber = turn;
        }
    }
}
