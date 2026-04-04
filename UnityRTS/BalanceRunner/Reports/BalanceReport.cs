using BalanceRunner.Analysis;

namespace BalanceRunner.Reports
{
    /// <summary>
    /// Complete balance evaluation report aggregating all analysis results.
    /// </summary>
    public class BalanceReport
    {
        public int TotalMatches { get; set; }
        public WinRateMatrix WinRateMatrix { get; set; }
        public CostEfficiencyAnalysis CostEfficiency { get; set; }
        public DominanceDetector Dominance { get; set; }
        public DiversityAnalysis Diversity { get; set; }
        public TimingAnalysis Timing { get; set; }
        public CounterAnalysis Counters { get; set; }
    }
}
