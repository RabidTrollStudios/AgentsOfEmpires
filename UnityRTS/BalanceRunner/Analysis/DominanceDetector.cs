using System.Collections.Generic;
using System.Linq;

namespace BalanceRunner.Analysis
{
    /// <summary>
    /// Detects dominant (overpowered) and non-viable (underpowered) strategies
    /// based on aggregate win rates.
    /// </summary>
    public class DominanceDetector
    {
        /// <summary>Agents with aggregate win rate above the dominance threshold.</summary>
        public List<DominanceEntry> DominantAgents { get; } = new List<DominanceEntry>();

        /// <summary>Agents with aggregate win rate below the weakness threshold.</summary>
        public List<DominanceEntry> WeakAgents { get; } = new List<DominanceEntry>();

        public const float DominanceThreshold = 0.75f;
        public const float WeaknessThreshold = 0.25f;

        public static DominanceDetector Compute(WinRateMatrix matrix)
        {
            var detector = new DominanceDetector();

            foreach (var kvp in matrix.AggregateWinRates.OrderByDescending(k => k.Value))
            {
                if (kvp.Value >= DominanceThreshold)
                {
                    detector.DominantAgents.Add(new DominanceEntry
                    {
                        AgentName = kvp.Key,
                        AggregateWinRate = kvp.Value
                    });
                }
                else if (kvp.Value <= WeaknessThreshold)
                {
                    detector.WeakAgents.Add(new DominanceEntry
                    {
                        AgentName = kvp.Key,
                        AggregateWinRate = kvp.Value
                    });
                }
            }

            return detector;
        }
    }

    public class DominanceEntry
    {
        public string AgentName { get; set; }
        public float AggregateWinRate { get; set; }
    }
}
