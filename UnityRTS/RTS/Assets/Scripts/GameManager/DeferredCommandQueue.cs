using System.Collections.Generic;
using AgentSDK;
using GameManager.GameElements;
using UnityEngine;

namespace GameManager
{
    internal enum DeferredCommandType
    {
        Move, Build, Gather, Train, Attack, Repair, Heal
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
        public Agent Agent;      // Owning agent (passed as sender to EventDispatcher)

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
        /// Enqueue a command. Returns true if accepted, false if a command for this
        /// unit was already queued this tick (duplicate suppressed).
        /// </summary>
        public static bool Enqueue(DeferredCommand cmd)
        {
            // Only accept the first command per unit per tick.
            // With deferred dispatch, agents may re-issue the same command on
            // consecutive frames before the first one executes.
            if (!unitsSeen.Add(cmd.UnitNbr)) return false;
            pending.Add(cmd);
            return true;
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

            var events = GameManager.Instance.Events;

            // Use shared CommandProcessor for identical logic with SimGame.
            var world = GameManager.Instance.GetTickWorld();

            foreach (var cmd in pending)
            {
                // Units may have died between queue time and dispatch time
                if (cmd.Unit == null) continue;

                AgentSDK.CommandResult result;
                switch (cmd.Type)
                {
                    case DeferredCommandType.Move:
                        result = AgentSDK.CommandProcessor.ProcessMove(
                            cmd.Unit, new AgentSDK.Position(cmd.Target.x, cmd.Target.y), world);
                        break;
                    case DeferredCommandType.Build:
                        result = AgentSDK.CommandProcessor.ProcessBuild(
                            cmd.Unit, new AgentSDK.Position(cmd.Target.x, cmd.Target.y),
                            cmd.BuildingType, world);
                        break;
                    case DeferredCommandType.Gather:
                        if (cmd.MineUnit == null || cmd.BaseUnit == null) { result = AgentSDK.CommandResult.INVALID_TARGET; break; }
                        result = AgentSDK.CommandProcessor.ProcessGather(
                            cmd.Unit, cmd.MineUnit.UnitNbr, cmd.BaseUnit.UnitNbr, world);
                        break;
                    case DeferredCommandType.Train:
                        result = AgentSDK.CommandProcessor.ProcessTrain(
                            cmd.Unit, cmd.BuildingType, world);
                        break;
                    case DeferredCommandType.Attack:
                        if (cmd.TargetUnit == null) { result = AgentSDK.CommandResult.INVALID_TARGET; break; }
                        result = AgentSDK.CommandProcessor.ProcessAttack(
                            cmd.Unit, cmd.TargetUnit.UnitNbr, world);
                        break;
                    case DeferredCommandType.Repair:
                        if (cmd.TargetUnit == null) { result = AgentSDK.CommandResult.INVALID_TARGET; break; }
                        result = AgentSDK.CommandProcessor.ProcessRepair(
                            cmd.Unit, cmd.TargetUnit.UnitNbr, world);
                        break;
                    case DeferredCommandType.Heal:
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
