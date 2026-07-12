using System.Collections.Generic;
using AgentSDK;
using GameManager.GameElements;
using UnityEngine;

namespace GameManager
{
    /// <summary>
    /// Implements IAgentActions by validating commands via Agent.Commands and queueing
    /// them for deferred execution. Commands are collected during Update() and dispatched
    /// in deterministic order at the start of the next FixedUpdate via DeferredCommandQueue.
    ///
    /// This ensures command execution order matches SimGame for parity.
    ///
    /// NOTE: this adapter previously carried a per-unit failure "cooldown" (keyed on
    /// Time.frameCount) that dropped repeated failing commands to throttle spam. The
    /// headless SimGame has no such throttle, so it caused a cross-engine parity skew:
    /// a command retried right after a precondition finally cleared (e.g. a build issued
    /// the tick a base completes) was still on cooldown in Unity and took effect one tick
    /// later than in the sim. The cooldown was removed so both engines queue every command
    /// immediately; the shared CommandProcessor still validates and rejects invalid
    /// commands at dispatch, so nothing actually executes that shouldn't.
    /// </summary>
    public class AgentActionsAdapter : IAgentActions
    {
        private Agent agent;
        private UnitManager unitManager;
        private ParityExporter parityExporter;

        public AgentActionsAdapter(Agent agent, UnitManager unitManager)
        {
            this.agent = agent;
            this.unitManager = unitManager;
            parityExporter = Object.FindFirstObjectByType<ParityExporter>();
        }

        private void RecordCmd(string type, int unitNbr,
            int targetX = 0, int targetY = 0, int targetUnit = -1,
            string buildingType = "", int mineNbr = -1, int baseNbr = -1)
        {
            parityExporter?.RecordCommand(agent.AgentNbr, type, unitNbr,
                targetX, targetY, targetUnit, buildingType, mineNbr, baseNbr);
        }

        /// <summary>
        /// Do lightweight capability validation here, then queue the command for
        /// deferred dispatch. The full rule validation, pathfinding, gold, and state
        /// init happen at dispatch time in AgentSDK.CommandProcessor (via
        /// DeferredCommandQueue.ProcessAll) — the same shared path SimGame uses.
        /// Returns SUCCESS. Sets enqueued=true if this was the first command for this
        /// unit this tick (for recording purposes).
        /// </summary>
        private CommandResult ValidateAndQueue(int unitNbr, DeferredCommand cmd, out bool enqueued)
        {
            enqueued = DeferredCommandQueue.Enqueue(cmd);
            return CommandResult.SUCCESS;
        }

        public CommandResult Move(int unitNbr, Position target)
        {
            var unit = unitManager.GetUnit(unitNbr);
            if (unit == null) { return CommandResult.UNIT_NOT_FOUND; }
            if (!unit.CanMove) { return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            var targetVec = new Vector3Int(target.X, target.Y, 0);
            if (unit.CurrentAction == UnitAction.MOVE && unit.TargetGridPos == targetVec)
                return CommandResult.SUCCESS;

            var result = ValidateAndQueue(unitNbr, new DeferredCommand
            {
                AgentNbr = agent.AgentNbr, Agent = agent,
                Type = DeferredCommandType.MOVE, UnitNbr = unitNbr,
                Unit = unit, Target = targetVec
            }, out bool enqueued);
            if (enqueued) RecordCmd("MOVE", unitNbr, target.X, target.Y);
            return result;
        }

        public CommandResult Build(int unitNbr, Position target, AgentSDK.UnitType unitType)
        {
            var unit = unitManager.GetUnit(unitNbr);
            if (unit == null) { return CommandResult.UNIT_NOT_FOUND; }
            if (!unit.CanBuild) { return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            var result = ValidateAndQueue(unitNbr, new DeferredCommand
            {
                AgentNbr = agent.AgentNbr, Agent = agent,
                Type = DeferredCommandType.BUILD, UnitNbr = unitNbr,
                Unit = unit, Target = new Vector3Int(target.X, target.Y, 0),
                BuildingType = unitType
            }, out bool enqueued);
            if (enqueued) RecordCmd("BUILD", unitNbr, target.X, target.Y, buildingType: unitType.ToString());
            return result;
        }

        public CommandResult Gather(int pawnNbr, int mineNbr, int baseNbr)
        {
            var pawn = unitManager.GetUnit(pawnNbr);
            var mine = unitManager.GetUnit(mineNbr);
            var baseUnit = unitManager.GetUnit(baseNbr);
            if (pawn == null) { return CommandResult.UNIT_NOT_FOUND; }
            if (mine == null || baseUnit == null) { return CommandResult.TARGET_NOT_FOUND; }
            if (!pawn.CanGather) { return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            var result = ValidateAndQueue(pawnNbr, new DeferredCommand
            {
                AgentNbr = agent.AgentNbr, Agent = agent,
                Type = DeferredCommandType.GATHER, UnitNbr = pawnNbr,
                Unit = pawn, MineUnit = mine, BaseUnit = baseUnit
            }, out bool enqueued);
            if (enqueued) RecordCmd("GATHER", pawnNbr, mineNbr: mineNbr, baseNbr: baseNbr);
            return result;
        }

        public CommandResult Train(int buildingNbr, AgentSDK.UnitType unitType)
        {
            var building = unitManager.GetUnit(buildingNbr);
            if (building == null) { return CommandResult.UNIT_NOT_FOUND; }
            if (!building.CanTrain) { return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            var result = ValidateAndQueue(buildingNbr, new DeferredCommand
            {
                AgentNbr = agent.AgentNbr, Agent = agent,
                Type = DeferredCommandType.TRAIN, UnitNbr = buildingNbr,
                Unit = building, BuildingType = unitType
            }, out bool enqueued);
            if (enqueued) RecordCmd("TRAIN", buildingNbr, buildingType: unitType.ToString());
            return result;
        }

        public CommandResult Attack(int unitNbr, int targetNbr)
        {
            var unit = unitManager.GetUnit(unitNbr);
            var target = unitManager.GetUnit(targetNbr);
            if (unit == null) { return CommandResult.UNIT_NOT_FOUND; }
            if (target == null) { return CommandResult.TARGET_NOT_FOUND; }
            if (!unit.CanAttack) { return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            if (unit.CurrentAction == UnitAction.ATTACK
                && unit.AttackUnit != null
                && unit.AttackUnit.UnitNbr == targetNbr)
                return CommandResult.SUCCESS;

            var result = ValidateAndQueue(unitNbr, new DeferredCommand
            {
                AgentNbr = agent.AgentNbr, Agent = agent,
                Type = DeferredCommandType.ATTACK, UnitNbr = unitNbr,
                Unit = unit, TargetUnit = target
            }, out bool enqueued);
            if (enqueued) RecordCmd("ATTACK", unitNbr, targetUnit: targetNbr);
            return result;
        }

        public CommandResult Repair(int pawnNbr, int buildingNbr)
        {
            var pawn = unitManager.GetUnit(pawnNbr);
            var building = unitManager.GetUnit(buildingNbr);
            if (pawn == null) { return CommandResult.UNIT_NOT_FOUND; }
            if (building == null) { return CommandResult.TARGET_NOT_FOUND; }
            if (!pawn.CanBuild) { return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            var result = ValidateAndQueue(pawnNbr, new DeferredCommand
            {
                AgentNbr = agent.AgentNbr, Agent = agent,
                Type = DeferredCommandType.REPAIR, UnitNbr = pawnNbr,
                Unit = pawn, TargetUnit = building
            }, out bool enqueued);
            if (enqueued) RecordCmd("REPAIR", pawnNbr, targetUnit: buildingNbr);
            return result;
        }

        public CommandResult Heal(int monkNbr, int targetNbr)
        {
            var monk = unitManager.GetUnit(monkNbr);
            var target = unitManager.GetUnit(targetNbr);
            if (monk == null) { return CommandResult.UNIT_NOT_FOUND; }
            if (target == null) { return CommandResult.TARGET_NOT_FOUND; }
            if (!monk.CanHeal) { return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            if (monk.CurrentAction == UnitAction.HEAL)
                return CommandResult.SUCCESS;

            var result = ValidateAndQueue(monkNbr, new DeferredCommand
            {
                AgentNbr = agent.AgentNbr, Agent = agent,
                Type = DeferredCommandType.HEAL, UnitNbr = monkNbr,
                Unit = monk, TargetUnit = target
            }, out bool enqueued);
            if (enqueued) RecordCmd("HEAL", monkNbr, targetUnit: targetNbr);
            return result;
        }

        public void Log(string message)
        {
            agent.Log(message);
        }
    }
}
