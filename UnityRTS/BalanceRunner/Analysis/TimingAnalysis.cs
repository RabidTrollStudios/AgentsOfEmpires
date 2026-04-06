using System;
using System.Collections.Generic;
using System.Linq;
using BalanceRunner.Telemetry;

namespace BalanceRunner.Analysis
{
    /// <summary>
    /// Analyzes game duration distributions and key timing milestones
    /// to identify rush/stalemate tendencies.
    /// </summary>
    public class TimingAnalysis
    {
        /// <summary>Game duration statistics (frames).</summary>
        public DistributionStats DurationStats { get; set; }

        /// <summary>First military unit timing statistics (frames).</summary>
        public DistributionStats FirstMilitaryStats { get; set; }

        /// <summary>First attack timing statistics (frames).</summary>
        public DistributionStats FirstAttackStats { get; set; }

        /// <summary>Percentage of games that ended in timeout.</summary>
        public float TimeoutRate { get; set; }

        /// <summary>Percentage of games ending before frame 500 (rush indicator).</summary>
        public float EarlyEndRate { get; set; }

        /// <summary>Matches ending by elimination vs base destruction vs timeout.</summary>
        public Dictionary<MatchEndReason, int> EndReasonCounts { get; set; }

        public static TimingAnalysis Compute(List<MatchResult> results)
        {
            var analysis = new TimingAnalysis();

            // Exclude self-play
            var matches = results.Where(r => r.Agent0Name != r.Agent1Name).ToList();
            if (matches.Count == 0) return analysis;

            // Duration distribution
            var durations = matches.Select(r => r.DurationFrames).ToList();
            analysis.DurationStats = DistributionStats.From(durations);

            // First military timing (exclude -1 = never)
            var firstMilitary = matches
                .SelectMany(r => new[] { r.Agent0Stats.FirstMilitaryFrame, r.Agent1Stats.FirstMilitaryFrame })
                .Where(t => t >= 0)
                .ToList();
            analysis.FirstMilitaryStats = DistributionStats.From(firstMilitary);

            // First attack timing
            var firstAttack = matches
                .SelectMany(r => new[] { r.Agent0Stats.FirstAttackFrame, r.Agent1Stats.FirstAttackFrame })
                .Where(t => t >= 0)
                .ToList();
            analysis.FirstAttackStats = DistributionStats.From(firstAttack);

            // Timeout and early end rates
            int timeouts = matches.Count(r => r.EndReason == MatchEndReason.Timeout);
            int earlyEnds = matches.Count(r => r.DurationFrames < 500);
            analysis.TimeoutRate = (float)timeouts / matches.Count;
            analysis.EarlyEndRate = (float)earlyEnds / matches.Count;

            // End reason breakdown
            analysis.EndReasonCounts = matches
                .GroupBy(r => r.EndReason)
                .ToDictionary(g => g.Key, g => g.Count());

            return analysis;
        }
    }

    /// <summary>Basic distribution statistics.</summary>
    public class DistributionStats
    {
        public int Count { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public float Mean { get; set; }
        public int Median { get; set; }
        public int P25 { get; set; }
        public int P75 { get; set; }

        public static DistributionStats From(List<int> values)
        {
            if (values == null || values.Count == 0)
                return new DistributionStats();

            values.Sort();
            return new DistributionStats
            {
                Count = values.Count,
                Min = values[0],
                Max = values[values.Count - 1],
                Mean = (float)values.Sum() / values.Count,
                Median = values[values.Count / 2],
                P25 = values[values.Count / 4],
                P75 = values[3 * values.Count / 4]
            };
        }
    }
}
