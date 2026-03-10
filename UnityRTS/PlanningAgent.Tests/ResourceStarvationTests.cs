using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for resource starvation and economy edge cases:
    /// zero gold, depleted mines, insufficient funds, economic recovery.
    /// </summary>
    public class ResourceStarvationTests
    {
        // ------------------------------------------------------------------
        // Zero gold scenarios
        // ------------------------------------------------------------------

        [Fact]
        public void ZeroGold_TrainingRejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithAgent(0, new SpamTrainAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            // Should not have trained any pawns with 0 gold
            Assert.Single(game.GetUnitsByType(0, UnitType.BASE));
            Assert.Empty(game.GetUnitsByType(0, UnitType.PAWN));
            Assert.Equal(0, game.GetGold(0));
        }

        [Fact]
        public void ZeroGold_BuildingRejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithAgent(0, new BuildOnceAgent(UnitType.BARRACKS, new Position(15, 15)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(200);

            Assert.Empty(game.GetUnitsByType(0, UnitType.BARRACKS));
            Assert.Equal(0, game.GetGold(0));
        }

        // ------------------------------------------------------------------
        // Mine depletion
        // ------------------------------------------------------------------

        [Fact]
        public void MineDepleted_PawnSurvivesAndGoesIdle()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithMine(new Position(12, 5), health: 10) // Tiny mine
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(1000);

            // Mine should be gone
            Assert.Empty(game.GetUnitsByType(-1, UnitType.MINE));
            // Pawn should survive
            Assert.Single(game.GetUnitsByType(0, UnitType.PAWN));
        }

        [Fact]
        public void AllMinesDepleted_EconomyStalls()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithMine(new Position(12, 5), health: 50)
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            // Mine depleted
            Assert.Empty(game.GetUnitsByType(-1, UnitType.MINE));

            int goldAfterDepletion = game.GetGold(0);
            game.Run(500);

            // Gold should not increase further
            Assert.Equal(goldAfterDepletion, game.GetGold(0));
        }

        // ------------------------------------------------------------------
        // Spending races
        // ------------------------------------------------------------------

        [Fact]
        public void InsufficientGold_PartialTrainBatch()
        {
            // Only enough gold for 1 pawn (50g), not 2
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 60)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithAgent(0, new SpamTrainAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            bool trained = game.RunUntil(g =>
                g.GetUnitsByType(0, UnitType.PAWN).Count >= 1, 500);

            Assert.True(trained, "Should train exactly 1 pawn with 60 gold");

            // Gold should be nearly depleted (50 spent)
            Assert.True(game.GetGold(0) < 50,
                $"Gold should be depleted after training. Got: {game.GetGold(0)}");
        }

        [Fact]
        public void GoldNeverGoesNegative()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 100)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithAgent(0, new SpamTrainAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(500);

            Assert.True(game.GetGold(0) >= 0,
                $"Gold should never go negative. Got: {game.GetGold(0)}");
        }

        // ------------------------------------------------------------------
        // Recovery
        // ------------------------------------------------------------------

        [Fact]
        public void EconomyRecovery_GatherAfterSpendingAllGold()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithMine(new Position(12, 5), health: 50000)
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(1000);

            // Starting from 0, should have recovered gold through gathering
            Assert.True(game.GetGold(0) > 0,
                $"Should recover gold from gathering. Got: {game.GetGold(0)}");
        }
    }
}
