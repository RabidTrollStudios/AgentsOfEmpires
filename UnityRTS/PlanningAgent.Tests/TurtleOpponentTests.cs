using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the [MEDIUM] Turtle opponent.
    /// Verifies the agent runs without crashing.
    /// </summary>
    public class TurtleOpponentTests : OpponentTestBase
    {
        [Fact]
        public void TurtleOpponent_NoCrash()
        {
            RunOpponentTest(new TurtleOpponent());
        }
    }
}
