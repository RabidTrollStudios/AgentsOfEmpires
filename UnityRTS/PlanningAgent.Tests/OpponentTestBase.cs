using AgentSDK;
using AgentTestHarness;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Shared test helpers for opponent agent tests.
    /// Provides standard game setup and run methods.
    /// </summary>
    public abstract class OpponentTestBase
    {
        /// <summary>
        /// Standard game setup for testing an opponent.
        /// Opponent is agent 1 with a base, pawn, and mine.
        /// </summary>
        protected SimGame BuildStandardGame(PlanningAgentBase opponent)
        {
            return new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithUnit(1, UnitType.BASE, new Position(25, 25), isBuilt: true)
                .WithUnit(1, UnitType.PAWN, new Position(22, 25))
                .WithMine(new Position(15, 10), health: 10000)
                .WithMine(new Position(15, 20), health: 10000)
                .WithAgent(0, new DoNothingAgent())
                .WithAgent(1, opponent)
                .Build();
        }

        /// <summary>
        /// Standard PvP game setup for testing two opponents against each other.
        /// </summary>
        protected SimGame BuildPvPGame(PlanningAgentBase agent0, PlanningAgentBase agent1)
        {
            return new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithUnit(1, UnitType.BASE, new Position(25, 25), isBuilt: true)
                .WithUnit(1, UnitType.PAWN, new Position(22, 25))
                .WithMine(new Position(15, 10), health: 10000)
                .WithMine(new Position(15, 20), health: 10000)
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
