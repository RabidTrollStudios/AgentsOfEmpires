using Xunit;

namespace Opponent.Tests
{
    /// <summary>
    /// Tests for the [MEDIUM] ArcherSwarm opponent.
    /// Verifies the agent runs without crashing.
    /// </summary>
    public class ArcherSwarmOpponentTests : OpponentTestBase
    {
        [Fact]
        public void ArcherSwarmOpponent_NoCrash()
        {
            RunOpponentTest(new ArcherSwarmOpponent());
        }
    }
}
