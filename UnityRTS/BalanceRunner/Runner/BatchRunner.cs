using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BalanceRunner.Config;
using BalanceRunner.Telemetry;

namespace BalanceRunner.Runner
{
    /// <summary>
    /// Runs an N×N matchup matrix across multiple seeds.
    /// Each matchup pair is run in both seat orderings to eliminate positional bias.
    /// </summary>
    public class BatchRunner
    {
        /// <summary>
        /// Execute the full batch run and return all match results.
        /// </summary>
        /// <param name="config">Run configuration.</param>
        /// <param name="progress">Optional callback for progress reporting (matchIndex, totalMatches, result).</param>
        public static List<MatchResult> Run(RunConfig config, Action<int, int, MatchResult> progress = null)
        {
            var agentNames = config.Agents != null && config.Agents.Count > 0
                ? config.Agents
                : AgentRegistry.All.Keys.ToList();

            // Validate all agent names
            foreach (var name in agentNames)
            {
                if (!AgentRegistry.Exists(name))
                    throw new ArgumentException($"Unknown agent: '{name}'");
            }

            // Build matchup list: all unique pairs (including self-play)
            var matchups = new List<(string A, string B)>();
            for (int i = 0; i < agentNames.Count; i++)
            {
                for (int j = i; j < agentNames.Count; j++)
                {
                    matchups.Add((agentNames[i], agentNames[j]));
                }
            }

            // Calculate total matches
            int matchesPerPair = config.SeedCount * (config.BothSeatOrderings ? 2 : 1);
            // Self-play pairs only need one ordering
            int selfPlayPairs = agentNames.Count;
            int crossPairs = matchups.Count - selfPlayPairs;
            int totalMatches = (selfPlayPairs * config.SeedCount)
                             + (crossPairs * matchesPerPair);

            var results = new List<MatchResult>(totalMatches);
            int matchIndex = 0;

            foreach (var (nameA, nameB) in matchups)
            {
                bool isSelfPlay = nameA == nameB;

                for (int seedIdx = 0; seedIdx < config.SeedCount; seedIdx++)
                {
                    // Deterministic seed from matchup names and index
                    int seed = ComputeSeed(nameA, nameB, seedIdx);

                    // Run A@P0 vs B@P1
                    var result = MatchRunner.Run(
                        nameA, AgentRegistry.Create(nameA),
                        nameB, AgentRegistry.Create(nameB),
                        seed, config.FrameLimit, config.MapTemplate);

                    results.Add(result);
                    matchIndex++;
                    progress?.Invoke(matchIndex, totalMatches, result);

                    // Run B@P0 vs A@P1 (skip for self-play)
                    if (config.BothSeatOrderings && !isSelfPlay)
                    {
                        int reverseSeed = ComputeSeed(nameB, nameA, seedIdx);
                        var reverseResult = MatchRunner.Run(
                            nameB, AgentRegistry.Create(nameB),
                            nameA, AgentRegistry.Create(nameA),
                            reverseSeed, config.FrameLimit, config.MapTemplate);

                        results.Add(reverseResult);
                        matchIndex++;
                        progress?.Invoke(matchIndex, totalMatches, reverseResult);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Compute a deterministic seed from matchup names and seed index.
        /// </summary>
        private static int ComputeSeed(string nameA, string nameB, int seedIdx)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + nameA.GetHashCode();
                hash = hash * 31 + nameB.GetHashCode();
                hash = hash * 31 + seedIdx;
                return Math.Abs(hash);
            }
        }
    }
}
