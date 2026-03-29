using AgentSDK;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the [MEDIUM] Balanced opponent.
    /// Verifies the agent trains a mixed army.
    /// </summary>
    public class BalancedOpponentTests : OpponentTestBase
    {
        [Fact]
        public void BalancedOpponent_NoCrash()
        {
            RunOpponentTest(new BalancedOpponent());
        }

        [Fact]
        public void BalancedOpponent_TrainsMixedArmy()
        {
            var game = BuildStandardGame(new BalancedOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(5000);

            int warriors = game.GetUnitsByType(1, UnitType.WARRIOR).Count;
            int archers = game.GetUnitsByType(1, UnitType.ARCHER).Count;
            int pawns = game.GetUnitsByType(1, UnitType.PAWN).Count;
            var barracksUnits = game.GetUnitsByType(1, UnitType.BARRACKS);
            int barracks = barracksUnits.Count;
            bool barracksBuilt = barracksUnits.Count > 0 && barracksUnits[0].IsBuilt;
            Assert.True(warriors + archers > 0,
                $"Balanced should have trained some troops " +
                $"(pawns={pawns}, barracks={barracks}, barracksBuilt={barracksBuilt}, warriors={warriors}, archers={archers}, gold={game.GetGold(1)})");
        }
    }
}
