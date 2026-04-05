using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using AgentTestHarness;
using BalanceRunner.Telemetry;

namespace BalanceRunner.Runner
{
    /// <summary>
    /// Runs a single match between two agents with telemetry collection.
    /// Builds the SimGame, runs tick-by-tick, determines the winner,
    /// and returns a complete MatchResult.
    /// </summary>
    public class MatchRunner
    {
        /// <summary>
        /// Run a single match and return telemetry results.
        /// </summary>
        /// <param name="agent0Name">Display name for agent 0.</param>
        /// <param name="agent0">Agent instance for slot 0.</param>
        /// <param name="agent1Name">Display name for agent 1.</param>
        /// <param name="agent1">Agent instance for slot 1.</param>
        /// <param name="seed">Map generation seed.</param>
        /// <param name="tickLimit">Maximum ticks before timeout.</param>
        /// <param name="mapTemplate">Map template for generation.</param>
        public static MatchResult Run(
            string agent0Name, PlanningAgentBase agent0,
            string agent1Name, PlanningAgentBase agent1,
            int seed, int tickLimit = 5000, MapTemplate mapTemplate = MapTemplate.OpenField)
        {
            // Procedural map matching Unity settings: 75x40, OpenField, 20% trees.
            // 1000g starting gold. Map generator places pawns and mines automatically.
            var game = new SimGameBuilder()
                .WithGold(0, 1000)
                .WithGold(1, 1000)
                .WithGeneratedMap(new AgentTestHarness.MapGeneratorConfig
                {
                    Width = 75,
                    Height = 40,
                    Seed = seed,
                    Template = mapTemplate,
                    ObstacleDensity = 0.20f,
                    MinesPerPlayer = 2,
                    Symmetry = AgentSDK.SymmetryType.Mirror
                })
                .WithAgent(0, agent0)
                .WithAgent(1, agent1)
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            var collector = new TelemetryCollector(game);
            MatchEndReason endReason = MatchEndReason.Timeout;
            int winner = -1;

            for (int tick = 0; tick < tickLimit; tick++)
            {
                collector.TickAndCollect();

                // Check for elimination: agent lost all units
                bool agent0HasUnits = HasAnyUnits(game, 0);
                bool agent1HasUnits = HasAnyUnits(game, 1);

                if (!agent0HasUnits && !agent1HasUnits)
                {
                    winner = -1;
                    endReason = MatchEndReason.Draw;
                    break;
                }
                if (!agent0HasUnits)
                {
                    winner = 1;
                    endReason = MatchEndReason.Elimination;
                    break;
                }
                if (!agent1HasUnits)
                {
                    winner = 0;
                    endReason = MatchEndReason.Elimination;
                    break;
                }

                // Check for base destruction (skip first 500 ticks to let both players build)
                if (tick >= 500)
                {
                bool agent0HasBase = game.GetUnitsByType(0, UnitType.BASE).Count > 0;
                bool agent1HasBase = game.GetUnitsByType(1, UnitType.BASE).Count > 0;

                if (agent0HasBase || agent1HasBase)
                {
                    if (!agent0HasBase && !agent1HasBase)
                    {
                        winner = -1;
                        endReason = MatchEndReason.Draw;
                        break;
                    }
                    if (!agent0HasBase)
                    {
                        winner = 1;
                        endReason = MatchEndReason.BaseDestroyed;
                        break;
                    }
                    if (!agent1HasBase)
                    {
                        winner = 0;
                        endReason = MatchEndReason.BaseDestroyed;
                        break;
                    }
                }
                } // tick >= 500 grace period
            }

            // Timeout: determine winner by score
            if (endReason == MatchEndReason.Timeout)
            {
                int score0 = ComputeScore(game, 0);
                int score1 = ComputeScore(game, 1);

                if (score0 > score1)
                    winner = 0;
                else if (score1 > score0)
                    winner = 1;
                else
                    winner = -1;
            }

            collector.FinalizeStats();

            return new MatchResult
            {
                Agent0Name = agent0Name,
                Agent1Name = agent1Name,
                Seed = seed,
                MapTemplate = mapTemplate,
                TickLimit = tickLimit,
                Winner = winner,
                DurationTicks = game.CurrentTick,
                EndReason = endReason,
                Agent0Stats = collector.GetStats(0),
                Agent1Stats = collector.GetStats(1)
            };
        }

        private static bool HasAnyUnits(SimGame game, int agentNbr)
        {
            return game.Units.Values.Any(u => u.OwnerAgentNbr == agentNbr);
        }

        /// <summary>
        /// Score = sum of UNIT_VALUE for surviving units + gold / SCORING_SCALAR.
        /// </summary>
        private static int ComputeScore(SimGame game, int agentNbr)
        {
            int score = 0;
            foreach (var unit in game.Units.Values)
            {
                if (unit.OwnerAgentNbr == agentNbr)
                    score += DerivedGameConstants.UNIT_VALUE[unit.UnitType];
            }
            score += (int)(game.GetGold(agentNbr) / GameConstants.SCORING_SCALAR);
            return score;
        }
    }
}
