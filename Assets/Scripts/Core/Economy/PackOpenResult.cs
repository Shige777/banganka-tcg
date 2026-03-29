using System.Collections.Generic;
using Banganka.Core.Data;

namespace Banganka.Core.Economy
{
    /// <summary>
    /// パック開封1枚分の結果データ
    /// </summary>
    public class PackCardEntry
    {
        public CardData card;
        public string rarity;
        public bool isNew;        // 未所持カードだったか
        public bool isDuplicate;  // 3枚超の重複か（ゴールド変換対象）
        public int goldConverted; // 重複変換で得たゴールド
        public int slotIndex;     // 0-4（パック内の位置）
    }

    /// <summary>
    /// パック開封全体の結果データ (PACK_OPENING_SPEC.md §10)
    /// </summary>
    public class PackOpenResult
    {
        public List<PackCardEntry> cards = new();
        public int totalGoldConverted;
        public bool hasPityTriggered;

        /// <summary>パック内の最高レアリティを返す</summary>
        public string MaxRarity
        {
            get
            {
                int max = 0;
                foreach (var entry in cards)
                {
                    int rank = RarityRank(entry.rarity);
                    if (rank > max) max = rank;
                }
                return max switch
                {
                    3 => "SSR",
                    2 => "SR",
                    1 => "R",
                    _ => "C"
                };
            }
        }

        static int RarityRank(string rarity) => rarity switch
        {
            "SSR" => 3,
            "SR" => 2,
            "R" => 1,
            _ => 0
        };
    }
}
