using System.Linq;
using BalanceRunner.Analysis;
using BalanceRunner.Config;
using BalanceRunner.Runner;
using Xunit;
using Xunit.Abstractions;

namespace Balance.Tests
{
    /// <summary>
    /// Asserts that multiple distinct strategy archetypes are viable,
    /// ensuring the metagame is not degenerate.
    /// </summary>
    public class StrategyDiversityTests
    {
        private readonly ITestOutputHelper _output;

        public StrategyDiversityTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void AtLeast3Agents_Above40Percent_WinRate()
        {
            var config = new RunConfig
            {
                SeedCount = 2,
                FrameLimit = 3000
            };

            var results = BatchRunner.Run(config);
            var matrix = WinRateMatrix.Compute(results);

            // Exclude Idle and Gatherer (not real strategies)
            var realAgents = matrix.AggregateWinRates
                .Where(kvp => kvp.Key != "Idle" && kvp.Key != "Gatherer")
                .OrderByDescending(kvp => kvp.Value)
                .ToList();

            foreach (var kvp in realAgents)
                _output.WriteLine($"  {kvp.Key}: {kvp.Value * 100:F0}%");

            int viableCount = realAgents.Count(kvp => kvp.Value >= 0.40f);
            _output.WriteLine($"\n  Viable agents (>=40%): {viableCount}");

            Assert.True(viableCount >= 3,
                $"Only {viableCount} strategies achieve >=40% win rate. " +
                "Need at least 3 viable archetypes for healthy metagame diversity.");
        }
    }
}
