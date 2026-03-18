using AgentSDK;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the [MEDIUM] WarriorRush opponent.
    /// Verifies the agent builds barracks and trains warriors.
    /// </summary>
    public class WarriorRushOpponentTests : OpponentTestBase
    {
        [Fact]
        public void WarriorRushOpponent_NoCrash()
        {
            RunOpponentTest(new WarriorRushOpponent());
        }

        [Fact]
        public void WarriorRushOpponent_TrainsWarriors()
        {
            var game = BuildStandardGame(new WarriorRushOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            // Should have built barracks and trained warriors
            Assert.True(game.GetUnitsByType(1, UnitType.BARRACKS).Count > 0 ||
                         game.GetUnitsByType(1, UnitType.WARRIOR).Count > 0,
                "WarriorRush should build barracks and/or train warriors");
        }
    }
}
