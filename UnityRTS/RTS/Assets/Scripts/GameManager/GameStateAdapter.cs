using AgentSDK;
using GameManager.GameElements;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameManager
{
    /// <summary>
    /// Implements IGameState by wrapping the game engine's managers.
    /// Converts between Unity types (Vector3Int) and SDK types (Position).
    /// Includes per-frame pathfinding cache and rate limiting.
    /// </summary>
    public class GameStateAdapter : IGameState
    {
        private int agentNbr;
        private int enemyAgentNbr;
        private UnitManager unitManager;
        private MapManager mapManager;

        // Per-frame pathfinding cache and rate limiting
        private const int MAX_PATH_CALLS_PER_FRAME = 20;
        private int lastCacheFrame = -1;
        private int pathCallsThisFrame = 0;
        private Dictionary<(Position, Position), IReadOnlyList<Position>> pathBetweenCache
            = new Dictionary<(Position, Position), IReadOnlyList<Position>>();
        private Dictionary<(Position, AgentSDK.UnitType, Position), IReadOnlyList<Position>> pathToUnitCache
            = new Dictionary<(Position, AgentSDK.UnitType, Position), IReadOnlyList<Position>>();
        private static readonly IReadOnlyList<Position> EmptyPath = new List<Position>();

        public GameStateAdapter(int agentNbr, UnitManager unitManager, MapManager mapManager)
        {
            this.agentNbr = agentNbr;
            this.unitManager = unitManager;
            this.mapManager = mapManager;
        }

        internal void UpdateEnemyAgentNbr(int enemyNbr)
        {
            this.enemyAgentNbr = enemyNbr;
        }

        /// <summary>
        /// Clears the path cache if we're on a new frame.
        /// </summary>
        private void ResetCacheIfNewFrame()
        {
            int currentFrame = Time.frameCount;
            if (currentFrame != lastCacheFrame)
            {
                lastCacheFrame = currentFrame;
                pathCallsThisFrame = 0;
                pathBetweenCache.Clear();
                pathToUnitCache.Clear();
            }
        }

        public int MyAgentNbr => agentNbr;
        public int EnemyAgentNbr => enemyAgentNbr;
        public int MyGold => GameManager.Instance.GetAgent(agentNbr).Gold;
        public int EnemyGold => enemyAgentNbr >= 0 ? GameManager.Instance.GetAgent(enemyAgentNbr).Gold : 0;
        public Position MapSize => new Position(mapManager.MapSize.x, mapManager.MapSize.y);
        public int MyWins => GameManager.Instance.GetAgent(agentNbr).AgentNbrWins;

        public IReadOnlyList<int> GetMyUnits(AgentSDK.UnitType unitType)
            => unitManager.GetUnitNbrsOfType(unitType, agentNbr);

        public IReadOnlyList<int> GetEnemyUnits(AgentSDK.UnitType unitType)
            => enemyAgentNbr >= 0 ? unitManager.GetUnitNbrsOfType(unitType, enemyAgentNbr) : new List<int>();

        public IReadOnlyList<int> GetAllUnits(AgentSDK.UnitType unitType)
            => unitManager.GetUnitNbrsOfType(unitType);

        public UnitInfo? GetUnit(int unitNbr)
        {
            var unit = unitManager.GetUnit(unitNbr);
            if (unit == null) return null;

            int ownerNbr = unit.OwnerAgentNbr;
            return new UnitInfo(
                unit.UnitNbr,
                unit.UnitType,
                new Position(unit.GridPosition.x, unit.GridPosition.y),
                unit.Health,
                unit.IsBuilt,
                unit.CurrentAction,
                unit.CanMove,
                unit.CanBuild,
                unit.CanTrain,
                unit.CanAttack,
                unit.CanGather,
                unit.CanHeal,
                unit.Mana,
                ownerNbr
            );
        }

        public bool IsPositionBuildable(Position pos)
            => mapManager.IsGridPositionBuildable(new Vector3Int(pos.X, pos.Y, 0));

        /// <summary>
        /// Collects grid positions of all this agent's pawns (1x1 units that can move).
        /// These are excluded from buildability checks because the agent can move them.
        /// </summary>
        private HashSet<Vector3Int> GetMyPawnPositions()
        {
            var positions = new HashSet<Vector3Int>();
            foreach (int unitNbr in unitManager.GetUnitNbrsOfType(AgentSDK.UnitType.PAWN, agentNbr))
            {
                var unit = unitManager.GetUnit(unitNbr);
                if (unit != null)
                    positions.Add(unit.GridPosition);
            }
            return positions;
        }

        public bool IsAreaBuildable(AgentSDK.UnitType unitType, Position pos)
            => mapManager.IsAreaBuildable(unitType, new Vector3Int(pos.X, pos.Y, 0), GetMyPawnPositions());

        public bool IsBoundedAreaBuildable(AgentSDK.UnitType unitType, Position pos)
            => mapManager.IsBoundedAreaBuildable(unitType, new Vector3Int(pos.X, pos.Y, 0), GetMyPawnPositions());

        public IReadOnlyList<Position> GetPathBetween(Position start, Position end)
        {
            ResetCacheIfNewFrame();

            var key = (start, end);
            if (pathBetweenCache.TryGetValue(key, out var cached))
                return cached;

            if (pathCallsThisFrame >= MAX_PATH_CALLS_PER_FRAME)
            {
                GameManager.Instance.Log("WARNING: Agent " + agentNbr
                    + " exceeded pathfinding budget (" + MAX_PATH_CALLS_PER_FRAME + " calls/frame)",
                    GameManager.Instance.gameObject);
                return EmptyPath;
            }

            pathCallsThisFrame++;
            var path = mapManager.GetPathBetweenGridPositions(
                new Vector3Int(start.X, start.Y, 0),
                new Vector3Int(end.X, end.Y, 0));
            var result = path.Select(p => new Position(p.x, p.y)).ToList();
            pathBetweenCache[key] = result;
            return result;
        }

        public IReadOnlyList<Position> GetPathBetween(Position start, Position end, bool avoidUnits)
        {
            if (!avoidUnits)
                return GetPathBetween(start, end);

            ResetCacheIfNewFrame();

            if (pathCallsThisFrame >= MAX_PATH_CALLS_PER_FRAME)
            {
                GameManager.Instance.Log("WARNING: Agent " + agentNbr
                    + " exceeded pathfinding budget (" + MAX_PATH_CALLS_PER_FRAME + " calls/frame)",
                    GameManager.Instance.gameObject);
                return EmptyPath;
            }

            pathCallsThisFrame++;
            var path = mapManager.GetPathBetweenGridPositions(
                new Vector3Int(start.X, start.Y, 0),
                new Vector3Int(end.X, end.Y, 0),
                avoidUnits: true);
            var result = path.Select(p => new Position(p.x, p.y)).ToList();
            return result;
        }

        public IReadOnlyList<Position> GetPathToUnit(Position start, AgentSDK.UnitType unitType, Position unitPos)
        {
            ResetCacheIfNewFrame();

            var key = (start, unitType, unitPos);
            if (pathToUnitCache.TryGetValue(key, out var cached))
                return cached;

            if (pathCallsThisFrame >= MAX_PATH_CALLS_PER_FRAME)
            {
                GameManager.Instance.Log("WARNING: Agent " + agentNbr
                    + " exceeded pathfinding budget (" + MAX_PATH_CALLS_PER_FRAME + " calls/frame)",
                    GameManager.Instance.gameObject);
                return EmptyPath;
            }

            pathCallsThisFrame++;
            var path = mapManager.GetPathToUnit(
                new Vector3Int(start.X, start.Y, 0),
                unitType,
                new Vector3Int(unitPos.X, unitPos.Y, 0));
            var result = path.Select(p => new Position(p.x, p.y)).ToList();
            pathToUnitCache[key] = result;
            return result;
        }

        public IReadOnlyList<Position> GetBuildablePositionsNearUnit(AgentSDK.UnitType unitType, Position unitPos)
        {
            var positions = mapManager.GetBuildableGridPositionsNearUnit(
                unitType, new Vector3Int(unitPos.X, unitPos.Y, 0));
            return positions.Select(p => new Position(p.x, p.y)).ToList();
        }

        public IReadOnlyList<Position> FindProspectiveBuildPositions(AgentSDK.UnitType unitType)
        {
            var pawnPositions = GetMyPawnPositions();
            var result = new List<Position>();
            for (int i = 0; i < mapManager.MapSize.x; ++i)
            {
                for (int j = 0; j < mapManager.MapSize.y; ++j)
                {
                    var testPos = new Vector3Int(i, j, 0);
                    if (Utility.IsValidGridLocation(testPos)
                        && mapManager.IsBoundedAreaBuildable(unitType, testPos, pawnPositions))
                    {
                        result.Add(new Position(i, j));
                    }
                }
            }
            return result;
        }

        // Failed commands from Phase 1 processing, cleared each frame
        private readonly List<AgentSDK.FailedCommand> failedCommands = new List<AgentSDK.FailedCommand>();

        /// <summary>Record a command that failed during dispatch/execution.</summary>
        internal void AddFailedCommand(AgentSDK.FailedCommand failure)
        {
            failedCommands.Add(failure);
        }

        /// <summary>Clear failed commands at the start of each tick.</summary>
        internal void ClearFailedCommands()
        {
            failedCommands.Clear();
        }

        public IReadOnlyList<AgentSDK.FailedCommand> GetFailedCommands()
        {
            return failedCommands;
        }
    }
}
