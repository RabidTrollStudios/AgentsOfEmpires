namespace AgentTestHarness
{
    /// <summary>
    /// Structured result of a parity test run.
    /// If DivergenceFrame is -1, the scenario passed (all frames matched).
    /// </summary>
    public class DivergenceReport
    {
        public string ScenarioName { get; set; }
        public int DivergenceFrame { get; set; } = -1;
        public long ExpectedHash { get; set; }
        public long ActualHash { get; set; }
        public int TotalFrames { get; set; }
        public bool Passed => DivergenceFrame == -1;

        public override string ToString()
        {
            if (Passed)
                return $"[PASS] {ScenarioName} ({TotalFrames} frames)";

            return $"[FAIL] {ScenarioName}: diverged at frame {DivergenceFrame}/{TotalFrames} " +
                   $"(expected 0x{ExpectedHash:X16}, got 0x{ActualHash:X16})";
        }
    }
}
