using System;
using System.Collections;
using System.Collections.Generic;
using Banganka.Core.Battle;
using UnityEngine;

namespace Banganka.Core.Replay
{
    /// <summary>
    /// リプレイ再生プレイヤー (REPLAY_SPEC.md 1.3 準拠)
    /// コマンドログから BattleEngine を使って対戦を再構成・再生する
    /// 再生速度: 1x / 2x / 4x 切替可能
    /// コントロール: 再生 / 一時停止 / 次ターン / 前ターン / ターンシーク
    /// </summary>
    public class ReplayPlayer
    {
        public enum PlaybackState { Stopped, Playing, Paused }
        public enum PlaybackSpeed { Normal = 1, Fast = 2, VeryFast = 4 }

        // 現在の再生状態
        public PlaybackState State { get; private set; } = PlaybackState.Stopped;
        public PlaybackSpeed Speed { get; private set; } = PlaybackSpeed.Normal;
        public int CurrentCommandIndex { get; private set; }
        public int CurrentTurn { get; private set; }
        public int TotalTurns => _replayData?.result?.totalTurns ?? 0;
        public int TotalCommands => _replayData?.commands?.Count ?? 0;
        public ReplayData ReplayData => _replayData;

        // イベント
        public event Action<ReplayCommand> OnCommandExecuted;
        public event Action<int> OnTurnChanged;
        public event Action OnPlaybackComplete;
        public event Action<PlaybackState> OnStateChanged;

        ReplayData _replayData;
        MonoBehaviour _coroutineHost;
        Coroutine _playbackCoroutine;

        // ターン開始時点のコマンドインデックスを記録（前ターンへ戻る用）
        readonly Dictionary<int, int> _turnStartIndices = new();

        /// <summary>
        /// リプレイデータを読み込む
        /// </summary>
        public void Load(ReplayData data, MonoBehaviour coroutineHost)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (coroutineHost == null) throw new ArgumentNullException(nameof(coroutineHost));

            Stop();
            _replayData = data;
            _coroutineHost = coroutineHost;
            CurrentCommandIndex = 0;
            CurrentTurn = 0;

            BuildTurnIndex();
        }

        /// <summary>
        /// 再生開始
        /// </summary>
        public void Play()
        {
            if (_replayData == null) return;

            if (State == PlaybackState.Paused)
            {
                State = PlaybackState.Playing;
                OnStateChanged?.Invoke(State);
                return;
            }

            if (State == PlaybackState.Playing) return;

            State = PlaybackState.Playing;
            OnStateChanged?.Invoke(State);

            if (_playbackCoroutine != null)
                _coroutineHost.StopCoroutine(_playbackCoroutine);

            _playbackCoroutine = _coroutineHost.StartCoroutine(PlaybackRoutine());
        }

        /// <summary>
        /// 一時停止
        /// </summary>
        public void Pause()
        {
            if (State != PlaybackState.Playing) return;
            State = PlaybackState.Paused;
            OnStateChanged?.Invoke(State);
        }

        /// <summary>
        /// 再生停止（先頭に戻る）
        /// </summary>
        public void Stop()
        {
            if (_playbackCoroutine != null && _coroutineHost != null)
            {
                _coroutineHost.StopCoroutine(_playbackCoroutine);
                _playbackCoroutine = null;
            }

            State = PlaybackState.Stopped;
            CurrentCommandIndex = 0;
            CurrentTurn = 0;
            OnStateChanged?.Invoke(State);
        }

        /// <summary>
        /// 再生速度を設定
        /// </summary>
        public void SetSpeed(PlaybackSpeed speed)
        {
            Speed = speed;
        }

        /// <summary>
        /// 次のターンへスキップ
        /// </summary>
        public void NextTurn()
        {
            if (_replayData == null) return;

            int targetTurn = CurrentTurn + 1;
            SeekToTurn(targetTurn);
        }

        /// <summary>
        /// 前のターンへ戻る
        /// </summary>
        public void PrevTurn()
        {
            if (_replayData == null) return;

            int targetTurn = Math.Max(0, CurrentTurn - 1);
            SeekToTurn(targetTurn);
        }

        /// <summary>
        /// 指定ターンにシーク
        /// </summary>
        public void SeekToTurn(int turn)
        {
            if (_replayData == null) return;

            bool wasPlaying = State == PlaybackState.Playing;

            // コルーチンを停止
            if (_playbackCoroutine != null && _coroutineHost != null)
            {
                _coroutineHost.StopCoroutine(_playbackCoroutine);
                _playbackCoroutine = null;
            }

            // ターンの開始コマンドインデックスを検索
            if (_turnStartIndices.TryGetValue(turn, out int cmdIndex))
            {
                CurrentCommandIndex = cmdIndex;
                CurrentTurn = turn;
            }
            else
            {
                // 指定ターンが見つからない場合、最も近いターンへ
                int closestTurn = 0;
                int closestIdx = 0;
                foreach (var kv in _turnStartIndices)
                {
                    if (kv.Key <= turn && kv.Key > closestTurn)
                    {
                        closestTurn = kv.Key;
                        closestIdx = kv.Value;
                    }
                }
                CurrentCommandIndex = closestIdx;
                CurrentTurn = closestTurn;
            }

            OnTurnChanged?.Invoke(CurrentTurn);

            // 再生中だった場合はコルーチンを再開
            if (wasPlaying)
            {
                State = PlaybackState.Playing;
                _playbackCoroutine = _coroutineHost.StartCoroutine(PlaybackRoutine());
            }
            else
            {
                State = PlaybackState.Paused;
                OnStateChanged?.Invoke(State);
            }
        }

        /// <summary>
        /// 次の1コマンドだけ実行（ステップ実行）
        /// </summary>
        public bool StepForward()
        {
            if (_replayData == null) return false;
            if (CurrentCommandIndex >= _replayData.commands.Count) return false;

            ExecuteCurrentCommand();
            return CurrentCommandIndex < _replayData.commands.Count;
        }

        // ====================================================================
        // Private
        // ====================================================================

        void BuildTurnIndex()
        {
            _turnStartIndices.Clear();
            if (_replayData?.commands == null) return;

            int lastTurn = -1;
            for (int i = 0; i < _replayData.commands.Count; i++)
            {
                int turn = _replayData.commands[i].turn;
                if (turn != lastTurn)
                {
                    _turnStartIndices[turn] = i;
                    lastTurn = turn;
                }
            }
        }

        void ExecuteCurrentCommand()
        {
            if (CurrentCommandIndex >= _replayData.commands.Count) return;

            var cmd = _replayData.commands[CurrentCommandIndex];
            int prevTurn = CurrentTurn;
            CurrentTurn = cmd.turn;
            CurrentCommandIndex++;

            OnCommandExecuted?.Invoke(cmd);

            if (cmd.turn != prevTurn)
            {
                OnTurnChanged?.Invoke(CurrentTurn);
            }
        }

        IEnumerator PlaybackRoutine()
        {
            while (CurrentCommandIndex < _replayData.commands.Count)
            {
                // 一時停止中は待機
                while (State == PlaybackState.Paused)
                {
                    yield return null;
                }

                if (State == PlaybackState.Stopped)
                    yield break;

                ExecuteCurrentCommand();

                // コマンド間の待機時間（速度に応じて調整）
                float delay = CalculateDelay();
                if (delay > 0f)
                {
                    float elapsed = 0f;
                    while (elapsed < delay)
                    {
                        // 一時停止チェック
                        if (State == PlaybackState.Paused)
                        {
                            yield return null;
                            continue;
                        }
                        if (State == PlaybackState.Stopped)
                            yield break;

                        elapsed += Time.deltaTime;
                        yield return null;
                    }
                }
            }

            // 再生完了
            State = PlaybackState.Stopped;
            OnPlaybackComplete?.Invoke();
            OnStateChanged?.Invoke(State);
        }

        /// <summary>
        /// コマンド間のディレイを計算（再生速度に応じて）
        /// </summary>
        float CalculateDelay()
        {
            // ベースディレイ: コマンド間 0.8 秒
            const float baseDelay = 0.8f;

            // ターン切り替え時は長め
            if (CurrentCommandIndex < _replayData.commands.Count &&
                CurrentCommandIndex > 0 &&
                _replayData.commands[CurrentCommandIndex].turn != _replayData.commands[CurrentCommandIndex - 1].turn)
            {
                return 1.5f / (int)Speed;
            }

            return baseDelay / (int)Speed;
        }
    }
}
