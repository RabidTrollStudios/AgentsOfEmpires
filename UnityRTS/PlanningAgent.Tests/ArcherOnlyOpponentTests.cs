using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the [EASY] ArcherOnly opponent.
    /// Verifies the agent runs without crashing.
    /// </summary>
    public class ArcherOnlyOpponentTests : OpponentTestBase
    {
        [Fact]
        public void ArcherOnlyOpponent_NoCrash()
        {
            RunOpponentTest(new ArcherOnlyOpponent());
        }
    }
}
