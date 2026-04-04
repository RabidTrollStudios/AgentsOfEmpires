using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using BalanceRunner.Telemetry;

namespace BalanceRunner.Analysis
{
    /// <summary>
    /// Analyzes unit production patterns vs win rates to identify
    /// over-performing and under-performing units relative to their cost.
    /// References GameConstants directly for cost/stat lookups.
    /// </summary>
    public class CostEfficiencyAnalysis
    {
        /// <summary>Per-unit-type efficiency metrics.</summary>
        public List<UnitEfficiency> UnitEfficiencies { get; } = new List<UnitEfficiency>();

        public static CostEfficiencyAnalysis Compute(List<MatchResult> results)
        {
            var analysis = new CostEfficiencyAnalysis();

            // Only analyze military + monk units (not buildings, pawns, mines)
            var unitTypes = new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER, UnitType.MONK };

            foreach (var unitType in unitTypes)
            {
                int producedInWins = 0;
                int producedInLosses = 0;
                int totalProduced = 0;
                int matchesWithUnit = 0;
                int winsWithUnit = 0;

                foreach (var r in results)
                {
                    if (r.Agent0Name == r.Agent1Name) continue;

                    for (int a = 0; a < 2; a++)
                    {
                        var stats = r.GetStats(a);
                        int produced = stats.GetProduced(unitType);

                        if (produced > 0)
                        {
                            totalProduced += produced;
                            matchesWithUnit++;

                            if (r.Winner == a)
                            {
                                producedInWins += produced;
                                winsWithUnit++;
                            }
                            else
                            {
                                producedInLosses += produced;
                            }
                        }
                    }
                }

                float cost = GameConstants.COST[unitType];
                float hp = GameConstants.HEALTH[unitType];
                float baseDmg = GameConstants.BASE_DAMAGE.ContainsKey(unitType)
                    ? GameConstants.BASE_DAMAGE[unitType] : 0;

                analysis.UnitEfficiencies.Add(new UnitEfficiency
                {
                    UnitType = unitType,
                    Cost = (int)cost,
                    HpPerGold = hp / cost,
                    DpsPerGold = baseDmg / cost,
                    TotalProduced = totalProduced,
                    ProducedInWins = producedInWins,
                    ProducedInLosses = producedInLosses,
                    MatchesWithUnit = matchesWithUnit,
                    WinsWithUnit = winsWithUnit,
                    WinRateWhenProduced = matchesWithUnit > 0
                        ? (float)winsWithUnit / matchesWithUnit
                        : 0f
                });
            }

            return analysis;
        }
    }

    /// <summary>Per-unit-type cost efficiency metrics.</summary>
    public class UnitEfficiency
    {
        public UnitType UnitType { get; set; }
        public int Cost { get; set; }
        public float HpPerGold { get; set; }
        public float DpsPerGold { get; set; }
        public int TotalProduced { get; set; }
        public int ProducedInWins { get; set; }
        public int ProducedInLosses { get; set; }
        public int MatchesWithUnit { get; set; }
        public int WinsWithUnit { get; set; }
        public float WinRateWhenProduced { get; set; }
    }
}
