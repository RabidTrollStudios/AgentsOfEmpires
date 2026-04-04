using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using BalanceRunner.Telemetry;

namespace BalanceRunner.Analysis
{
    /// <summary>
    /// Checks which unit types are viable in practice: produced by agents
    /// that actually win matches. Flags unit types that are never produced
    /// or only produced in losing strategies.
    /// </summary>
    public class DiversityAnalysis
    {
        /// <summary>Per-unit-type viability data.</summary>
        public List<UnitViability> UnitViabilities { get; } = new List<UnitViability>();

        /// <summary>Unit types never produced by any agent.</summary>
        public List<UnitType> NeverProduced { get; } = new List<UnitType>();

        /// <summary>Unit types only produced in losing matches.</summary>
        public List<UnitType> OnlyInLosses { get; } = new List<UnitType>();

        public static DiversityAnalysis Compute(List<MatchResult> results)
        {
            var analysis = new DiversityAnalysis();
            var militaryTypes = new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER, UnitType.MONK };

            foreach (var unitType in militaryTypes)
            {
                int totalProduced = 0;
                int producedInWins = 0;
                var producingAgents = new HashSet<string>();
                var winningAgents = new HashSet<string>();

                foreach (var r in results)
                {
                    if (r.Agent0Name == r.Agent1Name) continue;

                    for (int a = 0; a < 2; a++)
                    {
                        var stats = r.GetStats(a);
                        int produced = stats.GetProduced(unitType);
                        string name = r.GetAgentName(a);

                        if (produced > 0)
                        {
                            totalProduced += produced;
                            producingAgents.Add(name);

                            if (r.Winner == a)
                            {
                                producedInWins += produced;
                                winningAgents.Add(name);
                            }
                        }
                    }
                }

                var viability = new UnitViability
                {
                    UnitType = unitType,
                    TotalProduced = totalProduced,
                    ProducedInWins = producedInWins,
                    ProducingAgentCount = producingAgents.Count,
                    WinningAgentCount = winningAgents.Count,
                    ProducingAgents = producingAgents.OrderBy(n => n).ToList(),
                    WinningAgents = winningAgents.OrderBy(n => n).ToList()
                };

                analysis.UnitViabilities.Add(viability);

                if (totalProduced == 0)
                    analysis.NeverProduced.Add(unitType);
                else if (producedInWins == 0)
                    analysis.OnlyInLosses.Add(unitType);
            }

            return analysis;
        }
    }

    public class UnitViability
    {
        public UnitType UnitType { get; set; }
        public int TotalProduced { get; set; }
        public int ProducedInWins { get; set; }
        public int ProducingAgentCount { get; set; }
        public int WinningAgentCount { get; set; }
        public List<string> ProducingAgents { get; set; }
        public List<string> WinningAgents { get; set; }
    }
}
