using System.Collections.Generic;
using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Behavioral tests for SimGame's 3-phase collision avoidance system.
    /// Verifies that units properly wait, detour, and re-path when blocked
    /// by other mobile units, matching Unity's FixedUpdate avoidance logic.
    /// </summary>
    public class CollisionAvoidanceTests
    {
        [Fact]
        public void BlockedByStationaryUnit_Detours()
        {
            // Place a moving pawn and a stationary pawn blocking its direct path
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(2, 10))   // mover
                .WithUnit(0, UnitType.PAWN, new Position(10, 10))  // blocker (idle)
                .WithAgent(0, new MoveOnceAgent(new Position(18, 10)))
                .Build();
            game.InitializeMatch();
            game.InitializeRound();

            // Run enough ticks for the mover to reach the blocker and detour
            for (int i = 0; i < 400; i++) game.Tick();

            // The mover should have arrived near the target
            var pawns = game.GetUnitsByType(0, UnitType.PAWN);
            bool anyNearTarget = false;
            foreach (var p in pawns)
            {
                if (p.GridPosition.X > 15)
                {
                    anyNearTarget = true;
                    break;
                }
            }
            Assert.True(anyNearTarget, "Mover should have detourred past the blocker");
        }

        [Fact]
        public void MoveNearDestination_StopsWhenBlocked()
        {
            // Place a pawn trying to move to a cell occupied by another pawn
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(8, 10))   // mover
                .WithUnit(0, UnitType.PAWN, new Position(10, 10))  // blocker at target
                .WithAgent(0, new MoveOnceAgent(new Position(10, 10)))
                .Build();
            game.InitializeMatch();
            game.InitializeRound();

            for (int i = 0; i < 200; i++) game.Tick();

            // The mover should have stopped (IDLE) near the blocker
            var pawns = game.GetUnitsByType(0, UnitType.PAWN);
            bool foundIdleMover = false;
            foreach (var u in pawns)
            {
                if (u.GridPosition.X >= 7 && u.GridPosition.X <= 10
                    && u.GridPosition.Y == 10
                    && u.CurrentAction == UnitAction.IDLE)
                {
                    foundIdleMover = true;
                }
            }
            Assert.True(foundIdleMover, "Mover should have stopped near the blocker");
        }

        [Fact]
        public void HeadOnMovement_UnitsPassThroughButDontOverlapAtRest()
        {
            // Two pawns moving toward each other on the same row.
            // Mobile units pass through each other mid-path but stop at
            // the last free cell before an occupied destination.
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(2, 10))
                .WithUnit(1, UnitType.PAWN, new Position(18, 10))
                .WithAgent(0, new MoveOnceAgent(new Position(18, 10)))
                .WithAgent(1, new MoveOnceAgent(new Position(2, 10)))
                .Build();
            game.InitializeMatch();
            game.InitializeRound();

            for (int i = 0; i < 300; i++) game.Tick();

            // Both units should be IDLE and not on the same cell
            var p0 = game.GetUnitsByType(0, UnitType.PAWN);
            var p1 = game.GetUnitsByType(1, UnitType.PAWN);
            Assert.True(p0.Count > 0 && p0[0].CurrentAction == UnitAction.IDLE,
                "Agent 0 pawn should be IDLE");
            Assert.True(p1.Count > 0 && p1[0].CurrentAction == UnitAction.IDLE,
                "Agent 1 pawn should be IDLE");
            Assert.NotEqual(p0[0].GridPosition, p1[0].GridPosition);
        }

        [Fact]
        public void CorridorCongestion_UnitsNavigateNarrowPath()
        {
            var builder = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(2, 10))
                .WithUnit(0, UnitType.PAWN, new Position(2, 11))
                .WithAgent(0, new MoveAllPawnsAgent(new Position(18, 10)));

            // Build walls leaving only row 10 open between x=8..12
            for (int x = 8; x <= 12; x++)
            {
                builder.WithWall(new Position(x, 11), new Position(x, 11));
                builder.WithWall(new Position(x, 9), new Position(x, 9));
            }

            var game = builder.Build();
            game.InitializeMatch();
            game.InitializeRound();

            for (int i = 0; i < 600; i++) game.Tick();

            // At least one pawn should have made it past the corridor
            var pawns = game.GetUnitsByType(0, UnitType.PAWN);
            bool anyPastCorridor = false;
            foreach (var p in pawns)
            {
                if (p.GridPosition.X > 12)
                {
                    anyPastCorridor = true;
                    break;
                }
            }
            Assert.True(anyPastCorridor, "At least one pawn should navigate through the corridor");
        }

        [Fact]
        public void AttackerBlockedByAlly_WaitsAndDetours()
        {
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.WARRIOR, new Position(2, 10))
                .WithUnit(0, UnitType.PAWN, new Position(5, 10))    // blocking ally
                .WithUnit(1, UnitType.WARRIOR, new Position(18, 10))
                .WithAgent(0, new AttackFirstEnemyAgent())
                .WithAgent(1, new DoNothingAgent())
                .Build();
            game.InitializeMatch();
            game.InitializeRound();

            for (int i = 0; i < 300; i++) game.Tick();

            // Warrior should have moved past the blocker
            var warriors = game.GetUnitsByType(0, UnitType.WARRIOR);
            if (warriors.Count > 0)
            {
                Assert.True(warriors[0].GridPosition.X > 5,
                    $"Warrior should have detourred past the blocker, but is at x={warriors[0].GridPosition.X}");
            }
        }

        [Fact]
        public void TerrainBlockingDuringMove_RepathsImmediately()
        {
            // A base is placed in the direct path — pawn should repath around it
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.PAWN, new Position(2, 10))
                .WithUnit(0, UnitType.BASE, new Position(10, 12), isBuilt: true)
                .WithAgent(0, new MoveOnceAgent(new Position(18, 10)))
                .Build();
            game.InitializeMatch();
            game.InitializeRound();

            for (int i = 0; i < 400; i++) game.Tick();

            var pawns = game.GetUnitsByType(0, UnitType.PAWN);
            Assert.True(pawns.Count > 0, "Pawn should still exist");
            Assert.True(pawns[0].GridPosition.X >= 14,
                $"Pawn should have navigated around the base, but is at x={pawns[0].GridPosition.X}");
        }

        [Fact]
        public void CollisionAvoidance_IsDeterministic()
        {
            var scenario = Scenarios.HeadOnCollision();
            var report = RunScenario(scenario);
            Assert.True(report.Passed, report.ToString());
        }

        [Fact]
        public void CorridorCongestion_IsDeterministic()
        {
            var scenario = Scenarios.CorridorCongestion();
            var report = RunScenario(scenario);
            Assert.True(report.Passed, report.ToString());
        }

        [Fact]
        public void AttackerBlocked_IsDeterministic()
        {
            var scenario = Scenarios.AttackerBlockedByAlly();
            var report = RunScenario(scenario);
            Assert.True(report.Passed, report.ToString());
        }

        private DivergenceReport RunScenario(ParityScenario scenario)
        {
            var builder = scenario.BuilderFactory();
            var agent0 = scenario.Agent0Factory();
            var agent1 = scenario.Agent1Factory();
            int ticks = scenario.Ticks;

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
    }
}
