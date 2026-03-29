namespace Banganka.Core.Config
{
    public static class BalanceConfig
    {
        // HP System (GAME_DESIGN.md §4.1)
        public const int MaxHP = 100;
        public static readonly int[] WishThresholds = { 85, 70, 55, 40, 25, 10 };
        public const int WishCardSlotCount = 6;

        public const int TurnLimitTotal = 24;
        public const int MaxCPCap = 10;
        public const int InitialHand = 5;
        public const int DrawPerTurn = 1;
        public const int FieldFrontSize = 3;
        public const int FieldBackSize = 3;
        public const int FieldTotalSize = FieldFrontSize + FieldBackSize;
        public const string LeaderKeyAspect = "Contest";
        public const int LeaderBasePower = 5000;
        public const int LeaderBaseWishDamage = 1;
        public const int EvoGaugeMaxLv1To2 = 3;
        public const int EvoGaugeMaxLv2To3 = 4;
        public const int LevelUpPowerGain = 1000;
        public const int LevelUpWishDamageGain = 1;
        public const int LeaderMaxLevel = 3;
        public const int WishThresholdEvoGain = 1; // 願力カード閾値踏み時の願成ゲージ増加量
        public const bool GaugeVisibleNumbers = false;

        // ターンタイマー (GAME_DESIGN.md §10.1a)
        // v0.5.5: 後攻補正（バランスシミュレーション108,000戦に基づく）
        public const float TurnTimerSeconds = 60f;
        public const int ConsecutiveTimeoutLimit = 3;
        public const int SecondPlayerExtraCards = 1;

        // ブロック選択タイムアウト (GAME_DESIGN.md §10.5)
        public const float BlockSelectionTimeout = 5f;

        public const int DeckSize = 34;
        public const int WishCardCount = 30;
        public const int AlgorithmCardCount = 4;
        public const int SameNameLimit = 3;
    }
}
