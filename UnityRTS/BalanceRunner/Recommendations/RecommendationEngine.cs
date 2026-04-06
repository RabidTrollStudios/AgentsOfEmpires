using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using BalanceRunner.Analysis;
using BalanceRunner.Reports;

namespace BalanceRunner.Recommendations
{
    /// <summary>
    /// Rule-based balance issue detection engine.
    /// Analyzes a BalanceReport and produces ranked, actionable recommendations.
    /// </summary>
    public static class RecommendationEngine
    {
        /// <summary>
        /// Analyze a balance report and return ranked issues with tuning suggestions.
        /// </summary>
        public static List<BalanceIssue> Analyze(BalanceReport report)
        {
            var issues = new List<BalanceIssue>();

            CheckDominance(report, issues);
            CheckCounterBalance(report, issues);
            CheckUnitViability(report, issues);
            CheckCostEfficiency(report, issues);
            CheckTimingWindows(report, issues);

            // Sort by severity (Critical first)
            issues.Sort((a, b) => a.Severity.CompareTo(b.Severity));
            return issues;
        }

        private static void CheckDominance(BalanceReport report, List<BalanceIssue> issues)
        {
            foreach (var dominant in report.Dominance.DominantAgents)
            {
                bool isCritical = dominant.AggregateWinRate >= 0.90f;

                issues.Add(new BalanceIssue
                {
                    Severity = isCritical ? IssueSeverity.Critical : IssueSeverity.High,
                    Category = IssueCategory.Dominance,
                    Description = $"Strategy '{dominant.AgentName}' is dominant with {dominant.AggregateWinRate * 100:F0}% aggregate win rate.",
                    Evidence = { $"Wins {dominant.AggregateWinRate * 100:F0}% of all matchups against other strategies." },
                    Rationale = "A healthy metagame requires that no single strategy dominates all others. "
                              + "Examine which units this strategy relies on and consider increasing their cost or reducing their stats.",
                    FollowUpTest = $"--matchup \"{dominant.AgentName},WarriorRush,Balanced,Swarm\" --seeds 5"
                });
            }
        }

        private static void CheckCounterBalance(BalanceReport report, List<BalanceIssue> issues)
        {
            foreach (var agent in report.Counters.Uncounterable)
            {
                var counters = report.Counters.AgentCounterData.FirstOrDefault(c => c.AgentName == agent);
                int beatsCount = counters?.Beats.Count ?? 0;

                issues.Add(new BalanceIssue
                {
                    Severity = IssueSeverity.High,
                    Category = IssueCategory.CounterBalance,
                    Description = $"Strategy '{agent}' has no counter — beats {beatsCount} strategies, loses to none.",
                    Evidence =
                    {
                        $"Beats: {(counters != null ? string.Join(", ", counters.Beats) : "none")}",
                        "No strategy achieves >60% win rate against this agent."
                    },
                    Rationale = "Every strategy should have at least one viable counter for healthy metagame diversity.",
                    FollowUpTest = $"--matchup \"{agent},MixedArms,TechRush,HealerSupport\" --seeds 10"
                });
            }
        }

        private static void CheckUnitViability(BalanceReport report, List<BalanceIssue> issues)
        {
            foreach (var unitType in report.Diversity.NeverProduced)
            {
                float cost = GameConstants.COST[unitType];
                float hp = GameConstants.HEALTH[unitType];

                issues.Add(new BalanceIssue
                {
                    Severity = IssueSeverity.High,
                    Category = IssueCategory.UnitViability,
                    Description = $"{unitType} is never produced by any agent in the evaluation.",
                    Evidence = { "Zero production across all matches in the balance matrix." },
                    ConstantName = $"COST[{unitType}]",
                    CurrentValue = cost,
                    SuggestedValue = cost * 0.85f,
                    Rationale = $"Reducing {unitType} cost by 15% may make it more attractive relative to alternatives.",
                    FollowUpTest = $"Re-run matrix after cost adjustment and verify {unitType} appears in at least one winning strategy."
                });
            }

            foreach (var unitType in report.Diversity.OnlyInLosses)
            {
                float cost = GameConstants.COST[unitType];

                issues.Add(new BalanceIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.UnitViability,
                    Description = $"{unitType} is only produced in losing strategies.",
                    Evidence = { $"Produced across matches but never by a winning agent." },
                    ConstantName = $"COST[{unitType}]",
                    CurrentValue = cost,
                    Rationale = "This unit may be too expensive for its combat effectiveness, or the strategies using it are flawed.",
                    FollowUpTest = $"Run focused tests with pre-spawned equal-cost armies to isolate unit effectiveness."
                });
            }
        }

        private static void CheckCostEfficiency(BalanceReport report, List<BalanceIssue> issues)
        {
            var efficiencies = report.CostEfficiency.UnitEfficiencies
                .Where(e => e.MatchesWithUnit > 0)
                .ToList();

            if (efficiencies.Count < 2) return;

            // Check for extreme win-rate-when-produced deviations
            float meanWinRate = efficiencies.Average(e => e.WinRateWhenProduced);
            float stdDev = (float)Math.Sqrt(efficiencies.Average(e =>
                Math.Pow(e.WinRateWhenProduced - meanWinRate, 2)));

            foreach (var ue in efficiencies)
            {
                float zScore = stdDev > 0.01f
                    ? (ue.WinRateWhenProduced - meanWinRate) / stdDev
                    : 0;

                if (zScore > 1.5f)
                {
                    issues.Add(new BalanceIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Category = IssueCategory.CostEfficiency,
                        Description = $"{ue.UnitType} may be over-performing: {ue.WinRateWhenProduced * 100:F0}% win rate when produced (mean={meanWinRate * 100:F0}%).",
                        Evidence =
                        {
                            $"Cost={ue.Cost}g, HP/gold={ue.HpPerGold:F1}, DPS/gold={ue.DpsPerGold:F2}",
                            $"Produced {ue.TotalProduced} times, in {ue.WinsWithUnit}/{ue.MatchesWithUnit} winning matches"
                        },
                        ConstantName = $"COST[{ue.UnitType}]",
                        CurrentValue = ue.Cost,
                        SuggestedValue = ue.Cost * 1.15f,
                        Rationale = $"Increasing {ue.UnitType} cost by 15% would reduce its cost efficiency without changing combat feel."
                    });
                }
                else if (zScore < -1.5f)
                {
                    issues.Add(new BalanceIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Category = IssueCategory.CostEfficiency,
                        Description = $"{ue.UnitType} may be under-performing: {ue.WinRateWhenProduced * 100:F0}% win rate when produced (mean={meanWinRate * 100:F0}%).",
                        Evidence =
                        {
                            $"Cost={ue.Cost}g, HP/gold={ue.HpPerGold:F1}, DPS/gold={ue.DpsPerGold:F2}",
                            $"Produced {ue.TotalProduced} times, in {ue.WinsWithUnit}/{ue.MatchesWithUnit} winning matches"
                        },
                        ConstantName = $"COST[{ue.UnitType}]",
                        CurrentValue = ue.Cost,
                        SuggestedValue = ue.Cost * 0.85f,
                        Rationale = $"Decreasing {ue.UnitType} cost by 15% would improve its cost efficiency."
                    });
                }
            }
        }

        private static void CheckTimingWindows(BalanceReport report, List<BalanceIssue> issues)
        {
            if (report.Timing.EarlyEndRate > 0.50f)
            {
                issues.Add(new BalanceIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.TimingWindow,
                    Description = $"Rush strategies dominate: {report.Timing.EarlyEndRate * 100:F0}% of games end before frame 500.",
                    Evidence =
                    {
                        $"Median duration: {report.Timing.DurationStats?.Median ?? 0} frames",
                        "Most games are decided before eco strategies can develop."
                    },
                    Rationale = "Consider increasing early military costs or building times to extend the opening window."
                });
            }

            if (report.Timing.TimeoutRate > 0.30f)
            {
                issues.Add(new BalanceIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.TimingWindow,
                    Description = $"High stalemate rate: {report.Timing.TimeoutRate * 100:F0}% of games reach timeout.",
                    Evidence =
                    {
                        $"Median duration: {report.Timing.DurationStats?.Median ?? 0} frames",
                        "Many games fail to reach a decisive conclusion."
                    },
                    Rationale = "Consider reducing defensive building HP or increasing military unit damage to break stalemates."
                });
            }
        }
    }
}
