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

        // Agent-facing unit lists are sorted ascending by UnitNbr so both engines
        // present units in an identical, deterministic order regardless of Dictionary
        // enumeration (which differs between Mono and .NET). See engine-parity H3.
        public IReadOnlyList<int> GetMyUnits(UnitType unitType)
        {
            return game.Units.Values
                .Where(u => u.OwnerAgentNbr == agentNbr && u.UnitType == unitType)
                .Select(u => u.UnitNbr)
                .OrderBy(n => n)
                .ToList();
        }

        public IReadOnlyList<int> GetEnemyUnits(UnitType unitType)
        {
            return game.Units.Values
                .Where(u => u.OwnerAgentNbr == EnemyAgentNbr && u.UnitType == unitType)
                .Select(u => u.UnitNbr)
                .OrderBy(n => n)
                .ToList();
        }

        public IReadOnlyList<int> GetAllUnits(UnitType unitType)
        {
            return game.Units.Values
                .Where(u => u.UnitType == unitType)
                .Select(u => u.UnitNbr)
                .OrderBy(n => n)
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
            // Exclude the agent's own pawns (they can move out of the way before
            // building), matching Unity's GameStateAdapter.IsAreaBuildable — so both
            // engines answer identically. See engine-parity H2.
            return game.Map.Grid.IsAreaBuildable(unitType, position, GetMyPawnPositions());
        }

        public bool IsBoundedAreaBuildable(UnitType unitType, Position position)
        {
            return game.Map.Grid.IsBoundedAreaBuildable(unitType, position, GetMyPawnPositions());
        }

        /// <summary>
        /// Cells occupied by this agent's own PAWNs. Excluded from buildability
        /// queries because the agent can move them before building — mirrors Unity's
        /// GameStateAdapter.GetMyPawnPositions.
        /// </summary>
        private ISet<Position> GetMyPawnPositions()
        {
            var positions = new HashSet<Position>();
            foreach (var u in game.Units.Values)
            {
                if (u.OwnerAgentNbr == agentNbr && u.UnitType == UnitType.PAWN)
                    positions.Add(u.GridPosition);
            }
            return positions;
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
            // Exclude the agent's own pawns from the candidate scan, matching Unity's
            // GameStateAdapter.FindProspectiveBuildPositions — otherwise the two engines
            // produce different candidate lists and pick different build sites (H2).
            return game.Map.Grid.FindProspectiveBuildPositions(unitType, GetMyPawnPositions());
        }

        public IReadOnlyList<FailedCommand> GetFailedCommands()
        {
            return game.FailedCommands[agentNbr];
        }
    }
}
