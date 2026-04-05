using AgentSDK;
using AgentTestHarness;

namespace Opponent.Tests
{
    /// <summary>
    /// Shared test helpers for opponent agent tests.
    /// Provides standard game setup and run methods.
    /// </summary>
    public abstract class OpponentTestBase
    {
        /// <summary>
        /// Standard game setup for testing an opponent.
        /// 1 pawn each, no base, 1000g, 4 mines at 3000g.
        /// </summary>
        protected SimGame BuildStandardGame(PlanningAgentBase opponent)
        {
            return new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 1000)
                .WithGold(1, 1000)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithUnit(1, UnitType.PAWN, new Position(22, 25))
                .WithMine(new Position(10, 3), health: 3000)
                .WithMine(new Position(20, 27), health: 3000)
                .WithMine(new Position(8, 20), health: 3000)
                .WithMine(new Position(22, 10), health: 3000)
                .WithAgent(0, new DoNothingAgent())
                .WithAgent(1, opponent)
                .Build();
        }

        /// <summary>
        /// Standard PvP game setup for testing two opponents against each other.
        /// 1 pawn each, no base, 1000g, 4 mines at 3000g.
        /// </summary>
        protected SimGame BuildPvPGame(PlanningAgentBase agent0, PlanningAgentBase agent1)
        {
            return new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 1000)
                .WithGold(1, 1000)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithUnit(1, UnitType.PAWN, new Position(22, 25))
                .WithMine(new Position(10, 3), health: 3000)
                .WithMine(new Position(20, 27), health: 3000)
                .WithMine(new Position(8, 20), health: 3000)
                .WithMine(new Position(22, 10), health: 3000)
                .WithAgent(0, agent0)
                .WithAgent(1, agent1)
                .Build();
        }

        protected void RunOpponentTest(PlanningAgentBase opponent, int ticks = 1000)
        {
            var game = BuildStandardGame(opponent);
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(ticks);
        }
    }
}
