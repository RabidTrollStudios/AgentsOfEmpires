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
    /// </summary>
    public class AgentActionsAdapter : IAgentActions
    {
        private Agent agent;
        private UnitManager unitManager;

        private readonly Dictionary<int, int> cooldownExpiry = new Dictionary<int, int>();
        private readonly Dictionary<int, int> failureCount = new Dictionary<int, int>();
        private const int BASE_COOLDOWN_FRAMES = 15;
        private const int MAX_COOLDOWN_FRAMES = 120;

        public AgentActionsAdapter(Agent agent, UnitManager unitManager)
        {
            this.agent = agent;
            this.unitManager = unitManager;
        }

        private bool IsOnCooldown(int unitNbr)
        {
            if (cooldownExpiry.TryGetValue(unitNbr, out int expiry))
            {
                if (Time.frameCount < expiry) return true;
                cooldownExpiry.Remove(unitNbr);
            }
            return false;
        }

        private void ApplyCooldown(int unitNbr)
        {
            failureCount.TryGetValue(unitNbr, out int failures);
            failures++;
            failureCount[unitNbr] = failures;
            int cooldown = BASE_COOLDOWN_FRAMES * (1 << System.Math.Min(failures - 1, 3));
            if (cooldown > MAX_COOLDOWN_FRAMES) cooldown = MAX_COOLDOWN_FRAMES;
            cooldownExpiry[unitNbr] = Time.frameCount + cooldown;
        }

        private void ResetCooldown(int unitNbr)
        {
            failureCount.Remove(unitNbr);
            cooldownExpiry.Remove(unitNbr);
        }

        /// <summary>
        /// Validate through Agent.Commands. If it succeeds, queue the deferred command
        /// instead of letting the Agent dispatch immediately.
        /// Agent.Commands still calls EventDispatcher — we intercept by checking
        /// the result and queueing separately. But we need to prevent the immediate dispatch.
        ///
        /// CHANGE: We no longer call agent.Move/Build/etc (which dispatch immediately).
        /// Instead we do lightweight validation here and queue for deferred dispatch.
        /// Heavy validation (pathfinding, buildability) happens at dispatch time via EventDispatcher.
        /// </summary>
        /// <summary>
        /// Queue the command. Returns SUCCESS. Sets enqueued=true if this was the first
        /// command for this unit this frame (for recording purposes).
        /// </summary>
        private CommandResult ValidateAndQueue(int unitNbr, DeferredCommand cmd, out bool enqueued)
        {
            enqueued = DeferredCommandQueue.Enqueue(cmd);
            if (enqueued) ResetCooldown(unitNbr);
            return CommandResult.SUCCESS;
        }

        public CommandResult Move(int unitNbr, Position target)
        {
            if (IsOnCooldown(unitNbr)) return CommandResult.ON_COOLDOWN;
            var unit = unitManager.GetUnit(unitNbr);
            if (unit == null) { ApplyCooldown(unitNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (!unit.CanMove) { ApplyCooldown(unitNbr); return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            var targetVec = new Vector3Int(target.X, target.Y, 0);
            if (unit.CurrentAction == UnitAction.MOVE && unit.TargetGridPos == targetVec)
                return CommandResult.SUCCESS;

            var result = ValidateAndQueue(unitNbr, new DeferredCommand
            {
                AgentNbr = agent.AgentNbr, Agent = agent,
                Type = DeferredCommandType.Move, UnitNbr = unitNbr,
                Unit = unit, Target = targetVec
            }, out bool enqueued);

            return result;
        }

        public CommandResult Build(int unitNbr, Position target, AgentSDK.UnitType unitType)
        {
            if (IsOnCooldown(unitNbr)) return CommandResult.ON_COOLDOWN;
            var unit = unitManager.GetUnit(unitNbr);
            if (unit == null) { ApplyCooldown(unitNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (!unit.CanBuild) { ApplyCooldown(unitNbr); return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            var result = ValidateAndQueue(unitNbr, new DeferredCommand
            {
                AgentNbr = agent.AgentNbr, Agent = agent,
                Type = DeferredCommandType.Build, UnitNbr = unitNbr,
                Unit = unit, Target = new Vector3Int(target.X, target.Y, 0),
                BuildingType = unitType
            }, out bool enqueued);

            return result;
        }

        public CommandResult Gather(int pawnNbr, int mineNbr, int baseNbr)
        {
            if (IsOnCooldown(pawnNbr)) return CommandResult.ON_COOLDOWN;
            var pawn = unitManager.GetUnit(pawnNbr);
            var mine = unitManager.GetUnit(mineNbr);
            var baseUnit = unitManager.GetUnit(baseNbr);
            if (pawn == null) { ApplyCooldown(pawnNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (mine == null || baseUnit == null) { ApplyCooldown(pawnNbr); return CommandResult.TARGET_NOT_FOUND; }
            if (!pawn.CanGather) { ApplyCooldown(pawnNbr); return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            var result = ValidateAndQueue(pawnNbr, new DeferredCommand
            {
                AgentNbr = agent.AgentNbr, Agent = agent,
                Type = DeferredCommandType.Gather, UnitNbr = pawnNbr,
                Unit = pawn, MineUnit = mine, BaseUnit = baseUnit
            }, out bool enqueued);

            return result;
        }

        public CommandResult Train(int buildingNbr, AgentSDK.UnitType unitType)
        {
            if (IsOnCooldown(buildingNbr)) return CommandResult.ON_COOLDOWN;
            var building = unitManager.GetUnit(buildingNbr);
            if (building == null) { ApplyCooldown(buildingNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (!building.CanTrain) { ApplyCooldown(buildingNbr); return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            var result = ValidateAndQueue(buildingNbr, new DeferredCommand
            {
                AgentNbr = agent.AgentNbr, Agent = agent,
                Type = DeferredCommandType.Train, UnitNbr = buildingNbr,
                Unit = building, BuildingType = unitType
            }, out bool enqueued);

            return result;
        }

        public CommandResult Attack(int unitNbr, int targetNbr)
        {
            if (IsOnCooldown(unitNbr)) return CommandResult.ON_COOLDOWN;
            var unit = unitManager.GetUnit(unitNbr);
            var target = unitManager.GetUnit(targetNbr);
            if (unit == null) { ApplyCooldown(unitNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (target == null) { ApplyCooldown(unitNbr); return CommandResult.TARGET_NOT_FOUND; }
            if (!unit.CanAttack) { ApplyCooldown(unitNbr); return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            if (unit.CurrentAction == UnitAction.ATTACK
                && unit.AttackUnit != null
                && unit.AttackUnit.UnitNbr == targetNbr)
                return CommandResult.SUCCESS;

            var result = ValidateAndQueue(unitNbr, new DeferredCommand
            {
                AgentNbr = agent.AgentNbr, Agent = agent,
                Type = DeferredCommandType.Attack, UnitNbr = unitNbr,
                Unit = unit, TargetUnit = target
            }, out bool enqueued);

            return result;
        }

        public CommandResult Repair(int pawnNbr, int buildingNbr)
        {
            if (IsOnCooldown(pawnNbr)) return CommandResult.ON_COOLDOWN;
            var pawn = unitManager.GetUnit(pawnNbr);
            var building = unitManager.GetUnit(buildingNbr);
            if (pawn == null) { ApplyCooldown(pawnNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (building == null) { ApplyCooldown(pawnNbr); return CommandResult.TARGET_NOT_FOUND; }
            if (!pawn.CanBuild) { ApplyCooldown(pawnNbr); return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            var result = ValidateAndQueue(pawnNbr, new DeferredCommand
            {
                AgentNbr = agent.AgentNbr, Agent = agent,
                Type = DeferredCommandType.Repair, UnitNbr = pawnNbr,
                Unit = pawn, TargetUnit = building
            }, out bool enqueued);

            return result;
        }

        public CommandResult Heal(int monkNbr, int targetNbr)
        {
            if (IsOnCooldown(monkNbr)) return CommandResult.ON_COOLDOWN;
            var monk = unitManager.GetUnit(monkNbr);
            var target = unitManager.GetUnit(targetNbr);
            if (monk == null) { ApplyCooldown(monkNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (target == null) { ApplyCooldown(monkNbr); return CommandResult.TARGET_NOT_FOUND; }
            if (!monk.CanHeal) { ApplyCooldown(monkNbr); return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            if (monk.CurrentAction == UnitAction.HEAL)
                return CommandResult.SUCCESS;

            var result = ValidateAndQueue(monkNbr, new DeferredCommand
            {
                AgentNbr = agent.AgentNbr, Agent = agent,
                Type = DeferredCommandType.Heal, UnitNbr = monkNbr,
                Unit = monk, TargetUnit = target
            }, out bool enqueued);

            return result;
        }

        public void Log(string message)
        {
            agent.Log(message);
        }
    }
}
