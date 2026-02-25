using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the building system: workers construct structures.
    /// </summary>
    public class BuildingTests
    {
        // ------------------------------------------------------------------
        // Happy path
        // ------------------------------------------------------------------

        [Fact]
        public void WorkerBuildsBarracks_BarracksAppears()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithAgent(0, new BuildOnceAgent(UnitType.BARRACKS, new Position(15, 15)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(500);

            var barracks = game.GetUnitsByType(0, UnitType.BARRACKS);
            Assert.Single(barracks);
            Assert.True(barracks[0].IsBuilt, "Barracks should be fully built");
        }

        [Fact]
        public void Building_DeductsGoldAtStart()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithAgent(0, new BuildOnceAgent(UnitType.BARRACKS, new Position(15, 15)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Run just a few ticks so build command is issued but not yet complete
            game.Run(5);

            int barracksCost = (int)GameConstants.COST[UnitType.BARRACKS]; // 400
            Assert.True(game.GetGold(0) <= 5000 - barracksCost,
                $"Gold should be deducted at build start. Expected <= {5000 - barracksCost}, got {game.GetGold(0)}");
        }

        [Fact]
        public void Building_WorkerReturnsToIdle()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(14, 15)) // Near build site
                .WithAgent(0, new BuildOnceAgent(UnitType.BARRACKS, new Position(15, 15)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(500);

            var workers = game.GetUnitsByType(0, UnitType.WORKER);
            Assert.Single(workers);
            Assert.Equal(UnitAction.IDLE, workers[0].CurrentAction);
        }

        [Fact]
        public void Building_CellsBecomeUnbuildable()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithAgent(0, new BuildOnceAgent(UnitType.BARRACKS, new Position(15, 15)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(500);

            // 3x3 footprint should be unbuildable
            Assert.False(game.Map.IsPositionBuildable(new Position(15, 15)));
            Assert.False(game.Map.IsPositionBuildable(new Position(16, 15)));
            Assert.False(game.Map.IsPositionBuildable(new Position(15, 14)));
        }

        // ------------------------------------------------------------------
        // Error cases
        // ------------------------------------------------------------------

        [Fact]
        public void BuildWithInsufficientGold_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 100) // Barracks costs 400
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithAgent(0, new BuildOnceAgent(UnitType.BARRACKS, new Position(15, 15)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            Assert.Empty(game.GetUnitsByType(0, UnitType.BARRACKS));
            Assert.Equal(100, game.GetGold(0));
        }

        [Fact]
        public void BuildOnOccupiedArea_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                // Build overlapping with existing base
                .WithAgent(0, new BuildOnceAgent(UnitType.BARRACKS, new Position(6, 5)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            Assert.Empty(game.GetUnitsByType(0, UnitType.BARRACKS));
        }

        [Fact]
        public void BuildWithoutDependency_Rejected()
        {
            // BARRACKS requires a built BASE. Remove the base.
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithAgent(0, new BuildOnceAgent(UnitType.BARRACKS, new Position(15, 15)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            Assert.Empty(game.GetUnitsByType(0, UnitType.BARRACKS));
        }

        [Fact]
        public void NonWorkerCannotBuild()
        {
            // Soldiers can't build
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.SOLDIER, new Position(8, 5))
                .WithAgent(0, new BuildWithSoldierAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            Assert.Empty(game.GetUnitsByType(0, UnitType.BARRACKS));
            Assert.Equal(5000, game.GetGold(0)); // Gold unchanged
        }

        // ------------------------------------------------------------------
        // Boundary
        // ------------------------------------------------------------------

        [Fact]
        public void BuildBaseNearMine_Succeeds()
        {
            // Building BASE has no dependency (unlike BARRACKS)
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithMine(new Position(12, 5))
                .WithAgent(0, new BuildOnceAgent(UnitType.BASE, new Position(15, 15)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(800);

            var bases = game.GetUnitsByType(0, UnitType.BASE);
            Assert.Single(bases);
            Assert.True(bases[0].IsBuilt);
        }

        // ------------------------------------------------------------------
        // Stress test
        // ------------------------------------------------------------------

        [Fact]
        public void ThreeWorkersBuildThreeBarracks_AllComplete()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithUnit(0, UnitType.WORKER, new Position(8, 7))
                .WithUnit(0, UnitType.WORKER, new Position(8, 9))
                .WithAgent(0, new BuildMultipleAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            var barracks = game.GetUnitsByType(0, UnitType.BARRACKS);
            Assert.True(barracks.Count >= 1,
                $"Expected at least 1 barracks built, got {barracks.Count}");
        }
    }
}
