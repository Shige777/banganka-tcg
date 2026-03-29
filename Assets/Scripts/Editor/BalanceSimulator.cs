using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Banganka.Core.Battle;
using Banganka.Core.Data;
using Banganka.Core.Config;

namespace Banganka.Editor
{
    /// <summary>
    /// バランステストシミュレーター (TEST_PLAN.md / BALANCE_POLICY.md)
    /// 各デッキ組み合わせ最低3,000自動対戦、先攻勝率目標: 45%〜55%
    /// Editor > Banganka > Balance Simulator でウィンドウを開く
    /// </summary>
    public class BalanceSimulator : EditorWindow
    {
        int _matchesPerPair = 3000;
        BotDifficulty _difficulty = BotDifficulty.Normal;
        bool _running;
        string _resultLog = "";
        Vector2 _scrollPos;

        [MenuItem("Banganka/Balance Simulator")]
        static void Open()
        {
            GetWindow<BalanceSimulator>("Balance Simulator");
        }

        void OnGUI()
        {
            GUILayout.Label("Banganka Balance Simulator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _matchesPerPair = EditorGUILayout.IntField("Matches per pair", _matchesPerPair);
            _difficulty = (BotDifficulty)EditorGUILayout.EnumPopup("Bot Difficulty", _difficulty);

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(_running);
            if (GUILayout.Button("Run Full Balance Test", GUILayout.Height(30)))
            {
                _running = true;
                RunFullBalanceTest();
                _running = false;
            }

            if (GUILayout.Button("Run First-Player Advantage Test", GUILayout.Height(24)))
            {
                _running = true;
                RunFirstPlayerTest();
                _running = false;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(_resultLog, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_resultLog) && GUILayout.Button("Copy to Clipboard"))
            {
                EditorGUIUtility.systemCopyBuffer = _resultLog;
            }
        }

        void RunFullBalanceTest()
        {
            var presets = CardDatabase.PresetDecks;
            if (presets == null || presets.Count == 0)
            {
                _resultLog = "ERROR: No preset decks found. Ensure CardDatabase is loaded.";
                return;
            }

            var deckIds = presets.Keys.ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"=== Balance Test: {_matchesPerPair} matches/pair, {_difficulty} difficulty ===");
            sb.AppendLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Decks: {deckIds.Count}");
            sb.AppendLine();

            var overallStats = new OverallStats();
            var deckWins = new Dictionary<string, int>();
            var deckGames = new Dictionary<string, int>();
            foreach (var id in deckIds) { deckWins[id] = 0; deckGames[id] = 0; }

            int pairIndex = 0;
            int totalPairs = deckIds.Count * (deckIds.Count - 1) / 2;

            for (int i = 0; i < deckIds.Count; i++)
            {
                for (int j = i + 1; j < deckIds.Count; j++)
                {
                    pairIndex++;
                    var idA = deckIds[i];
                    var idB = deckIds[j];
                    var pairResult = RunMatchPair(idA, idB, _matchesPerPair);

                    float winRateA = (float)pairResult.p1Wins / _matchesPerPair * 100f;
                    string balance = winRateA >= 45f && winRateA <= 55f ? "OK" : "WARN";

                    sb.AppendLine($"[{pairIndex}/{totalPairs}] {presets[idA].name} vs {presets[idB].name}");
                    sb.AppendLine($"  {presets[idA].name}: {pairResult.p1Wins} wins ({winRateA:F1}%)");
                    sb.AppendLine($"  {presets[idB].name}: {pairResult.p2Wins} wins ({(float)pairResult.p2Wins / _matchesPerPair * 100f:F1}%)");
                    sb.AppendLine($"  Draws: {pairResult.draws}, Avg turns: {pairResult.avgTurns:F1} [{balance}]");
                    sb.AppendLine();

                    deckWins[idA] += pairResult.p1Wins;
                    deckWins[idB] += pairResult.p2Wins;
                    deckGames[idA] += _matchesPerPair;
                    deckGames[idB] += _matchesPerPair;

                    overallStats.totalMatches += _matchesPerPair;
                    overallStats.totalP1Wins += pairResult.p1Wins;
                    overallStats.totalP2Wins += pairResult.p2Wins;
                    overallStats.totalDraws += pairResult.draws;
                    overallStats.totalTurns += pairResult.totalTurns;

                    EditorUtility.DisplayProgressBar("Balance Test",
                        $"{presets[idA].name} vs {presets[idB].name}",
                        (float)pairIndex / totalPairs);
                }
            }

            EditorUtility.ClearProgressBar();

            sb.AppendLine("=== Overall Deck Win Rates ===");
            foreach (var id in deckIds)
            {
                float rate = deckGames[id] > 0 ? (float)deckWins[id] / deckGames[id] * 100f : 0;
                sb.AppendLine($"  {presets[id].name}: {deckWins[id]}/{deckGames[id]} ({rate:F1}%)");
            }

            sb.AppendLine();
            sb.AppendLine("=== First-Player Advantage ===");
            float p1Rate = overallStats.totalMatches > 0
                ? (float)overallStats.totalP1Wins / (overallStats.totalP1Wins + overallStats.totalP2Wins) * 100f
                : 0;
            string p1Balance = p1Rate >= 45f && p1Rate <= 55f ? "OK" : "WARN";
            sb.AppendLine($"  P1 (first): {overallStats.totalP1Wins} wins ({p1Rate:F1}%) [{p1Balance}]");
            sb.AppendLine($"  P2 (second): {overallStats.totalP2Wins} wins ({100f - p1Rate:F1}%)");
            sb.AppendLine($"  Draws: {overallStats.totalDraws}");
            sb.AppendLine($"  Avg turns: {(float)overallStats.totalTurns / overallStats.totalMatches:F1}");
            sb.AppendLine($"Completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            _resultLog = sb.ToString();
            Debug.Log(_resultLog);
        }

        void RunFirstPlayerTest()
        {
            var presets = CardDatabase.PresetDecks;
            if (presets == null || presets.Count == 0)
            {
                _resultLog = "ERROR: No preset decks found.";
                return;
            }

            var firstDeck = presets.Values.First();
            var deckA = CardDatabase.BuildDeck(firstDeck.cardIds);
            var leaderA = CardDatabase.GetLeader(firstDeck.leaderId) ?? CardDatabase.DefaultLeader;

            var sb = new StringBuilder();
            sb.AppendLine($"=== First-Player Advantage Test ({_matchesPerPair} matches, mirror match) ===");

            int p1Wins = 0, p2Wins = 0, draws = 0;
            long totalTurns = 0;

            for (int m = 0; m < _matchesPerPair; m++)
            {
                var result = SimulateMatch(leaderA, leaderA, deckA, deckA, m);
                if (result.winner == MatchResult.Player1Win) p1Wins++;
                else if (result.winner == MatchResult.Player2Win) p2Wins++;
                else draws++;
                totalTurns += result.turns;

                if (m % 500 == 0)
                    EditorUtility.DisplayProgressBar("First-Player Test",
                        $"Match {m}/{_matchesPerPair}", (float)m / _matchesPerPair);
            }

            EditorUtility.ClearProgressBar();

            float p1Rate = (p1Wins + p2Wins) > 0 ? (float)p1Wins / (p1Wins + p2Wins) * 100f : 50f;
            string balance = p1Rate >= 45f && p1Rate <= 55f ? "OK" : "WARN";

            sb.AppendLine($"  P1 (first): {p1Wins} wins ({p1Rate:F1}%) [{balance}]");
            sb.AppendLine($"  P2 (second): {p2Wins} wins ({100f - p1Rate:F1}%)");
            sb.AppendLine($"  Draws: {draws}");
            sb.AppendLine($"  Avg turns: {(float)totalTurns / _matchesPerPair:F1}");

            _resultLog = sb.ToString();
            Debug.Log(_resultLog);
        }

        struct PairResult
        {
            public int p1Wins, p2Wins, draws;
            public long totalTurns;
            public float avgTurns;
        }

        PairResult RunMatchPair(string deckIdA, string deckIdB, int matches)
        {
            var presets = CardDatabase.PresetDecks;
            var presetA = presets[deckIdA];
            var presetB = presets[deckIdB];
            var deckA = CardDatabase.BuildDeck(presetA.cardIds);
            var deckB = CardDatabase.BuildDeck(presetB.cardIds);
            var leaderA = CardDatabase.GetLeader(presetA.leaderId) ?? CardDatabase.DefaultLeader;
            var leaderB = CardDatabase.GetLeader(presetB.leaderId) ?? CardDatabase.DefaultLeader;

            var result = new PairResult();

            for (int m = 0; m < matches; m++)
            {
                var mr = SimulateMatch(leaderA, leaderB, deckA, deckB, m);
                if (mr.winner == MatchResult.Player1Win) result.p1Wins++;
                else if (mr.winner == MatchResult.Player2Win) result.p2Wins++;
                else result.draws++;
                result.totalTurns += mr.turns;
            }

            result.avgTurns = matches > 0 ? (float)result.totalTurns / matches : 0;
            return result;
        }

        struct MatchSimResult
        {
            public MatchResult winner;
            public int turns;
        }

        MatchSimResult SimulateMatch(LeaderData leaderP1, LeaderData leaderP2,
            List<CardData> deckP1, List<CardData> deckP2, int seed)
        {
            var engine = new BattleEngine(seed);
            engine.InitMatch(leaderP1, leaderP2,
                new List<CardData>(deckP1), new List<CardData>(deckP2));

            var ai1 = new SimpleAI(engine, PlayerSide.Player1, _difficulty);
            var ai2 = new SimpleAI(engine, PlayerSide.Player2, _difficulty);

            engine.StartTurn();

            int maxIterations = BalanceConfig.TurnLimitTotal * 2 + 10;
            int iterations = 0;

            while (!engine.State.isGameOver && iterations < maxIterations)
            {
                iterations++;
                if (engine.State.activePlayer == PlayerSide.Player1)
                    ai1.PlayTurn();
                else
                    ai2.PlayTurn();

                if (!engine.State.isGameOver)
                    engine.EndTurn();
            }

            return new MatchSimResult
            {
                winner = engine.State.result,
                turns = engine.State.turnTotal
            };
        }

        struct OverallStats
        {
            public int totalMatches, totalP1Wins, totalP2Wins, totalDraws;
            public long totalTurns;
        }
    }
}
