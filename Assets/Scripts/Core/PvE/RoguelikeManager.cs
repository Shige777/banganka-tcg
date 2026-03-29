using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Banganka.Core.Data;
using Banganka.Core.Config;

namespace Banganka.Core.PvE
{
    /// <summary>
    /// PvEローグライクモード管理。
    /// 1ラン8ステージ。初期デッキ→ステージ勝利→報酬でデッキ強化→次ステージ。
    /// 敗北時はランHP減少、HP0でラン終了。
    /// </summary>
    public static class RoguelikeManager
    {
        const int TotalStages = 8;
        const int StartingHp = 30;
        const int StartingGold = 100;
        const int DeckSize = 20; // ローグライクは軽量デッキ
        const string SaveFile = "roguelike_run.json";

        static RoguelikeRunData _currentRun;
        static readonly System.Random _rng = new();

        public static RoguelikeRunData CurrentRun => _currentRun;
        public static bool HasActiveRun => _currentRun != null && _currentRun.isActive;

        public static event Action OnRunUpdated;

        // ================================================================
        // Run lifecycle
        // ================================================================

        /// <summary>新しいランを開始</summary>
        public static RoguelikeRunData StartNewRun(string leaderId)
        {
            _currentRun = new RoguelikeRunData
            {
                runId = $"rgl_{DateTime.UtcNow:yyyyMMddHHmmss}_{_rng.Next(1000):D3}",
                leaderId = leaderId,
                currentStage = 0,
                maxStage = TotalStages,
                hp = StartingHp,
                maxHp = StartingHp,
                gold = StartingGold,
                isActive = true,
                startedAt = DateTime.UtcNow.ToString("o")
            };

            // 初期デッキ: リーダーアスペクトの低コストカードで構成
            BuildStarterDeck(leaderId);

            SaveRun();
            OnRunUpdated?.Invoke();
            Debug.Log($"[Roguelike] New run started: {_currentRun.runId}");
            return _currentRun;
        }

        /// <summary>ランを放棄</summary>
        public static void AbandonRun()
        {
            if (_currentRun == null) return;
            _currentRun.isActive = false;
            SaveRun();
            Debug.Log("[Roguelike] Run abandoned");
            _currentRun = null;
            OnRunUpdated?.Invoke();
        }

        /// <summary>保存済みランを読み込み</summary>
        public static void LoadRun()
        {
            string path = Path.Combine(Application.persistentDataPath, SaveFile);
            if (!File.Exists(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                _currentRun = JsonUtility.FromJson<RoguelikeRunData>(json);
                if (_currentRun != null && !_currentRun.isActive)
                    _currentRun = null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Roguelike] Load failed: {e.Message}");
                _currentRun = null;
            }
        }

        // ================================================================
        // Stage management
        // ================================================================

        /// <summary>現在のステージ情報を取得</summary>
        public static RoguelikeStage GetCurrentStage()
        {
            if (_currentRun == null || _currentRun.IsComplete) return null;
            return GenerateStage(_currentRun.currentStage);
        }

        /// <summary>ステージ戦闘結果を記録</summary>
        public static void RecordStageResult(bool won, int hpLost)
        {
            if (_currentRun == null) return;

            _currentRun.hp = Math.Max(0, _currentRun.hp - hpLost);

            var result = new StageResult
            {
                stage = _currentRun.currentStage,
                won = won,
                hpLost = hpLost
            };
            _currentRun.completedStages.Add(result);

            if (won)
            {
                var stage = GetCurrentStage();
                if (stage != null)
                    _currentRun.gold += stage.bonusGold;
            }

            // HPが0になったらラン終了
            if (_currentRun.hp <= 0)
            {
                _currentRun.isActive = false;
                GrantRunRewards();
                Debug.Log($"[Roguelike] Run ended at stage {_currentRun.currentStage + 1} (HP depleted)");
            }

            SaveRun();
            OnRunUpdated?.Invoke();
        }

        /// <summary>報酬選択後、次のステージへ進む</summary>
        public static void AdvanceToNextStage(StageReward chosenReward)
        {
            if (_currentRun == null) return;

            ApplyReward(chosenReward);

            _currentRun.currentStage++;

            // 全ステージクリア
            if (_currentRun.IsComplete)
            {
                _currentRun.isActive = false;
                GrantRunRewards();
                Debug.Log("[Roguelike] Run completed! All stages cleared.");
            }

            SaveRun();
            OnRunUpdated?.Invoke();
        }

        /// <summary>ステージクリア後の報酬選択肢を生成（3択）</summary>
        public static List<StageReward> GenerateRewardChoices()
        {
            if (_currentRun == null) return new List<StageReward>();

            var choices = new List<StageReward>();
            var stage = GetCurrentStage();
            var difficulty = stage?.difficulty ?? DifficultyTier.Normal;

            // 選択肢1: カード追加
            var card = PickRewardCard(difficulty);
            if (card != null)
            {
                choices.Add(new StageReward
                {
                    type = RewardType.Card,
                    cardId = card.id,
                    displayName = card.cardName,
                    description = $"CP:{card.cpCost} {card.type}"
                });
            }

            // 選択肢2: ゴールド or アーティファクト
            if (_rng.NextDouble() < 0.4 && _currentRun.artifacts.Count < 5)
            {
                var artifact = PickArtifact();
                if (artifact != null)
                {
                    choices.Add(new StageReward
                    {
                        type = RewardType.Artifact,
                        artifactId = artifact.id,
                        displayName = artifact.displayName,
                        description = artifact.description
                    });
                }
            }
            else
            {
                int goldReward = difficulty switch
                {
                    DifficultyTier.Easy => 30,
                    DifficultyTier.Normal => 50,
                    DifficultyTier.Hard => 80,
                    DifficultyTier.Boss => 150,
                    _ => 50
                };
                choices.Add(new StageReward
                {
                    type = RewardType.Gold,
                    goldAmount = goldReward,
                    displayName = $"{goldReward}G",
                    description = "ラン内ゴールドを獲得"
                });
            }

            // 選択肢3: HP回復 or カード除去
            if (_currentRun.hp < _currentRun.maxHp && _rng.NextDouble() < 0.5)
            {
                int heal = Math.Min(5, _currentRun.maxHp - _currentRun.hp);
                choices.Add(new StageReward
                {
                    type = RewardType.Heal,
                    healAmount = heal,
                    displayName = $"HP+{heal}",
                    description = "ランHPを回復"
                });
            }
            else if (_currentRun.deckCards.Count > 15)
            {
                choices.Add(new StageReward
                {
                    type = RewardType.RemoveCard,
                    displayName = "カード除去",
                    description = "デッキから1枚除去してスリム化"
                });
            }

            return choices;
        }

        // ================================================================
        // Artifacts catalog
        // ================================================================

        static readonly ArtifactData[] AllArtifacts =
        {
            new() { id = "art_cp_start", displayName = "願力の種", description = "初期CP+1", effectKey = "start_cp_plus", effectValue = 1, rarity = "C" },
            new() { id = "art_draw", displayName = "知識の書", description = "初期手札+1", effectKey = "draw_plus", effectValue = 1, rarity = "R" },
            new() { id = "art_hp", displayName = "生命の果実", description = "最大HP+5", effectKey = "max_hp_plus", effectValue = 5, rarity = "C" },
            new() { id = "art_power", displayName = "戦士の腕輪", description = "全顕現の戦力+500", effectKey = "power_plus_all", effectValue = 500, rarity = "R" },
            new() { id = "art_gold", displayName = "商人の袋", description = "ステージ報酬ゴールド+20", effectKey = "gold_bonus", effectValue = 20, rarity = "C" },
            new() { id = "art_heal", displayName = "再生の環", description = "勝利時HP+2回復", effectKey = "win_heal", effectValue = 2, rarity = "R" },
            new() { id = "art_wish", displayName = "願力の加護", description = "願力ダメージ+1%", effectKey = "wish_damage_plus", effectValue = 1, rarity = "SR" },
            new() { id = "art_shield", displayName = "守りの盾", description = "敗北時HP減少-2", effectKey = "loss_hp_reduce", effectValue = 2, rarity = "SR" },
        };

        // ================================================================
        // Private helpers
        // ================================================================

        static void BuildStarterDeck(string leaderId)
        {
            Aspect aspect = Aspect.Contest;
            if (CardDatabase.AllLeaders.TryGetValue(leaderId, out var leader))
                aspect = leader.keyAspect;

            var candidates = CardDatabase.AllCards.Values
                .Where(c => c.aspect == aspect && c.cpCost <= 4)
                .OrderBy(c => c.cpCost)
                .ThenByDescending(c => c.battlePower)
                .ToList();

            _currentRun.deckCards.Clear();

            // 低コスト中心でDeckSize枚のデッキを構築
            foreach (var card in candidates)
            {
                if (_currentRun.deckCards.Count >= DeckSize) break;
                int copies = Math.Min(2, DeckSize - _currentRun.deckCards.Count);
                for (int i = 0; i < copies; i++)
                    _currentRun.deckCards.Add(card.id);
            }

            // 足りない場合は他アスペクトの低コストで補填
            if (_currentRun.deckCards.Count < DeckSize)
            {
                var filler = CardDatabase.AllCards.Values
                    .Where(c => c.aspect != aspect && c.cpCost <= 3)
                    .OrderBy(_ => _rng.Next())
                    .ToList();

                foreach (var card in filler)
                {
                    if (_currentRun.deckCards.Count >= DeckSize) break;
                    _currentRun.deckCards.Add(card.id);
                }
            }
        }

        static RoguelikeStage GenerateStage(int index)
        {
            var difficulty = index switch
            {
                <= 1 => DifficultyTier.Easy,
                <= 4 => DifficultyTier.Normal,
                <= 6 => DifficultyTier.Hard,
                _ => DifficultyTier.Boss
            };

            var aspects = Enum.GetValues(typeof(Aspect));
            var enemyAspect = (Aspect)aspects.GetValue(_rng.Next(aspects.Length));

            string[] easyNames = { "見習い果求者", "迷い子", "放浪者" };
            string[] normalNames = { "果求者", "挑戦者", "守護者" };
            string[] hardNames = { "熟練果求者", "古の戦士", "闇の使者" };
            string[] bossNames = { "交界の支配者", "万願果の守り手", "伝説の果求者" };

            string[] namePool = difficulty switch
            {
                DifficultyTier.Easy => easyNames,
                DifficultyTier.Normal => normalNames,
                DifficultyTier.Hard => hardNames,
                _ => bossNames
            };

            return new RoguelikeStage
            {
                stageIndex = index,
                enemyName = namePool[_rng.Next(namePool.Length)],
                enemyDeckProfile = $"{difficulty}_{enemyAspect}".ToLower(),
                enemyAspect = enemyAspect,
                difficulty = difficulty,
                bonusGold = difficulty switch
                {
                    DifficultyTier.Easy => 20,
                    DifficultyTier.Normal => 40,
                    DifficultyTier.Hard => 60,
                    DifficultyTier.Boss => 100,
                    _ => 30
                }
            };
        }

        static CardData PickRewardCard(DifficultyTier difficulty)
        {
            string targetRarity = difficulty switch
            {
                DifficultyTier.Easy => _rng.NextDouble() < 0.8 ? "C" : "R",
                DifficultyTier.Normal => _rng.NextDouble() < 0.6 ? "R" : "SR",
                DifficultyTier.Hard => _rng.NextDouble() < 0.5 ? "R" : "SR",
                DifficultyTier.Boss => _rng.NextDouble() < 0.4 ? "SR" : "SSR",
                _ => "R"
            };

            var candidates = CardDatabase.AllCards.Values
                .Where(c => c.rarity == targetRarity)
                .ToList();

            return candidates.Count > 0 ? candidates[_rng.Next(candidates.Count)] : null;
        }

        static ArtifactData PickArtifact()
        {
            var available = AllArtifacts
                .Where(a => !_currentRun.artifacts.Contains(a.id))
                .ToList();

            return available.Count > 0 ? available[_rng.Next(available.Count)] : null;
        }

        static void ApplyReward(StageReward reward)
        {
            switch (reward.type)
            {
                case RewardType.Card:
                    if (!string.IsNullOrEmpty(reward.cardId))
                        _currentRun.deckCards.Add(reward.cardId);
                    break;
                case RewardType.Artifact:
                    if (!string.IsNullOrEmpty(reward.artifactId))
                        _currentRun.artifacts.Add(reward.artifactId);
                    // アーティファクト即時効果
                    var art = AllArtifacts.FirstOrDefault(a => a.id == reward.artifactId);
                    if (art?.effectKey == "max_hp_plus")
                    {
                        _currentRun.maxHp += art.effectValue;
                        _currentRun.hp += art.effectValue;
                    }
                    break;
                case RewardType.Gold:
                    _currentRun.gold += reward.goldAmount;
                    break;
                case RewardType.Heal:
                    _currentRun.hp = Math.Min(_currentRun.hp + reward.healAmount, _currentRun.maxHp);
                    break;
                case RewardType.RemoveCard:
                    // カード除去はUI側で選択後に RemoveCardFromDeck() を呼ぶ
                    break;
            }

            if (_currentRun.completedStages.Count > 0)
            {
                var last = _currentRun.completedStages[^1];
                last.rewardChosen = reward.type == RewardType.Card ? reward.cardId :
                                    reward.type == RewardType.Artifact ? reward.artifactId :
                                    reward.displayName;
            }
        }

        /// <summary>カード除去（RemoveCard報酬選択時にUI側から呼ぶ）</summary>
        public static void RemoveCardFromDeck(string cardId)
        {
            if (_currentRun == null) return;
            _currentRun.deckCards.Remove(cardId);
            SaveRun();
            OnRunUpdated?.Invoke();
        }

        /// <summary>指定アーティファクトを所持しているか</summary>
        public static bool HasArtifact(string artifactId)
        {
            return _currentRun?.artifacts?.Contains(artifactId) ?? false;
        }

        /// <summary>アーティファクト効果値の合計を取得</summary>
        public static int GetArtifactEffect(string effectKey)
        {
            if (_currentRun == null) return 0;
            int total = 0;
            foreach (var artId in _currentRun.artifacts)
            {
                var art = AllArtifacts.FirstOrDefault(a => a.id == artId);
                if (art != null && art.effectKey == effectKey)
                    total += art.effectValue;
            }
            return total;
        }

        /// <summary>ラン完了/失敗時に永続報酬を付与</summary>
        static void GrantRunRewards()
        {
            int stagesCleared = _currentRun.completedStages.Count(s => s.won);
            int goldReward = stagesCleared * 50;
            if (_currentRun.IsComplete) goldReward += 500; // 全ステージクリアボーナス

            PlayerData.Instance.gold += goldReward;
            PlayerData.Save();
            Debug.Log($"[Roguelike] Run rewards: {goldReward}G (stages cleared: {stagesCleared}/{TotalStages})");
        }

        static void SaveRun()
        {
            if (_currentRun == null) return;
            try
            {
                string json = JsonUtility.ToJson(_currentRun, true);
                File.WriteAllText(Path.Combine(Application.persistentDataPath, SaveFile), json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Roguelike] Save failed: {e.Message}");
            }
        }
    }
}
