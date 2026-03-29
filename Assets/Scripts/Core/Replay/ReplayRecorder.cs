using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Banganka.Core.Battle;
using Banganka.Core.Data;
using UnityEngine;

namespace Banganka.Core.Replay
{
    /// <summary>
    /// バトル中にリプレイデータを記録する (REPLAY_SPEC.md 1.1-1.2 準拠)
    /// コマンドログ再生方式: BattleEngine のイベントを購読して全コマンドを記録
    /// </summary>
    public class ReplayRecorder
    {
        ReplayData _data;
        BattleEngine _engine;
        long _matchStartTime;
        bool _isRecording;

        public bool IsRecording => _isRecording;
        public ReplayData CurrentData => _data;

        /// <summary>
        /// 記録を開始する。BattleEngine の初期化後、マリガン前に呼ぶこと。
        /// </summary>
        public void StartRecording(
            BattleEngine engine,
            string matchId,
            PlayerData p1,
            PlayerData p2,
            string p1WishMasterId,
            string p2WishMasterId,
            List<CardData> deckP1,
            List<CardData> deckP2)
        {
            _engine = engine;
            _matchStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _isRecording = true;

            _data = new ReplayData
            {
                replayId = GenerateReplayId(),
                matchId = matchId,
                version = Application.version,
                createdAt = DateTime.UtcNow.ToString("o"),
                players = new[]
                {
                    new ReplayPlayerInfo
                    {
                        uid = p1.uid,
                        name = p1.displayName,
                        wishMasterId = p1WishMasterId,
                        deckHash = ComputeDeckHash(deckP1)
                    },
                    new ReplayPlayerInfo
                    {
                        uid = p2.uid,
                        name = p2.displayName,
                        wishMasterId = p2WishMasterId,
                        deckHash = ComputeDeckHash(deckP2)
                    }
                },
                initialState = CaptureInitialState(engine.State, deckP1, deckP2),
                commands = new List<ReplayCommand>(),
                result = null
            };

            // BattleEngine イベント購読
            _engine.OnLog += OnBattleLog;
            _engine.OnMatchEnd += OnMatchEnd;
        }

        /// <summary>
        /// マリガンアクションを記録する
        /// </summary>
        public void RecordMulligan(int player, List<int> returnedIndices)
        {
            if (!_isRecording) return;

            int idx = player - 1;
            if (idx >= 0 && idx < _data.initialState.mulliganActions.Length)
            {
                _data.initialState.mulliganActions[idx] = returnedIndices?.ToArray() ?? Array.Empty<int>();
            }
        }

        /// <summary>
        /// コマンドを記録する
        /// </summary>
        public void RecordCommand(int turn, int player, string type, object data)
        {
            if (!_isRecording) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var cmd = new ReplayCommand
            {
                turn = turn,
                player = player,
                type = type,
                dataJson = data != null ? JsonUtility.ToJson(data) : "{}",
                timestamp = now - _matchStartTime
            };
            _data.commands.Add(cmd);
        }

        /// <summary>
        /// JSON文字列を直接渡してコマンドを記録する
        /// </summary>
        public void RecordCommandRaw(int turn, int player, string type, string dataJson)
        {
            if (!_isRecording) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var cmd = new ReplayCommand
            {
                turn = turn,
                player = player,
                type = type,
                dataJson = dataJson ?? "{}",
                timestamp = now - _matchStartTime
            };
            _data.commands.Add(cmd);
        }

        /// <summary>
        /// 記録を終了してリプレイデータを返す
        /// </summary>
        public ReplayData FinishRecording(MatchResult matchResult)
        {
            if (!_isRecording) return null;
            _isRecording = false;

            // イベント購読解除
            if (_engine != null)
            {
                _engine.OnLog -= OnBattleLog;
                _engine.OnMatchEnd -= OnMatchEnd;
            }

            _data.result = new ReplayResult
            {
                winner = matchResult == MatchResult.Player1Win ? 1
                       : matchResult == MatchResult.Player2Win ? 2
                       : 0,
                reason = DetermineWinReason(matchResult),
                totalTurns = _engine?.State?.turnTotal ?? 0,
                finalHps = new[]
                {
                    _engine?.State?.player1?.hp ?? 0,
                    _engine?.State?.player2?.hp ?? 0
                }
            };

            return _data;
        }

        /// <summary>
        /// 記録を中断（試合切断時など）
        /// </summary>
        public void CancelRecording()
        {
            if (!_isRecording) return;
            _isRecording = false;

            if (_engine != null)
            {
                _engine.OnLog -= OnBattleLog;
                _engine.OnMatchEnd -= OnMatchEnd;
            }
        }

        // ====================================================================
        // Private helpers
        // ====================================================================

        ReplayInitialState CaptureInitialState(BattleState state, List<CardData> deckP1, List<CardData> deckP2)
        {
            return new ReplayInitialState
            {
                firstPlayer = state.activePlayer == PlayerSide.Player1 ? 1 : 2,
                decks = new[]
                {
                    deckP1.Select(c => c.id).ToArray(),
                    deckP2.Select(c => c.id).ToArray()
                },
                initialHands = new[]
                {
                    Enumerable.Range(0, state.player1.hand.Count).ToArray(),
                    Enumerable.Range(0, state.player2.hand.Count).ToArray()
                },
                mulliganActions = new[] { Array.Empty<int>(), Array.Empty<int>() },
                hpSnapshots = new[]
                {
                    new ReplayHpSnapshot { player = 1, hp = state.player1.hp, maxHp = state.player1.maxHp },
                    new ReplayHpSnapshot { player = 2, hp = state.player2.hp, maxHp = state.player2.maxHp }
                },
                thresholdCards = new[]
                {
                    BuildThresholdState(1, state.player1),
                    BuildThresholdState(2, state.player2)
                }
            };
        }

        ReplayThresholdState BuildThresholdState(int player, PlayerState ps)
        {
            return new ReplayThresholdState
            {
                player = player,
                thresholds = ps.wishZone.Select(w => w.threshold).ToArray(),
                state = ps.wishZone.Select(w => w.triggered).ToArray()
            };
        }

        void OnBattleLog(BattleLogEntry entry)
        {
            // BattleEngine のログイベントから自動的にコマンドを記録
            // 主要イベントのみ記録（再生に必要な情報）
            switch (entry.eventType)
            {
                case "PLAY_SHOW":
                case "SET_SHARED_ALGO":
                case "SET_SHARED_ALGO_FACEDOWN":
                case "FLIP_ALGO":
                case "AUTO_FLIP_ALGO":
                case "ATTACK_DECLARED":
                case "BLOCK_DECLARED":
                case "DIRECT_HIT":
                case "FINAL_BLOW":
                case "BATTLE_RESOLVED":
                case "HP_DAMAGE":
                case "HP_HEAL":
                case "WISH_TRIGGER":
                case "WISH_EFFECT":
                case "LEADER_SKILL":
                case "LEADER_LEVEL_UP":
                case "LEADER_EVO_GAIN":
                case "AMBUSH_PLAY":
                case "AMBUSH_EFFECT":
                case "END_TURN":
                case "TURN_TIMEOUT":
                case "AUTO_LOSE":
                case "FINAL_STATE":
                case "TURN_LIMIT":
                    int currentPlayer = _engine.State.activePlayer == PlayerSide.Player1 ? 1 : 2;
                    RecordCommandRaw(
                        entry.turnNumber,
                        currentPlayer,
                        entry.eventType,
                        $"{{\"detail\":\"{EscapeJson(entry.detail)}\"}}");
                    break;
            }
        }

        void OnMatchEnd(MatchResult result)
        {
            // MatchEnd は FinishRecording で明示的に処理するため、ここでは何もしない
        }

        string DetermineWinReason(MatchResult result)
        {
            if (_engine?.State == null) return "不明";

            // ログから最終イベントを判定
            var log = _engine.State.log;
            if (log.Count > 0)
            {
                var lastEntry = log[^1];
                if (lastEntry.eventType == "FINAL_BLOW") return "鯱鉾勝利";
                if (lastEntry.eventType == "TURN_LIMIT") return "塗り勝利";
                if (lastEntry.eventType == "AUTO_LOSE") return "タイムアウト敗北";
            }

            if (result == MatchResult.Draw) return "引き分け";
            return "勝利";
        }

        /// <summary>
        /// リプレイID生成: "rpl_{yyyyMMdd}_{random6}"
        /// </summary>
        static string GenerateReplayId()
        {
            string datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            string randomPart = Guid.NewGuid().ToString("N").Substring(0, 6);
            return $"rpl_{datePart}_{randomPart}";
        }

        /// <summary>
        /// デッキのカードID一覧からハッシュを生成
        /// </summary>
        static string ComputeDeckHash(List<CardData> deck)
        {
            var ids = string.Join(",", deck.Select(c => c.id).OrderBy(id => id));
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(ids));
            return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 8).ToLowerInvariant();
        }

        static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }
    }
}
