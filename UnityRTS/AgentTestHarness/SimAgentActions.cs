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

        public void Move(int unitNbr, Position target)
        {
            if (!game.Units.TryGetValue(unitNbr, out var unit)) return;
            if (unit.OwnerAgentNbr != agentNbr) return;
            if (!GameConstants.CAN_MOVE[unit.UnitType]) return;
            if (!game.Map.IsPositionValid(target)) return;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.Move,
                UnitNbr = unitNbr,
                Target = target
            });
        }

        public void Build(int unitNbr, Position target, UnitType unitType)
        {
            if (!game.Units.TryGetValue(unitNbr, out var unit)) return;
            if (unit.OwnerAgentNbr != agentNbr) return;
            if (!GameConstants.CAN_BUILD[unit.UnitType]) return;
            if (!GameConstants.BUILDS[unit.UnitType].Contains(unitType)) return;
            if (!game.Map.IsPositionValid(target)) return;
            if (!game.Map.IsAreaBuildable(unitType, target)) return;

            // Check gold
            float cost = GameConstants.COST[unitType];
            if (game.GetGold(agentNbr) < cost) return;

            // Check dependencies
            foreach (UnitType dep in GameConstants.DEPENDENCY[unitType])
            {
                bool hasDep = game.Units.Values.Any(u =>
                    u.OwnerAgentNbr == agentNbr && u.UnitType == dep && u.IsBuilt);
                if (!hasDep) return;
            }

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.Build,
                UnitNbr = unitNbr,
                Target = target,
                UnitType = unitType
            });
        }

        public void Gather(int pawnNbr, int mineNbr, int baseNbr)
        {
            if (!game.Units.TryGetValue(pawnNbr, out var pawn)) return;
            if (pawn.OwnerAgentNbr != agentNbr) return;
            if (!GameConstants.CAN_GATHER[pawn.UnitType]) return;

            if (!game.Units.TryGetValue(mineNbr, out var mine)) return;
            if (mine.UnitType != UnitType.MINE) return;

            if (!game.Units.TryGetValue(baseNbr, out var baseUnit)) return;
            if (baseUnit.UnitType != UnitType.BASE) return;
            if (baseUnit.OwnerAgentNbr != agentNbr) return;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.Gather,
                UnitNbr = pawnNbr,
                MineNbr = mineNbr,
                BaseNbr = baseNbr
            });
        }

        public void Train(int buildingNbr, UnitType unitType)
        {
            if (!game.Units.TryGetValue(buildingNbr, out var building)) return;
            if (building.OwnerAgentNbr != agentNbr) return;
            if (!GameConstants.CAN_TRAIN[building.UnitType]) return;
            if (!building.IsBuilt) return;
            if (building.CurrentAction != UnitAction.IDLE) return;
            if (!GameConstants.TRAINS[building.UnitType].Contains(unitType)) return;

            float cost = GameConstants.COST[unitType];
            if (game.GetGold(agentNbr) < cost) return;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.Train,
                UnitNbr = buildingNbr,
                UnitType = unitType
            });
        }

        public void Attack(int unitNbr, int targetNbr)
        {
            if (!game.Units.TryGetValue(unitNbr, out var unit)) return;
            if (unit.OwnerAgentNbr != agentNbr) return;
            if (!GameConstants.CAN_ATTACK[unit.UnitType]) return;

            if (!game.Units.TryGetValue(targetNbr, out var target)) return;
            // Can't attack own units
            if (target.OwnerAgentNbr == agentNbr) return;
            // Can't attack mines
            if (target.UnitType == UnitType.MINE) return;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.Attack,
                UnitNbr = unitNbr,
                TargetUnitNbr = targetNbr
            });
        }

        public void Repair(int pawnNbr, int buildingNbr)
        {
            if (!game.Units.TryGetValue(pawnNbr, out var pawn)) return;
            if (pawn.OwnerAgentNbr != agentNbr) return;
            if (!GameConstants.CAN_BUILD[pawn.UnitType]) return;

            if (!game.Units.TryGetValue(buildingNbr, out var building)) return;
            // Target must be a non-mobile, non-mine building
            if (GameConstants.CAN_MOVE[building.UnitType]) return;
            if (building.UnitType == UnitType.MINE) return;
            // Building must be finished
            if (!building.IsBuilt) return;
            // Must belong to the same agent
            if (building.OwnerAgentNbr != agentNbr) return;

            PendingCommands.Add(new SimCommand
            {
                Type = CommandType.Repair,
                UnitNbr = pawnNbr,
                TargetUnitNbr = buildingNbr
            });
        }

        public void Log(string message)
        {
            LogMessages.Add(message);
        }
    }
}
