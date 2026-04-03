using Xunit;

namespace Opponent.Tests
{
    /// <summary>
    /// Tests for the [HARD] Swarm opponent.
    /// Verifies the agent runs without crashing.
    /// </summary>
    public class SwarmOpponentTests : OpponentTestBase
    {
        [Fact]
        public void SwarmOpponent_NoCrash()
        {
            RunOpponentTest(new SwarmOpponent());
        }
    }
}
