using System;
using System.Collections.Generic;
using AgentSDK;
using Opponent.Tests;

namespace BalanceRunner.Runner
{
    /// <summary>
    /// Registry of all available balance test opponents.
    /// Maps display names to factory functions for agent instantiation.
    /// Naming convention: &lt;Units&gt;&lt;Strategy&gt; — strategy name is consistent within each tier.
    /// </summary>
    public static class AgentRegistry
    {
        private static readonly Dictionary<string, Func<PlanningAgentBase>> _agents =
            new Dictionary<string, Func<PlanningAgentBase>>(StringComparer.OrdinalIgnoreCase)
            {
                // Sanity check (non-combat)
                ["PawnIdle"] = () => new PawnIdleOpponent(),
                ["PawnGather"] = () => new PawnGatherOpponent(),

                // Easy (8-32% win rate): turtle — greedy economy, delayed attack
                ["WarriorTurtle"] = () => new WarriorTurtleOpponent(),
                ["ArcherTurtle"] = () => new ArcherTurtleOpponent(),
                ["LancerTurtle"] = () => new LancerTurtleOpponent(),
                ["MixedTurtle"] = () => new MixedTurtleOpponent(),

                // Mid (44-53% win rate): rush — minimal economy, early aggression
                ["WarriorRush"] = () => new WarriorRushOpponent(),
                ["ArcherRush"] = () => new ArcherRushOpponent(),
                ["LancerRush"] = () => new LancerRushOpponent(),
                ["MixedRush"] = () => new MixedRushOpponent(),

                // Hard (85-97% win rate): dual — two production buildings, maximum throughput
                ["WarriorDual"] = () => new WarriorDualOpponent(),
                ["ArcherDual"] = () => new ArcherDualOpponent(),
                ["LancerDual"] = () => new LancerDualOpponent(),
                ["MixedDual"] = () => new MixedDualOpponent(),

                // Impossible: spend — max-spend multi-building with monk sustain
                ["WarriorMonkSpend"] = () => new WarriorMonkSpendOpponent(),
                ["LancerMonkSpend"] = () => new LancerMonkSpendOpponent(),
                ["ArcherMonkSpend"] = () => new ArcherMonkSpendOpponent(),
                ["AllSpend"] = () => new AllSpendOpponent(),
            };

        /// <summary>All registered agents (name -> factory).</summary>
        public static IReadOnlyDictionary<string, Func<PlanningAgentBase>> All => _agents;

        /// <summary>Create an agent by name. Returns null if not found.</summary>
        public static PlanningAgentBase Create(string name)
        {
            return _agents.TryGetValue(name, out var factory) ? factory() : null;
        }

        /// <summary>Check if an agent name is registered.</summary>
        public static bool Exists(string name) => _agents.ContainsKey(name);
    }
}
