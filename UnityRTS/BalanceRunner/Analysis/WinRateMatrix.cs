using System.Collections.Generic;
using System.Linq;
using BalanceRunner.Telemetry;

namespace BalanceRunner.Analysis
{
    /// <summary>
    /// Computes win rates for each matchup pair across all seeds and seat orderings.
    /// </summary>
    public class WinRateMatrix
    {
        /// <summary>Win rate of row-agent vs column-agent (0.0 to 1.0). Key = (winner, loser).</summary>
        public Dictionary<(string, string), float> Rates { get; } = new Dictionary<(string, string), float>();

        /// <summary>Aggregate win rate per agent across all opponents.</summary>
        public Dictionary<string, float> AggregateWinRates { get; } = new Dictionary<string, float>();

        /// <summary>All agent names found in results.</summary>
        public List<string> AgentNames { get; } = new List<string>();

        public static WinRateMatrix Compute(List<MatchResult> results)
        {
            var matrix = new WinRateMatrix();

            // Collect all agent names
            var names = new HashSet<string>();
            foreach (var r in results)
            {
                names.Add(r.Agent0Name);
                names.Add(r.Agent1Name);
            }
            matrix.AgentNames.AddRange(names.OrderBy(n => n));

            // Count wins and total matches per ordered pair
            var wins = new Dictionary<(string, string), int>();
            var totals = new Dictionary<(string, string), int>();

            foreach (var r in results)
            {
                // Skip self-play for win rate matrix
                if (r.Agent0Name == r.Agent1Name) continue;

                var key01 = (r.Agent0Name, r.Agent1Name);
                var key10 = (r.Agent1Name, r.Agent0Name);

                if (!totals.ContainsKey(key01)) { totals[key01] = 0; wins[key01] = 0; }
                if (!totals.ContainsKey(key10)) { totals[key10] = 0; wins[key10] = 0; }

                totals[key01]++;
                totals[key10]++;

                if (r.Winner == 0)
                    wins[key01]++;
                else if (r.Winner == 1)
                    wins[key10]++;
                // Draw: neither gets a win
            }

            // Compute rates
            foreach (var key in totals.Keys)
            {
                matrix.Rates[key] = totals[key] > 0 ? (float)wins[key] / totals[key] : 0f;
            }

            // Compute aggregate win rates
            foreach (var name in matrix.AgentNames)
            {
                int totalWins = 0;
                int totalMatches = 0;

                foreach (var key in totals.Keys)
                {
                    if (key.Item1 == name && key.Item1 != key.Item2)
                    {
                        totalWins += wins[key];
                        totalMatches += totals[key];
                    }
                }

                matrix.AggregateWinRates[name] = totalMatches > 0
                    ? (float)totalWins / totalMatches
                    : 0f;
            }

            return matrix;
        }

        /// <summary>Get win rate of agent A vs agent B. Returns -1 if no data.</summary>
        public float GetRate(string a, string b)
        {
            return Rates.TryGetValue((a, b), out float rate) ? rate : -1f;
        }
    }
}
