using System.Collections.Generic;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Pathfinding methods delegating to the shared GameGrid.
    /// </summary>
    public partial class SimMap
    {
        public List<Position> FindPath(Position start, Position end) => Grid.FindPath(start, end);
        public List<Position> FindPath(Position start, Position end, bool avoidUnits) => Grid.FindPath(start, end, avoidUnits);
        public List<Position> FindPathToUnit(Position start, UnitType unitType, Position unitAnchor) => Grid.FindPathToUnit(start, unitType, unitAnchor);
    }
}
