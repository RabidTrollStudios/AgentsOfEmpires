using AgentSDK;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the [EASY] Idle opponent.
    /// Verifies the agent runs without crashing and does nothing.
    /// </summary>
    public class IdleOpponentTests : OpponentTestBase
    {
        [Fact]
        public void IdleOpponent_NoCrash()
        {
            RunOpponentTest(new IdleOpponent());
        }

        [Fact]
        public void IdleOpponent_DoesNothing()
        {
            var game = BuildStandardGame(new IdleOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(500);

            // Idle opponent should still have exactly 1 base and 1 pawn, nothing more
            Assert.Single(game.GetUnitsByType(1, UnitType.BASE));
            Assert.Single(game.GetUnitsByType(1, UnitType.PAWN));
            Assert.Empty(game.GetUnitsByType(1, UnitType.WARRIOR));
            Assert.Empty(game.GetUnitsByType(1, UnitType.ARCHER));
            Assert.Empty(game.GetUnitsByType(1, UnitType.BARRACKS));
        }
    }
}
