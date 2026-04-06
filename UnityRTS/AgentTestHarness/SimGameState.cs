using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Read-only view of the simulation state for a specific agent.
    /// Implements IGameState by delegating to SimGame and SimMap.
    ///
    /// Includes per-frame pathfinding cache/budget and buildability pawn filtering
    /// to match Unity's GameStateAdapter for cross-engine parity.
    /// </summary>
    public class SimGameState : IGameState
    {
        private readonly SimGame game;
        private readonly int agentNbr;

        // Per-frame pathfinding cache and rate limiting — mirrors GameStateAdapter
        private const int MAX_PATH_CALLS_PER_FRAME = 20;
        private int lastCacheFrame = -1;
        private int pathCallsThisFrame = 0;
        private Dictionary<(Position, Position), IReadOnlyList<Position>> pathBetweenCache
            = new Dictionary<(Position, Position), IReadOnlyList<Position>>();
        private Dictionary<(Position, UnitType, Position), IReadOnlyList<Position>> pathToUnitCache
            = new Dictionary<(Position, UnitType, Position), IReadOnlyList<Position>>();
        private static readonly IReadOnlyList<Position> EmptyPath = new List<Position>();

        internal SimGameState(SimGame game, int agentNbr)
        {
            this.game = game;
            this.agentNbr = agentNbr;
        }

        /// <summary>
        /// Clear the path cache at the start of each frame.
        /// Called by SimGame.Step() before agent update.
        /// </summary>
        internal void ClearPathCache()
        {
            pathCallsThisFrame = 0;
            pathBetweenCache.Clear();
            pathToUnitCache.Clear();
        }

        /// <summary>
        /// Reset cache if we're on a new frame (fallback — ClearPathCache is the primary mechanism).
        /// </summary>
        private void ResetCacheIfNewFrame()
        {
            int currentFrame = game.CurrentFrame;
            if (currentFrame != lastCacheFrame)
            {
                lastCacheFrame = currentFrame;
                pathCallsThisFrame = 0;
                pathBetweenCache.Clear();
                pathToUnitCache.Clear();
            }
        }

        public int MyAgentNbr => agentNbr;
        public int EnemyAgentNbr => agentNbr == 0 ? 1 : 0;
        public int MyGold => game.GetGold(agentNbr);
        public int EnemyGold => game.GetGold(EnemyAgentNbr);
        public Position MapSize => game.Map.Size;
        public int GameSpeed => game.Config.GameSpeed;
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

        /// <summary>
        /// Collects grid positions of all this agent's pawns.
        /// These are excluded from buildability checks because the agent can move them.
        /// Matches Unity's GameStateAdapter.GetMyPawnPositions().
        /// </summary>
        private HashSet<Position> GetMyPawnPositions()
        {
            var positions = new HashSet<Position>();
            foreach (var unit in game.Units.Values)
            {
                if (unit.OwnerAgentNbr == agentNbr && unit.UnitType == UnitType.PAWN)
                    positions.Add(unit.GridPosition);
            }
            return positions;
        }

        public bool IsAreaBuildable(UnitType unitType, Position position)
        {
            return game.Map.IsAreaBuildable(unitType, position, GetMyPawnPositions());
        }

        public bool IsBoundedAreaBuildable(UnitType unitType, Position position)
        {
            return game.Map.IsBoundedAreaBuildable(unitType, position, GetMyPawnPositions());
        }

        public IReadOnlyList<Position> GetPathBetween(Position start, Position end)
        {
            ResetCacheIfNewFrame();

            var key = (start, end);
            if (pathBetweenCache.TryGetValue(key, out var cached))
                return cached;

            if (pathCallsThisFrame >= MAX_PATH_CALLS_PER_FRAME)
                return EmptyPath;

            pathCallsThisFrame++;
            var result = game.Map.FindPath(start, end);
            pathBetweenCache[key] = result;
            return result;
        }

        public IReadOnlyList<Position> GetPathBetween(Position start, Position end, bool avoidUnits)
        {
            if (!avoidUnits)
                return GetPathBetween(start, end);

            ResetCacheIfNewFrame();

            if (pathCallsThisFrame >= MAX_PATH_CALLS_PER_FRAME)
                return EmptyPath;

            pathCallsThisFrame++;
            return game.Map.FindPath(start, end, avoidUnits);
        }

        public IReadOnlyList<Position> GetPathToUnit(Position start, UnitType unitType, Position unitPosition)
        {
            ResetCacheIfNewFrame();

            var key = (start, unitType, unitPosition);
            if (pathToUnitCache.TryGetValue(key, out var cached))
                return cached;

            if (pathCallsThisFrame >= MAX_PATH_CALLS_PER_FRAME)
                return EmptyPath;

            pathCallsThisFrame++;
            var result = game.Map.FindPathToUnit(start, unitType, unitPosition);
            pathToUnitCache[key] = result;
            return result;
        }

        public IReadOnlyList<Position> GetBuildablePositionsNearUnit(UnitType unitType, Position unitPosition)
        {
            return game.Map.GetBuildablePositionsNearUnit(unitType, unitPosition);
        }

        public IReadOnlyList<Position> FindProspectiveBuildPositions(UnitType unitType)
        {
            var pawnPositions = GetMyPawnPositions();
            var result = new List<Position>();
            var size = game.Map.Size;
            for (int i = 0; i < size.X; ++i)
            {
                for (int j = 0; j < size.Y; ++j)
                {
                    var pos = new Position(i, j);
                    if (game.Map.IsPositionValid(pos)
                        && game.Map.IsBoundedAreaBuildable(unitType, pos, pawnPositions))
                    {
                        result.Add(pos);
                    }
                }
            }
            return result;
        }

        public IReadOnlyList<FailedCommand> GetFailedCommands()
        {
            return game.FailedCommands[agentNbr];
        }
    }
}
