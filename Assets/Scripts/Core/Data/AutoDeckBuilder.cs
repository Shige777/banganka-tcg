using System;
using System.Collections.Generic;
using System.Linq;
using Banganka.Core.Config;

namespace Banganka.Core.Data
{
    /// <summary>
    /// オートデッキ構築。
    /// 指定アスペクト+所持カードから34枚の最適デッキを自動生成。
    /// コストカーブ・タイプバランスを考慮。
    /// </summary>
    public static class AutoDeckBuilder
    {
        // 理想コストカーブ (コスト0-10+のカード枚数目安)
        static readonly int[] IdealCurve = { 0, 4, 6, 6, 5, 4, 3, 2, 2, 1, 1 };

        // タイプ比率目安 (34枚中)
        const int TargetManifests = 20;
        const int TargetSpells = 10;
        const int TargetAlgorithms = 2;
        // 残り2枚は柔軟

        /// <summary>
        /// 所持カードから最適デッキを自動構築する。
        /// </summary>
        /// <param name="aspect">構築するアスペクト</param>
        /// <param name="leaderId">使用する願主ID</param>
        /// <returns>34枚のカードIDリスト。足りない場合はnull</returns>
        public static List<string> Build(Aspect aspect, string leaderId)
        {
            var owned = PlayerData.Instance.cardCollection;
            var candidates = new List<ScoredCard>();

            foreach (var kv in CardDatabase.AllCards)
            {
                var card = kv.Value;
                int ownedCount = owned.TryGetValue(card.id, out int c) ? c : 0;
                if (ownedCount <= 0) continue;

                float score = ScoreCard(card, aspect);
                int maxCopies = Math.Min(ownedCount, BalanceConfig.SameNameLimit);

                candidates.Add(new ScoredCard
                {
                    card = card,
                    score = score,
                    maxCopies = maxCopies
                });
            }

            // スコア降順でソート
            candidates.Sort((a, b) => b.score.CompareTo(a.score));

            var deck = new List<string>();
            var usedCounts = new Dictionary<string, int>();
            var typeCounts = new Dictionary<CardType, int>
            {
                { CardType.Manifest, 0 },
                { CardType.Spell, 0 },
                { CardType.Algorithm, 0 }
            };
            var costCounts = new int[11]; // 0-10+

            // Pass 1: アスペクト一致カードを優先
            FillFromCandidates(candidates.Where(c => c.card.aspect == aspect),
                deck, usedCounts, typeCounts, costCounts);

            // Pass 2: 残り枠を他アスペクトカードで埋める
            if (deck.Count < BalanceConfig.DeckSize)
            {
                FillFromCandidates(candidates.Where(c => c.card.aspect != aspect),
                    deck, usedCounts, typeCounts, costCounts);
            }

            if (deck.Count < BalanceConfig.DeckSize)
                return null; // 所持カードが足りない

            return deck;
        }

        /// <summary>リーダーアスペクトに対応するデッキを自動構築</summary>
        public static List<string> BuildForLeader(string leaderId)
        {
            if (!CardDatabase.AllLeaders.TryGetValue(leaderId, out var leader))
                return null;

            return Build(leader.keyAspect, leaderId);
        }

        static void FillFromCandidates(
            IEnumerable<ScoredCard> candidates,
            List<string> deck,
            Dictionary<string, int> usedCounts,
            Dictionary<CardType, int> typeCounts,
            int[] costCounts)
        {
            foreach (var sc in candidates)
            {
                if (deck.Count >= BalanceConfig.DeckSize) break;

                int used = usedCounts.TryGetValue(sc.card.id, out int u) ? u : 0;
                int canAdd = sc.maxCopies - used;
                if (canAdd <= 0) continue;

                // タイプバランスチェック
                int typeLimit = sc.card.type switch
                {
                    CardType.Manifest => TargetManifests + 4,  // 上限に余裕
                    CardType.Spell => TargetSpells + 4,
                    CardType.Algorithm => TargetAlgorithms + 1,
                    _ => 99
                };
                if (typeCounts[sc.card.type] >= typeLimit) continue;

                // コストカーブチェック
                int bucket = Math.Min(sc.card.cpCost, 10);
                int idealForBucket = bucket < IdealCurve.Length ? IdealCurve[bucket] : 1;
                // コストカーブの2倍まで許容
                if (costCounts[bucket] >= idealForBucket * 2) continue;

                int toAdd = Math.Min(canAdd, BalanceConfig.DeckSize - deck.Count);
                // コストカーブを超えないように枚数を調整
                toAdd = Math.Min(toAdd, Math.Max(1, idealForBucket * 2 - costCounts[bucket]));

                for (int i = 0; i < toAdd; i++)
                {
                    deck.Add(sc.card.id);
                    costCounts[bucket]++;
                    typeCounts[sc.card.type]++;
                }
                usedCounts[sc.card.id] = used + toAdd;
            }
        }

        /// <summary>
        /// カードのデッキ適性スコアを計算。
        /// アスペクト一致・コスト効率・レアリティ・キーワードを評価。
        /// </summary>
        static float ScoreCard(CardData card, Aspect targetAspect)
        {
            float score = 0;

            // アスペクト一致: +100
            if (card.aspect == targetAspect)
                score += 100f;

            // レアリティボーナス
            score += card.rarity switch
            {
                "SSR" => 30f,
                "SR" => 20f,
                "R" => 10f,
                _ => 5f
            };

            // タイプ別評価
            switch (card.type)
            {
                case CardType.Manifest:
                    // 戦力/CP比率
                    float efficiency = card.cpCost > 0
                        ? (float)card.battlePower / (card.cpCost * 2000 + 1000)
                        : 0.5f;
                    score += efficiency * 20f;
                    // 願力ダメージボーナス
                    score += card.wishDamage * 2f;
                    // キーワードボーナス
                    if (card.HasKeyword("Rush")) score += 8f;
                    if (card.HasKeyword("Blocker")) score += 6f;
                    if (card.HasKeyword("Stealth")) score += 5f;
                    break;

                case CardType.Spell:
                    // 除去カードは重要
                    if (!string.IsNullOrEmpty(card.removeCondition)) score += 15f;
                    // ドローカード
                    if (card.drawCount > 0) score += card.drawCount * 8f;
                    // バウンス
                    if (card.bounceCount > 0) score += 10f;
                    break;

                case CardType.Algorithm:
                    // 界律は2枚程度でいいので基礎スコアを少し下げる
                    score -= 10f;
                    break;
            }

            // 低コストは序盤に重要
            if (card.cpCost <= 2) score += 5f;

            return score;
        }

        struct ScoredCard
        {
            public CardData card;
            public float score;
            public int maxCopies;
        }
    }
}
