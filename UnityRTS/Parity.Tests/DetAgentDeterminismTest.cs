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
    /// Per-agent headless determinism gate for the frozen Det&lt;Name&gt; snapshot agents.
    ///
    /// Each faithful deterministic copy of a competitive agent (Det&lt;Name&gt;.cs, built to
    /// EnemyAgents/PlanningAgent_Det&lt;Name&gt;.dll) is loaded from its DLL and run against ITSELF
    /// twice on the parity map; the two runs must produce byte-identical per-tick state. This is
    /// the cheap headless proof — BEFORE any Unity recording — that a Det agent carries no
    /// residual non-determinism (leftover RNG, Dictionary/HashSet iteration feeding a decision,
    /// unstable sort). If a Det copy fails here, it is not yet a valid parity agent and the
    /// cross-engine recording would fail too, so this catches it in seconds instead of a Unity
    /// round-trip.
    ///
    /// The competitive originals are intentionally NOT tested here — they may be non-deterministic
    /// by design (that is why the Det snapshots exist).
    /// </summary>
    public class DetAgentDeterminismTest
    {
        private const int Seed = 1914087774;
        private const int Ticks = 300;

        private readonly ITestOutputHelper _output;
        public DetAgentDeterminismTest(ITestOutputHelper output) { _output = output; }

        /// <summary>Every frozen Det snapshot that should be byte-deterministic vs itself.</summary>
        public static IEnumerable<object[]> DetAgents() => new[]
        {
            new object[] { "DetSimple" },
            new object[] { "DetArcherOnly" },
            new object[] { "DetArcherSwarm" },
            new object[] { "DetBalanced" },
            new object[] { "DetCommander" },
            new object[] { "DetEconBoom" },
            new object[] { "DetExampleHeuristics" },
            new object[] { "DetGatherer" },
            new object[] { "DetLancerRush" },
            new object[] { "DetPhalanx" },
            new object[] { "DetSwarm" },
            new object[] { "DetTurtle" },
            new object[] { "DetUnfinishedHeuristics" },
            new object[] { "DetVolley" },
            new object[] { "DetWarriorRush" },
        };

        [Theory]
        [MemberData(nameof(DetAgents))]
        public void DetAgent_TwoSimRuns_AreIdentical(string agentName)
        {
            var type = LoadAgentType(agentName);
            Assert.True(type != null, $"Could not load PlanningAgent_{agentName}.dll — build it first.");

            var runA = RunMatch(type);
            var runB = RunMatch(type);

            int firstDiff = -1;
            for (int t = 0; t < Ticks; t++)
                if (runA[t] != runB[t]) { firstDiff = t + 1; break; }

            if (firstDiff > 0)
            {
                _output.WriteLine($"{agentName}: two sim runs DIVERGED at tick {firstDiff}");
                var au = runA[firstDiff - 1].Split('|');
                var bu = runB[firstDiff - 1].Split('|');
                for (int k = 0; k < Math.Max(au.Length, bu.Length); k++)
                {
                    string a = k < au.Length ? au[k] : "<none>";
                    string b = k < bu.Length ? bu[k] : "<none>";
                    if (a != b) { _output.WriteLine($"  A: {a}"); _output.WriteLine($"  B: {b}"); }
                }
            }
            else
            {
                _output.WriteLine($"{agentName}: two sim runs IDENTICAL through {Ticks} ticks.");
            }

            Assert.Equal(-1, firstDiff);
        }

        private List<string> RunMatch(Type agentType)
        {
            var builder = new SimGameBuilder();
            builder.WithConfig(new SimConfig { GameSpeed = 10, StartingGold = 1000 });
            builder.WithGeneratedMap(new MapGeneratorConfig
            {
                Seed = Seed, Width = 75, Height = 30,
                Template = MapTemplate.OPEN_FIELD,
                ObstacleDensity = 0.20f,
                Symmetry = SymmetryType.MIRROR,
            });
            builder.WithAgent(0, (IPlanningAgent)Activator.CreateInstance(agentType));
            builder.WithAgent(1, (IPlanningAgent)Activator.CreateInstance(agentType));
            var game = builder.Build();
            game.InitializeMatch();
            game.InitializeRound();

            var digests = new List<string>(Ticks);
            for (int t = 0; t < Ticks; t++) { game.Tick(); digests.Add(Digest(game)); }
            return digests;
        }

        /// <summary>Full per-field digest — same field set the parity exporter serializes.</summary>
        private static string Digest(SimGame game)
        {
            var parts = new List<string>();
            for (int i = 0; i < 2000; i++)
            {
                ITickUnit u = game.GetUnit(i);
                if (u == null) continue;
                parts.Add(
                    $"n{u.UnitNbr}:t{u.UnitType}:o{u.OwnerAgentNbr}:x{u.GridPosition.X}:y{u.GridPosition.Y}" +
                    $":hp{u.Health:F1}:b{(u.IsBuilt ? 1 : 0)}:a{u.CurrentAction}:pp{u.PathProgress:F4}:pi{u.PathIndex}" +
                    $":mana{u.Mana:F2}:bp{u.BuildProgress:F4}:tt{u.TrainTimer:F4}:gph{u.GatherPhase}:mt{u.MiningTimer:F4}" +
                    $":gc{u.GoldCarried}:atk{u.AttackTargetNbr}:bld{u.BuildTargetNbr}:rep{u.RepairBuildingNbr}" +
                    $":heal{u.HealTargetNbr}:rpp{(u.RepathPending ? 1 : 0)}");
            }
            return $"g{game.GetGold(0)},{game.GetGold(1)}|" + string.Join("|", parts);
        }

        private Type LoadAgentType(string agentName)
        {
            string dir = FindEnemyAgentsDir();
            if (dir == null) { _output.WriteLine("EnemyAgents dir not found"); return null; }
            string dll = Path.Combine(dir, $"PlanningAgent_{agentName}.dll");
            if (!File.Exists(dll)) { _output.WriteLine($"missing: {dll}"); return null; }
            try
            {
                var asm = Assembly.LoadFrom(dll);
                return asm.GetTypes().FirstOrDefault(t =>
                    typeof(IPlanningAgent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
            }
            catch (Exception ex) { _output.WriteLine($"load failed {dll}: {ex.Message}"); return null; }
        }

        private static string FindEnemyAgentsDir()
        {
            string dir = Path.GetDirectoryName(typeof(DetAgentDeterminismTest).Assembly.Location);
            for (int i = 0; i < 10 && dir != null; i++)
            {
                string enemyDir = Path.Combine(dir, "EnemyAgents");
                if (Directory.Exists(enemyDir)) return enemyDir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }
    }
}
