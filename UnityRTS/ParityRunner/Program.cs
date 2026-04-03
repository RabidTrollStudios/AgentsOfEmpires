using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using AgentTestHarness;
using Parity.Tests;

namespace ParityRunner
{
    class Program
    {
        static int Main(string[] args)
        {
            bool jsonOutput = args.Contains("--json");
            string jsonFile = null;
            string filterName = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--json" && i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    jsonFile = args[i + 1];
                    i++;
                }
                else if (args[i] == "--scenario" && i + 1 < args.Length)
                {
                    filterName = args[i + 1];
                    i++;
                }
                else if (args[i] == "--help" || args[i] == "-h")
                {
                    PrintUsage();
                    return 0;
                }
                else if (args[i] == "--list")
                {
                    ListScenarios();
                    return 0;
                }
            }

            var scenarios = GetAllScenarios();

            if (filterName != null)
            {
                scenarios = scenarios
                    .Where(s => s.Name.Contains(filterName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (scenarios.Count == 0)
                {
                    Console.Error.WriteLine($"No scenarios matching '{filterName}'");
                    return 1;
                }
            }

            Console.WriteLine($"ParityRunner: {scenarios.Count} scenario(s)");
            Console.WriteLine(new string('=', 60));

            var results = new List<ScenarioResult>();
            int passed = 0;
            int failed = 0;
            var totalSw = Stopwatch.StartNew();

            foreach (var scenario in scenarios)
            {
                var sw = Stopwatch.StartNew();
                var report = RunScenario(scenario);
                sw.Stop();

                var result = new ScenarioResult
                {
                    ScenarioName = scenario.Name,
                    Passed = report.Passed,
                    Ticks = scenario.Ticks,
                    DivergenceTick = report.DivergenceTick,
                    ExpectedHash = report.ExpectedHash,
                    ActualHash = report.ActualHash,
                    ElapsedMs = sw.ElapsedMilliseconds
                };
                results.Add(result);

                if (report.Passed)
                {
                    passed++;
                    Console.WriteLine($"  PASS  {scenario.Name} ({scenario.Ticks} ticks, {sw.ElapsedMilliseconds}ms)");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"  FAIL  {scenario.Name}: diverged at tick {report.DivergenceTick}/{scenario.Ticks}");
                    Console.WriteLine($"        expected 0x{report.ExpectedHash:X16}, got 0x{report.ActualHash:X16}");

                    // Run subsystem diff for failed scenario
                    var subsystemDiff = RunSubsystemDiff(scenario, report.DivergenceTick);
                    if (subsystemDiff != null)
                    {
                        Console.WriteLine($"        subsystems: {subsystemDiff}");
                        result.SubsystemDiff = subsystemDiff;
                    }
                }
            }

            totalSw.Stop();
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Results: {passed} passed, {failed} failed, {totalSw.ElapsedMilliseconds}ms total");

            // JSON output
            if (jsonOutput || jsonFile != null)
            {
                var summary = new RunSummary
                {
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    TotalScenarios = scenarios.Count,
                    Passed = passed,
                    Failed = failed,
                    TotalElapsedMs = totalSw.ElapsedMilliseconds,
                    Results = results
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(summary, options);

                if (jsonFile != null)
                {
                    File.WriteAllText(jsonFile, json);
                    Console.WriteLine($"JSON report written to: {jsonFile}");
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine(json);
                }
            }

            return failed > 0 ? 1 : 0;
        }

        static void PrintUsage()
        {
            Console.WriteLine("ParityRunner - Deterministic parity test runner");
            Console.WriteLine();
            Console.WriteLine("Usage: ParityRunner [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --list                List all available scenarios");
            Console.WriteLine("  --scenario <name>     Run only scenarios matching <name> (case-insensitive)");
            Console.WriteLine("  --json [file]         Output JSON report (to stdout or file)");
            Console.WriteLine("  --help, -h            Show this help");
            Console.WriteLine();
            Console.WriteLine("Exit codes:");
            Console.WriteLine("  0  All scenarios passed");
            Console.WriteLine("  1  One or more scenarios failed (or no scenarios matched)");
        }

        static void ListScenarios()
        {
            var scenarios = GetAllScenarios();
            Console.WriteLine($"Available scenarios ({scenarios.Count}):");
            foreach (var s in scenarios)
                Console.WriteLine($"  {s.Name} ({s.Ticks} ticks)");
        }

        static List<ParityScenario> GetAllScenarios()
        {
            return Scenarios.AllScenarios().ToList();
        }

        static DivergenceReport RunScenario(ParityScenario scenario)
        {
            var builder = scenario.BuilderFactory();
            var agent0 = scenario.Agent0Factory();
            var agent1 = scenario.Agent1Factory();
            int ticks = scenario.Ticks;

            // Recording run
            builder.WithAgent(0, agent0).WithAgent(1, agent1);
            var game1 = builder.Build();
            game1.EnableRecording();
            game1.InitializeMatch();
            game1.InitializeRound();

            var hashes1 = new long[ticks];
            for (int t = 0; t < ticks; t++)
            {
                game1.Tick();
                hashes1[t] = game1.GetStateHash();
            }

            var recorded0 = game1.GetRecordedCommands(0);
            var recorded1 = game1.GetRecordedCommands(1);

            // Replay run
            var replayBuilder = scenario.BuilderFactory();
            replayBuilder.WithAgent(0, new CommandPlayer(recorded0))
                         .WithAgent(1, new CommandPlayer(recorded1));
            var game2 = replayBuilder.Build();
            game2.InitializeMatch();
            game2.InitializeRound();

            for (int t = 0; t < ticks; t++)
            {
                game2.Tick();
                long hash2 = game2.GetStateHash();
                if (hashes1[t] != hash2)
                {
                    return new DivergenceReport
                    {
                        ScenarioName = scenario.Name,
                        DivergenceTick = t + 1,
                        ExpectedHash = hashes1[t],
                        ActualHash = hash2,
                        TotalTicks = ticks
                    };
                }
            }

            return new DivergenceReport
            {
                ScenarioName = scenario.Name,
                TotalTicks = ticks
            };
        }

        /// <summary>
        /// Re-run the scenario up to the divergence tick and compare subsystem hashes.
        /// </summary>
        static string RunSubsystemDiff(ParityScenario scenario, int divergenceTick)
        {
            try
            {
                var builder = scenario.BuilderFactory();
                var agent0 = scenario.Agent0Factory();
                var agent1 = scenario.Agent1Factory();

                builder.WithAgent(0, agent0).WithAgent(1, agent1);
                var game1 = builder.Build();
                game1.EnableRecording();
                game1.InitializeMatch();
                game1.InitializeRound();

                for (int t = 0; t < divergenceTick; t++)
                    game1.Tick();

                var sub1 = game1.GetSubsystemHash();

                var recorded0 = game1.GetRecordedCommands(0);
                var recorded1 = game1.GetRecordedCommands(1);

                var replayBuilder = scenario.BuilderFactory();
                replayBuilder.WithAgent(0, new CommandPlayer(recorded0))
                             .WithAgent(1, new CommandPlayer(recorded1));
                var game2 = replayBuilder.Build();
                game2.InitializeMatch();
                game2.InitializeRound();

                for (int t = 0; t < divergenceTick; t++)
                    game2.Tick();

                var sub2 = game2.GetSubsystemHash();

                return SubsystemHash.Diff(sub1, sub2);
            }
            catch
            {
                return null;
            }
        }

        #region JSON models

        class RunSummary
        {
            public string Timestamp { get; set; }
            public int TotalScenarios { get; set; }
            public int Passed { get; set; }
            public int Failed { get; set; }
            public long TotalElapsedMs { get; set; }
            public List<ScenarioResult> Results { get; set; }
        }

        class ScenarioResult
        {
            public string ScenarioName { get; set; }
            public bool Passed { get; set; }
            public int Ticks { get; set; }
            public int DivergenceTick { get; set; }
            public long ExpectedHash { get; set; }
            public long ActualHash { get; set; }
            public long ElapsedMs { get; set; }
            public string SubsystemDiff { get; set; }
        }

        #endregion
    }
}
