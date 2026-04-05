using System.Collections.Generic;

namespace AgentSDK
{
    /// <summary>
    /// Cell access level for the grid. Each cell is in exactly one state.
    ///   OPEN     — empty, can walk/build/stand
    ///   WALKABLE — can walk through but not build or stand (unit occupying, or building top row)
    ///   BLOCKED  — terrain or building body, impassable
    /// </summary>
    public enum CellState : byte
    {
        OPEN = 0,
        WALKABLE = 1,
        BLOCKED = 2,
    }

    /// <summary>
    /// Pure-C# grid for an RTS map. Each cell has a single CellState.
    ///
    /// Both the Unity game engine and the AgentTestHarness SimGame share this
    /// single implementation so grid state and queries are guaranteed identical.
    ///
    /// Coordinate system: (0,0) bottom-left; +X right, +Y up.
    /// Anchor = bottom-left corner. Building footprints extend RIGHT and UP:
    ///   cells = (anchor.X + i, anchor.Y + j) for i in [0..sizeX), j in [0..sizeY).
    /// The top row (j == sizeY-1) of multi-row buildings is the passage row (walkable).
    /// </summary>
    public class GameGrid
    {
        private readonly CellState[,] cells;
        private readonly bool[,] isPassage; // true for building top-row cells (permanent while building exists)
        private readonly int[,] occupantCount; // number of mobile units on each cell

        public int Width { get; }
        public int Height { get; }
        public Position Size => new Position(Width, Height);

        public GameGrid(int width, int height)
        {
            Width = width;
            Height = height;
            cells = new CellState[width, height];
            isPassage = new bool[width, height];
            occupantCount = new int[width, height];
            // All cells start OPEN, no passages, no occupants
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

        /// <summary>Get the state of a cell. Out-of-bounds returns BLOCKED.</summary>
        public CellState GetCell(Position p)
        {
            return IsPositionValid(p) ? cells[p.X, p.Y] : CellState.BLOCKED;
        }

        /// <summary>Get the state of a cell. Out-of-bounds returns BLOCKED.</summary>
        public CellState GetCell(int x, int y)
        {
            return IsPositionValid(x, y) ? cells[x, y] : CellState.BLOCKED;
        }

        /// <summary>Can a building be placed on this cell? Only OPEN cells.</summary>
        public bool IsPositionBuildable(Position p)
        {
            return IsPositionValid(p) && cells[p.X, p.Y] == CellState.OPEN;
        }

        /// <summary>Can a unit path through this cell? OPEN or WALKABLE cells.</summary>
        public bool IsPositionWalkable(Position p)
        {
            return IsPositionValid(p) && cells[p.X, p.Y] != CellState.BLOCKED;
        }

        /// <summary>Can a unit stop on this cell? Only OPEN cells.</summary>
        public bool IsPositionStandable(Position p)
        {
            return IsPositionValid(p) && cells[p.X, p.Y] == CellState.OPEN;
        }

        /// <summary>Is this a WALKABLE cell? (building passage or unit-occupied).</summary>
        public bool IsPositionPassage(Position p)
        {
            return IsPositionValid(p) && cells[p.X, p.Y] == CellState.WALKABLE;
        }

        /// <summary>Is this specifically a building passage cell (top row)?</summary>
        public bool IsPassageCell(Position p)
        {
            return IsPositionValid(p) && isPassage[p.X, p.Y];
        }

        /// <summary>
        /// Check if the full footprint for a unit type is buildable at the given anchor.
        /// Anchor is the bottom-left corner; footprint extends right (+X) and up (+Y).
        /// </summary>
        public bool IsAreaBuildable(UnitType unitType, Position anchor)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            for (int i = 0; i < size.X; i++)
            {
                for (int j = 0; j < size.Y; j++)
                {
                    var cell = new Position(anchor.X + i, anchor.Y + j);
                    if (!IsPositionBuildable(cell))
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
                    var cell = new Position(anchor.X + i, anchor.Y + j);
                    if (cell == exclude) continue;
                    if (!IsPositionBuildable(cell))
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
                    var cell = new Position(anchor.X + i, anchor.Y + j);
                    if (!IsPositionBuildable(cell))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Check if the full footprint is buildable, excluding a set of positions
        /// (e.g., the agent's own pawns which can be moved out of the way).
        /// </summary>
        public bool IsAreaBuildable(UnitType unitType, Position anchor, HashSet<Position> excludePositions)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            for (int i = 0; i < size.X; i++)
            {
                for (int j = 0; j < size.Y; j++)
                {
                    var cell = new Position(anchor.X + i, anchor.Y + j);
                    if (excludePositions != null && excludePositions.Contains(cell)) continue;
                    if (!IsPositionBuildable(cell))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Check if the unit footprint plus a 1-cell border is all buildable,
        /// excluding a set of positions.
        /// </summary>
        public bool IsBoundedAreaBuildable(UnitType unitType, Position anchor, HashSet<Position> excludePositions)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            for (int i = -1; i <= size.X; i++)
            {
                for (int j = -1; j <= size.Y; j++)
                {
                    var cell = new Position(anchor.X + i, anchor.Y + j);
                    if (excludePositions != null && excludePositions.Contains(cell)) continue;
                    if (!IsPositionBuildable(cell))
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region Cell Modification

        /// <summary>Set a single cell's state.</summary>
        public void SetCell(Position p, CellState state)
        {
            if (IsPositionValid(p))
                cells[p.X, p.Y] = state;
        }

        /// <summary>Set a single cell's state.</summary>
        public void SetCell(int x, int y, CellState state)
        {
            if (IsPositionValid(x, y))
                cells[x, y] = state;
        }

        /// <summary>Mark a single cell as BLOCKED (terrain wall).</summary>
        public void SetCellBlocked(Position p)
        {
            SetCell(p, CellState.BLOCKED);
        }

        /// <summary>Mark a single cell as BLOCKED (terrain wall).</summary>
        public void SetCellBlocked(int x, int y)
        {
            SetCell(x, y, CellState.BLOCKED);
        }

        /// <summary>
        /// Update grid state for a unit's footprint.
        /// Anchor is bottom-left; footprint extends right (+X) and up (+Y).
        /// When placing (occupy=true): building body→BLOCKED, building top row→WALKABLE, mobile unit→WALKABLE.
        /// When removing (occupy=false): all cells→OPEN.
        /// </summary>
        public void SetUnitFootprint(UnitType unitType, Position anchor, bool occupy)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            bool canMove = GameConstants.CAN_MOVE[unitType];

            for (int i = 0; i < size.X; i++)
            {
                for (int j = 0; j < size.Y; j++)
                {
                    var cell = new Position(anchor.X + i, anchor.Y + j);
                    if (!IsPositionValid(cell)) continue;

                    if (!occupy)
                    {
                        cells[cell.X, cell.Y] = CellState.OPEN;
                        if (!canMove)
                            isPassage[cell.X, cell.Y] = false;
                    }
                    else if (canMove)
                    {
                        // Mobile unit: WALKABLE (others can walk through, can't stand/build)
                        cells[cell.X, cell.Y] = CellState.WALKABLE;
                    }
                    else
                    {
                        // Building: top row (highest Y) = WALKABLE (passage), body = BLOCKED
                        bool topRow = j == size.Y - 1 && size.Y > 1;
                        cells[cell.X, cell.Y] = topRow ? CellState.WALKABLE : CellState.BLOCKED;
                        isPassage[cell.X, cell.Y] = topRow;
                    }
                }
            }
        }

        #endregion

        #region Neighbor Queries

        /// <summary>
        /// Get all grid positions in the ring around a unit's footprint.
        /// Anchor is bottom-left; footprint occupies [anchor.X..anchor.X+sizeX-1, anchor.Y..anchor.Y+sizeY-1].
        /// </summary>
        public List<Position> GetPositionsNearUnit(UnitType unitType, Position anchor)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            var positions = new List<Position>();

            // Top and bottom rows of the ring
            for (int i = anchor.X - 1; i <= anchor.X + size.X; i++)
            {
                var top = new Position(i, anchor.Y + size.Y);
                if (IsPositionValid(top)) positions.Add(top);

                var bottom = new Position(i, anchor.Y - 1);
                if (IsPositionValid(bottom)) positions.Add(bottom);
            }

            // Left and right columns of the ring (excluding corners already added)
            for (int j = anchor.Y; j <= anchor.Y + size.Y - 1; j++)
            {
                var left = new Position(anchor.X - 1, j);
                if (IsPositionValid(left)) positions.Add(left);

                var right = new Position(anchor.X + size.X, j);
                if (IsPositionValid(right)) positions.Add(right);
            }

            return positions;
        }

        /// <summary>
        /// Find the nearest OPEN cell to a position, searching in expanding rings
        /// until one is found or the map edge is reached. Returns the position itself
        /// if it's already OPEN, or null if no open cell exists on the map.
        /// </summary>
        public Position? FindNearestOpenCell(Position center, int maxRadius = 0)
        {
            if (maxRadius <= 0) maxRadius = System.Math.Max(Width, Height);

            if (IsPositionValid(center) && cells[center.X, center.Y] == CellState.OPEN)
                return center;

            for (int r = 1; r <= maxRadius; r++)
            {
                Position? best = null;
                float bestDist = float.MaxValue;

                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        // Ring only — skip interior
                        if (System.Math.Abs(dx) != r && System.Math.Abs(dy) != r) continue;

                        var p = new Position(center.X + dx, center.Y + dy);
                        if (!IsPositionValid(p)) continue;
                        if (cells[p.X, p.Y] != CellState.OPEN) continue;

                        float dist = Position.Distance(center, p);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = p;
                        }
                    }
                }

                if (best.HasValue) return best;
            }
            return null;
        }

        /// <summary>
        /// Get OPEN positions near a unit where a mobile unit can stand.
        /// Used for spawn positions, pathfinding targets, and adjacency checks.
        /// Only returns cells in the neighbor ring (not passage cells inside the footprint).
        /// </summary>
        public List<Position> GetBuildablePositionsNearUnit(UnitType unitType, Position anchor)
        {
            var result = new List<Position>();
            foreach (var p in GetPositionsNearUnit(unitType, anchor))
            {
                if (cells[p.X, p.Y] == CellState.OPEN)
                    result.Add(p);
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

            // Also accept passage cells on the building's top row
            var size = GameConstants.UNIT_SIZE[unitType];
            if (!GameConstants.CAN_MOVE[unitType] && size.Y > 1
                && pos.Y == unitAnchor.Y + size.Y - 1
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
        /// Find shortest path using walkable grid (OPEN or WALKABLE cells).
        /// Returns empty list if no path exists. Excludes start, includes end.
        /// </summary>
        public List<Position> FindPath(Position start, Position end)
        {
            return Pathfinder.FindPath(start, end, Width, Height,
                (x, y) => cells[x, y] != CellState.BLOCKED);
        }

        /// <summary>
        /// Find shortest path. When avoidUnits=true, only uses OPEN cells
        /// (treats WALKABLE cells as impassable to avoid mobile units).
        /// </summary>
        public List<Position> FindPath(Position start, Position end, bool avoidUnits)
        {
            if (!avoidUnits)
                return FindPath(start, end);

            return Pathfinder.FindPath(start, end, Width, Height,
                (x, y) => cells[x, y] == CellState.OPEN);
        }

        /// <summary>
        /// Find a path from start to any walkable cell adjacent to the given unit.
        /// Prefers OPEN cells over WALKABLE (occupied) cells, then sorts by distance
        /// to the start position. Uses start position hash for tiebreaking so different
        /// units approaching the same building naturally spread to different cells.
        /// Returns the first successful path.
        /// </summary>
        public List<Position> FindPathToUnit(Position start, UnitType unitType, Position unitAnchor)
        {
            // Get all neighbor cells around the target unit
            var allNeighbors = GetPositionsNearUnit(unitType, unitAnchor);

            // Separate into OPEN (preferred) and WALKABLE (fallback) lists
            var openCells = new List<Position>();
            var walkableCells = new List<Position>();
            foreach (var p in allNeighbors)
            {
                if (cells[p.X, p.Y] == CellState.OPEN)
                    openCells.Add(p);
                else if (cells[p.X, p.Y] == CellState.WALKABLE)
                    walkableCells.Add(p);
            }

            // Sort each list by distance from the moving unit (closest first),
            // with position-based tiebreaking so different units prefer different cells
            int tiebreakSeed = start.X * 397 ^ start.Y;
            System.Comparison<Position> sortByDistance = (a, b) =>
            {
                int dxA = a.X - start.X, dyA = a.Y - start.Y;
                int dxB = b.X - start.X, dyB = b.Y - start.Y;
                int distA = dxA * dxA + dyA * dyA;
                int distB = dxB * dxB + dyB * dyB;
                int cmp = distA.CompareTo(distB);
                if (cmp != 0) return cmp;
                int hashA = (a.X * 31 + a.Y) ^ tiebreakSeed;
                int hashB = (b.X * 31 + b.Y) ^ tiebreakSeed;
                return hashA.CompareTo(hashB);
            };
            openCells.Sort(sortByDistance);
            walkableCells.Sort(sortByDistance);

            // Try OPEN cells first (no occupant — unit can stop here cleanly)
            foreach (var cell in openCells)
            {
                var path = FindPath(start, cell);
                if (path.Count > 0)
                    return path;
            }

            // Fallback: WALKABLE cells (occupied — may stack, but better than no path)
            foreach (var cell in walkableCells)
            {
                var path = FindPath(start, cell);
                if (path.Count > 0)
                    return path;
            }

            return new List<Position>();
        }

        #endregion

        #region Legacy Compatibility

        /// <summary>Alias for SetUnitFootprint. Matches old API used by callers.</summary>
        public void SetAreaBuildability(UnitType unitType, Position anchor, bool isBuildable)
        {
            SetUnitFootprint(unitType, anchor, !isBuildable);
        }

        /// <summary>
        /// Mark a mobile unit as occupying (occupy=true) or leaving (occupy=false) a cell.
        /// Uses reference counting so a cell stays WALKABLE until ALL occupants leave.
        /// On last leave, restores to WALKABLE if the cell is a building passage, otherwise OPEN.
        /// </summary>
        public void SetCellOccupied(Position p, bool occupy)
        {
            if (!IsPositionValid(p)) return;
            if (occupy)
            {
                occupantCount[p.X, p.Y]++;
                cells[p.X, p.Y] = CellState.WALKABLE;
            }
            else
            {
                occupantCount[p.X, p.Y] = System.Math.Max(0, occupantCount[p.X, p.Y] - 1);
                if (occupantCount[p.X, p.Y] == 0)
                    cells[p.X, p.Y] = isPassage[p.X, p.Y] ? CellState.WALKABLE : CellState.OPEN;
            }
        }

        /// <summary>Check how many mobile units occupy a cell.</summary>
        public int GetOccupantCount(Position p)
        {
            return IsPositionValid(p) ? occupantCount[p.X, p.Y] : 0;
        }

        /// <summary>Legacy alias for SetCellOccupied.</summary>
        public void SetCellStandable(Position p, bool isStandable)
        {
            SetCellOccupied(p, !isStandable);
        }

        #endregion
    }
}
