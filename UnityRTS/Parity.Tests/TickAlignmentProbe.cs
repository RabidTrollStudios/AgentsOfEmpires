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
    /// Tests whether the parity divergence is a one-tick PHASE OFFSET between the
    /// engines. Runs the real agents in the sim, records the sim's (gold0, unitCount)
    /// at each tick, and prints it next to Unity's recorded snapshots so we can see
    /// whether sim-tick-N matches Unity-tick-N (aligned) or Unity-tick-(N-1) (offset).
    /// </summary>
    public class TickAlignmentProbe
    {
        private readonly ITestOutputHelper _out;
        public TickAlignmentProbe(ITestOutputHelper o) => _out = o;

        private static IPlanningAgent LoadReal(string dllName)
        {
            string dir = Path.GetDirectoryName(typeof(TickAlignmentProbe).Assembly.Location);
            for (int i = 0; i < 10 && dir != null; i++)
            {
                string enemyDir = Path.Combine(dir, "EnemyAgents");
                if (Directory.Exists(enemyDir))
                {
                    string path = Path.Combine(enemyDir, $"PlanningAgent_{dllName}.dll");
                    if (!File.Exists(path)) return null;
                    foreach (var t in Assembly.LoadFrom(path).GetTypes())
                        if (typeof(IPlanningAgent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                            return (IPlanningAgent)Activator.CreateInstance(t);
                }
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        [Fact]
        public void SimVsUnity_TickByTick_Offset()
        {
            // Unity ground truth: parse (tick -> gold0, unitCount) from the CSV.
            string csv = Directory.GetFiles(@"C:\Git\Warcrap\UnityRTS\RTS", "ParityState_*.csv")
                .OrderByDescending(f => f).FirstOrDefault();
            _out.WriteLine($"CSV: {Path.GetFileName(csv)}");
            var unity = new Dictionary<int, (int gold0, int units)>();
            foreach (var line in File.ReadLines(csv))
            {
                if (line.StartsWith("#") || line.StartsWith("tick")) continue;
                var p = line.Split(',');
                if (p.Length < 4) continue;
                if (int.TryParse(p[0], out int tk))
                    unity[tk] = (int.Parse(p[1]), int.Parse(p[3]));
            }

            var builder = new SimGameBuilder();
            builder.WithConfig(new SimConfig { GameSpeed = 10, StartingGold = 1000 });
            builder.WithGeneratedMap(new MapGeneratorConfig
            {
                Seed = 1914087774, Width = 75, Height = 30,
                ObstacleDensity = 0.2000f, Template = MapTemplate.OPEN_FIELD, Symmetry = SymmetryType.MIRROR,
            });
            builder.WithAgent(0, LoadReal("ArcherSwarm"));
            builder.WithAgent(1, LoadReal("Commander"));
            var game = builder.Build();
            game.InitializeMatch();
            game.InitializeRound();

            _out.WriteLine("simTick | sim(gold0,units) | unity@same | unity@same-1 | aligned?");
            for (int t = 0; t < 6; t++)
            {
                game.Tick();
                int simTick = t + 1;
                int simUnits = 0;
                for (int i = 0; i < 40; i++) if (game.GetUnit(i) != null) simUnits++;
                var sim = (game.Gold[0], simUnits);

                string uSame = unity.TryGetValue(simTick, out var us) ? $"({us.gold0},{us.units})" : "—";
                string uPrev = unity.TryGetValue(simTick - 1, out var up) ? $"({up.gold0},{up.units})" : "—";
                bool alignedSame = unity.TryGetValue(simTick, out var a) && a.gold0 == sim.Item1 && a.units == sim.Item2;
                bool alignedPrev = unity.TryGetValue(simTick - 1, out var b) && b.gold0 == sim.Item1 && b.units == sim.Item2;
                string verdict = alignedSame ? "SAME-tick" : alignedPrev ? "OFFSET-by-1" : "neither";
                _out.WriteLine($"  {simTick,4}   | ({sim.Item1},{sim.Item2})        | {uSame,-10} | {uPrev,-10} | {verdict}");
            }
        }
    }
}
