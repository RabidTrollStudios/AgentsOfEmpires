using Xunit;

namespace Opponent.Tests
{
    /// <summary>
    /// Tests for the [HARD] EconBoom opponent.
    /// Verifies the agent runs without crashing.
    /// </summary>
    public class EconBoomOpponentTests : OpponentTestBase
    {
        [Fact]
        public void EconBoomOpponent_NoCrash()
        {
            RunOpponentTest(new EconBoomOpponent());
        }
    }
}
