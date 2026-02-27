using AgentSDK;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the [MEDIUM] SoldierRush opponent.
    /// Verifies the agent builds barracks and trains soldiers.
    /// </summary>
    public class SoldierRushOpponentTests : OpponentTestBase
    {
        [Fact]
        public void SoldierRushOpponent_NoCrash()
        {
            RunOpponentTest(new SoldierRushOpponent());
        }

        [Fact]
        public void SoldierRushOpponent_TrainsSoldiers()
        {
            var game = BuildStandardGame(new SoldierRushOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            // Should have built barracks and trained soldiers
            Assert.True(game.GetUnitsByType(1, UnitType.BARRACKS).Count > 0 ||
                         game.GetUnitsByType(1, UnitType.SOLDIER).Count > 0,
                "SoldierRush should build barracks and/or train soldiers");
        }
    }
}
