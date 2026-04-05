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
        /// Procedural 75x40 OpenField map, 20% trees, 1000g, matching Unity settings.
        /// </summary>
        protected SimGame BuildStandardGame(PlanningAgentBase opponent)
        {
            return new SimGameBuilder()
                .WithGold(0, 1000)
                .WithGold(1, 1000)
                .WithGeneratedMap(new AgentTestHarness.MapGeneratorConfig
                {
                    Width = 75, Height = 40, Seed = 42,
                    Template = AgentSDK.MapTemplate.OpenField,
                    ObstacleDensity = 0.20f, MinesPerPlayer = 2,
                    Symmetry = AgentSDK.SymmetryType.Mirror
                })
                .WithAgent(0, new DoNothingAgent())
                .WithAgent(1, opponent)
                .Build();
        }

        /// <summary>
        /// Standard PvP game setup for testing two opponents against each other.
        /// Procedural 75x40 OpenField map, 20% trees, 1000g, matching Unity settings.
        /// </summary>
        protected SimGame BuildPvPGame(PlanningAgentBase agent0, PlanningAgentBase agent1)
        {
            return new SimGameBuilder()
                .WithGold(0, 1000)
                .WithGold(1, 1000)
                .WithGeneratedMap(new AgentTestHarness.MapGeneratorConfig
                {
                    Width = 75, Height = 40, Seed = 42,
                    Template = AgentSDK.MapTemplate.OpenField,
                    ObstacleDensity = 0.20f, MinesPerPlayer = 2,
                    Symmetry = AgentSDK.SymmetryType.Mirror
                })
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
