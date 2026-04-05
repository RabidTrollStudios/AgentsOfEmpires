using Xunit;

namespace Opponent.Tests
{
    /// <summary>
    /// PvP tests — opponents play against each other without crashing.
    /// Tests one matchup per tier to verify stability.
    /// </summary>
    public class OpponentPvPTests : OpponentTestBase
    {
        [Fact]
        public void EasyTier_WarriorTurtle_vs_ArcherTurtle_NoCrash()
        {
            var game = BuildPvPGame(new WarriorTurtleOpponent(), new ArcherTurtleOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);
        }

        [Fact]
        public void MidTier_WarriorRush_vs_LancerRush_NoCrash()
        {
            var game = BuildPvPGame(new WarriorRushOpponent(), new LancerRushOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);
        }

        [Fact]
        public void HardTier_ArcherDual_vs_LancerDual_NoCrash()
        {
            var game = BuildPvPGame(new ArcherDualOpponent(), new LancerDualOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);
        }

        [Fact]
        public void CrossTier_MixedTurtle_vs_MixedDual_NoCrash()
        {
            var game = BuildPvPGame(new MixedTurtleOpponent(), new MixedDualOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);
        }
    }
}
