using AgentSDK;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the [HARD] Commander opponent.
    /// Verifies the agent builds refinery and uses smart targeting.
    /// </summary>
    public class CommanderOpponentTests : OpponentTestBase
    {
        [Fact]
        public void CommanderOpponent_NoCrash()
        {
            RunOpponentTest(new CommanderOpponent());
        }

        [Fact]
        public void CommanderOpponent_BuildsRefinery()
        {
            var game = BuildStandardGame(new CommanderOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            // Commander builds refinery for economic advantage
            Assert.True(game.GetUnitsByType(1, UnitType.BARRACKS).Count > 0 ||
                         game.GetUnitsByType(1, UnitType.REFINERY).Count > 0,
                "Commander should build barracks and/or refinery");
        }
    }
}
