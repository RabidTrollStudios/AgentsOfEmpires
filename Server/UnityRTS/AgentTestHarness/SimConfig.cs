namespace AgentTestHarness
{
    /// <summary>
    /// Configurable game parameters for the simulation.
    /// Defaults match the real game at GAME_SPEED = 20.
    /// </summary>
    public class SimConfig
    {
        /// <summary>Grid width in cells.</summary>
        public int MapWidth { get; set; } = 30;

        /// <summary>Grid height in cells.</summary>
        public int MapHeight { get; set; } = 30;

        /// <summary>Starting gold for each agent.</summary>
        public int StartingGold { get; set; } = 1000;

        /// <summary>Starting gold in each mine (also used as mine health).</summary>
        public int StartingMineGold { get; set; } = 10000;

        /// <summary>
        /// Game speed multiplier. Controls movement speed, damage, creation time, etc.
        /// Matches Unity Constants.GAME_SPEED.
        /// </summary>
        public int GameSpeed { get; set; } = 20;

        /// <summary>
        /// Simulated seconds per tick. Default 0.05s = 20 ticks per second.
        /// </summary>
        public float TickDuration { get; set; } = 0.05f;
    }
}
