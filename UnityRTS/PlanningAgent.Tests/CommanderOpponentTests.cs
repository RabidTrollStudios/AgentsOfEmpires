using AgentSDK;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the [HARD] Commander opponent.
    /// Verifies the agent uses smart targeting and builds infrastructure.
    /// </summary>
    public class CommanderOpponentTests : OpponentTestBase
    {
        [Fact]
        public void CommanderOpponent_NoCrash()
        {
            RunOpponentTest(new CommanderOpponent());
        }

        [Fact]
        public void CommanderOpponent_BuildsBarracks()
        {
            var game = BuildStandardGame(new CommanderOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            // Commander builds barracks for military production
            Assert.True(game.GetUnitsByType(1, UnitType.BARRACKS).Count > 0,
                "Commander should build barracks");
        }
    }
}
