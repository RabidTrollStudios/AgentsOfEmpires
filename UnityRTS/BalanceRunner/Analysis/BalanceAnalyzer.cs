using System.Collections.Generic;
using BalanceRunner.Reports;
using BalanceRunner.Telemetry;

namespace BalanceRunner.Analysis
{
    /// <summary>
    /// Orchestrates all analysis modules and produces a complete BalanceReport.
    /// </summary>
    public static class BalanceAnalyzer
    {
        /// <summary>
        /// Run all analysis modules on a batch of match results.
        /// </summary>
        public static BalanceReport Analyze(List<MatchResult> results)
        {
            var winRateMatrix = WinRateMatrix.Compute(results);
            var costEfficiency = CostEfficiencyAnalysis.Compute(results);
            var dominance = DominanceDetector.Compute(winRateMatrix);
            var diversity = DiversityAnalysis.Compute(results);
            var timing = TimingAnalysis.Compute(results);
            var counters = CounterAnalysis.Compute(winRateMatrix);

            return new BalanceReport
            {
                TotalMatches = results.Count,
                WinRateMatrix = winRateMatrix,
                CostEfficiency = costEfficiency,
                Dominance = dominance,
                Diversity = diversity,
                Timing = timing,
                Counters = counters
            };
        }
    }
}
