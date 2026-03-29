namespace Banganka.Core.Config
{
    public enum MatchMode
    {
        Standard,
        Quick
    }

    public struct MatchParams
    {
        public int hp;
        public int turnLimit;
        public float timerSeconds;
        public int startCP;
        public int[] wishThresholds;

        public MatchParams(int hp, int turnLimit, float timerSeconds, int startCP, int[] wishThresholds)
        {
            this.hp = hp;
            this.turnLimit = turnLimit;
            this.timerSeconds = timerSeconds;
            this.startCP = startCP;
            this.wishThresholds = wishThresholds;
        }
    }

    public static class MatchModeConfig
    {
        public static MatchMode CurrentMode { get; set; } = MatchMode.Standard;

        public static MatchParams Get(MatchMode mode) => mode switch
        {
            MatchMode.Quick => new MatchParams(
                hp: 60,
                turnLimit: 16,
                timerSeconds: 30f,
                startCP: 2,
                wishThresholds: new[] { 50, 40, 30, 20, 10, 5 }
            ),
            _ => new MatchParams(
                hp: BalanceConfig.MaxHP,
                turnLimit: BalanceConfig.TurnLimitTotal,
                timerSeconds: BalanceConfig.TurnTimerSeconds,
                startCP: 0,
                wishThresholds: new[] { 85, 70, 55, 40, 25, 10 }
            )
        };

        public static MatchParams Current => Get(CurrentMode);
    }
}
