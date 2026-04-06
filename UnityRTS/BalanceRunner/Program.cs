using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AgentSDK;
using BalanceRunner.Analysis;
using BalanceRunner.Config;
using BalanceRunner.Recommendations;
using BalanceRunner.Reports;
using BalanceRunner.Runner;
using BalanceRunner.Telemetry;

namespace BalanceRunner
{
    class Program
    {
        static int Main(string[] args)
        {
            string singleMatchup = null;
            string matrixMode = null;
            string matchupList = null;
            int seed = 42;
            int seeds = 5;
            int frameLimit = 5000;
            string jsonFile = null;
            bool jsonOutput = false;
            bool recommend = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--recommend":
                        recommend = true;
                        break;
                    case "--single" when i + 1 < args.Length:
                        singleMatchup = args[++i];
                        break;
                    case "--matrix" when i + 1 < args.Length:
                        matrixMode = args[++i];
                        break;
                    case "--matchup" when i + 1 < args.Length:
                        matchupList = args[++i];
                        break;
                    case "--seed" when i + 1 < args.Length:
                        seed = int.Parse(args[++i]);
                        break;
                    case "--seeds" when i + 1 < args.Length:
                        seeds = int.Parse(args[++i]);
                        break;
                    case "--frames" when i + 1 < args.Length:
                        frameLimit = int.Parse(args[++i]);
                        break;
                    case "--json":
                        jsonOutput = true;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                            jsonFile = args[++i];
                        break;
                    case "--list":
                        ListAgents();
                        return 0;
                    case "--help":
                    case "-h":
                        PrintUsage();
                        return 0;
                }
            }

            if (singleMatchup != null)
                return RunSingle(singleMatchup, seed, frameLimit, jsonOutput, jsonFile);

            if (matrixMode != null || matchupList != null)
                return RunBatch(matrixMode, matchupList, seeds, frameLimit, jsonOutput, jsonFile, recommend);

            Console.Error.WriteLine("No mode specified. Use --single, --matrix, --matchup, or --list. See --help.");
            return 1;
        }

        static int RunSingle(string matchup, int seed, int frameLimit, bool jsonOutput, string jsonFile)
        {
            var parts = matchup.Split(',');
            if (parts.Length != 2)
            {
                Console.Error.WriteLine("--single requires exactly two agent names separated by comma.");
                Console.Error.WriteLine("Example: --single \"WarriorRush,Turtle\"");
                return 1;
            }

            string name0 = parts[0].Trim();
            string name1 = parts[1].Trim();

            if (!AgentRegistry.Exists(name0))
            {
                Console.Error.WriteLine($"Unknown agent: '{name0}'. Use --list to see available agents.");
                return 1;
            }
            if (!AgentRegistry.Exists(name1))
            {
                Console.Error.WriteLine($"Unknown agent: '{name1}'. Use --list to see available agents.");
                return 1;
            }

            Console.WriteLine($"BalanceRunner: {name0} vs {name1} (seed={seed}, frames={frameLimit})");
            Console.WriteLine(new string('=', 60));

            var sw = Stopwatch.StartNew();
            var result = MatchRunner.Run(
                name0, AgentRegistry.Create(name0),
                name1, AgentRegistry.Create(name1),
                seed, frameLimit);
            sw.Stop();

            PrintMatchResult(result, sw.ElapsedMilliseconds);

            if (jsonOutput || jsonFile != null)
                WriteJson(result, sw.ElapsedMilliseconds, jsonFile);

            return 0;
        }

        static int RunBatch(string matrixMode, string matchupList, int seeds, int frameLimit,
            bool jsonOutput, string jsonFile, bool recommend)
        {
            var config = new RunConfig
            {
                SeedCount = seeds,
                FrameLimit = frameLimit
            };

            // Determine agent list
            if (matchupList != null)
            {
                config.Agents = matchupList.Split(',').Select(s => s.Trim()).ToList();
            }
            else if (matrixMode != null && !matrixMode.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                config.Agents = matrixMode.Split(',').Select(s => s.Trim()).ToList();
            }
            // else: empty list = all agents

            // Validate agents
            foreach (var name in config.Agents)
            {
                if (!AgentRegistry.Exists(name))
                {
                    Console.Error.WriteLine($"Unknown agent: '{name}'. Use --list to see available agents.");
                    return 1;
                }
            }

            int agentCount = config.Agents.Count > 0 ? config.Agents.Count : AgentRegistry.All.Count;
            Console.WriteLine($"BalanceRunner: {agentCount} agents, {seeds} seeds, {frameLimit} frame limit");
            Console.WriteLine(new string('=', 60));

            var sw = Stopwatch.StartNew();
            var results = BatchRunner.Run(config, (idx, total, result) =>
            {
                string winner = result.Winner >= 0 ? result.GetAgentName(result.Winner) : "Draw";
                Console.Write($"\r  [{idx}/{total}] {result.Agent0Name} vs {result.Agent1Name}: {winner} ({result.DurationFrames}t)");
            });
            sw.Stop();

            Console.WriteLine($"\r  Completed {results.Count} matches in {sw.ElapsedMilliseconds}ms{new string(' ', 40)}");

            // Run analysis
            var report = BalanceAnalyzer.Analyze(results);
            ReportFormatter.PrintToConsole(report, sw.ElapsedMilliseconds);

            // Always compute recommendations for JSON; only print if --recommend
            var issues = RecommendationEngine.Analyze(report);
            if (recommend)
                ReportFormatter.PrintRecommendations(issues);

            if (jsonOutput || jsonFile != null)
                ReportFormatter.WriteJson(report, results, sw.ElapsedMilliseconds, jsonFile, issues);

            return 0;
        }

        static void PrintMatchResult(MatchResult result, long elapsedMs)
        {
            string winnerName = result.Winner >= 0
                ? result.GetAgentName(result.Winner)
                : "Draw";

            Console.WriteLine($"  Winner:   {winnerName}");
            Console.WriteLine($"  Reason:   {result.EndReason}");
            Console.WriteLine($"  Duration: {result.DurationFrames} frames ({elapsedMs}ms wall)");
            Console.WriteLine();

            for (int a = 0; a < 2; a++)
            {
                var stats = result.GetStats(a);
                string name = result.GetAgentName(a);
                string marker = result.Winner == a ? " [W]" : "";

                Console.WriteLine($"  {name}{marker}:");
                Console.WriteLine($"    Gold:  final={stats.FinalGold}  mined={stats.GoldMined}  spent={stats.GoldSpent}");
                Console.WriteLine($"    Peak army value: {stats.PeakArmyValue}g");

                var produced = FormatUnitCounts(stats.UnitsProduced);
                var lost = FormatUnitCounts(stats.UnitsLost);
                var surviving = FormatUnitCounts(stats.SurvivingUnits);

                Console.WriteLine($"    Produced:  {produced}");
                Console.WriteLine($"    Lost:      {lost}");
                Console.WriteLine($"    Surviving: {surviving} ({stats.SurvivingHpPercent:F1}% HP)");

                Console.WriteLine($"    Timings:   military@{FormatFrame(stats.FirstMilitaryFrame)}  attack@{FormatFrame(stats.FirstAttackFrame)}  kill@{FormatFrame(stats.FirstKillFrame)}");
                Console.WriteLine();
            }
        }

        static string FormatUnitCounts(Dictionary<UnitType, int> counts)
        {
            if (counts == null || counts.Count == 0) return "(none)";
            var sb = new StringBuilder();
            foreach (var kvp in counts.OrderBy(k => k.Key))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append($"{kvp.Key}={kvp.Value}");
            }
            return sb.ToString();
        }

        static string FormatFrame(int frame)
        {
            return frame >= 0 ? frame.ToString() : "never";
        }

        static void WriteJson(MatchResult result, long elapsedMs, string jsonFile)
        {
            var output = new
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                ElapsedMs = elapsedMs,
                Match = result
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

            string json = JsonSerializer.Serialize(output, options);

            if (jsonFile != null)
            {
                File.WriteAllText(jsonFile, json);
                Console.WriteLine($"JSON written to: {jsonFile}");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine(json);
            }
        }

        static void ListAgents()
        {
            Console.WriteLine($"Available agents ({AgentRegistry.All.Count}):");
            foreach (var name in AgentRegistry.All.Keys.OrderBy(n => n))
                Console.WriteLine($"  {name}");
        }

        static void PrintUsage()
        {
            Console.WriteLine("BalanceRunner - Balance evaluation tool for Warcrap");
            Console.WriteLine();
            Console.WriteLine("Usage: BalanceRunner [options]");
            Console.WriteLine();
            Console.WriteLine("Modes:");
            Console.WriteLine("  --single \"A,B\"        Run a single match between agents A and B");
            Console.WriteLine("  --matrix all          Run all-vs-all matchup matrix with analysis");
            Console.WriteLine("  --matchup \"A,B,C\"     Run matrix for a subset of agents");
            Console.WriteLine("  --list                List all available agents");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --seed <n>            Map generation seed for --single (default: 42)");
            Console.WriteLine("  --seeds <n>           Seeds per matchup for --matrix (default: 5)");
            Console.WriteLine("  --frames <n>           Max frames per match (default: 5000)");
            Console.WriteLine("  --recommend           Include balance recommendations in output");
            Console.WriteLine("  --json [file]         Output JSON report (to stdout or file)");
            Console.WriteLine("  --help, -h            Show this help");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  BalanceRunner --single \"WarriorRush,Turtle\" --seed 42 --frames 5000");
            Console.WriteLine("  BalanceRunner --matrix all --seeds 3 --frames 3000");
            Console.WriteLine("  BalanceRunner --matchup \"WarriorRush,Turtle,Balanced\" --seeds 10");
            Console.WriteLine("  BalanceRunner --matrix all --seeds 5 --recommend");
            Console.WriteLine("  BalanceRunner --matrix all --seeds 1 --json report.json");
        }
    }
}
