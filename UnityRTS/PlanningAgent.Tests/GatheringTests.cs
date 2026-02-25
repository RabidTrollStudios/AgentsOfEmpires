using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the gathering system: workers mine gold and deposit at base.
    /// </summary>
    public class GatheringTests
    {
        // ------------------------------------------------------------------
        // Happy path
        // ------------------------------------------------------------------

        [Fact]
        public void WorkerGathers_GoldIncreases()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithMine(new Position(12, 5), health: 10000)
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(500);

            Assert.True(game.GetGold(0) > 0,
                $"Expected gold > 0 after gathering, got {game.GetGold(0)}");
        }

        [Fact]
        public void WorkerGathers_MineHealthDecreases()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithMine(new Position(12, 5), health: 10000)
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Find the mine
            var mines = game.GetUnitsByType(-1, UnitType.MINE);
            Assert.Single(mines);
            float healthBefore = mines[0].Health;

            game.Run(500);

            Assert.True(mines[0].Health < healthBefore,
                $"Mine health should decrease. Before: {healthBefore}, After: {mines[0].Health}");
        }

        [Fact]
        public void WorkerGathers_CyclesBackToMine()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithMine(new Position(12, 5), health: 10000)
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Run long enough for multiple gather trips
            game.Run(1500);

            // Gold should be significantly more than 1 trip (100 gold per trip)
            float miningCapacity = GameConstants.MINING_CAPACITY[UnitType.WORKER];
            Assert.True(game.GetGold(0) > (int)miningCapacity,
                $"Expected multiple gather cycles. Gold: {game.GetGold(0)}, single trip: {miningCapacity}");
        }

        // ------------------------------------------------------------------
        // Boundary cases
        // ------------------------------------------------------------------

        [Fact]
        public void GatherFromNearbyMine_WorksWithShortPath()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                // Mine very close to base
                .WithMine(new Position(9, 5), health: 10000)
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(300);

            Assert.True(game.GetGold(0) > 0);
        }

        [Fact]
        public void GatherFromFarMine_EventuallySucceeds()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(2, 2), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(5, 2))
                // Mine in far corner
                .WithMine(new Position(25, 25), health: 10000)
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            Assert.True(game.GetGold(0) > 0,
                $"Expected gold after gathering from far mine, got {game.GetGold(0)}");
        }

        [Fact]
        public void MineDepleted_WorkerGoesIdle()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                // Mine with very little gold — will deplete quickly
                .WithMine(new Position(12, 5), health: 50)
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(1000);

            // Mine should be depleted (removed from game)
            var mines = game.GetUnitsByType(-1, UnitType.MINE);
            Assert.Empty(mines);

            // Worker should not crash — should be idle or gathering (if agent retries)
            var workers = game.GetUnitsByType(0, UnitType.WORKER);
            Assert.Single(workers);
        }

        // ------------------------------------------------------------------
        // Error cases
        // ------------------------------------------------------------------

        [Fact]
        public void GatherWithNonWorker_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.SOLDIER, new Position(8, 5))
                .WithMine(new Position(12, 5), health: 10000)
                .WithAgent(0, new GatherWithSoldierAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            Assert.Equal(0, game.GetGold(0));
        }

        [Fact]
        public void GatherFromNonMine_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithUnit(0, UnitType.BARRACKS, new Position(12, 5), isBuilt: true)
                .WithAgent(0, new GatherFromBarracksAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            Assert.Equal(0, game.GetGold(0));
        }

        // ------------------------------------------------------------------
        // Stress test
        // ------------------------------------------------------------------

        [Fact]
        public void FiveWorkersGatherSimultaneously_AllDepositGold()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 3))
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithUnit(0, UnitType.WORKER, new Position(8, 7))
                .WithUnit(0, UnitType.WORKER, new Position(9, 4))
                .WithUnit(0, UnitType.WORKER, new Position(9, 6))
                .WithMine(new Position(15, 5), health: 50000)
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(1000);

            // Five workers should gather significantly more than one
            Assert.True(game.GetGold(0) > 200,
                $"5 workers should have gathered significant gold, got {game.GetGold(0)}");
        }
    }
}
