using System;
using System.Collections.Generic;
using System.Linq;

namespace AgentTestHarness
{
    /// <summary>
    /// Per-subsystem hash breakdown. When a parity test fails, comparing subsystem
    /// hashes narrows the divergence to a specific category of state.
    /// </summary>
    public struct SubsystemHash
    {
        /// <summary>CurrentTick, Gold[0], Gold[1], Units.Count, NextUnitNbr</summary>
        public long Global;

        /// <summary>Per-unit: UnitNbr, UnitType, OwnerAgentNbr, GridPosition</summary>
        public long UnitPositions;

        /// <summary>Per-unit: Health, IsBuilt</summary>
        public long UnitHealth;

        /// <summary>Per-unit: CurrentAction, AttackTargetNbr, GatherPhase, RepairBuildingNbr</summary>
        public long UnitActions;

        /// <summary>Per-unit: MoveAccumulator, PathIndex, Path.Count, TrainTimer, BuildTimer, MiningTimer, etc.</summary>
        public long UnitTimers;

        /// <summary>Combined hash of all subsystems (equivalent to GetStateHash())</summary>
        public long Combined;

        /// <summary>
        /// Returns which subsystems differ between two hashes.
        /// </summary>
        public static string Diff(SubsystemHash a, SubsystemHash b)
        {
            var diffs = new List<string>();
            if (a.Global != b.Global) diffs.Add($"Global (0x{a.Global:X16} vs 0x{b.Global:X16})");
            if (a.UnitPositions != b.UnitPositions) diffs.Add($"UnitPositions (0x{a.UnitPositions:X16} vs 0x{b.UnitPositions:X16})");
            if (a.UnitHealth != b.UnitHealth) diffs.Add($"UnitHealth (0x{a.UnitHealth:X16} vs 0x{b.UnitHealth:X16})");
            if (a.UnitActions != b.UnitActions) diffs.Add($"UnitActions (0x{a.UnitActions:X16} vs 0x{b.UnitActions:X16})");
            if (a.UnitTimers != b.UnitTimers) diffs.Add($"UnitTimers (0x{a.UnitTimers:X16} vs 0x{b.UnitTimers:X16})");
            return diffs.Count > 0 ? string.Join("; ", diffs) : "no differences";
        }
    }

    /// <summary>
    /// State hashing for deterministic parity comparison.
    ///
    /// Hashing scheme:
    /// - Global state: CurrentTick, Gold[0], Gold[1], unit count, NextUnitNbr
    /// - Per-unit state: all mutable fields including internal task state
    /// - Units sorted by UnitNbr for deterministic ordering (Dictionary iteration is unordered)
    /// - Floats converted to bit-level representation via BitConverter.SingleToInt32Bits
    /// - Map state is NOT hashed separately — it's a function of unit placements
    ///
    /// Replay assumption: given identical commands and initial state, SimGame produces
    /// identical state at every tick. GetStateHash() verifies this by producing a
    /// reproducible hash that changes if any game state differs.
    /// </summary>
    public partial class SimGame
    {
        /// <summary>
        /// Compute a deterministic hash of the entire game state.
        /// Two SimGame instances with identical state will produce the same hash.
        /// </summary>
        public long GetStateHash()
        {
            unchecked
            {
                long hash = 17;
                hash = hash * 31 + CurrentTick;
                hash = hash * 31 + Gold[0];
                hash = hash * 31 + Gold[1];
                hash = hash * 31 + Units.Count;
                hash = hash * 31 + NextUnitNbr;

                foreach (var kvp in Units.OrderBy(k => k.Key))
                {
                    hash = HashUnit(hash, kvp.Value);
                }

                return hash;
            }
        }

        /// <summary>
        /// Compute per-subsystem hashes for diagnosing which category of state diverged.
        /// </summary>
        public SubsystemHash GetSubsystemHash()
        {
            unchecked
            {
                var result = new SubsystemHash();

                // Global
                long g = 17;
                g = g * 31 + CurrentTick;
                g = g * 31 + Gold[0];
                g = g * 31 + Gold[1];
                g = g * 31 + Units.Count;
                g = g * 31 + NextUnitNbr;
                result.Global = g;

                long pos = 17;
                long health = 17;
                long actions = 17;
                long timers = 17;

                foreach (var kvp in Units.OrderBy(k => k.Key))
                {
                    var u = kvp.Value;

                    // Positions: identity + location
                    pos = pos * 31 + u.UnitNbr;
                    pos = pos * 31 + (int)u.UnitType;
                    pos = pos * 31 + u.OwnerAgentNbr;
                    pos = pos * 31 + u.GridPosition.X;
                    pos = pos * 31 + u.GridPosition.Y;

                    // Health
                    health = health * 31 + u.UnitNbr;
                    health = health * 31 + FloatToStableBits(u.Health);
                    health = health * 31 + (u.IsBuilt ? 1 : 0);

                    // Actions: current action + target references
                    actions = actions * 31 + u.UnitNbr;
                    actions = actions * 31 + (int)u.CurrentAction;
                    actions = actions * 31 + u.AttackTargetNbr;
                    actions = actions * 31 + u.RepairBuildingNbr;
                    actions = actions * 31 + u.GatherMineNbr;
                    actions = actions * 31 + u.GatherBaseNbr;
                    actions = actions * 31 + (int)u.GatherPhase;
                    actions = actions * 31 + (int)u.TrainTarget;
                    actions = actions * 31 + (int)u.BuildTarget;
                    actions = actions * 31 + u.BuildSite.X;
                    actions = actions * 31 + u.BuildSite.Y;
                    actions = actions * 31 + (u.BuildPlaced ? 1 : 0);

                    // Timers: accumulators and progress
                    timers = timers * 31 + u.UnitNbr;
                    timers = timers * 31 + FloatToStableBits(u.MoveAccumulator);
                    timers = timers * 31 + u.PathIndex;
                    timers = timers * 31 + (u.Path != null ? u.Path.Count : -1);
                    timers = timers * 31 + FloatToStableBits(u.TrainTimer);
                    timers = timers * 31 + FloatToStableBits(u.BuildTimer);
                    timers = timers * 31 + FloatToStableBits(u.MiningTimer);
                    timers = timers * 31 + u.LocalAvoidWaitTicks;
                }

                result.UnitPositions = pos;
                result.UnitHealth = health;
                result.UnitActions = actions;
                result.UnitTimers = timers;

                // Combined: fold all subsystems
                long combined = 17;
                combined = combined * 31 + result.Global;
                combined = combined * 31 + result.UnitPositions;
                combined = combined * 31 + result.UnitHealth;
                combined = combined * 31 + result.UnitActions;
                combined = combined * 31 + result.UnitTimers;
                result.Combined = combined;

                return result;
            }
        }

        private static long HashUnit(long hash, SimUnit u)
        {
            unchecked
            {
                hash = hash * 31 + u.UnitNbr;
                hash = hash * 31 + (int)u.UnitType;
                hash = hash * 31 + u.OwnerAgentNbr;
                hash = hash * 31 + u.GridPosition.X;
                hash = hash * 31 + u.GridPosition.Y;
                hash = hash * 31 + FloatToStableBits(u.Health);
                hash = hash * 31 + (u.IsBuilt ? 1 : 0);
                hash = hash * 31 + (int)u.CurrentAction;
                hash = hash * 31 + FloatToStableBits(u.MoveAccumulator);
                hash = hash * 31 + u.PathIndex;
                hash = hash * 31 + (u.Path != null ? u.Path.Count : -1);
                hash = hash * 31 + FloatToStableBits(u.TrainTimer);
                hash = hash * 31 + (int)u.TrainTarget;
                hash = hash * 31 + FloatToStableBits(u.BuildTimer);
                hash = hash * 31 + (int)u.BuildTarget;
                hash = hash * 31 + u.BuildSite.X;
                hash = hash * 31 + u.BuildSite.Y;
                hash = hash * 31 + (u.BuildPlaced ? 1 : 0);
                hash = hash * 31 + u.GatherMineNbr;
                hash = hash * 31 + u.GatherBaseNbr;
                hash = hash * 31 + (int)u.GatherPhase;
                hash = hash * 31 + FloatToStableBits(u.MiningTimer);
                hash = hash * 31 + u.AttackTargetNbr;
                hash = hash * 31 + u.RepairBuildingNbr;
                hash = hash * 31 + u.LocalAvoidWaitTicks;
                return hash;
            }
        }

        /// <summary>
        /// Convert float to int bits for stable hashing.
        /// Avoids issues with NaN representations and floating-point comparison.
        /// </summary>
        private static int FloatToStableBits(float f)
        {
            return BitConverter.SingleToInt32Bits(f);
        }
    }
}
