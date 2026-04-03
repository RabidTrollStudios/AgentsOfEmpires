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
    /// </summary>
    public class SimAgentActions : IAgentActions
    {
        private readonly SimGame game;
        private readonly int agentNbr;

        internal List<SimCommand> PendingCommands { get; } = new List<SimCommand>();

        /// <summary>All log messages issued by this agent.</summary>
        public List<string> LogMessages { get; } = new List<string>();

        internal SimAgentActions(SimGame game, int agentNbr)
        {
            this.game = game;
            this.agentNbr = agentNbr;
        }

        internal void ClearPending()
        {
            PendingCommands.Clear();
        }

        public CommandResult Move(int unitNbr, Position target)
        {
            if (!game.Units.TryGetValue(unitNbr, out var unit)) return CommandResult.UNIT_NOT_FOUND;
            if (unit.OwnerAgentNbr != agentNbr) return CommandResult.UNIT_NOT_FOUND;

            var result = CommandValidator.ValidateMove(unit.UnitType, game.Map.IsPositionValid(target));
            if (result != CommandResult.SUCCESS) return result;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.MOVE,
                UnitNbr = unitNbr,
                Target = target
            });
            return CommandResult.SUCCESS;
        }

        public CommandResult Build(int unitNbr, Position target, UnitType unitType)
        {
            if (!game.Units.TryGetValue(unitNbr, out var unit)) return CommandResult.UNIT_NOT_FOUND;
            if (unit.OwnerAgentNbr != agentNbr) return CommandResult.UNIT_NOT_FOUND;
            if (!GameConstants.CAN_BUILD[unit.UnitType]) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
            if (!GameConstants.BUILDS[unit.UnitType].Contains(unitType)) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
            if (!game.Map.IsPositionValid(target)) return CommandResult.INVALID_POSITION;

            // Heavy validation (gold, dependencies, area) is deferred to ProcessBuild,
            // matching Unity's two-phase approach where AgentActionsAdapter does light
            // checks and EventDispatcher does heavy checks at dispatch time.
            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.BUILD,
                UnitNbr = unitNbr,
                Target = target,
                UnitType = unitType
            });
            return CommandResult.SUCCESS;
        }

        public CommandResult Gather(int pawnNbr, int mineNbr, int baseNbr)
        {
            if (!game.Units.TryGetValue(pawnNbr, out var pawn)) return CommandResult.UNIT_NOT_FOUND;
            if (pawn.OwnerAgentNbr != agentNbr) return CommandResult.UNIT_NOT_FOUND;

            if (!game.Units.TryGetValue(mineNbr, out var mine)) return CommandResult.TARGET_NOT_FOUND;
            if (!game.Units.TryGetValue(baseNbr, out var baseUnit)) return CommandResult.TARGET_NOT_FOUND;

            var result = CommandValidator.ValidateGather(
                pawn.UnitType, mine.UnitType, baseUnit.UnitType,
                baseUnit.OwnerAgentNbr, agentNbr);
            if (result != CommandResult.SUCCESS) return result;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.GATHER,
                UnitNbr = pawnNbr,
                MineNbr = mineNbr,
                BaseNbr = baseNbr
            });
            return CommandResult.SUCCESS;
        }

        public CommandResult Train(int buildingNbr, UnitType unitType)
        {
            if (!game.Units.TryGetValue(buildingNbr, out var building)) return CommandResult.UNIT_NOT_FOUND;
            if (building.OwnerAgentNbr != agentNbr) return CommandResult.UNIT_NOT_FOUND;
            if (!GameConstants.CAN_TRAIN[building.UnitType]) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
            if (!GameConstants.TRAINS[building.UnitType].Contains(unitType)) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;

            // Heavy validation (gold, isBuilt, idle) deferred to ProcessTrain
            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.TRAIN,
                UnitNbr = buildingNbr,
                UnitType = unitType
            });
            return CommandResult.SUCCESS;
        }

        public CommandResult Attack(int unitNbr, int targetNbr)
        {
            if (!game.Units.TryGetValue(unitNbr, out var unit)) return CommandResult.UNIT_NOT_FOUND;
            if (unit.OwnerAgentNbr != agentNbr) return CommandResult.UNIT_NOT_FOUND;

            if (!game.Units.TryGetValue(targetNbr, out var target)) return CommandResult.TARGET_NOT_FOUND;

            var result = CommandValidator.ValidateAttack(
                unit.UnitType, target.OwnerAgentNbr, agentNbr, target.UnitType);
            if (result != CommandResult.SUCCESS) return result;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.ATTACK,
                UnitNbr = unitNbr,
                TargetUnitNbr = targetNbr
            });
            return CommandResult.SUCCESS;
        }

        public CommandResult Repair(int pawnNbr, int buildingNbr)
        {
            if (!game.Units.TryGetValue(pawnNbr, out var pawn)) return CommandResult.UNIT_NOT_FOUND;
            if (pawn.OwnerAgentNbr != agentNbr) return CommandResult.UNIT_NOT_FOUND;

            if (!game.Units.TryGetValue(buildingNbr, out var building)) return CommandResult.TARGET_NOT_FOUND;

            var result = CommandValidator.ValidateRepair(
                pawn.UnitType, building.UnitType, building.IsBuilt,
                building.OwnerAgentNbr, agentNbr);
            if (result != CommandResult.SUCCESS) return result;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.REPAIR,
                UnitNbr = pawnNbr,
                TargetUnitNbr = buildingNbr
            });
            return CommandResult.SUCCESS;
        }

        public CommandResult Heal(int monkNbr, int targetNbr)
        {
            if (!game.Units.TryGetValue(monkNbr, out var monk)) return CommandResult.UNIT_NOT_FOUND;
            if (monk.OwnerAgentNbr != agentNbr) return CommandResult.UNIT_NOT_FOUND;

            if (!game.Units.TryGetValue(targetNbr, out var target)) return CommandResult.TARGET_NOT_FOUND;

            var result = CommandValidator.ValidateHeal(
                monk.UnitType, monk.Mana,
                target.UnitType, target.Health,
                target.OwnerAgentNbr, agentNbr);
            if (result != CommandResult.SUCCESS) return result;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.HEAL,
                UnitNbr = monkNbr,
                TargetUnitNbr = targetNbr
            });
            return CommandResult.SUCCESS;
        }

        public void Log(string message)
        {
            LogMessages.Add(message);
        }
    }
}
