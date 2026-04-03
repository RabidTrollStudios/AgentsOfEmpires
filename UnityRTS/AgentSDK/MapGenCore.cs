using System;
using System.Collections.Generic;
using System.Linq;

namespace AgentSDK
{
    /// <summary>Symmetry mode for map generation.</summary>
    public enum SymmetryType
    {
        /// <summary>No symmetry enforcement.</summary>
        NONE,
        /// <summary>Point reflection through the map center (180° rotation).</summary>
        MIRROR,
        /// <summary>90° rotational symmetry (requires square maps, 4 players).</summary>
        ROTATIONAL
    }

    /// <summary>Predefined map layout templates.</summary>
    public enum MapTemplate
    {
        /// <summary>Low obstacle density with small scattered tree groves.</summary>
        OPEN_FIELD,
        /// <summary>Cave-like terrain generated via cellular automata.</summary>
        MAZE,
        /// <summary>Medium-high density with large organic tree groves.</summary>
        FOREST
    }

    /// <summary>Configuration parameters for procedural map generation.</summary>
    public class MapGenConfig
    {
        /// <summary>Random seed for deterministic generation.</summary>
        public int Seed { get; set; } = 42;

        /// <summary>Map width in cells.</summary>
        public int Width { get; set; } = 30;

        /// <summary>Map height in cells.</summary>
        public int Height { get; set; } = 30;

        /// <summary>Number of players (currently only 2 supported).</summary>
        public int PlayerCount { get; set; } = 2;

        /// <summary>
        /// Target fraction of cells that are obstacles (0.0–1.0).
        /// Actual density may vary due to template algorithms and exclusion zones.
        /// </summary>
        public float ObstacleDensity { get; set; } = 0.15f;

        /// <summary>Number of gold mines per player.</summary>
        public int MinesPerPlayer { get; set; } = 1;

        /// <summary>Symmetry enforcement mode.</summary>
        public SymmetryType Symmetry { get; set; } = SymmetryType.MIRROR;

        /// <summary>Map layout template.</summary>
        public MapTemplate Template { get; set; } = MapTemplate.OPEN_FIELD;
    }

    /// <summary>A cluster of blocked cells sharing a single visual tree type.</summary>
    public class GroveData
    {
        /// <summary>Visual tree type identifier (1–4, matching Tree1–Tree4 sprites).</summary>
        public int TreeType { get; }

        /// <summary>Grid positions belonging to this grove.</summary>
        public List<Position> Cells { get; }

        public GroveData(int treeType, List<Position> cells)
        {
            TreeType = treeType;
            Cells = cells;
        }
    }

    /// <summary>Output of map generation: blocked cells, spawn/mine positions, and grove data.</summary>
    public class MapGenResult
    {
        /// <summary>Map width in cells.</summary>
        public int Width { get; }

        /// <summary>Map height in cells.</summary>
        public int Height { get; }

        /// <summary>All blocked cell positions on the map.</summary>
        public HashSet<Position> BlockedCells { get; }

        /// <summary>Starting pawn positions, one per player (indexed by player number).</summary>
        public Position[] SpawnPositions { get; }

        /// <summary>Mine positions (player 0's mines first, then player 1's, etc.).</summary>
        public Position[] MinePositions { get; }

        /// <summary>Tree groves for visual rendering (each grove has a single tree type).</summary>
        public List<GroveData> Groves { get; }

        internal MapGenResult(int width, int height, HashSet<Position> blocked,
            Position[] spawns, Position[] mines, List<GroveData> groves)
        {
            Width = width;
            Height = height;
            BlockedCells = blocked;
            SpawnPositions = spawns;
            MinePositions = mines;
            Groves = groves;
        }
    }

    /// <summary>
    /// Pure game-logic procedural map generator. No engine dependencies.
    /// Shared by Unity and SimGame to guarantee identical map layouts.
    ///
    /// Fairness guarantees (Mirror symmetry):
    /// - Obstacle layout is point-symmetric through the map center.
    /// - Mine distance from each player's spawn is identical.
    /// - All spawns are connected to each other, their mines, and the map center.
    /// </summary>
    public static class MapGenCore
    {
        private const int SpawnExclusionRadius = 4;
        private const int MineExclusionRadius = 3;
        private const int CenterExclusionRadius = 2;
        private const int MaxRetries = 50;

        /// <summary>
        /// Generate a procedural map with the given parameters.
        /// Retries on connectivity failure with advancing RNG state.
        /// </summary>
        public static MapGenResult Generate(MapGenConfig config)
        {
            if (config.PlayerCount != 2)
                throw new ArgumentException("Only 2 players supported.", nameof(config));
            if (config.Width < 15 || config.Height < 15)
                throw new ArgumentException("Map must be at least 15x15.", nameof(config));

            var rng = new Random(config.Seed);
            var spawns = ComputeSpawnPositions(config.Width, config.Height);
            var mines = ComputeMinePositions(spawns[0], config, rng);
            var exclusions = BuildExclusionZones(config.Width, config.Height, spawns, mines);

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var (blocked, groves) = GenerateObstacles(config, exclusions, rng);

                if (ValidateConnectivity(config.Width, config.Height, blocked, spawns, mines))
                    return new MapGenResult(config.Width, config.Height,
                        blocked, spawns, mines, groves);
            }

            // Fallback: empty map
            return new MapGenResult(config.Width, config.Height,
                new HashSet<Position>(), spawns, mines, new List<GroveData>());
        }

        #region Spawn & Mine Placement

        private static Position[] ComputeSpawnPositions(int w, int h)
        {
            int margin = Math.Max(3, w / 10);
            var s0 = new Position(margin, margin);
            return new[] { s0, MirrorPos(s0, w, h) };
        }

        private static Position[] ComputeMinePositions(Position spawn0, MapGenConfig config, Random rng)
        {
            var mines = new Position[config.PlayerCount * config.MinesPerPlayer];
            for (int m = 0; m < config.MinesPerPlayer; m++)
            {
                var mine0 = PickMineNear(spawn0, config.Width, config.Height, rng);
                mines[m * 2] = mine0;
                var mineSize = GameConstants.UNIT_SIZE[UnitType.MINE];
                mines[m * 2 + 1] = MirrorUnit(mine0, config.Width, config.Height, mineSize.X, mineSize.Y);
            }
            return mines;
        }

        private static Position PickMineNear(Position spawn, int w, int h, Random rng)
        {
            int maxDist = Math.Min(8, Math.Max(w, h) / 4);
            int minDist = Math.Min(5, maxDist);

            for (int i = 0; i < 100; i++)
            {
                int dx = rng.Next(minDist, maxDist + 1);
                int dy = rng.Next(minDist, maxDist + 1);
                int x = spawn.X + dx;
                int y = spawn.Y + dy;

                // Stay in player 0's quadrant (bottom-left) and in bounds
                if (x >= 2 && x < w / 2 && y >= 2 && y < h / 2)
                    return new Position(x, y);
            }

            // Fallback: deterministic position near spawn
            return new Position(
                Math.Min(spawn.X + 7, w / 2 - 2),
                Math.Min(spawn.Y + 7, h / 2 - 2));
        }

        #endregion

        #region Exclusion Zones

        private static HashSet<Position> BuildExclusionZones(int w, int h,
            Position[] spawns, Position[] mines)
        {
            var zones = new HashSet<Position>();
            foreach (var s in spawns) AddCircle(zones, s, SpawnExclusionRadius, w, h);
            foreach (var m in mines)  AddCircle(zones, m, MineExclusionRadius, w, h);
            AddCircle(zones, new Position(w / 2, h / 2), CenterExclusionRadius, w, h);
            return zones;
        }

        private static void AddCircle(HashSet<Position> set, Position center,
            int radius, int w, int h)
        {
            int r2 = radius * radius;
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int x = center.X + dx, y = center.Y + dy;
                    if (x >= 0 && x < w && y >= 0 && y < h)
                        set.Add(new Position(x, y));
                }
        }

        #endregion

        #region Template Dispatch

        private static (HashSet<Position>, List<GroveData>) GenerateObstacles(
            MapGenConfig config, HashSet<Position> exclusions, Random rng)
        {
            HashSet<Position> blocked;
            List<GroveData> groves;

            switch (config.Template)
            {
                case MapTemplate.OPEN_FIELD:
                    (blocked, groves) = GenerateGroveBased(config.Width, config.Height,
                        config.ObstacleDensity, exclusions, rng,
                        minGrove: 2, maxGrove: 5, growthProb: 0.80f, growthFalloff: 0.10f,
                        groveSpacing: 0);
                    break;
                case MapTemplate.FOREST:
                    (blocked, groves) = GenerateGroveBased(config.Width, config.Height,
                        config.ObstacleDensity, exclusions, rng,
                        minGrove: 15, maxGrove: 40, growthProb: 0.90f, growthFalloff: 0.015f,
                        groveSpacing: 3);
                    break;
                case MapTemplate.MAZE:
                    return GenerateMaze(config.Width, config.Height, config.ObstacleDensity,
                        config.Symmetry, exclusions, rng);
                default:
                    return (new HashSet<Position>(), new List<GroveData>());
            }

            if (config.Symmetry == SymmetryType.MIRROR)
                ApplyMirrorSymmetry(config.Width, config.Height, ref blocked, ref groves);

            return (blocked, groves);
        }

        #endregion

        #region Grove-Based Generation (OpenField / Forest)

        private static (HashSet<Position>, List<GroveData>) GenerateGroveBased(
            int w, int h, float density, HashSet<Position> exclusions, Random rng,
            int minGrove, int maxGrove, float growthProb, float growthFalloff,
            int groveSpacing)
        {
            var blocked = new HashSet<Position>();
            var groves = new List<GroveData>();
            var buffer = new HashSet<Position>();
            int targetCells = (int)(w * h * density * 0.5f);
            int cellsPlaced = 0;
            int maxAttempts = Math.Max(targetCells * 4, 50);

            for (int a = 0; a < maxAttempts && cellsPlaced < targetCells; a++)
            {
                int groveSize = rng.Next(minGrove, maxGrove + 1);
                var seed = RandomInPrimaryHalf(w, h, exclusions, blocked, buffer, rng);
                if (seed == null) continue;

                int treeType = rng.Next(1, 5);
                var cells = GrowGrove(seed.Value, groveSize, w, h, exclusions, blocked, buffer, rng,
                    growthProb, growthFalloff);
                if (cells.Count == 0) continue;

                groves.Add(new GroveData(treeType, cells));
                foreach (var c in cells)
                {
                    blocked.Add(c);
                    if (groveSpacing > 0)
                        AddSquareBuffer(buffer, c, groveSpacing, w, h);
                }
                cellsPlaced += cells.Count;
            }

            return (blocked, groves);
        }

        private static void AddSquareBuffer(HashSet<Position> buffer, Position center,
            int radius, int w, int h)
        {
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = center.X + dx, y = center.Y + dy;
                    if (x >= 0 && x < w && y >= 0 && y < h)
                        buffer.Add(new Position(x, y));
                }
        }

        private static List<Position> GrowGrove(Position seed, int targetSize,
            int w, int h, HashSet<Position> exclusions,
            HashSet<Position> existing, HashSet<Position> buffer, Random rng,
            float baseProb, float falloff)
        {
            var grove = new List<Position> { seed };
            var frontier = new List<Position> { seed };
            var visited = new HashSet<Position> { seed };
            int halfH = h / 2;

            while (grove.Count < targetSize && frontier.Count > 0)
            {
                int idx = rng.Next(frontier.Count);
                var current = frontier[idx];
                frontier.RemoveAt(idx);

                var neighbors = Get8Neighbors(current);
                Shuffle(neighbors, rng);

                foreach (var n in neighbors)
                {
                    if (grove.Count >= targetSize) break;
                    if (visited.Contains(n)) continue;
                    visited.Add(n);

                    if (n.X < 0 || n.X >= w || n.Y < 0 || n.Y >= halfH) continue;
                    if (exclusions.Contains(n) || existing.Contains(n)) continue;
                    if (buffer.Contains(n)) continue;

                    float dx = n.X - seed.X, dy = n.Y - seed.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    float prob = baseProb - dist * falloff;
                    if (prob > 0f && rng.NextDouble() < prob)
                    {
                        grove.Add(n);
                        frontier.Add(n);
                    }
                }
            }
            return grove;
        }

        private static Position? RandomInPrimaryHalf(int w, int h,
            HashSet<Position> exclusions, HashSet<Position> existing,
            HashSet<Position> buffer, Random rng)
        {
            int halfH = h / 2;
            for (int i = 0; i < 100; i++)
            {
                int x = rng.Next(0, w), y = rng.Next(0, halfH);
                var pos = new Position(x, y);
                if (!exclusions.Contains(pos) && !existing.Contains(pos) && !buffer.Contains(pos))
                    return pos;
            }
            return null;
        }

        #endregion

        #region Cellular Automata (Maze)

        private static (HashSet<Position>, List<GroveData>) GenerateMaze(
            int w, int h, float density, SymmetryType symmetry,
            HashSet<Position> exclusions, Random rng)
        {
            float initialFill = Math.Max(0.20f, Math.Min(0.55f, density * 1.3f));

            var grid = new bool[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    grid[x, y] = !exclusions.Contains(new Position(x, y))
                                 && rng.NextDouble() < initialFill;

            // Smooth with 4-5 rule
            for (int pass = 0; pass < 5; pass++)
            {
                var next = new bool[w, h];
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        if (exclusions.Contains(new Position(x, y)))
                        { next[x, y] = false; continue; }

                        int walls = 0;
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || nx >= w || ny < 0 || ny >= h) walls++;
                                else if (grid[nx, ny]) walls++;
                            }
                        next[x, y] = walls >= 5;
                    }
                grid = next;
            }

            // Enforce mirror symmetry
            if (symmetry == SymmetryType.MIRROR)
            {
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h / 2; y++)
                        grid[w - 1 - x, h - 1 - y] = grid[x, y];

                foreach (var pos in exclusions)
                    if (pos.X >= 0 && pos.X < w && pos.Y >= 0 && pos.Y < h)
                        grid[pos.X, pos.Y] = false;
            }

            var blocked = new HashSet<Position>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (grid[x, y]) blocked.Add(new Position(x, y));

            return (blocked, FloodFillGroves(blocked, rng));
        }

        #endregion

        #region Symmetry

        private static void ApplyMirrorSymmetry(int w, int h,
            ref HashSet<Position> blocked, ref List<GroveData> groves)
        {
            var mirrored = new HashSet<Position>(blocked);
            var mirroredGroves = new List<GroveData>(groves);

            foreach (var pos in blocked) mirrored.Add(MirrorPos(pos, w, h));
            foreach (var grove in groves)
            {
                var mirrorCells = grove.Cells.Select(c => MirrorPos(c, w, h)).ToList();
                mirroredGroves.Add(new GroveData(grove.TreeType, mirrorCells));
            }

            blocked = mirrored;
            groves = mirroredGroves;
        }

        /// <summary>Point reflection for 1x1 cells: (x, y) -> (W-1-x, H-1-y).</summary>
        public static Position MirrorPos(Position pos, int w, int h)
            => new Position(w - 1 - pos.X, h - 1 - pos.Y);

        /// <summary>
        /// Mirror a unit anchor accounting for its footprint size.
        /// Bottom-left anchor: unit occupies [pos.X..pos.X+sizeX-1, pos.Y..pos.Y+sizeY-1].
        /// Mirrored anchor = (w - sizeX - pos.X, h - sizeY - pos.Y).
        /// </summary>
        public static Position MirrorUnit(Position pos, int w, int h, int sizeX, int sizeY)
            => new Position(w - sizeX - pos.X, h - sizeY - pos.Y);

        #endregion

        #region Connectivity Validation

        private static bool ValidateConnectivity(int w, int h,
            HashSet<Position> blocked, Position[] spawns, Position[] mines)
        {
            var reachable = new HashSet<Position>();
            var queue = new Queue<Position>();
            queue.Enqueue(spawns[0]);
            reachable.Add(spawns[0]);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var n = new Position(current.X + dx, current.Y + dy);
                        if (n.X >= 0 && n.X < w && n.Y >= 0 && n.Y < h
                            && !blocked.Contains(n) && reachable.Add(n))
                            queue.Enqueue(n);
                    }
            }

            foreach (var s in spawns) if (!reachable.Contains(s)) return false;
            foreach (var m in mines)  if (!reachable.Contains(m)) return false;
            if (!reachable.Contains(new Position(w / 2, h / 2))) return false;
            return true;
        }

        #endregion

        #region Helpers

        private static List<GroveData> FloodFillGroves(HashSet<Position> blocked, Random rng)
        {
            var groves = new List<GroveData>();
            var remaining = new HashSet<Position>(blocked);

            while (remaining.Count > 0)
            {
                var seed = remaining.First();
                var cells = new List<Position>();
                var queue = new Queue<Position>();
                queue.Enqueue(seed);
                remaining.Remove(seed);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    cells.Add(current);
                    foreach (var n in Get8Neighbors(current))
                        if (remaining.Remove(n)) queue.Enqueue(n);
                }
                groves.Add(new GroveData(rng.Next(1, 5), cells));
            }
            return groves;
        }

        private static List<Position> Get8Neighbors(Position pos)
        {
            return new List<Position>
            {
                new Position(pos.X + 1, pos.Y),     new Position(pos.X - 1, pos.Y),
                new Position(pos.X, pos.Y + 1),      new Position(pos.X, pos.Y - 1),
                new Position(pos.X + 1, pos.Y + 1),  new Position(pos.X - 1, pos.Y + 1),
                new Position(pos.X + 1, pos.Y - 1),  new Position(pos.X - 1, pos.Y - 1),
            };
        }

        private static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }

        #endregion
    }
}
