using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AgentSDK;
using AgentTestHarness;
using Xunit;
using Xunit.Abstractions;

namespace Opponent.Tests
{
    /// <summary>
    /// Loads every PlanningAgent_*.dll from EnemyAgents/ and runs each
    /// through SimGame to verify the built DLLs are functional.
    /// This catches compilation issues, interface mismatches, and runtime crashes.
    /// </summary>
    public class DllSmokeTests : OpponentTestBase
    {
        private readonly ITestOutputHelper _output;

        public DllSmokeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Discovers all PlanningAgent_*.dll files (excluding Mine) in EnemyAgents/.
        /// </summary>
        public static IEnumerable<object[]> AllOpponentDlls()
        {
            string enemyAgentsDir = FindEnemyAgentsDir();
            if (!Directory.Exists(enemyAgentsDir))
                yield break;

            var regex = new Regex(@"^PlanningAgent_(\w+)\.dll$");
            foreach (string file in Directory.GetFiles(enemyAgentsDir, "PlanningAgent_*.dll"))
            {
                string fileName = Path.GetFileName(file);
                var match = regex.Match(fileName);
                if (match.Success && match.Groups[1].Value != "Mine")
                {
                    yield return new object[] { match.Groups[1].Value, file };
                }
            }
        }

        [Theory]
        [MemberData(nameof(AllOpponentDlls))]
        public void DllLoads_AndImplementsIPlanningAgent(string agentName, string dllPath)
        {
            var agent = LoadAgentFromDll(dllPath, agentName);
            Assert.NotNull(agent);
            _output.WriteLine($"{agentName}: loaded {agent.GetType().FullName}");
        }

        [Theory]
        [MemberData(nameof(AllOpponentDlls))]
        public void DllAgent_RunsWithoutCrashing(string agentName, string dllPath)
        {
            var agent = LoadAgentFromDll(dllPath, agentName);
            _output.WriteLine($"Running {agentName} for 1000 frames...");

            RunOpponentTest(agent, frames: 1000);

            _output.WriteLine($"{agentName}: completed 1000 frames without crashing");
        }

        [Theory]
        [MemberData(nameof(AllOpponentDlls))]
        public void DllAgent_SurvivesMultipleRounds(string agentName, string dllPath)
        {
            var agent = LoadAgentFromDll(dllPath, agentName);

            var game = BuildStandardGame(agent);
            game.InitializeMatch();

            // Run 3 rounds to test InitializeRound/Learn lifecycle
            for (int round = 0; round < 3; round++)
            {
                game.InitializeRound();
                game.Run(200);
                _output.WriteLine($"{agentName} round {round + 1}: completed 200 frames");
            }
        }

        /// <summary>
        /// Loads a PlanningAgent DLL and instantiates the IPlanningAgent class within it.
        /// </summary>
        private static PlanningAgentBase LoadAgentFromDll(string dllPath, string agentName)
        {
            Assert.True(File.Exists(dllPath), $"DLL not found: {dllPath}");

            // Load from bytes to avoid file locks (same pattern as AgentLoader)
            byte[] bytes = File.ReadAllBytes(dllPath);
            var assembly = Assembly.Load(bytes);

            // Find the first type that implements IPlanningAgent
            Type agentType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPlanningAgent).IsAssignableFrom(t) && !t.IsAbstract);

            Assert.True(agentType != null,
                $"No IPlanningAgent implementation found in {Path.GetFileName(dllPath)}");

            var agent = Activator.CreateInstance(agentType);
            Assert.NotNull(agent);
            Assert.IsAssignableFrom<PlanningAgentBase>(agent);

            return (PlanningAgentBase)agent;
        }

        /// <summary>
        /// Walks up from the test assembly directory to find the EnemyAgents/ folder.
        /// Test runs from PlanningAgent.Tests/bin/Debug/net8.0/ so we walk up
        /// to find the repo root containing EnemyAgents/.
        /// </summary>
        private static string FindEnemyAgentsDir()
        {
            string dir = Path.GetDirectoryName(
                typeof(DllSmokeTests).Assembly.Location);

            for (int i = 0; i < 10 && dir != null; i++)
            {
                string candidate = Path.Combine(dir, "EnemyAgents");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = Path.GetDirectoryName(dir);
            }

            // Should not get here in normal usage
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "EnemyAgents");
        }
    }
}
