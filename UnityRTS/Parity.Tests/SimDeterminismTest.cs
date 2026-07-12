using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using AgentTestHarness;
using Xunit;
using Xunit.Abstractions;

namespace Parity.Tests
{
    /// <summary>
    /// Headless self-consistency check: runs a KNOWN-deterministic agent
    /// (DetTestAgent below — no RNG, priority targeting via already-sorted
    /// lists, first-buildable-cell placement) against itself in TWO
    /// independent SimGame runs on the divergent-parity map, and asserts the
    /// two runs produce byte-identical per-tick state.
    ///
    /// Purpose: isolate ENGINE reproducibility from AGENT non-determinism.
    /// If two sim runs of a deterministic agent already diverge, the problem
    /// is in the engine (no Unity recording needed to chase it). If they
    /// match, the engine is self-consistent and a Unity recording of a
    /// deterministic agent is expected to match too — the premise of the
    /// deterministic-test-agent parity approach.
    /// </summary>
    public class SimDeterminismTest
    {
        private const int Seed = 1914087774;
        private const int Ticks = 400;

        private readonly ITestOutputHelper _output;
        public SimDeterminismTest(ITestOutputHelper output) { _output = output; }

        /// <summary>
        /// Minimal fully-deterministic agent: build base near mine, gather with
        /// idle pawns, train pawns then warriors from barracks, attack the
        /// lowest-unitNbr enemy once an army forms. Every choice is a pure
        /// function of the (deterministically ordered) state — no RNG, no
        /// float-tie ambiguity (targets picked by list order, not distance).
        /// </summary>
        private sealed class DetTestAgent : PlanningAgentBase
        {
            private const int MAX_PAWNS = 5;
            public override void InitializeMatch() { }

            public override void Update(IGameState state, IAgentActions actions)
            {
                UpdateGameState(state);
                mainMineNbr = mines.Count > 0 ? mines[0] : -1;
                mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

                // Build a base first (first buildable cell — deterministic).
                if (myBases.Count == 0)
                {
                    BuildFirst(UnitType.BASE, state, actions);
                    return;
                }

                // Train pawns from an idle built base.
                foreach (int baseNbr in myBases)
                {
                    var info = state.GetUnit(baseNbr);
                    if (info.HasValue && info.Value.IsBuilt
                        && info.Value.CurrentAction == UnitAction.IDLE
                        && myPawns.Count < MAX_PAWNS
                        && state.MyGold >= GameConstants.COST[UnitType.PAWN])
                        actions.Train(baseNbr, UnitType.PAWN);
                }

                // Gather with idle pawns.
                if (mainBaseNbr >= 0 && mainMineNbr >= 0)
                {
                    var mineInfo = state.GetUnit(mainMineNbr);
                    if (mineInfo.HasValue && mineInfo.Value.Health > 0)
                        foreach (int pawn in myPawns)
                        {
                            var info = state.GetUnit(pawn);
                            if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                                actions.Gather(pawn, mainMineNbr, mainBaseNbr);
                        }
                }

                // Barracks once a base is built.
                if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                    BuildFirst(UnitType.BARRACKS, state, actions);

                // Train warriors from an idle built barracks.
                foreach (int barracksNbr in myBarracks)
                {
                    var info = state.GetUnit(barracksNbr);
                    if (info.HasValue && info.Value.IsBuilt
                        && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
                        actions.Train(barracksNbr, UnitType.WARRIOR);
                }

                // Attack the priority target (lowest-unitNbr enemy, by type order)
                // once we have a few warriors.
                if (myWarriors.Count >= 3)
                {
                    int? target = PriorityTarget(state);
                    if (target.HasValue)
                        foreach (int w in myWarriors)
                        {
                            var info = state.GetUnit(w);
                            if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                                actions.Attack(w, target.Value);
                        }
                }
            }

            private int? PriorityTarget(IGameState state)
            {
                foreach (UnitType ut in new[] { UnitType.PAWN, UnitType.BASE, UnitType.BARRACKS,
                                                UnitType.WARRIOR, UnitType.ARCHER })
                {
                    var enemies = state.GetEnemyUnits(ut);
                    if (enemies.Count > 0) return enemies[0];
                }
                return null;
            }

            private void BuildFirst(UnitType type, IGameState state, IAgentActions actions)
            {
                if (state.MyGold < GameConstants.COST[type]) return;
                int pawn = -1;
                foreach (int p in myPawns)
                {
                    var info = state.GetUnit(p);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE) { pawn = p; break; }
                }
                if (pawn < 0)
                    foreach (int p in myPawns)
                    {
                        var info = state.GetUnit(p);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.GATHER) { pawn = p; break; }
                    }
                if (pawn < 0) return;

                // Build nearest the pawn (deterministic), not the global first cell —
                // otherwise the top-right spawn walks the whole map. Mirrors DetCommander.
                var pawnInfo = state.GetUnit(pawn);
                Position anchor = pawnInfo.HasValue ? pawnInfo.Value.GridPosition : new Position(0, 0);
                var candidates = new List<Position>(state.FindProspectiveBuildPositions(type));
                DeterministicSort.SortByDistance(candidates, anchor);

                foreach (Position pos in candidates)
                    if (state.IsAreaBuildable(type, pos))
                    {
                        actions.Build(pawn, pos, type);
                        return;
                    }
            }
        }

        private static SimGame BuildGame()
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
            builder.WithAgent(0, new DetTestAgent());
            builder.WithAgent(1, new DetTestAgent());
            var game = builder.Build();
            game.InitializeMatch();
            game.InitializeRound();
            return game;
        }

        /// <summary>One-line-per-tick digest of all unit state, for exact comparison.</summary>
        private static string Digest(SimGame game)
        {
            var parts = new List<string>();
            for (int i = 0; i < 2000; i++)
            {
                var u = game.GetUnit(i);
                if (u == null) continue;
                parts.Add($"{u.UnitNbr}:{u.UnitType}:{u.OwnerAgentNbr}:{u.GridPosition.X}:{u.GridPosition.Y}:{u.Health:F1}:{(u.IsBuilt ? 1 : 0)}:{u.CurrentAction}");
            }
            return $"g{game.GetGold(0)},{game.GetGold(1)}|" + string.Join("|", parts);
        }

        [Fact]
        public void DeterministicAgent_TwoSimRuns_AreIdentical()
        {
            var runA = new List<string>();
            var runB = new List<string>();

            var gameA = BuildGame();
            for (int t = 0; t < Ticks; t++) { gameA.Tick(); runA.Add(Digest(gameA)); }

            var gameB = BuildGame();
            for (int t = 0; t < Ticks; t++) { gameB.Tick(); runB.Add(Digest(gameB)); }

            int firstDiff = -1;
            for (int t = 0; t < Ticks; t++)
                if (runA[t] != runB[t]) { firstDiff = t + 1; break; }

            if (firstDiff > 0)
            {
                _output.WriteLine($"Two sim runs DIVERGED at tick {firstDiff}");
                _output.WriteLine($"  A: {runA[firstDiff - 1]}");
                _output.WriteLine($"  B: {runB[firstDiff - 1]}");
            }
            else
            {
                _output.WriteLine($"Two sim runs IDENTICAL through {Ticks} ticks — engine is self-consistent.");
            }

            // Sanity: confirm the agent actually reaches an interesting state
            // (built a barracks, trained warriors, engaged in combat) so the parity
            // matchup exercises train/build/gather/attack + the PathBudget pursuit gate,
            // not just idle gathering.
            var byType = new Dictionary<string, int>();
            var acts = new Dictionary<string, int>();
            for (int i = 0; i < 3000; i++)
            {
                var u = gameA.GetUnit(i);
                if (u == null) continue;
                string k = $"{u.OwnerAgentNbr}:{u.UnitType}";
                byType[k] = byType.GetValueOrDefault(k) + 1;
                acts[u.CurrentAction.ToString()] = acts.GetValueOrDefault(u.CurrentAction.ToString()) + 1;
            }
            _output.WriteLine("End-state units -> " + string.Join(", ",
                byType.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}")));
            _output.WriteLine("End-state actions -> " + string.Join(", ",
                acts.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}")));

            // Confirm combat actually occurred so the matchup exercises the
            // combat-pursuit PathBudget gate (not just idle gathering).
            int maxAttack = 0, attackTick = 0;
            for (int t = 0; t < Ticks; t++)
            {
                int atk = runA[t].Split('|').Count(p => p.EndsWith(":ATTACK"));
                if (atk > maxAttack) { maxAttack = atk; attackTick = t + 1; }
            }
            _output.WriteLine($"Peak concurrent ATTACK units: {maxAttack} at tick {attackTick}");
            Assert.True(maxAttack > 0, "matchup never entered combat — pursuit gate untested");

            Assert.Equal(-1, firstDiff);
        }
    }
}
