using System.Collections.Generic;

namespace AgentSDK
{
    /// <summary>
    /// Pure-C# grid for an RTS map. Tracks three boolean layers per cell:
    ///   buildable — can a new unit/building be placed here?
    ///   walkable  — can a mobile unit path through here?
    ///   passage   — building top-row (walkable, not buildable, units move freely).
    ///
    /// Both the Unity game engine and the AgentTestHarness SimGame share this
    /// single implementation so grid state and queries are guaranteed identical.
    ///
    /// Coordinate system: (0,0) bottom-left; +X right, +Y up.
    /// Building footprints extend from anchor (x,y) rightward and downward:
    ///   cells = (x+i, y-j) for i in [0..sizeX), j in [0..sizeY).
    /// </summary>
    public class GameGrid
    {
        private readonly bool[,] buildable;
        private readonly bool[,] walkable;
        private readonly bool[,] passage;

        public int Width { get; }
        public int Height { get; }
        public Position Size => new Position(Width, Height);

        public GameGrid(int width, int height)
        {
            Width = width;
            Height = height;
            buildable = new bool[width, height];
            walkable = new bool[width, height];
            passage = new bool[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    buildable[x, y] = true;
                    walkable[x, y] = true;
                }
            }
        }

        #region Cell Queries

        public bool IsPositionValid(Position p)
        {
            return p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;
        }

        public bool IsPositionValid(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public bool IsPositionBuildable(Position p)
        {
            return IsPositionValid(p) && buildable[p.X, p.Y];
        }

        public bool IsPositionWalkable(Position p)
        {
            return IsPositionValid(p) && walkable[p.X, p.Y];
        }

        public bool IsPositionPassage(Position p)
        {
            return IsPositionValid(p) && passage[p.X, p.Y];
        }

        /// <summary>
        /// Check if the full footprint for a unit type is buildable at the given anchor.
        /// </summary>
        public bool IsAreaBuildable(UnitType unitType, Position anchor)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            for (int i = 0; i < size.X; i++)
            {
                for (int j = 0; j < size.Y; j++)
                {
                    var cell = new Position(anchor.X + i, anchor.Y - j);
                    if (!IsPositionValid(cell) || !buildable[cell.X, cell.Y])
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Check if the full footprint is buildable, excluding one position
        /// (e.g., the building pawn's cell which it will vacate).
        /// </summary>
        public bool IsAreaBuildable(UnitType unitType, Position anchor, Position exclude)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            for (int i = 0; i < size.X; i++)
            {
                for (int j = 0; j < size.Y; j++)
                {
                    var cell = new Position(anchor.X + i, anchor.Y - j);
                    if (cell == exclude) continue;
                    if (!IsPositionValid(cell) || !buildable[cell.X, cell.Y])
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Check if the unit footprint plus a 1-cell border is all buildable.
        /// </summary>
        public bool IsBoundedAreaBuildable(UnitType unitType, Position anchor)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            for (int i = -1; i <= size.X; i++)
            {
                for (int j = -1; j <= size.Y; j++)
                {
                    var cell = new Position(anchor.X + i, anchor.Y - j);
                    if (!IsPositionValid(cell) || !buildable[cell.X, cell.Y])
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region Cell Modification

        /// <summary>
        /// Set buildability/walkability for the footprint of a unit.
        /// Mobile units keep cells walkable when placed. Building top rows are
        /// marked as passage cells (walkable but not buildable).
        /// </summary>
        public void SetAreaBuildability(UnitType unitType, Position anchor, bool isBuildable)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            bool canMove = GameConstants.CAN_MOVE[unitType];

            for (int i = 0; i < size.X; i++)
            {
                for (int j = 0; j < size.Y; j++)
                {
                    var cell = new Position(anchor.X + i, anchor.Y - j);
                    if (IsPositionValid(cell))
                    {
                        buildable[cell.X, cell.Y] = isBuildable;

                        bool isTopRow = j == 0 && size.Y > 1;
                        if (isBuildable || (!canMove && !isTopRow))
                            walkable[cell.X, cell.Y] = isBuildable;

                        passage[cell.X, cell.Y] = !canMove && isTopRow && !isBuildable;
                    }
                }
            }
        }

        /// <summary>Mark a single cell as unbuildable/unwalkable (terrain wall).</summary>
        public void SetCellBlocked(Position p)
        {
            if (IsPositionValid(p))
            {
                buildable[p.X, p.Y] = false;
                walkable[p.X, p.Y] = false;
            }
        }

        /// <summary>Mark a single cell as unbuildable/unwalkable (terrain wall).</summary>
        public void SetCellBlocked(int x, int y)
        {
            if (IsPositionValid(x, y))
            {
                buildable[x, y] = false;
                walkable[x, y] = false;
            }
        }

        #endregion

        #region Neighbor Queries

        /// <summary>
        /// Get all grid positions in the ring around a unit's footprint.
        /// </summary>
        public List<Position> GetPositionsNearUnit(UnitType unitType, Position anchor)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            var positions = new List<Position>();

            for (int i = anchor.X - 1; i <= anchor.X + size.X; i++)
            {
                var top = new Position(i, anchor.Y + 1);
                if (IsPositionValid(top)) positions.Add(top);

                var bottom = new Position(i, anchor.Y - size.Y);
                if (IsPositionValid(bottom)) positions.Add(bottom);
            }

            for (int j = anchor.Y - size.Y + 1; j <= anchor.Y; j++)
            {
                var left = new Position(anchor.X - 1, j);
                if (IsPositionValid(left)) positions.Add(left);

                var right = new Position(anchor.X + size.X, j);
                if (IsPositionValid(right)) positions.Add(right);
            }

            return positions;
        }

        /// <summary>
        /// Get buildable positions near a unit, plus any walkable passage cells
        /// (building top row) as valid approach positions.
        /// </summary>
        public List<Position> GetBuildablePositionsNearUnit(UnitType unitType, Position anchor)
        {
            var result = new List<Position>();
            foreach (var p in GetPositionsNearUnit(unitType, anchor))
            {
                if (buildable[p.X, p.Y])
                    result.Add(p);
            }

            var size = GameConstants.UNIT_SIZE[unitType];
            if (!GameConstants.CAN_MOVE[unitType] && size.Y > 1)
            {
                for (int i = 0; i < size.X; i++)
                {
                    var pos = new Position(anchor.X + i, anchor.Y);
                    if (IsPositionValid(pos) && passage[pos.X, pos.Y])
                        result.Add(pos);
                }
            }

            return result;
        }

        /// <summary>
        /// Check if a position is adjacent to a unit (in the neighbor ring or
        /// on a passage cell of the building's top row).
        /// </summary>
        public bool IsNeighborOfUnit(Position pos, UnitType unitType, Position unitAnchor)
        {
            foreach (var n in GetPositionsNearUnit(unitType, unitAnchor))
            {
                if (n == pos) return true;
            }

            var size = GameConstants.UNIT_SIZE[unitType];
            if (!GameConstants.CAN_MOVE[unitType] && size.Y > 1
                && pos.Y == unitAnchor.Y
                && pos.X >= unitAnchor.X && pos.X < unitAnchor.X + size.X)
                return true;

            return false;
        }

        /// <summary>
        /// Find all valid build positions on the map for a given unit type.
        /// </summary>
        public List<Position> FindProspectiveBuildPositions(UnitType unitType)
        {
            var result = new List<Position>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var pos = new Position(x, y);
                    if (IsBoundedAreaBuildable(unitType, pos))
                        result.Add(pos);
                }
            }
            return result;
        }

        #endregion

        #region Pathfinding

        /// <summary>
        /// Find shortest path using walkable grid.
        /// Returns empty list if no path exists. Excludes start, includes end.
        /// </summary>
        public List<Position> FindPath(Position start, Position end)
        {
            return Pathfinder.FindPath(start, end, Width, Height,
                (x, y) => walkable[x, y]);
        }

        /// <summary>
        /// Find shortest path. When avoidUnits=true, uses buildable grid
        /// (treats mobile-unit-occupied cells as impassable).
        /// </summary>
        public List<Position> FindPath(Position start, Position end, bool avoidUnits)
        {
            if (!avoidUnits)
                return FindPath(start, end);

            return Pathfinder.FindPath(start, end, Width, Height,
                (x, y) => buildable[x, y]);
        }

        /// <summary>
        /// Find a path from start to any walkable cell adjacent to the given unit.
        /// Neighbors are sorted by distance to the start position so the closest
        /// reachable cell is tried first. Returns the first successful path.
        /// </summary>
        public List<Position> FindPathToUnit(Position start, UnitType unitType, Position unitAnchor)
        {
            var neighbors = GetBuildablePositionsNearUnit(unitType, unitAnchor);

            // Sort by squared distance to start (closest first, deterministic)
            neighbors.Sort((a, b) =>
            {
                int dxA = a.X - start.X, dyA = a.Y - start.Y;
                int dxB = b.X - start.X, dyB = b.Y - start.Y;
                int distA = dxA * dxA + dyA * dyA;
                int distB = dxB * dxB + dyB * dyB;
                int cmp = distA.CompareTo(distB);
                if (cmp != 0) return cmp;
                // Deterministic tiebreak: Y descending then X ascending
                cmp = b.Y.CompareTo(a.Y);
                if (cmp != 0) return cmp;
                return a.X.CompareTo(b.X);
            });

            foreach (var neighbor in neighbors)
            {
                var path = FindPath(start, neighbor);
                if (path.Count > 0)
                    return path;
            }
            return new List<Position>();
        }

        #endregion
    }
}
