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
    /// Decisive probe: load the REAL ArcherSwarm/Commander agents into the exact
    /// failing scenario, wrap IAgentActions to log every command they issue at the
    /// opening ticks, and see whether/why they build (or don't build) their base.
    /// </summary>
    public class BaseBuildDesyncProbe
    {
        private readonly ITestOutputHelper _out;
        public BaseBuildDesyncProbe(ITestOutputHelper o) => _out = o;

        /// <summary>Wraps a real IAgentActions and logs every command issued, tagged by tick.</summary>
        private sealed class LoggingActions : IAgentActions
        {
            private readonly IAgentActions _inner;
            private readonly ITestOutputHelper _out;
            private readonly string _tag;
            public Func<int> Tick = () => -1;
            public LoggingActions(IAgentActions inner, ITestOutputHelper o, string tag) { _inner = inner; _out = o; _tag = tag; }

            public CommandResult Move(int unitNbr, Position target)
            { _out.WriteLine($"[{_tag} t{Tick()}] MOVE unit={unitNbr} -> {target}"); return _inner.Move(unitNbr, target); }
            public CommandResult Build(int unitNbr, Position target, UnitType unitType)
            { _out.WriteLine($"[{_tag} t{Tick()}] BUILD unit={unitNbr} {unitType} @ {target}"); return _inner.Build(unitNbr, target, unitType); }
            public CommandResult Gather(int pawnNbr, int mineNbr, int baseNbr)
            { _out.WriteLine($"[{_tag} t{Tick()}] GATHER pawn={pawnNbr} mine={mineNbr} base={baseNbr}"); return _inner.Gather(pawnNbr, mineNbr, baseNbr); }
            public CommandResult Train(int buildingNbr, UnitType unitType)
            { _out.WriteLine($"[{_tag} t{Tick()}] TRAIN building={buildingNbr} {unitType}"); return _inner.Train(buildingNbr, unitType); }
            public CommandResult Attack(int unitNbr, int targetNbr)
            { _out.WriteLine($"[{_tag} t{Tick()}] ATTACK unit={unitNbr} -> {targetNbr}"); return _inner.Attack(unitNbr, targetNbr); }
            public CommandResult Repair(int unitNbr, int targetNbr)
            { _out.WriteLine($"[{_tag} t{Tick()}] REPAIR unit={unitNbr} -> {targetNbr}"); return _inner.Repair(unitNbr, targetNbr); }
            public CommandResult Heal(int unitNbr, int targetNbr)
            { _out.WriteLine($"[{_tag} t{Tick()}] HEAL unit={unitNbr} -> {targetNbr}"); return _inner.Heal(unitNbr, targetNbr); }
            public void Log(string message) => _inner.Log(message);
        }

        /// <summary>Wraps a real agent so we can inject a LoggingActions into its Update.</summary>
        private sealed class WrappedAgent : IPlanningAgent
        {
            private readonly IPlanningAgent _real;
            private readonly ITestOutputHelper _out;
            private readonly string _tag;
            private int _tick = -1;
            public WrappedAgent(IPlanningAgent real, ITestOutputHelper o, string tag) { _real = real; _out = o; _tag = tag; }
            public void InitializeMatch() => _real.InitializeMatch();
            public void InitializeRound(IGameState state) => _real.InitializeRound(state);
            public void Learn(IGameState state) => _real.Learn(state);
            public void Update(IGameState state, IAgentActions actions)
            {
                _tick++;
                var logged = new LoggingActions(actions, _out, _tag) { Tick = () => _tick };
                _real.Update(state, logged);
            }
        }

        private static IPlanningAgent LoadReal(string dllName)
        {
            string dir = Path.GetDirectoryName(typeof(BaseBuildDesyncProbe).Assembly.Location);
            for (int i = 0; i < 10 && dir != null; i++)
            {
                string enemyDir = Path.Combine(dir, "EnemyAgents");
                if (Directory.Exists(enemyDir))
                {
                    string path = Path.Combine(enemyDir, $"PlanningAgent_{dllName}.dll");
                    if (!File.Exists(path)) return null;
                    var asm = Assembly.LoadFrom(path);
                    foreach (var t in asm.GetTypes())
                        if (typeof(IPlanningAgent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                            return (IPlanningAgent)Activator.CreateInstance(t);
                    return null;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        [Fact]
        public void RealAgents_OpeningCommands()
        {
            var blue = LoadReal("ArcherSwarm");
            var red = LoadReal("Commander");
            _out.WriteLine($"loaded blue(ArcherSwarm)={(blue != null)} red(Commander)={(red != null)}");
            Assert.NotNull(blue);
            Assert.NotNull(red);

            var builder = new SimGameBuilder();
            builder.WithConfig(new SimConfig { GameSpeed = 10, StartingGold = 1000 });
            builder.WithGeneratedMap(new MapGeneratorConfig
            {
                Seed = 1914087774, Width = 75, Height = 30,
                ObstacleDensity = 0.2000f, Template = MapTemplate.OPEN_FIELD, Symmetry = SymmetryType.MIRROR,
            });
            builder.WithAgent(0, new WrappedAgent(blue, _out, "BLUE"));
            builder.WithAgent(1, new WrappedAgent(red, _out, "RED"));
            var game = builder.Build();

            game.InitializeMatch();
            game.InitializeRound();
            _out.WriteLine("=== ticking (commands each agent issues) ===");
            for (int t = 0; t < 4; t++) game.Tick();

            _out.WriteLine("=== board after 4 ticks ===");
            for (int i = 0; i < 12; i++)
            {
                var u = game.GetUnit(i);
                if (u != null)
                    _out.WriteLine($"  unit#{i} {u.UnitType} owner={u.OwnerAgentNbr} pos={u.GridPosition} built={u.IsBuilt}");
            }
            _out.WriteLine($"gold: P0={game.Gold[0]} P1={game.Gold[1]}");
        }
    }
}
