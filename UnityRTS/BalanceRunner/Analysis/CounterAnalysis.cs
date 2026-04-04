using System.Collections.Generic;
using System.Linq;

namespace BalanceRunner.Analysis
{
    /// <summary>
    /// Maps counter relationships between agents.
    /// A "counters" B if A's win rate vs B exceeds the counter threshold.
    /// Identifies agents with no counter (never lose) — a sign of dominance.
    /// </summary>
    public class CounterAnalysis
    {
        /// <summary>Per-agent counter data.</summary>
        public List<AgentCounters> AgentCounterData { get; } = new List<AgentCounters>();

        /// <summary>Agents that have no counter (no agent beats them above threshold).</summary>
        public List<string> Uncounterable { get; } = new List<string>();

        /// <summary>Agents that counter nothing (don't beat anyone above threshold).</summary>
        public List<string> CountersNothing { get; } = new List<string>();

        public const float CounterThreshold = 0.60f;
        public const float LossThreshold = 0.40f;

        public static CounterAnalysis Compute(WinRateMatrix matrix)
        {
            var analysis = new CounterAnalysis();

            foreach (var agent in matrix.AgentNames)
            {
                var counters = new AgentCounters { AgentName = agent };

                foreach (var opponent in matrix.AgentNames)
                {
                    if (agent == opponent) continue;

                    float winRate = matrix.GetRate(agent, opponent);
                    if (winRate < 0) continue;

                    if (winRate >= CounterThreshold)
                        counters.Beats.Add(opponent);
                    if (winRate <= LossThreshold)
                        counters.LosesTo.Add(opponent);
                }

                analysis.AgentCounterData.Add(counters);

                if (counters.LosesTo.Count == 0 && counters.Beats.Count > 0)
                    analysis.Uncounterable.Add(agent);
                if (counters.Beats.Count == 0)
                    analysis.CountersNothing.Add(agent);
            }

            return analysis;
        }
    }

    public class AgentCounters
    {
        public string AgentName { get; set; }
        public List<string> Beats { get; set; } = new List<string>();
        public List<string> LosesTo { get; set; } = new List<string>();
    }
}
