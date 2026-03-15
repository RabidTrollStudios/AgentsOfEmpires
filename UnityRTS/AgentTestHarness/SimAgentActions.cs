using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Types of commands an agent can issue.
    /// </summary>
    public enum CommandType
    {
        Move,
        Build,
        Gather,
        Train,
        Attack,
        Repair,
        Log
    }

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
    /// Implements IAgentActions for the simulation. Validates commands against
    /// game rules and queues them for execution by SimGame.
    /// Invalid commands are silently ignored (matching real game behavior).
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
            if (!GameConstants.CAN_MOVE[unit.UnitType]) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
            if (!game.Map.IsPositionValid(target)) return CommandResult.INVALID_POSITION;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.Move,
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
            if (!game.Map.IsAreaBuildable(unitType, target)) return CommandResult.AREA_NOT_BUILDABLE;

            // Check gold
            float cost = GameConstants.COST[unitType];
            if (game.GetGold(agentNbr) < cost) return CommandResult.INSUFFICIENT_GOLD;

            // Check dependencies
            foreach (UnitType dep in GameConstants.DEPENDENCY[unitType])
            {
                bool hasDep = game.Units.Values.Any(u =>
                    u.OwnerAgentNbr == agentNbr && u.UnitType == dep && u.IsBuilt);
                if (!hasDep) return CommandResult.MISSING_DEPENDENCY;
            }

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.Build,
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
            if (!GameConstants.CAN_GATHER[pawn.UnitType]) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;

            if (!game.Units.TryGetValue(mineNbr, out var mine)) return CommandResult.TARGET_NOT_FOUND;
            if (mine.UnitType != UnitType.MINE) return CommandResult.INVALID_TARGET;

            if (!game.Units.TryGetValue(baseNbr, out var baseUnit)) return CommandResult.TARGET_NOT_FOUND;
            if (baseUnit.UnitType != UnitType.BASE) return CommandResult.INVALID_TARGET;
            if (baseUnit.OwnerAgentNbr != agentNbr) return CommandResult.INVALID_TARGET;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.Gather,
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
            if (!building.IsBuilt) return CommandResult.BUILDING_NOT_FINISHED;
            if (building.CurrentAction != UnitAction.IDLE) return CommandResult.UNIT_BUSY;
            if (!GameConstants.TRAINS[building.UnitType].Contains(unitType)) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;

            float cost = GameConstants.COST[unitType];
            if (game.GetGold(agentNbr) < cost) return CommandResult.INSUFFICIENT_GOLD;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.Train,
                UnitNbr = buildingNbr,
                UnitType = unitType
            });
            return CommandResult.SUCCESS;
        }

        public CommandResult Attack(int unitNbr, int targetNbr)
        {
            if (!game.Units.TryGetValue(unitNbr, out var unit)) return CommandResult.UNIT_NOT_FOUND;
            if (unit.OwnerAgentNbr != agentNbr) return CommandResult.UNIT_NOT_FOUND;
            if (!GameConstants.CAN_ATTACK[unit.UnitType]) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;

            if (!game.Units.TryGetValue(targetNbr, out var target)) return CommandResult.TARGET_NOT_FOUND;
            // Can't attack own units
            if (target.OwnerAgentNbr == agentNbr) return CommandResult.FRIENDLY_FIRE;
            // Can't attack mines
            if (target.UnitType == UnitType.MINE) return CommandResult.INVALID_TARGET;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.Attack,
                UnitNbr = unitNbr,
                TargetUnitNbr = targetNbr
            });
            return CommandResult.SUCCESS;
        }

        public CommandResult Repair(int pawnNbr, int buildingNbr)
        {
            if (!game.Units.TryGetValue(pawnNbr, out var pawn)) return CommandResult.UNIT_NOT_FOUND;
            if (pawn.OwnerAgentNbr != agentNbr) return CommandResult.UNIT_NOT_FOUND;
            if (!GameConstants.CAN_BUILD[pawn.UnitType]) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;

            if (!game.Units.TryGetValue(buildingNbr, out var building)) return CommandResult.TARGET_NOT_FOUND;
            // Target must be a non-mobile, non-mine building
            if (GameConstants.CAN_MOVE[building.UnitType]) return CommandResult.INVALID_TARGET;
            if (building.UnitType == UnitType.MINE) return CommandResult.INVALID_TARGET;
            // Building must be finished
            if (!building.IsBuilt) return CommandResult.BUILDING_NOT_FINISHED;
            // Must belong to the same agent
            if (building.OwnerAgentNbr != agentNbr) return CommandResult.FRIENDLY_FIRE;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.Repair,
                UnitNbr = pawnNbr,
                TargetUnitNbr = buildingNbr
            });
            return CommandResult.SUCCESS;
        }

        public void Log(string message)
        {
            LogMessages.Add(message);
        }
    }
}
