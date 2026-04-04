using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using BalanceRunner.Analysis;
using BalanceRunner.Config;
using BalanceRunner.Runner;
using Xunit;
using Xunit.Abstractions;

namespace Balance.Tests
{
    /// <summary>
    /// Asserts that every military unit type is produced by at least one
    /// agent that wins matches.
    /// </summary>
    public class UnitViabilityTests
    {
        private readonly ITestOutputHelper _output;

        public UnitViabilityTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void AllMilitaryUnits_ProducedInWinningStrategies()
        {
            var config = new RunConfig
            {
                SeedCount = 2,
                TickLimit = 3000
            };

            var results = BatchRunner.Run(config);
            var diversity = DiversityAnalysis.Compute(results);

            foreach (var uv in diversity.UnitViabilities)
            {
                _output.WriteLine($"  {uv.UnitType}: produced={uv.TotalProduced}, " +
                    $"agents={uv.ProducingAgentCount}, winning={uv.WinningAgentCount}");
            }

            var militaryTypes = new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER };
            foreach (var unitType in militaryTypes)
            {
                var viability = diversity.UnitViabilities.FirstOrDefault(v => v.UnitType == unitType);
                Assert.NotNull(viability);
                Assert.True(viability.TotalProduced > 0,
                    $"{unitType} is never produced — no agent uses it.");
                Assert.True(viability.WinningAgentCount > 0,
                    $"{unitType} is only produced in losing strategies — no winning agent uses it.");
            }
        }
    }
}
