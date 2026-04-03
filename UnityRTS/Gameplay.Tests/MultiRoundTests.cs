using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace Gameplay.Tests
{
    /// <summary>
    /// Tests for multi-round game flow: InitializeRound, Learn callbacks,
    /// and state across rounds.
    /// </summary>
    public class MultiRoundTests
    {
        [Fact]
        public void Learn_CalledWithoutCrash()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithMine(new Position(15, 10), health: 10000)
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            // Learn should not throw
            game.Learn();
        }

        [Fact]
        public void MultipleInitializeRound_NoException()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithMine(new Position(15, 10), health: 10000)
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();

            // Simulate multiple rounds
            for (int round = 0; round < 3; round++)
            {
                game.InitializeRound();
                game.Run(100);
                game.Learn();
            }
        }

        [Fact]
        public void GoldPersistsAcrossRounds()
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
            game.Run(500);

            int goldAfterRound1 = game.GetGold(0);
            Assert.True(goldAfterRound1 > 0, "Should have gathered gold in round 1");

            // Start round 2 — gold should persist
            game.Learn();
            game.InitializeRound();

            Assert.Equal(goldAfterRound1, game.GetGold(0));
        }

        [Fact]
        public void UnitsPersisteAcrossRounds()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithMine(new Position(15, 10), health: 10000)
                .WithAgent(0, new TrainOnceAgent(UnitType.PAWN))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Train a pawn in round 1
            bool trained = game.RunUntil(g =>
                g.GetUnitsByType(0, UnitType.PAWN).Count >= 2, 2000);
            Assert.True(trained, "Should train at least one pawn");

            int pawnsAfterRound1 = game.GetUnitsByType(0, UnitType.PAWN).Count;

            // Start round 2
            game.Learn();
            game.InitializeRound();

            Assert.Equal(pawnsAfterRound1, game.GetUnitsByType(0, UnitType.PAWN).Count);
        }

        [Fact]
        public void TickCounter_ContinuesAcrossRounds()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            int tickAfterRound1 = game.CurrentTick;
            Assert.Equal(100, tickAfterRound1);

            game.Learn();
            game.InitializeRound();
            game.Run(50);

            Assert.Equal(150, game.CurrentTick);
        }
    }
}
