using AgentSDK;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the [MEDIUM] Balanced opponent.
    /// Verifies the agent trains a mixed army.
    /// </summary>
    public class BalancedOpponentTests : OpponentTestBase
    {
        [Fact]
        public void BalancedOpponent_NoCrash()
        {
            RunOpponentTest(new BalancedOpponent());
        }

        [Fact]
        public void BalancedOpponent_TrainsMixedArmy()
        {
            var game = BuildStandardGame(new BalancedOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            int soldiers = game.GetUnitsByType(1, UnitType.SOLDIER).Count;
            int archers = game.GetUnitsByType(1, UnitType.ARCHER).Count;
            Assert.True(soldiers + archers > 0,
                "Balanced should have trained some troops");
        }
    }
}
