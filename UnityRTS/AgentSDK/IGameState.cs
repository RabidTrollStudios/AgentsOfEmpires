using System.Collections.Generic;

namespace AgentSDK
{
    /// <summary>
    /// Read-only view of the current game state.
    /// Passed to your agent each frame so you can make decisions.
    /// </summary>
    public interface IGameState
    {
        /// <summary>Your agent's unique number</summary>
        int MyAgentNbr { get; }

        /// <summary>The enemy agent's unique number</summary>
        int EnemyAgentNbr { get; }

        /// <summary>Your current gold</summary>
        int MyGold { get; }

        /// <summary>The enemy's current gold</summary>
        int EnemyGold { get; }

        /// <summary>Size of the map grid</summary>
        Position MapSize { get; }

        /// <summary>Your agent's win count</summary>
        int MyWins { get; }

        /// <summary>
        /// Get all of your units of a specific type.
        /// Returns a list of unit numbers (IDs).
        /// </summary>
        IReadOnlyList<int> GetMyUnits(UnitType unitType);

        /// <summary>
        /// Get all enemy units of a specific type.
        /// Returns a list of unit numbers (IDs).
        /// </summary>
        IReadOnlyList<int> GetEnemyUnits(UnitType unitType);

        /// <summary>
        /// Get all units of a specific type regardless of owner (e.g., mines).
        /// Returns a list of unit numbers (IDs).
        /// </summary>
        IReadOnlyList<int> GetAllUnits(UnitType unitType);

        /// <summary>
        /// Get detailed info about a specific unit by its number.
        /// Returns null if the unit doesn't exist.
        /// </summary>
        UnitInfo? GetUnit(int unitNbr);

        /// <summary>
        /// Is a specific grid position walkable/buildable?
        /// </summary>
        bool IsPositionBuildable(Position position);

        /// <summary>
        /// Can a unit of this type be placed at this position?
        /// Checks all tiles the unit would occupy based on its size.
        /// </summary>
        bool IsAreaBuildable(UnitType unitType, Position position);

        /// <summary>
        /// Can a unit of this type be placed at this position with a walkable border?
        /// Like IsAreaBuildable but also checks one tile of clearance around the unit.
        /// </summary>
        bool IsBoundedAreaBuildable(UnitType unitType, Position position);

        /// <summary>
        /// Find the shortest path between two grid positions.
        /// Returns an empty list if no path exists.
        /// </summary>
        IReadOnlyList<Position> GetPathBetween(Position start, Position end);

        /// <summary>
        /// Find the shortest path between two grid positions.
        /// When avoidUnits is true, cells occupied by mobile units are treated as impassable
        /// (uses buildable checks instead of walkable), producing paths that avoid other units.
        /// Returns an empty list if no path exists.
        /// </summary>
        IReadOnlyList<Position> GetPathBetween(Position start, Position end, bool avoidUnits);

        /// <summary>
        /// Find a path from a position to any walkable tile adjacent to a unit.
        /// Returns an empty list if no path exists.
        /// </summary>
        IReadOnlyList<Position> GetPathToUnit(Position start, UnitType unitType, Position unitPosition);

        /// <summary>
        /// Get all buildable positions adjacent to a unit.
        /// </summary>
        IReadOnlyList<Position> GetBuildablePositionsNearUnit(UnitType unitType, Position unitPosition);

        /// <summary>
        /// Find all valid build positions on the map for a given unit type.
        /// Checks bounded area buildability (clearance around the unit).
        /// </summary>
        IReadOnlyList<Position> FindProspectiveBuildPositions(UnitType unitType);

        /// <summary>
        /// Get commands from the previous tick that passed queue-time validation
        /// but failed during Phase 1 processing. Gold for failed Build/Train
        /// commands is automatically refunded.
        /// </summary>
        IReadOnlyList<FailedCommand> GetFailedCommands();
    }
}
