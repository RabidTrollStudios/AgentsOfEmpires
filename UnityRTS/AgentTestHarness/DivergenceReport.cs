namespace AgentTestHarness
{
    /// <summary>
    /// Structured result of a parity test run.
    /// If DivergenceTick is -1, the scenario passed (all ticks matched).
    /// </summary>
    public class DivergenceReport
    {
        public string ScenarioName { get; set; }
        public int DivergenceTick { get; set; } = -1;
        public long ExpectedHash { get; set; }
        public long ActualHash { get; set; }
        public int TotalTicks { get; set; }
        public bool Passed => DivergenceTick == -1;

        public override string ToString()
        {
            if (Passed)
                return $"[PASS] {ScenarioName} ({TotalTicks} ticks)";

            return $"[FAIL] {ScenarioName}: diverged at tick {DivergenceTick}/{TotalTicks} " +
                   $"(expected 0x{ExpectedHash:X16}, got 0x{ActualHash:X16})";
        }
    }
}
