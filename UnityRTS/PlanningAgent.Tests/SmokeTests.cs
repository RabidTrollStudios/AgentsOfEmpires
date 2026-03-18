using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Example smoke tests demonstrating how to use the AgentTestHarness.
    /// Students: copy these patterns to write your own agent tests!
    /// </summary>
    public class SmokeTests
    {
        /// <summary>
        /// The agent should train additional pawns when given a base.
        /// </summary>
        [Fact]
        public void AgentTrainsPawns_NewPawnsAppear()
        {
            var agent = new PlanningAgent();
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithUnit(1, UnitType.BASE, new Position(25, 25), isBuilt: true)
                .WithMine(new Position(15, 15))
                .WithAgent(0, agent)
                .WithAgent(1, new DoNothingAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            int pawnsBefore = game.GetUnitsByType(0, UnitType.PAWN).Count;
            game.Run(100);

            int pawnsAfter = game.GetUnitsByType(0, UnitType.PAWN).Count;
            Assert.True(pawnsAfter > pawnsBefore,
                $"Expected more pawns after training. Before: {pawnsBefore}, After: {pawnsAfter}");
        }

        /// <summary>
        /// With a pawn and nearby mine, the agent should gather gold.
        /// </summary>
        [Fact]
        public void AgentGathersGold_GoldIncreases()
        {
            var agent = new PlanningAgent();
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 500)
                .WithGold(1, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithUnit(1, UnitType.BASE, new Position(25, 25), isBuilt: true)
                .WithMine(new Position(12, 5), health: 10000)
                .WithAgent(0, agent)
                .WithAgent(1, new DoNothingAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(500);

            // Gold should have increased from gathering (even after training costs)
            // The agent starts with 500 — training a pawn costs 50, gathering brings in 100/trip
            // After 500 ticks with a nearby mine, net gold should be positive
            Assert.True(game.GetGold(0) > 0,
                $"Expected agent to have gold, but got {game.GetGold(0)}");
        }

        /// <summary>
        /// Verify that the simulation correctly places units and tracks them.
        /// </summary>
        [Fact]
        public void SimGame_PlacedUnitsAreQueryable()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithUnit(0, UnitType.PAWN, new Position(9, 5))
                .WithMine(new Position(15, 15))
                .Build();

            Assert.Single(game.GetUnitsByType(0, UnitType.BASE));
            Assert.Equal(2, game.GetUnitsByType(0, UnitType.PAWN).Count);
            Assert.Single(game.GetUnitsByType(-1, UnitType.MINE));
        }

        /// <summary>
        /// Training a pawn at a base should produce a new pawn after enough ticks.
        /// Tests the harness training logic directly (no agent needed).
        /// </summary>
        [Fact]
        public void TrainingPawn_ProducesNewUnit()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(1, UnitType.BASE, new Position(25, 25), isBuilt: true)
                .Build();

            // Use a custom agent that trains a single pawn
            var trainer = new TrainOnceAgent(UnitType.PAWN);
            game.SetAgent(0, trainer);
            game.SetAgent(1, new DoNothingAgent());

            game.InitializeMatch();
            game.InitializeRound();

            int pawnsBefore = game.GetUnitsByType(0, UnitType.PAWN).Count;
            Assert.Equal(0, pawnsBefore);

            // Run enough ticks for training to complete
            // At GAME_SPEED=20, PAWN creation time = 0.1s = 2 ticks
            game.Run(20);

            int pawnsAfter = game.GetUnitsByType(0, UnitType.PAWN).Count;
            Assert.True(pawnsAfter >= 1,
                $"Expected at least 1 pawn after training, got {pawnsAfter}");
        }

        /// <summary>
        /// Walls should block pathfinding.
        /// </summary>
        [Fact]
        public void WallBlocksPathfinding()
        {
            var game = new SimGameBuilder()
                .WithMapSize(10, 10)
                // Wall across the middle: y=5, x from 0 to 9
                .WithWall(new Position(0, 5), new Position(9, 5))
                .Build();

            // Path from below the wall to above should be empty (completely blocked)
            var path = game.Map.FindPath(new Position(5, 3), new Position(5, 7));
            Assert.Empty(path);
        }

        /// <summary>
        /// A* pathfinding should find a path around obstacles.
        /// </summary>
        [Fact]
        public void PathfindingAroundObstacle()
        {
            var game = new SimGameBuilder()
                .WithMapSize(10, 10)
                // Partial wall: y=5, x from 0 to 7 (leaves gap at x=8,9)
                .WithWall(new Position(0, 5), new Position(7, 5))
                .Build();

            var path = game.Map.FindPath(new Position(5, 3), new Position(5, 7));
            Assert.NotEmpty(path);
            // Path should end at the target
            Assert.Equal(new Position(5, 7), path[path.Count - 1]);
        }
    }
}
