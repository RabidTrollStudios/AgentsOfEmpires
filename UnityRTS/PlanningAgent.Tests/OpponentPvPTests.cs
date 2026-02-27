using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// PvP tests — opponents play against each other without crashing.
    /// </summary>
    public class OpponentPvPTests : OpponentTestBase
    {
        [Fact]
        public void SoldierRush_vs_Turtle_NoCrash()
        {
            var game = BuildPvPGame(new SoldierRushOpponent(), new TurtleOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);
        }

        [Fact]
        public void EconBoom_vs_Commander_NoCrash()
        {
            var game = BuildPvPGame(new EconBoomOpponent(), new CommanderOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);
        }

        [Fact]
        public void Swarm_vs_ArcherSwarm_NoCrash()
        {
            var game = BuildPvPGame(new SwarmOpponent(), new ArcherSwarmOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);
        }
    }
}
