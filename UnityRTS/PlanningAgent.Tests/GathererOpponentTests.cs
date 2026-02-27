using AgentSDK;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the [EASY] Gatherer opponent.
    /// Verifies the agent trains workers but never builds military.
    /// </summary>
    public class GathererOpponentTests : OpponentTestBase
    {
        [Fact]
        public void GathererOpponent_NoCrash()
        {
            RunOpponentTest(new GathererOpponent());
        }

        [Fact]
        public void GathererOpponent_TrainsWorkers_ButNoMilitary()
        {
            var game = BuildStandardGame(new GathererOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            // Should have multiple workers
            Assert.True(game.GetUnitsByType(1, UnitType.WORKER).Count > 1,
                "Gatherer should train workers");
            // No military
            Assert.Empty(game.GetUnitsByType(1, UnitType.SOLDIER));
            Assert.Empty(game.GetUnitsByType(1, UnitType.ARCHER));
            Assert.Empty(game.GetUnitsByType(1, UnitType.BARRACKS));
        }
    }
}
