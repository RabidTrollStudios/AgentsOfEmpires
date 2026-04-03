using System.Collections.Generic;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Thin wrapper around <see cref="GameGrid"/> for backward compatibility.
    /// All grid logic and pathfinding is delegated to the shared implementation.
    /// </summary>
    public partial class SimMap
    {
        /// <summary>The shared grid backing this map.</summary>
        public GameGrid Grid { get; }

        public int Width => Grid.Width;
        public int Height => Grid.Height;
        public Position Size => Grid.Size;

        public SimMap(int width, int height)
        {
            Grid = new GameGrid(width, height);
        }

        // --- Cell Queries (delegate to Grid) ---

        public bool IsPositionValid(Position p) => Grid.IsPositionValid(p);
        public bool IsPositionBuildable(Position p) => Grid.IsPositionBuildable(p);
        public bool IsPositionWalkable(Position p) => Grid.IsPositionWalkable(p);
        public bool IsPositionPassage(Position p) => Grid.IsPositionPassage(p);
        public bool IsAreaBuildable(UnitType unitType, Position anchor) => Grid.IsAreaBuildable(unitType, anchor);
        public bool IsAreaBuildable(UnitType unitType, Position anchor, Position exclude) => Grid.IsAreaBuildable(unitType, anchor, exclude);
        public bool IsAreaBuildable(UnitType unitType, Position anchor, HashSet<Position> exclude) => Grid.IsAreaBuildable(unitType, anchor, exclude);
        public bool IsBoundedAreaBuildable(UnitType unitType, Position anchor) => Grid.IsBoundedAreaBuildable(unitType, anchor);
        public bool IsBoundedAreaBuildable(UnitType unitType, Position anchor, HashSet<Position> exclude) => Grid.IsBoundedAreaBuildable(unitType, anchor, exclude);

        // --- Cell Modification (delegate to Grid) ---

        public void SetAreaBuildability(UnitType unitType, Position anchor, bool isBuildable) => Grid.SetAreaBuildability(unitType, anchor, isBuildable);
        public void SetCellBlocked(Position p) => Grid.SetCellBlocked(p);

        // --- Neighbor Queries (delegate to Grid) ---

        public List<Position> GetPositionsNearUnit(UnitType unitType, Position anchor) => Grid.GetPositionsNearUnit(unitType, anchor);
        public List<Position> GetBuildablePositionsNearUnit(UnitType unitType, Position anchor) => Grid.GetBuildablePositionsNearUnit(unitType, anchor);
        public List<Position> FindProspectiveBuildPositions(UnitType unitType) => Grid.FindProspectiveBuildPositions(unitType);
    }
}
