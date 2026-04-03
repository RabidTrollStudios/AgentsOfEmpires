using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AgentSDK;
using AgentTestHarness;
using Xunit;
using Xunit.Abstractions;

namespace Parity.Tests
{
    /// <summary>
    /// Runs every PlanningAgent_*.dll from EnemyAgents/ through the record-replay
    /// parity system to verify that the simulation produces deterministic results
    /// when driven by real opponent agents.
    ///
    /// This catches:
    /// - Non-determinism in agent code (e.g., using System.Random without fixed seed)
    /// - Sim engine bugs where identical commands produce different state
    /// - Hash coverage gaps (state fields not included in the hash)
    /// </summary>
    public class DllParityTests
    {
        private readonly ITestOutputHelper _output;

        public DllParityTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public static IEnumerable<object[]> AllOpponentDlls()
        {
            string dir = FindEnemyAgentsDir();
            if (!Directory.Exists(dir))
                yield break;

            var regex = new System.Text.RegularExpressions.Regex(@"^PlanningAgent_(\w+)\.dll$");
            foreach (string file in Directory.GetFiles(dir, "PlanningAgent_*.dll"))
            {
                string fileName = Path.GetFileName(file);
                var match = regex.Match(fileName);
                if (match.Success && match.Groups[1].Value != "Mine")
                    yield return new object[] { match.Groups[1].Value, file };
            }
        }

        /// <summary>
        /// Record-replay parity: run the agent for 500 ticks recording all commands,
        /// then replay the exact commands on a fresh game and verify state hashes match
        /// at every tick.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllOpponentDlls))]
        public void DllAgent_Parity_IsDeterministic(string agentName, string dllPath)
        {
            var agent = LoadAgent(dllPath);
            _output.WriteLine($"Parity test: {agentName} (500 ticks)");

            const int ticks = 500;

            // --- Recording run ---
            var game1 = BuildStandardGame(agent, new DoNothingAgent());
            game1.EnableRecording();
            game1.InitializeMatch();
            game1.InitializeRound();

            var hashes = new long[ticks];
            for (int t = 0; t < ticks; t++)
            {
                game1.Tick();
                hashes[t] = game1.GetStateHash();
            }

            var rec0 = game1.GetRecordedCommands(0);
            var rec1 = game1.GetRecordedCommands(1);
            _output.WriteLine($"  Recorded {rec0.Count} commands (agent0), {rec1.Count} (agent1)");

            // --- Replay run ---
            var game2 = BuildStandardGame(new CommandPlayer(rec0), new CommandPlayer(rec1));
            game2.InitializeMatch();
            game2.InitializeRound();

            for (int t = 0; t < ticks; t++)
            {
                game2.Tick();
                long h2 = game2.GetStateHash();
                if (hashes[t] != h2)
                {
                    var sub1 = game1.GetSubsystemHash();
                    var sub2 = game2.GetSubsystemHash();
                    _output.WriteLine($"  DIVERGED at tick {t + 1}: {SubsystemHash.Diff(sub1, sub2)}");

                    // Capture state snapshots for detailed diff
                    var snap1 = StateSnapshot.Capture(game1);
                    var snap2 = StateSnapshot.Capture(game2);
                    _output.WriteLine(StateSnapshot.Diff(snap1, snap2));

                    Assert.Fail($"{agentName}: diverged at tick {t + 1}/{ticks} " +
                                $"(expected 0x{hashes[t]:X16}, got 0x{h2:X16})");
                }
            }

            _output.WriteLine($"  PASSED — {ticks} ticks deterministic");
        }

        /// <summary>
        /// PvP parity: run two copies of the same agent against each other.
        /// Record-replay must still be deterministic.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllOpponentDlls))]
        public void DllAgent_PvP_Parity_IsDeterministic(string agentName, string dllPath)
        {
            var agent0 = LoadAgent(dllPath);
            var agent1 = LoadAgent(dllPath);
            _output.WriteLine($"PvP parity test: {agentName} vs {agentName} (300 ticks)");

            const int ticks = 300;

            var game1 = BuildStandardGame(agent0, agent1);
            game1.EnableRecording();
            game1.InitializeMatch();
            game1.InitializeRound();

            var hashes = new long[ticks];
            for (int t = 0; t < ticks; t++)
            {
                game1.Tick();
                hashes[t] = game1.GetStateHash();
            }

            var rec0 = game1.GetRecordedCommands(0);
            var rec1 = game1.GetRecordedCommands(1);

            var game2 = BuildStandardGame(new CommandPlayer(rec0), new CommandPlayer(rec1));
            game2.InitializeMatch();
            game2.InitializeRound();

            for (int t = 0; t < ticks; t++)
            {
                game2.Tick();
                long h2 = game2.GetStateHash();
                if (hashes[t] != h2)
                {
                    Assert.Fail($"{agentName} PvP: diverged at tick {t + 1}/{ticks}");
                }
            }

            _output.WriteLine($"  PASSED — {ticks} ticks deterministic");
        }

        #region Helpers

        private static SimGame BuildStandardGame(IPlanningAgent agent0, IPlanningAgent agent1)
        {
            return new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithUnit(1, UnitType.BASE, new Position(25, 25), isBuilt: true)
                .WithUnit(1, UnitType.PAWN, new Position(22, 25))
                .WithMine(new Position(15, 10), health: 10000)
                .WithMine(new Position(15, 20), health: 10000)
                .WithAgent(0, agent0)
                .WithAgent(1, agent1)
                .Build();
        }

        private static PlanningAgentBase LoadAgent(string dllPath)
        {
            byte[] bytes = File.ReadAllBytes(dllPath);
            var assembly = Assembly.Load(bytes);
            Type agentType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPlanningAgent).IsAssignableFrom(t) && !t.IsAbstract);
            Assert.True(agentType != null, $"No IPlanningAgent in {Path.GetFileName(dllPath)}");
            return (PlanningAgentBase)Activator.CreateInstance(agentType);
        }

        private static string FindEnemyAgentsDir()
        {
            string dir = Path.GetDirectoryName(typeof(DllParityTests).Assembly.Location);
            for (int i = 0; i < 10 && dir != null; i++)
            {
                string candidate = Path.Combine(dir, "EnemyAgents");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "EnemyAgents");
        }

        #endregion
    }
}
