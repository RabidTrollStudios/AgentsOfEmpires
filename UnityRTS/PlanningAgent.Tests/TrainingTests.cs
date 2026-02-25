using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the training system: base trains workers, barracks trains soldiers/archers.
    /// </summary>
    public class TrainingTests
    {
        // ------------------------------------------------------------------
        // Happy path
        // ------------------------------------------------------------------

        [Fact]
        public void BaseTrainsWorker_NewWorkerAppears()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainOnceAgent(UnitType.WORKER))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            Assert.Single(game.GetUnitsByType(0, UnitType.WORKER));
        }

        [Fact]
        public void BarracksTrainsSoldier_NewSoldierAppears()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.BARRACKS, new Position(15, 15), isBuilt: true)
                .WithAgent(0, new TrainFromBarracksAgent(UnitType.SOLDIER))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            Assert.Single(game.GetUnitsByType(0, UnitType.SOLDIER));
        }

        [Fact]
        public void BarracksTrainsArcher_NewArcherAppears()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.BARRACKS, new Position(15, 15), isBuilt: true)
                .WithAgent(0, new TrainFromBarracksAgent(UnitType.ARCHER))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(120);

            Assert.Single(game.GetUnitsByType(0, UnitType.ARCHER));
        }

        [Fact]
        public void Training_DeductsGold()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainOnceAgent(UnitType.WORKER))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            int goldBefore = game.GetGold(0);
            game.Run(50);

            int expectedCost = (int)GameConstants.COST[UnitType.WORKER]; // 50
            Assert.True(game.GetGold(0) <= goldBefore - expectedCost,
                $"Gold should decrease by at least {expectedCost}. Before: {goldBefore}, After: {game.GetGold(0)}");
        }

        [Fact]
        public void Training_BuildingReturnsToIdle()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainOnceAgent(UnitType.WORKER))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            var baseUnit = game.GetUnitsByType(0, UnitType.BASE)[0];
            Assert.Equal(UnitAction.IDLE, baseUnit.CurrentAction);
        }

        // ------------------------------------------------------------------
        // Error cases
        // ------------------------------------------------------------------

        [Fact]
        public void TrainWithInsufficientGold_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 10) // Not enough for a worker (costs 50)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainOnceAgent(UnitType.WORKER))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            Assert.Empty(game.GetUnitsByType(0, UnitType.WORKER));
            Assert.Equal(10, game.GetGold(0)); // Gold unchanged
        }

        [Fact]
        public void TrainInvalidUnitType_Rejected()
        {
            // BASE can't train SOLDIER (only BARRACKS can)
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainFromBaseAgent(UnitType.SOLDIER))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            Assert.Empty(game.GetUnitsByType(0, UnitType.SOLDIER));
            Assert.Equal(5000, game.GetGold(0)); // Gold unchanged
        }

        [Fact]
        public void TrainFromUnbuiltBuilding_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: false)
                .WithAgent(0, new TrainOnceAgent(UnitType.WORKER))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            Assert.Empty(game.GetUnitsByType(0, UnitType.WORKER));
        }

        [Fact]
        public void TrainWhileAlreadyTraining_SecondRejected()
        {
            // Agent that tries to train every tick
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new SpamTrainAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            int goldBefore = game.GetGold(0);
            // Run just a few ticks (training is still in progress)
            game.Run(1);

            // Should deduct for exactly one worker
            int expectedGold = goldBefore - (int)GameConstants.COST[UnitType.WORKER];
            Assert.Equal(expectedGold, game.GetGold(0));
        }

        // ------------------------------------------------------------------
        // Boundary cases
        // ------------------------------------------------------------------

        [Fact]
        public void TrainAtMaxSpeed_CompletesQuickly()
        {
            var config = new SimConfig { GameSpeed = 30, TickDuration = 1f / 30f };
            var game = new SimGameBuilder()
                .WithConfig(config)
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainOnceAgent(UnitType.WORKER))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(20);

            Assert.Single(game.GetUnitsByType(0, UnitType.WORKER));
        }

        [Fact]
        public void TrainedUnit_SpawnsOutsideBuildingFootprint()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainOnceAgent(UnitType.WORKER))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            var workers = game.GetUnitsByType(0, UnitType.WORKER);
            Assert.Single(workers);

            var worker = workers[0];
            var baseSize = GameConstants.UNIT_SIZE[UnitType.BASE];
            // Check worker is NOT inside the 3x3 footprint
            for (int i = 0; i < baseSize.X; i++)
            {
                for (int j = 0; j < baseSize.Y; j++)
                {
                    var footprintCell = new Position(10 + i, 10 - j);
                    Assert.NotEqual(footprintCell, worker.GridPosition);
                }
            }
        }

        // ------------------------------------------------------------------
        // Stress test
        // ------------------------------------------------------------------

        [Fact]
        public void TrainFiveWorkersSequentially_AllExist()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainNWorkersAgent(5))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(500);

            var workers = game.GetUnitsByType(0, UnitType.WORKER);
            Assert.Equal(5, workers.Count);

            // All on distinct cells
            var positions = new System.Collections.Generic.HashSet<Position>();
            foreach (var w in workers)
                Assert.True(positions.Add(w.GridPosition),
                    $"Workers share a cell at {w.GridPosition}");
        }
    }
}
