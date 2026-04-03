using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace Gameplay.Tests
{
    /// <summary>
    /// Tests for the training system: base trains pawns, barracks trains warriors/archers.
    /// </summary>
    public class TrainingTests
    {
        // ------------------------------------------------------------------
        // Happy path
        // ------------------------------------------------------------------

        [Fact]
        public void BaseTrainsPawn_NewPawnAppears()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainOnceAgent(UnitType.PAWN))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            Assert.Single(game.GetUnitsByType(0, UnitType.PAWN));
        }

        [Fact]
        public void BarracksTrainsWarrior_NewWarriorAppears()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.BARRACKS, new Position(15, 15), isBuilt: true)
                .WithAgent(0, new TrainFromBarracksAgent(UnitType.WARRIOR))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            Assert.Single(game.GetUnitsByType(0, UnitType.WARRIOR));
        }

        [Fact]
        public void ArcheryTrainsArcher_NewArcherAppears()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.ARCHERY, new Position(15, 15), isBuilt: true)
                .WithAgent(0, new TrainFromArcheryAgent(UnitType.ARCHER))
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
                .WithAgent(0, new TrainOnceAgent(UnitType.PAWN))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            int goldBefore = game.GetGold(0);
            game.Run(50);

            int expectedCost = (int)GameConstants.COST[UnitType.PAWN]; // 50
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
                .WithAgent(0, new TrainOnceAgent(UnitType.PAWN))
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
                .WithGold(0, 10) // Not enough for a pawn (costs 50)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainOnceAgent(UnitType.PAWN))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            Assert.Empty(game.GetUnitsByType(0, UnitType.PAWN));
            Assert.Equal(10, game.GetGold(0)); // Gold unchanged
        }

        [Fact]
        public void TrainInvalidUnitType_Rejected()
        {
            // BASE can't train WARRIOR (only BARRACKS can)
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainFromBaseAgent(UnitType.WARRIOR))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            Assert.Empty(game.GetUnitsByType(0, UnitType.WARRIOR));
            Assert.Equal(5000, game.GetGold(0)); // Gold unchanged
        }

        [Fact]
        public void TrainFromUnbuiltBuilding_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: false)
                .WithAgent(0, new TrainOnceAgent(UnitType.PAWN))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            Assert.Empty(game.GetUnitsByType(0, UnitType.PAWN));
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
            // Run 2 ticks: tick 1 = agent queues TRAIN, tick 2 = command dispatched
            game.Run(2);

            // Should deduct for exactly one pawn
            int expectedGold = goldBefore - (int)GameConstants.COST[UnitType.PAWN];
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
                .WithAgent(0, new TrainOnceAgent(UnitType.PAWN))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(20);

            Assert.Single(game.GetUnitsByType(0, UnitType.PAWN));
        }

        [Fact]
        public void TrainedUnit_SpawnsOutsideBuildingFootprint()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainOnceAgent(UnitType.PAWN))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            var pawns = game.GetUnitsByType(0, UnitType.PAWN);
            Assert.Single(pawns);

            var pawn = pawns[0];
            var baseSize = GameConstants.UNIT_SIZE[UnitType.BASE];
            // Check pawn is NOT inside the 3x3 footprint
            for (int i = 0; i < baseSize.X; i++)
            {
                for (int j = 0; j < baseSize.Y; j++)
                {
                    var footprintCell = new Position(10 + i, 10 - j);
                    Assert.NotEqual(footprintCell, pawn.GridPosition);
                }
            }
        }

        // ------------------------------------------------------------------
        // Stress test
        // ------------------------------------------------------------------

        [Fact]
        public void TrainFivePawnsSequentially_AllExist()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainNPawnsAgent(5))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(500);

            var pawns = game.GetUnitsByType(0, UnitType.PAWN);
            Assert.Equal(5, pawns.Count);

            // All on distinct cells
            var positions = new System.Collections.Generic.HashSet<Position>();
            foreach (var w in pawns)
                Assert.True(positions.Add(w.GridPosition),
                    $"Pawns share a cell at {w.GridPosition}");
        }
    }
}
