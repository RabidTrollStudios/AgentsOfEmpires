using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Read-only view of the simulation state for a specific agent.
    /// Implements IGameState by delegating to SimGame and SimMap.
    /// </summary>
    public class SimGameState : IGameState
    {
        private readonly SimGame game;
        private readonly int agentNbr;

        internal SimGameState(SimGame game, int agentNbr)
        {
            this.game = game;
            this.agentNbr = agentNbr;
        }

        public int MyAgentNbr => agentNbr;
        public int EnemyAgentNbr => agentNbr == 0 ? 1 : 0;
        public int MyGold => game.GetGold(agentNbr);
        public int EnemyGold => game.GetGold(EnemyAgentNbr);
        public Position MapSize => game.Map.Size;
        public int MyWins => game.GetWins(agentNbr);

        public IReadOnlyList<int> GetMyUnits(UnitType unitType)
        {
            return game.Units.Values
                .Where(u => u.OwnerAgentNbr == agentNbr && u.UnitType == unitType)
                .Select(u => u.UnitNbr)
                .ToList();
        }

        public IReadOnlyList<int> GetEnemyUnits(UnitType unitType)
        {
            return game.Units.Values
                .Where(u => u.OwnerAgentNbr == EnemyAgentNbr && u.UnitType == unitType)
                .Select(u => u.UnitNbr)
                .ToList();
        }

        public IReadOnlyList<int> GetAllUnits(UnitType unitType)
        {
            return game.Units.Values
                .Where(u => u.UnitType == unitType)
                .Select(u => u.UnitNbr)
                .ToList();
        }

        public UnitInfo? GetUnit(int unitNbr)
        {
            if (game.Units.TryGetValue(unitNbr, out var unit))
                return unit.ToUnitInfo();
            return null;
        }

        public bool IsPositionBuildable(Position position)
        {
            return game.Map.IsPositionBuildable(position);
        }

        public bool IsAreaBuildable(UnitType unitType, Position position)
        {
            return game.Map.IsAreaBuildable(unitType, position);
        }

        public bool IsBoundedAreaBuildable(UnitType unitType, Position position)
        {
            return game.Map.IsBoundedAreaBuildable(unitType, position);
        }

        public IReadOnlyList<Position> GetPathBetween(Position start, Position end)
        {
            return game.Map.FindPath(start, end);
        }

        public IReadOnlyList<Position> GetPathBetween(Position start, Position end, bool avoidUnits)
        {
            return game.Map.FindPath(start, end, avoidUnits);
        }

        public IReadOnlyList<Position> GetPathToUnit(Position start, UnitType unitType, Position unitPosition)
        {
            return game.Map.FindPathToUnit(start, unitType, unitPosition);
        }

        public IReadOnlyList<Position> GetBuildablePositionsNearUnit(UnitType unitType, Position unitPosition)
        {
            return game.Map.GetBuildablePositionsNearUnit(unitType, unitPosition);
        }

        public IReadOnlyList<Position> FindProspectiveBuildPositions(UnitType unitType)
        {
            return game.Map.FindProspectiveBuildPositions(unitType);
        }

        public IReadOnlyList<FailedCommand> GetFailedCommands()
        {
            return game.FailedCommands[agentNbr];
        }
    }
}
