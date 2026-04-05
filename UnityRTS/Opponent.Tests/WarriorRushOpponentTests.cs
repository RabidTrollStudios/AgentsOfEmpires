using Xunit;

namespace Opponent.Tests
{
    /// <summary>
    /// Tests for the WarriorRush opponent.
    /// Verifies the agent runs without crashing.
    /// </summary>
    public class WarriorRushOpponentTests : OpponentTestBase
    {
        [Fact]
        public void WarriorRushOpponent_NoCrash()
        {
            RunOpponentTest(new WarriorRushOpponent());
        }
    }
}
