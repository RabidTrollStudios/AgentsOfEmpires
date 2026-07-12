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
        /// Check if the full footprint is buildable, excluding a set of positions
        /// (e.g. the agent's own pawns, which it can move out of the way before
        /// building). Both engines must exclude the SAME set for parity.
        /// </summary>
        public bool IsAreaBuildable(UnitType unitType, Position anchor, ISet<Position> exclude)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            for (int i = 0; i < size.X; i++)
            {
                for (int j = 0; j < size.Y; j++)
                {
                    var cell = new Position(anchor.X + i, anchor.Y + j);
                    if (exclude != null && exclude.Contains(cell)) continue;
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
        /// Bounded-buildable check excluding a set of positions (the agent's own
        /// pawns). Mirrors <see cref="IsAreaBuildable(UnitType, Position, ISet{Position})"/>.
        /// </summary>
        public bool IsBoundedAreaBuildable(UnitType unitType, Position anchor, ISet<Position> exclude)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            for (int i = -1; i <= size.X; i++)
            {
                for (int j = -1; j <= size.Y; j++)
                {
                    var cell = new Position(anchor.X + i, anchor.Y + j);
                    if (exclude != null && exclude.Contains(cell)) continue;
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

        /// <summary>
        /// Prospective build positions, excluding a set of cells (the agent's own
        /// pawns) from the buildability test — the agent can move them before
        /// building. Both engines must scan in the SAME (x outer, y inner) order and
        /// exclude the SAME set for parity. Mirrors Unity's
        /// GameStateAdapter.FindProspectiveBuildPositions.
        /// </summary>
        public List<Position> FindProspectiveBuildPositions(UnitType unitType, ISet<Position> exclude)
        {
            var result = new List<Position>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var pos = new Position(x, y);
                    if (IsBoundedAreaBuildable(unitType, pos, exclude))
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
            var neighbors = GetBuildablePositionsNearUnit(unitType, unitAnchor);

            // Tiebreak seed based on start position so different pawns prefer different cells
            int tiebreakSeed = start.X * 397 ^ start.Y;

            // Sort by distance to start, with position-based tiebreaking so
            // different pawns approaching the same building spread to different cells
            neighbors.Sort((a, b) =>
            {
                int dxA = a.X - start.X, dyA = a.Y - start.Y;
                int dxB = b.X - start.X, dyB = b.Y - start.Y;
                int distA = dxA * dxA + dyA * dyA;
                int distB = dxB * dxB + dyB * dyB;
                int cmp = distA.CompareTo(distB);
                if (cmp != 0) return cmp;
                // Deterministic tiebreak using start-position hash
                int hashA = (a.X * 31 + a.Y) ^ tiebreakSeed;
                int hashB = (b.X * 31 + b.Y) ^ tiebreakSeed;
                return hashA.CompareTo(hashB);
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

        #region Parity digests

        // Order-stable FNV-1a digests of the grid's DERIVED internal state, hashed identically
        // for both engines because both call THIS shared method on the SAME shared grid type.
        // These catch a divergence in engine internals (occupancy, walkability, building
        // passages) that would NOT yet show up in any per-unit field — e.g. a footprint update
        // applied in a different order, or a building passage cell set on one engine but not the
        // other. FNV-1a (not string.GetHashCode) is used deliberately: it is deterministic across
        // runtimes and process runs, which the randomized string hash is not.

        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private static ulong FnvStep(ulong h, int value)
        {
            unchecked
            {
                // Fold the 32-bit value byte-by-byte, low-to-high, for a stable ordering.
                h = (h ^ (byte)(value)) * FnvPrime;
                h = (h ^ (byte)(value >> 8)) * FnvPrime;
                h = (h ^ (byte)(value >> 16)) * FnvPrime;
                h = (h ^ (byte)(value >> 24)) * FnvPrime;
                return h;
            }
        }

        /// <summary>
        /// Digest of the walkability map: every cell's <see cref="CellState"/> (OPEN/WALKABLE/
        /// BLOCKED) plus whether it is a building passage. Static terrain contributes a constant
        /// baseline; the digest MOVES when a building goes up or dies (footprint → BLOCKED, top
        /// row → WALKABLE passage). Iteration is column-major (x outer, y inner) — fixed order so
        /// the value is reproducible.
        /// </summary>
        public ulong ComputeWalkabilityDigest() => ComputeWalkabilityDigest(Width, Height);

        /// <summary>
        /// Walkability digest over only the [0,regionW) x [0,regionH) sub-region. Used for
        /// cross-engine parity: the Unity engine's grid is larger than the playable area (it
        /// includes a wide water/border margin outside the generated map), while the headless sim
        /// builds only the playable grid. Hashing the SHARED playable region — the generated map
        /// dimensions, at the common (0,0) origin — makes the two digests comparable without
        /// forcing the sim to replicate Unity's decorative border. Out-of-bounds cells read as
        /// BLOCKED (via GetCell), so passing a region larger than the grid is safe.
        /// </summary>
        public ulong ComputeWalkabilityDigest(int regionW, int regionH)
        {
            ulong h = FnvOffset;
            for (int x = 0; x < regionW; x++)
                for (int y = 0; y < regionH; y++)
                {
                    h = FnvStep(h, (int)GetCell(x, y));
                    h = FnvStep(h, (IsPositionValid(x, y) && isPassage[x, y]) ? 1 : 0);
                }
            return h;
        }

        /// <summary>
        /// Digest of the occupancy map: the mobile-unit occupant count per cell. Catches a
        /// divergence in which cells the two engines believe are occupied even when every unit's
        /// reported GridPosition already matches (an occupancy-accounting skew). Same fixed
        /// column-major iteration order as the walkability digest.
        /// </summary>
        public ulong ComputeOccupancyDigest() => ComputeOccupancyDigest(Width, Height);

        /// <summary>
        /// Occupancy digest over only the [0,regionW) x [0,regionH) sub-region. See
        /// <see cref="ComputeWalkabilityDigest(int,int)"/> for why the parity path restricts to
        /// the shared playable region. Out-of-bounds cells contribute a 0 occupant count.
        /// </summary>
        public ulong ComputeOccupancyDigest(int regionW, int regionH)
        {
            ulong h = FnvOffset;
            for (int x = 0; x < regionW; x++)
                for (int y = 0; y < regionH; y++)
                    h = FnvStep(h, IsPositionValid(x, y) ? occupantCount[x, y] : 0);
            return h;
        }

        #endregion
    }
}
