using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// A validated command queued for execution.
    /// </summary>
    internal class SimCommand
    {
        public CommandType Type;
        public int UnitNbr;
        public Position Target;
        public UnitType UnitType;
        public int MineNbr;
        public int BaseNbr;
        public int TargetUnitNbr;
    }

    /// <summary>
    /// Implements IAgentActions for the simulation. Uses shared CommandValidator
    /// for game-rule validation, then queues commands for execution by SimGame.
    ///
    /// Includes exponential-backoff cooldown system matching Unity's
    /// AgentActionsAdapter to ensure identical agent behavior in both engines.
    /// </summary>
    public class SimAgentActions : IAgentActions
    {
        private readonly SimGame game;
        private readonly int agentNbr;
        private readonly Func<int> getCurrentTick;

        // Cooldown system — mirrors AgentActionsAdapter exactly.
        // Uses tick count instead of Unity's Time.frameCount.
        private readonly Dictionary<int, int> cooldownExpiry = new Dictionary<int, int>();
        private readonly Dictionary<int, int> failureCount = new Dictionary<int, int>();
        private const int BASE_COOLDOWN_TICKS = 15;
        private const int MAX_COOLDOWN_TICKS = 120;

        internal List<SimCommand> PendingCommands { get; } = new List<SimCommand>();

        /// <summary>All log messages issued by this agent.</summary>
        public List<string> LogMessages { get; } = new List<string>();

        internal SimAgentActions(SimGame game, int agentNbr, Func<int> getCurrentTick)
        {
            this.game = game;
            this.agentNbr = agentNbr;
            this.getCurrentTick = getCurrentTick;
        }

        internal void ClearPending()
        {
            PendingCommands.Clear();
        }

        #region Cooldown System

        private bool IsOnCooldown(int unitNbr)
        {
            if (cooldownExpiry.TryGetValue(unitNbr, out int expiry))
            {
                if (getCurrentTick() < expiry) return true;
                cooldownExpiry.Remove(unitNbr);
            }
            return false;
        }

        private void ApplyCooldown(int unitNbr)
        {
            failureCount.TryGetValue(unitNbr, out int failures);
            failures++;
            failureCount[unitNbr] = failures;
            int cooldown = BASE_COOLDOWN_TICKS * (1 << Math.Min(failures - 1, 3));
            if (cooldown > MAX_COOLDOWN_TICKS) cooldown = MAX_COOLDOWN_TICKS;
            cooldownExpiry[unitNbr] = getCurrentTick() + cooldown;
        }

        private void ResetCooldown(int unitNbr)
        {
            failureCount.Remove(unitNbr);
            cooldownExpiry.Remove(unitNbr);
        }

        #endregion

        /// <summary>
        /// Queue a command. Dedup: only first command per unit per tick.
        /// Returns true if enqueued (first for this unit), false if duplicate.
        /// </summary>
        private bool Enqueue(SimCommand cmd)
        {
            // Check if this unit already has a pending command this tick
            if (PendingCommands.Any(c => c.UnitNbr == cmd.UnitNbr))
                return false;
            PendingCommands.Add(cmd);
            return true;
        }

        public CommandResult Move(int unitNbr, Position target)
        {
            if (IsOnCooldown(unitNbr)) return CommandResult.ON_COOLDOWN;
            if (!game.Units.TryGetValue(unitNbr, out var unit)) { ApplyCooldown(unitNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (unit.OwnerAgentNbr != agentNbr) { ApplyCooldown(unitNbr); return CommandResult.UNIT_NOT_FOUND; }

            // Already moving to same target — skip (matches Unity's AgentActionsAdapter)
            if (unit.CurrentAction == UnitAction.MOVE &&
                unit.SimPath != null && unit.SimPath.Count > 0 &&
                unit.SimPath[unit.SimPath.Count - 1] == target)
                return CommandResult.SUCCESS;

            var result = CommandValidator.ValidateMove(unit.UnitType, game.Map.IsPositionValid(target));
            if (result != CommandResult.SUCCESS) { ApplyCooldown(unitNbr); return result; }

            bool enqueued = Enqueue(new SimCommand
            {
                Type = CommandType.Move,
                UnitNbr = unitNbr,
                Target = target
            });
            if (enqueued) ResetCooldown(unitNbr);
            return CommandResult.SUCCESS;
        }

        public CommandResult Build(int unitNbr, Position target, UnitType unitType)
        {
            if (IsOnCooldown(unitNbr)) return CommandResult.ON_COOLDOWN;
            if (!game.Units.TryGetValue(unitNbr, out var unit)) { ApplyCooldown(unitNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (unit.OwnerAgentNbr != agentNbr) { ApplyCooldown(unitNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (!GameConstants.CAN_BUILD[unit.UnitType]) { ApplyCooldown(unitNbr); return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }
            if (!GameConstants.BUILDS[unit.UnitType].Contains(unitType)) { ApplyCooldown(unitNbr); return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }
            if (!game.Map.IsPositionValid(target)) { ApplyCooldown(unitNbr); return CommandResult.INVALID_POSITION; }

            bool enqueued = Enqueue(new SimCommand
            {
                Type = CommandType.Build,
                UnitNbr = unitNbr,
                Target = target,
                UnitType = unitType
            });
            if (enqueued) ResetCooldown(unitNbr);
            return CommandResult.SUCCESS;
        }

        public CommandResult Gather(int pawnNbr, int mineNbr, int baseNbr)
        {
            if (IsOnCooldown(pawnNbr)) return CommandResult.ON_COOLDOWN;
            if (!game.Units.TryGetValue(pawnNbr, out var pawn)) { ApplyCooldown(pawnNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (pawn.OwnerAgentNbr != agentNbr) { ApplyCooldown(pawnNbr); return CommandResult.UNIT_NOT_FOUND; }

            if (!game.Units.TryGetValue(mineNbr, out var mine)) { ApplyCooldown(pawnNbr); return CommandResult.TARGET_NOT_FOUND; }
            if (!game.Units.TryGetValue(baseNbr, out var baseUnit)) { ApplyCooldown(pawnNbr); return CommandResult.TARGET_NOT_FOUND; }

            var result = CommandValidator.ValidateGather(
                pawn.UnitType, mine.UnitType, baseUnit.UnitType,
                baseUnit.OwnerAgentNbr, agentNbr);
            if (result != CommandResult.SUCCESS) { ApplyCooldown(pawnNbr); return result; }

            bool enqueued = Enqueue(new SimCommand
            {
                Type = CommandType.Gather,
                UnitNbr = pawnNbr,
                MineNbr = mineNbr,
                BaseNbr = baseNbr
            });
            if (enqueued) ResetCooldown(pawnNbr);
            return CommandResult.SUCCESS;
        }

        public CommandResult Train(int buildingNbr, UnitType unitType)
        {
            if (IsOnCooldown(buildingNbr)) return CommandResult.ON_COOLDOWN;
            if (!game.Units.TryGetValue(buildingNbr, out var building)) { ApplyCooldown(buildingNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (building.OwnerAgentNbr != agentNbr) { ApplyCooldown(buildingNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (!GameConstants.CAN_TRAIN[building.UnitType]) { ApplyCooldown(buildingNbr); return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }
            if (!GameConstants.TRAINS[building.UnitType].Contains(unitType)) { ApplyCooldown(buildingNbr); return CommandResult.UNIT_CANNOT_PERFORM_ACTION; }

            bool enqueued = Enqueue(new SimCommand
            {
                Type = CommandType.Train,
                UnitNbr = buildingNbr,
                UnitType = unitType
            });
            if (enqueued) ResetCooldown(buildingNbr);
            return CommandResult.SUCCESS;
        }

        public CommandResult Attack(int unitNbr, int targetNbr)
        {
            if (IsOnCooldown(unitNbr)) return CommandResult.ON_COOLDOWN;
            if (!game.Units.TryGetValue(unitNbr, out var unit)) { ApplyCooldown(unitNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (unit.OwnerAgentNbr != agentNbr) { ApplyCooldown(unitNbr); return CommandResult.UNIT_NOT_FOUND; }

            if (!game.Units.TryGetValue(targetNbr, out var target)) { ApplyCooldown(unitNbr); return CommandResult.TARGET_NOT_FOUND; }

            // Already attacking same target — skip (matches Unity's AgentActionsAdapter)
            if (unit.CurrentAction == UnitAction.ATTACK && unit.AttackTargetNbr == targetNbr)
                return CommandResult.SUCCESS;

            var result = CommandValidator.ValidateAttack(
                unit.UnitType, target.OwnerAgentNbr, agentNbr, target.UnitType);
            if (result != CommandResult.SUCCESS) { ApplyCooldown(unitNbr); return result; }

            bool enqueued = Enqueue(new SimCommand
            {
                Type = CommandType.Attack,
                UnitNbr = unitNbr,
                TargetUnitNbr = targetNbr
            });
            if (enqueued) ResetCooldown(unitNbr);
            return CommandResult.SUCCESS;
        }

        public CommandResult Repair(int pawnNbr, int buildingNbr)
        {
            if (IsOnCooldown(pawnNbr)) return CommandResult.ON_COOLDOWN;
            if (!game.Units.TryGetValue(pawnNbr, out var pawn)) { ApplyCooldown(pawnNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (pawn.OwnerAgentNbr != agentNbr) { ApplyCooldown(pawnNbr); return CommandResult.UNIT_NOT_FOUND; }

            if (!game.Units.TryGetValue(buildingNbr, out var building)) { ApplyCooldown(pawnNbr); return CommandResult.TARGET_NOT_FOUND; }

            var result = CommandValidator.ValidateRepair(
                pawn.UnitType, building.UnitType, building.IsBuilt,
                building.OwnerAgentNbr, agentNbr);
            if (result != CommandResult.SUCCESS) { ApplyCooldown(pawnNbr); return result; }

            bool enqueued = Enqueue(new SimCommand
            {
                Type = CommandType.Repair,
                UnitNbr = pawnNbr,
                TargetUnitNbr = buildingNbr
            });
            if (enqueued) ResetCooldown(pawnNbr);
            return CommandResult.SUCCESS;
        }

        public CommandResult Heal(int monkNbr, int targetNbr)
        {
            if (IsOnCooldown(monkNbr)) return CommandResult.ON_COOLDOWN;
            if (!game.Units.TryGetValue(monkNbr, out var monk)) { ApplyCooldown(monkNbr); return CommandResult.UNIT_NOT_FOUND; }
            if (monk.OwnerAgentNbr != agentNbr) { ApplyCooldown(monkNbr); return CommandResult.UNIT_NOT_FOUND; }

            // Already healing — skip (matches Unity's AgentActionsAdapter)
            if (monk.CurrentAction == UnitAction.HEAL)
                return CommandResult.SUCCESS;

            if (!game.Units.TryGetValue(targetNbr, out var target)) { ApplyCooldown(monkNbr); return CommandResult.TARGET_NOT_FOUND; }

            var result = CommandValidator.ValidateHeal(
                monk.UnitType, monk.Mana,
                target.UnitType, target.Health,
                target.OwnerAgentNbr, agentNbr);
            if (result != CommandResult.SUCCESS) { ApplyCooldown(monkNbr); return result; }

            bool enqueued = Enqueue(new SimCommand
            {
                Type = CommandType.Heal,
                UnitNbr = monkNbr,
                TargetUnitNbr = targetNbr
            });
            if (enqueued) ResetCooldown(monkNbr);
            return CommandResult.SUCCESS;
        }

        public void Log(string message)
        {
            LogMessages.Add(message);
        }
    }
}
