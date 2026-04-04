using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BalanceRunner.Analysis;
using BalanceRunner.Recommendations;
using BalanceRunner.Telemetry;

namespace BalanceRunner.Reports
{
    /// <summary>
    /// Formats a BalanceReport for console output and JSON serialization.
    /// </summary>
    public static class ReportFormatter
    {
        /// <summary>Print the full report to console.</summary>
        public static void PrintToConsole(BalanceReport report, long elapsedMs)
        {
            Console.WriteLine($"\n{"=",-60}");
            Console.WriteLine($"  BALANCE REPORT — {report.TotalMatches} matches ({elapsedMs}ms)");
            Console.WriteLine($"{"=",-60}\n");

            PrintWinRateMatrix(report.WinRateMatrix);
            PrintDominance(report.Dominance);
            PrintCostEfficiency(report.CostEfficiency);
            PrintDiversity(report.Diversity);
            PrintTiming(report.Timing);
            PrintCounters(report.Counters);
        }

        /// <summary>Print recommendations to console.</summary>
        public static void PrintRecommendations(List<BalanceIssue> issues)
        {
            Console.WriteLine("  BALANCE RECOMMENDATIONS");
            Console.WriteLine($"  {new string('-', 50)}");

            if (issues.Count == 0)
            {
                Console.WriteLine("  No balance issues detected.");
                Console.WriteLine();
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                var issue = issues[i];
                Console.WriteLine($"  [{issue.Severity}] {issue.Category}: {issue.Description}");

                foreach (var ev in issue.Evidence)
                    Console.WriteLine($"    Evidence: {ev}");

                if (issue.ConstantName != null)
                {
                    Console.Write($"    Constant: {issue.ConstantName}");
                    if (issue.CurrentValue.HasValue)
                        Console.Write($" = {issue.CurrentValue.Value:F0}");
                    if (issue.SuggestedValue.HasValue)
                        Console.Write($" -> suggested {issue.SuggestedValue.Value:F0}");
                    Console.WriteLine();
                }

                if (issue.Rationale != null)
                    Console.WriteLine($"    Rationale: {issue.Rationale}");
                if (issue.FollowUpTest != null)
                    Console.WriteLine($"    Follow-up: BalanceRunner {issue.FollowUpTest}");

                Console.WriteLine();
            }
        }

        private static void PrintWinRateMatrix(WinRateMatrix matrix)
        {
            Console.WriteLine("  WIN RATE MATRIX");
            Console.WriteLine($"  {new string('-', 50)}");

            var names = matrix.AgentNames;
            int nameWidth = Math.Max(14, names.Max(n => n.Length) + 1);

            // Header row
            Console.Write("  " + "vs".PadRight(nameWidth));
            foreach (var name in names)
                Console.Write(Abbreviate(name).PadLeft(8));
            Console.Write("     Agg");
            Console.WriteLine();

            // Data rows
            foreach (var row in names)
            {
                Console.Write("  " + row.PadRight(nameWidth));
                foreach (var col in names)
                {
                    if (row == col)
                    {
                        Console.Write("     ---");
                    }
                    else
                    {
                        float rate = matrix.GetRate(row, col);
                        Console.Write(rate >= 0 ? $"{rate * 100,7:F0}%" : "       ?");
                    }
                }
                float agg = matrix.AggregateWinRates.TryGetValue(row, out float v) ? v : 0;
                Console.Write($"{agg * 100,7:F0}%");
                Console.WriteLine();
            }
            Console.WriteLine();
        }

        private static void PrintDominance(DominanceDetector dominance)
        {
            Console.WriteLine("  DOMINANCE DETECTION");
            Console.WriteLine($"  {new string('-', 50)}");

            if (dominance.DominantAgents.Count > 0)
            {
                Console.WriteLine("  Dominant (>75% win rate):");
                foreach (var d in dominance.DominantAgents)
                    Console.WriteLine($"    {d.AgentName}: {d.AggregateWinRate * 100:F0}%");
            }
            if (dominance.WeakAgents.Count > 0)
            {
                Console.WriteLine("  Weak (<25% win rate):");
                foreach (var d in dominance.WeakAgents)
                    Console.WriteLine($"    {d.AgentName}: {d.AggregateWinRate * 100:F0}%");
            }
            if (dominance.DominantAgents.Count == 0 && dominance.WeakAgents.Count == 0)
                Console.WriteLine("  No dominant or weak strategies detected.");
            Console.WriteLine();
        }

        private static void PrintCostEfficiency(CostEfficiencyAnalysis costEff)
        {
            Console.WriteLine("  UNIT COST EFFICIENCY");
            Console.WriteLine($"  {new string('-', 50)}");
            Console.WriteLine($"  {"Unit",-10} {"Cost",5} {"HP/g",6} {"DPS/g",6} {"Prod",6} {"WinRate",8}");

            foreach (var ue in costEff.UnitEfficiencies)
            {
                string wr = ue.MatchesWithUnit > 0
                    ? $"{ue.WinRateWhenProduced * 100:F0}%"
                    : "n/a";
                Console.WriteLine($"  {ue.UnitType,-10} {ue.Cost,5} {ue.HpPerGold,6:F1} {ue.DpsPerGold,6:F2} {ue.TotalProduced,6} {wr,8}");
            }
            Console.WriteLine();
        }

        private static void PrintDiversity(DiversityAnalysis diversity)
        {
            Console.WriteLine("  UNIT DIVERSITY");
            Console.WriteLine($"  {new string('-', 50)}");

            foreach (var uv in diversity.UnitViabilities)
            {
                string status = uv.TotalProduced == 0 ? "NEVER PRODUCED"
                              : uv.ProducedInWins == 0 ? "ONLY IN LOSSES"
                              : "Viable";
                Console.WriteLine($"  {uv.UnitType,-10} {status,-16} produced={uv.TotalProduced} by {uv.ProducingAgentCount} agents ({uv.WinningAgentCount} winning)");
            }

            if (diversity.NeverProduced.Count > 0)
                Console.WriteLine($"\n  WARNING: Never produced: {string.Join(", ", diversity.NeverProduced)}");
            if (diversity.OnlyInLosses.Count > 0)
                Console.WriteLine($"  WARNING: Only in losses: {string.Join(", ", diversity.OnlyInLosses)}");
            Console.WriteLine();
        }

        private static void PrintTiming(TimingAnalysis timing)
        {
            Console.WriteLine("  TIMING ANALYSIS");
            Console.WriteLine($"  {new string('-', 50)}");

            if (timing.DurationStats != null && timing.DurationStats.Count > 0)
            {
                var d = timing.DurationStats;
                Console.WriteLine($"  Duration:  median={d.Median}  mean={d.Mean:F0}  range=[{d.Min}-{d.Max}]  p25={d.P25}  p75={d.P75}");
            }
            if (timing.FirstMilitaryStats != null && timing.FirstMilitaryStats.Count > 0)
            {
                var m = timing.FirstMilitaryStats;
                Console.WriteLine($"  1st Military: median={m.Median}  range=[{m.Min}-{m.Max}]");
            }
            if (timing.FirstAttackStats != null && timing.FirstAttackStats.Count > 0)
            {
                var a = timing.FirstAttackStats;
                Console.WriteLine($"  1st Attack:   median={a.Median}  range=[{a.Min}-{a.Max}]");
            }

            Console.WriteLine($"  Timeout rate: {timing.TimeoutRate * 100:F0}%  Early end (<500t): {timing.EarlyEndRate * 100:F0}%");

            if (timing.EndReasonCounts != null)
            {
                var reasons = string.Join(", ", timing.EndReasonCounts.Select(kv => $"{kv.Key}={kv.Value}"));
                Console.WriteLine($"  End reasons: {reasons}");
            }
            Console.WriteLine();
        }

        private static void PrintCounters(CounterAnalysis counters)
        {
            Console.WriteLine("  COUNTER ANALYSIS");
            Console.WriteLine($"  {new string('-', 50)}");

            foreach (var ac in counters.AgentCounterData)
            {
                if (ac.Beats.Count == 0 && ac.LosesTo.Count == 0) continue;

                var beats = ac.Beats.Count > 0 ? string.Join(", ", ac.Beats) : "(none)";
                var losesTo = ac.LosesTo.Count > 0 ? string.Join(", ", ac.LosesTo) : "(none)";
                Console.WriteLine($"  {ac.AgentName,-14} beats: {beats}");
                Console.WriteLine($"  {"",14} loses: {losesTo}");
            }

            if (counters.Uncounterable.Count > 0)
                Console.WriteLine($"\n  WARNING: Uncounterable agents: {string.Join(", ", counters.Uncounterable)}");
            if (counters.CountersNothing.Count > 0)
                Console.WriteLine($"  WARNING: Counters nothing: {string.Join(", ", counters.CountersNothing)}");
            Console.WriteLine();
        }

        /// <summary>Write the report as JSON to a file or stdout.</summary>
        public static void WriteJson(BalanceReport report, List<MatchResult> results,
            long elapsedMs, string jsonFile, List<BalanceIssue> recommendations = null)
        {
            // Build win rate matrix as nested dictionary for easy JS consumption
            var winRateGrid = new Dictionary<string, Dictionary<string, float>>();
            foreach (var a in report.WinRateMatrix.AgentNames)
            {
                winRateGrid[a] = new Dictionary<string, float>();
                foreach (var b in report.WinRateMatrix.AgentNames)
                {
                    if (a == b) continue;
                    float rate = report.WinRateMatrix.GetRate(a, b);
                    if (rate >= 0) winRateGrid[a][b] = rate;
                }
            }

            // Game constants snapshot for the dashboard
            var unitTypes = new[] { AgentSDK.UnitType.WARRIOR, AgentSDK.UnitType.ARCHER,
                                    AgentSDK.UnitType.LANCER, AgentSDK.UnitType.MONK, AgentSDK.UnitType.PAWN };
            var buildingTypes = new[] { AgentSDK.UnitType.BASE, AgentSDK.UnitType.BARRACKS,
                                       AgentSDK.UnitType.ARCHERY, AgentSDK.UnitType.TOWER, AgentSDK.UnitType.MONASTERY };

            var unitStats = new List<object>();
            foreach (var ut in unitTypes)
            {
                unitStats.Add(new
                {
                    UnitType = ut.ToString(),
                    Cost = AgentSDK.GameConstants.COST[ut],
                    Health = AgentSDK.GameConstants.HEALTH[ut],
                    BaseDamage = AgentSDK.GameConstants.BASE_DAMAGE[ut],
                    AttackRange = AgentSDK.GameConstants.ATTACK_RANGE[ut],
                    Speed = AgentSDK.DerivedGameConstants.SPEED_MULTIPLIER[ut],
                    TrainTime = AgentSDK.GameConstants.CREATION_TIME_MULTIPLIER[ut],
                    HpPerGold = AgentSDK.GameConstants.HEALTH[ut] / AgentSDK.GameConstants.COST[ut],
                    DpsPerGold = AgentSDK.GameConstants.BASE_DAMAGE[ut] / AgentSDK.GameConstants.COST[ut]
                });
            }

            var buildingStats = new List<object>();
            foreach (var bt in buildingTypes)
            {
                var trains = AgentSDK.GameConstants.TRAINS.ContainsKey(bt)
                    ? AgentSDK.GameConstants.TRAINS[bt] : (IReadOnlyList<AgentSDK.UnitType>)new List<AgentSDK.UnitType>();
                var requires = AgentSDK.GameConstants.DEPENDENCY.ContainsKey(bt)
                    ? AgentSDK.GameConstants.DEPENDENCY[bt] : (IReadOnlyList<AgentSDK.UnitType>)new List<AgentSDK.UnitType>();
                var size = AgentSDK.GameConstants.UNIT_SIZE[bt];
                buildingStats.Add(new
                {
                    UnitType = bt.ToString(),
                    Cost = AgentSDK.GameConstants.COST[bt],
                    Health = AgentSDK.GameConstants.HEALTH[bt],
                    BuildTime = AgentSDK.GameConstants.CREATION_TIME_MULTIPLIER[bt],
                    Size = $"{size.X}x{size.Y}",
                    Trains = trains.Select(t => t.ToString()).ToList(),
                    Requires = requires.Select(t => t.ToString()).ToList()
                });
            }

            // RPS multipliers
            var rps = new[]
            {
                new { Attacker = "WARRIOR", Defender = "ARCHER", Multiplier = 1.25f },
                new { Attacker = "ARCHER", Defender = "WARRIOR", Multiplier = 0.75f },
                new { Attacker = "ARCHER", Defender = "LANCER", Multiplier = 1.25f },
                new { Attacker = "LANCER", Defender = "ARCHER", Multiplier = 0.75f },
                new { Attacker = "LANCER", Defender = "WARRIOR", Multiplier = 1.25f },
                new { Attacker = "WARRIOR", Defender = "LANCER", Multiplier = 0.75f },
            };

            var monkStats = new
            {
                MaxMana = AgentSDK.GameConstants.MAX_MANA[AgentSDK.UnitType.MONK],
                ManaCost = AgentSDK.GameConstants.MANA_COST,
                HealAmount = AgentSDK.GameConstants.HEAL_AMOUNT,
                HealRange = AgentSDK.GameConstants.HEAL_RANGE[AgentSDK.UnitType.MONK],
                BaseManaRegen = AgentSDK.GameConstants.BASE_MANA_REGEN
            };

            // Counter data
            var counterData = report.Counters.AgentCounterData.ConvertAll(ac => new
            {
                ac.AgentName,
                ac.Beats,
                ac.LosesTo
            });

            // Aggregate timeline data per agent (average across all matches)
            var agentTimelines = AggregateTimelines(results);

            var output = new
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                ElapsedMs = elapsedMs,
                TotalMatches = report.TotalMatches,
                AgentNames = report.WinRateMatrix.AgentNames,
                AggregateWinRates = report.WinRateMatrix.AggregateWinRates,
                WinRateMatrix = winRateGrid,
                DominantAgents = report.Dominance.DominantAgents,
                WeakAgents = report.Dominance.WeakAgents,
                UnitEfficiencies = report.CostEfficiency.UnitEfficiencies,
                UnitStats = unitStats,
                BuildingStats = buildingStats,
                RpsMultipliers = rps,
                MonkStats = monkStats,
                Diversity = report.Diversity.UnitViabilities,
                NeverProducedUnits = report.Diversity.NeverProduced,
                Timing = new
                {
                    report.Timing.DurationStats,
                    report.Timing.TimeoutRate,
                    report.Timing.EarlyEndRate,
                    report.Timing.EndReasonCounts
                },
                Counters = counterData,
                Uncounterable = report.Counters.Uncounterable,
                CountersNothing = report.Counters.CountersNothing,
                Recommendations = recommendations,
                AgentTimelines = agentTimelines
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };

            string json = JsonSerializer.Serialize(output, options);

            if (jsonFile != null)
            {
                // Ensure directory exists
                var dir = Path.GetDirectoryName(jsonFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(jsonFile, json);
                Console.WriteLine($"JSON report written to: {jsonFile}");
            }
            else
            {
                Console.WriteLine(json);
            }
        }

        /// <summary>
        /// Aggregate timeline snapshots per agent across all matches.
        /// Averages each metric at each tick bucket.
        /// </summary>
        private static Dictionary<string, object> AggregateTimelines(List<MatchResult> results)
        {
            // Collect all timeline snapshots per agent name
            var agentSnapshots = new Dictionary<string, List<List<Telemetry.TimelineSnapshot>>>();
            var agentMilestones = new Dictionary<string, List<Telemetry.MilestoneEvent>>();

            foreach (var r in results)
            {
                for (int a = 0; a < 2; a++)
                {
                    string name = r.GetAgentName(a);
                    var stats = r.GetStats(a);
                    if (stats.Timeline.Snapshots.Count == 0) continue;

                    if (!agentSnapshots.ContainsKey(name))
                    {
                        agentSnapshots[name] = new List<List<Telemetry.TimelineSnapshot>>();
                        agentMilestones[name] = new List<Telemetry.MilestoneEvent>();
                    }
                    agentSnapshots[name].Add(stats.Timeline.Snapshots);
                    agentMilestones[name].AddRange(stats.Timeline.Milestones);
                }
            }

            var output = new Dictionary<string, object>();

            foreach (var kvp in agentSnapshots)
            {
                string name = kvp.Key;
                var allRuns = kvp.Value;

                // Find all unique ticks across runs
                var tickSet = new HashSet<int>();
                foreach (var run in allRuns)
                    foreach (var snap in run)
                        tickSet.Add(snap.Tick);

                var ticks = tickSet.OrderBy(t => t).ToList();

                // Average each metric at each tick
                var avgSnapshots = new List<object>();
                foreach (int tick in ticks)
                {
                    int count = 0;
                    float gold = 0, goldMined = 0, goldSpent = 0, pawnCount = 0;
                    float armyValue = 0, totalHp = 0, totalMaxHp = 0;
                    float enemyKilled = 0, ownLost = 0;
                    var unitTotals = new Dictionary<string, float>();

                    foreach (var run in allRuns)
                    {
                        var snap = run.FirstOrDefault(s => s.Tick == tick);
                        if (snap == null) continue;
                        count++;
                        gold += snap.Gold;
                        goldMined += snap.GoldMined;
                        goldSpent += snap.GoldSpent;
                        pawnCount += snap.PawnCount;
                        armyValue += snap.ArmyValue;
                        totalHp += snap.TotalHp;
                        totalMaxHp += snap.TotalMaxHp;
                        enemyKilled += snap.EnemyGoldKilled;
                        ownLost += snap.OwnGoldLost;

                        foreach (var uc in snap.UnitCounts)
                        {
                            string key = uc.Key.ToString();
                            if (!unitTotals.ContainsKey(key)) unitTotals[key] = 0;
                            unitTotals[key] += uc.Value;
                        }
                    }

                    if (count == 0) continue;

                    var avgUnits = new Dictionary<string, float>();
                    foreach (var uc in unitTotals)
                        avgUnits[uc.Key] = uc.Value / count;

                    float hpPct = totalMaxHp > 0 ? (totalHp / totalMaxHp) * 100f : 0f;

                    avgSnapshots.Add(new
                    {
                        Tick = tick,
                        Gold = gold / count,
                        GoldMined = goldMined / count,
                        GoldSpent = goldSpent / count,
                        PawnCount = pawnCount / count,
                        ArmyValue = armyValue / count,
                        HpPercent = hpPct,
                        EnemyGoldKilled = enemyKilled / count,
                        OwnGoldLost = ownLost / count,
                        UnitCounts = avgUnits
                    });
                }

                // Summarize milestones (median tick for each type)
                var milestonesByType = agentMilestones[name]
                    .GroupBy(m => m.Type)
                    .Select(g =>
                    {
                        var sorted = g.Select(m => m.Tick).OrderBy(t => t).ToList();
                        return new
                        {
                            Type = g.Key,
                            MedianTick = sorted[sorted.Count / 2],
                            MinTick = sorted[0],
                            MaxTick = sorted[sorted.Count - 1],
                            Count = sorted.Count
                        };
                    })
                    .ToList();

                output[name] = new
                {
                    Snapshots = avgSnapshots,
                    Milestones = milestonesByType
                };
            }

            return output;
        }

        private static string Abbreviate(string name)
        {
            if (name.Length <= 7) return name;
            return name.Substring(0, 6) + ".";
        }
    }
}
