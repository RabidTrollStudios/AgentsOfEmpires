using System.Collections.Generic;
using AgentSDK;
using GameManager.GameElements;
using UnityEngine;

namespace GameManager
{
    internal enum DeferredCommandType
    {
        MOVE, BUILD, GATHER, TRAIN, ATTACK, REPAIR, HEAL
    }

    /// <summary>
    /// A command validated by AgentActionsAdapter but not yet dispatched.
    /// Queued during Update(), dispatched in deterministic order at the start of FixedUpdate.
    /// </summary>
    internal struct DeferredCommand
    {
        public int AgentNbr;
        public DeferredCommandType Type;
        public int UnitNbr;
        public Agent Agent;      // Owning agent (for command recording / attribution)

        // Move / Build
        public Vector3Int Target;
        public UnitType BuildingType;

        // Pre-resolved references (validated at queue time)
        public Unit Unit;
        public Unit TargetUnit;  // Attack target, Repair building, Heal target
        public Unit MineUnit;    // Gather mine
        public Unit BaseUnit;    // Gather base
    }

    /// <summary>
    /// Central queue for deferred commands. Commands are enqueued during agent Update()
    /// and processed at the start of the next FixedUpdate in deterministic order.
    ///
    /// Sort order: (AgentNbr, CommandType, UnitNbr) — ensures both agents' commands
    /// are interleaved consistently, matching SimGame's processing order.
    /// </summary>
    internal static class DeferredCommandQueue
    {
        private static readonly List<DeferredCommand> pending = new List<DeferredCommand>();

        private static readonly HashSet<int> unitsSeen = new HashSet<int>();

        /// <summary>
        /// Enqueue a command. Returns true if this is the first command queued for this
        /// unit this tick (used only for parity command recording — see below), false if
        /// a later duplicate.
        ///
        /// IMPORTANT: every command is kept in <see cref="pending"/> regardless of the
        /// return value. The per-unit "one command per tick" rule is applied at dispatch
        /// time in <see cref="ProcessAll"/>, AFTER sorting by (AgentNbr, Type, UnitNbr) —
        /// NOT here at enqueue time. This matches SimGame.ProcessCommandsSorted exactly:
        /// the winning command for a unit is the one with the lowest command-Type enum
        /// (BUILD=1 beats GATHER=2), not the one the agent happened to call first. Dropping
        /// duplicates at enqueue time instead kept the FIRST-CALLED command, so a GATHER
        /// issued before a BUILD in the same Update() beat the BUILD in Unity but lost in
        /// the sim — a one-tick cross-engine divergence (barracks built a tick late).
        /// </summary>
        public static bool Enqueue(DeferredCommand cmd)
        {
            pending.Add(cmd);
            // Report first-per-unit for recording purposes only; does not gate the queue.
            return unitsSeen.Add(cmd.UnitNbr);
        }

        public static int Count => pending.Count;

        /// <summary>
        /// Drain all pending commands in deterministic order and dispatch them.
        /// Called once per FixedUpdate before unit advancement.
        /// </summary>
        public static void ProcessAll()
        {
            if (pending.Count == 0) return;

            // Sort: agent 0 before agent 1, then by command type, then by unit number.
            // This ensures identical ordering with SimGame's ProcessCommands.
            pending.Sort((a, b) =>
            {
                int cmp = a.AgentNbr.CompareTo(b.AgentNbr);
                if (cmp != 0) return cmp;
                cmp = a.Type.CompareTo(b.Type);
                if (cmp != 0) return cmp;
                return a.UnitNbr.CompareTo(b.UnitNbr);
            });

            // Use shared CommandProcessor for identical logic with SimGame.
            var world = GameManager.Instance.GetTickWorld();

            // Per-unit "one command per tick", applied AFTER the sort so the winner is the
            // lowest-Type command (BUILD before GATHER), identical to SimGame's
            // ProcessCommandsSorted. See Enqueue for why this must not happen at enqueue time.
            var processedUnits = new HashSet<int>();

            foreach (var cmd in pending)
            {
                // Units may have died between queue time and dispatch time
                if (cmd.Unit == null) continue;
                if (!processedUnits.Add(cmd.UnitNbr)) continue; // unit already commanded this tick

                AgentSDK.CommandResult result;
                switch (cmd.Type)
                {
                    case DeferredCommandType.MOVE:
                        result = AgentSDK.CommandProcessor.ProcessMove(
                            cmd.Unit, new AgentSDK.Position(cmd.Target.x, cmd.Target.y), world);
                        break;
                    case DeferredCommandType.BUILD:
                        result = AgentSDK.CommandProcessor.ProcessBuild(
                            cmd.Unit, new AgentSDK.Position(cmd.Target.x, cmd.Target.y),
                            cmd.BuildingType, world);
                        break;
                    case DeferredCommandType.GATHER:
                        if (cmd.MineUnit == null || cmd.BaseUnit == null) { result = AgentSDK.CommandResult.INVALID_TARGET; break; }
                        result = AgentSDK.CommandProcessor.ProcessGather(
                            cmd.Unit, cmd.MineUnit.UnitNbr, cmd.BaseUnit.UnitNbr, world);
                        break;
                    case DeferredCommandType.TRAIN:
                        result = AgentSDK.CommandProcessor.ProcessTrain(
                            cmd.Unit, cmd.BuildingType, world);
                        break;
                    case DeferredCommandType.ATTACK:
                        if (cmd.TargetUnit == null) { result = AgentSDK.CommandResult.INVALID_TARGET; break; }
                        result = AgentSDK.CommandProcessor.ProcessAttack(
                            cmd.Unit, cmd.TargetUnit.UnitNbr, world);
                        break;
                    case DeferredCommandType.REPAIR:
                        if (cmd.TargetUnit == null) { result = AgentSDK.CommandResult.INVALID_TARGET; break; }
                        result = AgentSDK.CommandProcessor.ProcessRepair(
                            cmd.Unit, cmd.TargetUnit.UnitNbr, world);
                        break;
                    case DeferredCommandType.HEAL:
                        if (cmd.TargetUnit == null) { result = AgentSDK.CommandResult.INVALID_TARGET; break; }
                        result = AgentSDK.CommandProcessor.ProcessHeal(
                            cmd.Unit, cmd.TargetUnit.UnitNbr, world);
                        break;
                    default:
                        result = AgentSDK.CommandResult.SUCCESS;
                        break;
                }
            }

            pending.Clear();
            unitsSeen.Clear();
        }

        public static void Clear()
        {
            pending.Clear();
            unitsSeen.Clear();
        }
    }
}
