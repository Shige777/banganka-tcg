using System;
using System.Collections.Generic;

namespace Banganka.Core.Replay
{
    /// <summary>
    /// リプレイデータ全体 (REPLAY_SPEC.md 1.2 準拠)
    /// コマンドログ再生方式: 1試合 5-25KB
    /// </summary>
    [Serializable]
    public class ReplayData
    {
        public string replayId;       // "rpl_20260401_abc123"
        public string matchId;
        public string version;        // e.g. "0.5.3"
        public string createdAt;      // ISO 8601
        public ReplayPlayerInfo[] players;
        public ReplayInitialState initialState;
        public List<ReplayCommand> commands;
        public ReplayResult result;
    }

    [Serializable]
    public class ReplayPlayerInfo
    {
        public string uid;
        public string name;
        public string wishMasterId;
        public string deckHash;
    }

    [Serializable]
    public class ReplayInitialState
    {
        /// <summary>先攻プレイヤー (1 or 2)</summary>
        public int firstPlayer;

        /// <summary>各プレイヤーのデッキ (カードID配列 x2)</summary>
        public string[][] decks;

        /// <summary>初期手札のデッキ内インデックス</summary>
        public int[][] initialHands;

        /// <summary>マリガンで戻したカードのインデックス</summary>
        public int[][] mulliganActions;

        /// <summary>HP スナップショット</summary>
        public ReplayHpSnapshot[] hpSnapshots;

        /// <summary>願力カード閾値状態</summary>
        public ReplayThresholdState[] thresholdCards;
    }

    [Serializable]
    public class ReplayHpSnapshot
    {
        public int player;
        public int hp;
        public int maxHp;
    }

    [Serializable]
    public class ReplayThresholdState
    {
        public int player;
        public int[] thresholds;
        public bool[] state;
    }

    /// <summary>
    /// バトル中の1コマンド記録
    /// type: PlayManifest, PlaySpell, PlayAlgorithm, Attack, EndTurn,
    ///       UseSkill, FlipAlgorithm, Ambush, HP_DAMAGE, WISH_TRIGGER, etc.
    /// </summary>
    [Serializable]
    public class ReplayCommand
    {
        public int turn;
        public int player;       // 1 or 2
        public string type;
        public string dataJson;  // JSON-serialized command payload
        public long timestamp;   // milliseconds since match start
    }

    [Serializable]
    public class ReplayResult
    {
        public int winner;       // 1 or 2 (0 = draw)
        public string reason;    // e.g. "鯱鉾勝利", "塗り勝利", "タイムアウト敗北"
        public int totalTurns;
        public int[] finalHps;   // [player1Hp, player2Hp]
    }
}
