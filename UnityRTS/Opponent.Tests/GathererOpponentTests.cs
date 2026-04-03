using AgentSDK;
using Xunit;

namespace Opponent.Tests
{
    /// <summary>
    /// Tests for the [EASY] Gatherer opponent.
    /// Verifies the agent trains pawns but never builds military.
    /// </summary>
    public class GathererOpponentTests : OpponentTestBase
    {
        [Fact]
        public void GathererOpponent_NoCrash()
        {
            RunOpponentTest(new GathererOpponent());
        }

        [Fact]
        public void GathererOpponent_TrainsPawns_ButNoMilitary()
        {
            var game = BuildStandardGame(new GathererOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            // Should have multiple pawns
            Assert.True(game.GetUnitsByType(1, UnitType.PAWN).Count > 1,
                "Gatherer should train pawns");
            // No military
            Assert.Empty(game.GetUnitsByType(1, UnitType.WARRIOR));
            Assert.Empty(game.GetUnitsByType(1, UnitType.ARCHER));
            Assert.Empty(game.GetUnitsByType(1, UnitType.BARRACKS));
        }
    }
}
