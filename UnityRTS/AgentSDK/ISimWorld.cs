using System.Collections.Generic;

namespace AgentSDK
{
    /// <summary>
    /// World abstraction for the shared StepEngine.
    /// Provides unit lookup, pathfinding, gold management, and unit spawning.
    /// </summary>
    public interface ISimWorld
    {
        /// <summary>Look up a unit by number. Returns null if not found or dead.</summary>
        ISimUnit GetUnit(int unitNbr);

        /// <summary>All live units, for iteration.</summary>
        IEnumerable<ISimUnit> AllUnits { get; }

        /// <summary>The shared game grid.</summary>
        GameGrid Grid { get; }

        /// <summary>Find path between two positions.</summary>
        List<Position> FindPath(Position start, Position end);

        /// <summary>Find path to a neighbor of the target unit.</summary>
        List<Position> FindPathToUnit(Position start, UnitType unitType, Position anchor);

        /// <summary>Get OPEN positions adjacent to a unit (for spawning, adjacency checks).</summary>
        List<Position> GetBuildablePositionsNearUnit(UnitType unitType, Position anchor);

        /// <summary>Check if a position is adjacent to a unit.</summary>
        bool IsNeighborOfUnit(Position pos, UnitType unitType, Position anchor);

        /// <summary>Add gold to an agent's stockpile.</summary>
        void AddGold(int agentNbr, int amount);

        /// <summary>Get an agent's current gold.</summary>
        int GetGold(int agentNbr);

        /// <summary>Spawn a new unit on the map.</summary>
        ISimUnit SpawnUnit(int ownerAgentNbr, UnitType unitType, Position pos, float health, bool isBuilt);

        /// <summary>Remove a unit from the map (death).</summary>
        void RemoveUnit(ISimUnit unit);

        /// <summary>Game-speed-dependent constants.</summary>
        DerivedGameConstants Constants { get; }

        /// <summary>Seconds per step (0.05 at 20Hz).</summary>
        float StepDuration { get; }
    }
}
