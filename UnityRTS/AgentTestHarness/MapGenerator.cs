using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>Symmetry mode for map generation.</summary>
    public enum SymmetryType
    {
        /// <summary>No symmetry enforcement.</summary>
        None,
        /// <summary>Point reflection through the map center (180° rotation).</summary>
        Mirror,
        /// <summary>90° rotational symmetry (requires square maps, 4 players).</summary>
        Rotational
    }

    /// <summary>Predefined map layout templates.</summary>
    public enum MapTemplate
    {
        /// <summary>Low obstacle density with small scattered tree groves.</summary>
        OpenField,
        /// <summary>Cave-like terrain generated via cellular automata.</summary>
        Maze,
        /// <summary>Medium-high density with large organic tree groves.</summary>
        Forest
    }

    /// <summary>
    /// Configuration parameters for procedural map generation.
    /// </summary>
    public class MapGeneratorConfig
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
        public SymmetryType Symmetry { get; set; } = SymmetryType.Mirror;

        /// <summary>Map layout template.</summary>
        public MapTemplate Template { get; set; } = MapTemplate.OpenField;
    }

    /// <summary>
    /// A cluster of blocked cells sharing a single visual tree type.
    /// </summary>
    public class Grove
    {
        /// <summary>Visual tree type identifier (1–4, matching Tree1–Tree4 sprites).</summary>
        public int TreeType { get; }

        /// <summary>Grid positions belonging to this grove.</summary>
        public List<Position> Cells { get; }

        public Grove(int treeType, List<Position> cells)
        {
            TreeType = treeType;
            Cells = cells;
        }
    }

    /// <summary>
    /// Output of map generation: the grid, spawn/mine positions, and obstacle metadata.
    /// </summary>
    public class MapGeneratorResult
    {
        /// <summary>Seed used to generate this map.</summary>
        public int Seed { get; }

        /// <summary>Map width in cells.</summary>
        public int Width { get; }

        /// <summary>Map height in cells.</summary>
        public int Height { get; }

        /// <summary>The generated grid with blocked cells applied.</summary>
        public SimMap Map { get; }

        /// <summary>Starting pawn positions, one per player (indexed by player number).</summary>
        public Position[] SpawnPositions { get; }

        /// <summary>Mine positions (player 0's mines first, then player 1's, etc.).</summary>
        public Position[] MinePositions { get; }

        /// <summary>Tree groves for visual rendering (each grove has a single tree type).</summary>
        public List<Grove> Groves { get; }

        /// <summary>All blocked cell positions on the map.</summary>
        public HashSet<Position> BlockedCells { get; }

        internal MapGeneratorResult(int seed, int width, int height, SimMap map,
            Position[] spawns, Position[] mines, List<Grove> groves, HashSet<Position> blocked)
        {
            Seed = seed;
            Width = width;
            Height = height;
            Map = map;
            SpawnPositions = spawns;
            MinePositions = mines;
            Groves = groves;
            BlockedCells = blocked;
        }
    }

    /// <summary>
    /// Procedural map generator with fairness constraints for AI agent competition.
    /// Produces diverse, reproducible, and balanced maps.
    ///
    /// Fairness guarantees (Mirror symmetry):
    /// - Obstacle layout is point-symmetric through the map center.
    /// - Mine distance from each player's spawn is identical.
    /// - Shortest path lengths from spawn→mine and spawn→center are equal across players.
    /// - All spawns are connected to each other, their mines, and the map center.
    /// </summary>
    public class MapGenerator
    {
        // Hard exclusion: guarantees room for base placement near spawns/mines.
        // Kept small so trees can grow close to starting areas.
        private const int SpawnExclusionRadius = 4;
        private const int MineExclusionRadius = 3;
        private const int CenterExclusionRadius = 2;
        private const int MaxRetries = 50;

        /// <summary>
        /// Generate a map from the given configuration.
        /// Retries with advancing RNG state if connectivity validation fails.
        /// </summary>
        public MapGeneratorResult Generate(MapGeneratorConfig config)
        {
            if (config.PlayerCount != 2)
                throw new ArgumentException("Only 2 players supported.", nameof(config));
            if (config.Width < 15 || config.Height < 15)
                throw new ArgumentException("Map must be at least 15x15.", nameof(config));

            var rng = new Random(config.Seed);
            var spawns = ComputeSpawnPositions(config);
            var mines = ComputeMinePositions(config, spawns, rng);
            var exclusions = BuildExclusionZones(config, spawns, mines);

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var (blocked, groves) = GenerateObstacles(config, exclusions, rng);
                var map = BuildMap(config, blocked);

                if (ValidateConnectivity(map, spawns, mines, config))
                    return new MapGeneratorResult(config.Seed, config.Width, config.Height,
                        map, spawns, mines, groves, blocked);
            }

            // Fallback: open map with no obstacles
            return new MapGeneratorResult(config.Seed, config.Width, config.Height,
                new SimMap(config.Width, config.Height), spawns, mines,
                new List<Grove>(), new HashSet<Position>());
        }

        #region Spawn & Mine Placement

        private Position[] ComputeSpawnPositions(MapGeneratorConfig config)
        {
            int margin = Math.Max(3, config.Width / 10);
            var spawn0 = new Position(margin, margin);
            return new[] { spawn0, MirrorPos(spawn0, config.Width, config.Height) };
        }

        private Position[] ComputeMinePositions(MapGeneratorConfig config,
            Position[] spawns, Random rng)
        {
            var mines = new Position[config.PlayerCount * config.MinesPerPlayer];
            for (int m = 0; m < config.MinesPerPlayer; m++)
            {
                var mine0 = PickMineNear(spawns[0], config, rng);
                mines[m * 2] = mine0;
                var mineSize = GameConstants.UNIT_SIZE[UnitType.MINE];
                mines[m * 2 + 1] = MirrorUnit(mine0, config.Width, config.Height, mineSize.X, mineSize.Y);
            }
            return mines;
        }

        private Position PickMineNear(Position spawn, MapGeneratorConfig config, Random rng)
        {
            int maxDist = Math.Min(8, Math.Max(config.Width, config.Height) / 4);
            int minDist = Math.Min(5, maxDist);

            for (int i = 0; i < 100; i++)
            {
                int dx = rng.Next(minDist, maxDist + 1);
                int dy = rng.Next(minDist, maxDist + 1);
                int x = spawn.X + dx;
                int y = spawn.Y + dy;

                // Stay in player 0's quadrant (bottom-left) and in bounds
                if (x >= 2 && x < config.Width / 2 && y >= 2 && y < config.Height / 2)
                    return new Position(x, y);
            }

            // Fallback: deterministic position near spawn
            return new Position(
                Math.Min(spawn.X + 7, config.Width / 2 - 2),
                Math.Min(spawn.Y + 7, config.Height / 2 - 2));
        }

        #endregion

        #region Exclusion Zones

        private HashSet<Position> BuildExclusionZones(MapGeneratorConfig config,
            Position[] spawns, Position[] mines)
        {
            var zones = new HashSet<Position>();

            foreach (var spawn in spawns)
                AddCircle(zones, spawn, SpawnExclusionRadius, config);
            foreach (var mine in mines)
                AddCircle(zones, mine, MineExclusionRadius, config);

            var center = new Position(config.Width / 2, config.Height / 2);
            AddCircle(zones, center, CenterExclusionRadius, config);

            return zones;
        }

        private static void AddCircle(HashSet<Position> set, Position center,
            int radius, MapGeneratorConfig config)
        {
            int r2 = radius * radius;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int x = center.X + dx, y = center.Y + dy;
                    if (x >= 0 && x < config.Width && y >= 0 && y < config.Height)
                        set.Add(new Position(x, y));
                }
            }
        }

        #endregion

        #region Template Generators

        /// <summary>
        /// Dispatch to the appropriate template generator, applying symmetry as needed.
        /// Maze handles symmetry internally; grove-based templates mirror after generation.
        /// </summary>
        private (HashSet<Position> blocked, List<Grove> groves) GenerateObstacles(
            MapGeneratorConfig config, HashSet<Position> exclusions, Random rng)
        {
            HashSet<Position> blocked;
            List<Grove> groves;

            switch (config.Template)
            {
                case MapTemplate.OpenField:
                    // Many small scattered groves — compact individually but spread across the map
                    (blocked, groves) = GenerateGroveBased(config, exclusions, rng,
                        minGrove: 2, maxGrove: 5, growthProb: 0.80f, growthFalloff: 0.10f,
                        groveSpacing: 0);
                    break;
                case MapTemplate.Forest:
                    // Fewer but much larger, denser groves → thick forests with clearings
                    (blocked, groves) = GenerateGroveBased(config, exclusions, rng,
                        minGrove: 15, maxGrove: 40, growthProb: 0.90f, growthFalloff: 0.015f,
                        groveSpacing: 3);
                    break;
                case MapTemplate.Maze:
                    // Maze applies symmetry internally via cellular automata
                    return GenerateMaze(config, exclusions, rng);
                default:
                    return (new HashSet<Position>(), new List<Grove>());
            }

            // Apply symmetry for grove-based templates
            if (config.Symmetry == SymmetryType.Mirror)
                ApplyMirrorSymmetry(config, ref blocked, ref groves);

            return (blocked, groves);
        }

        /// <summary>
        /// Grove-based generation for OpenField and Forest templates.
        /// Grows organic tree clusters in the primary half (y &lt; height/2),
        /// each grove using a single randomly-chosen tree type (1–4).
        /// </summary>
        /// <param name="growthProb">Base probability a neighbor joins the grove (0–1).</param>
        /// <param name="growthFalloff">Probability reduction per Euclidean distance from seed.</param>
        /// <param name="groveSpacing">Minimum cell gap between groves (0 = no spacing).</param>
        private (HashSet<Position>, List<Grove>) GenerateGroveBased(
            MapGeneratorConfig config, HashSet<Position> exclusions, Random rng,
            int minGrove, int maxGrove, float growthProb, float growthFalloff,
            int groveSpacing)
        {
            var blocked = new HashSet<Position>();
            var groves = new List<Grove>();
            var buffer = new HashSet<Position>();

            // Target for primary half — mirroring will roughly double it
            int targetCells = (int)(config.Width * config.Height * config.ObstacleDensity * 0.5f);
            int cellsPlaced = 0;
            int maxAttempts = Math.Max(targetCells * 4, 50);

            for (int attempt = 0; attempt < maxAttempts && cellsPlaced < targetCells; attempt++)
            {
                int groveSize = rng.Next(minGrove, maxGrove + 1);
                var seed = RandomInPrimaryHalf(config, exclusions, blocked, buffer, rng);
                if (seed == null) continue;

                int treeType = rng.Next(1, 5);
                var cells = GrowGrove(seed.Value, groveSize, config, exclusions, blocked, buffer, rng,
                    growthProb, growthFalloff);
                if (cells.Count == 0) continue;

                groves.Add(new Grove(treeType, cells));
                foreach (var c in cells)
                {
                    blocked.Add(c);
                    if (groveSpacing > 0)
                        AddSquareBuffer(buffer, c, groveSpacing, config.Width, config.Height);
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

        /// <summary>
        /// Cellular automata cave generation for the Maze template.
        /// 1. Random fill scaled by ObstacleDensity (clamped to 0.20–0.55).
        /// 2. Five smoothing passes with the 4-5 rule (cell is wall if ≥5 of 9 neighbors are walls).
        /// 3. Symmetry enforced by copying primary half (y &lt; h/2) to secondary half.
        /// 4. Contiguous blocked regions grouped into groves via flood fill.
        /// </summary>
        private (HashSet<Position>, List<Grove>) GenerateMaze(
            MapGeneratorConfig config, HashSet<Position> exclusions, Random rng)
        {
            int w = config.Width, h = config.Height;
            float initialFill = Math.Max(0.20f, Math.Min(0.55f, config.ObstacleDensity * 1.3f));

            // Step 1: random fill (full map)
            var grid = new bool[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    grid[x, y] = !exclusions.Contains(new Position(x, y))
                                 && rng.NextDouble() < initialFill;

            // Step 2: cellular automata smoothing
            for (int pass = 0; pass < 5; pass++)
            {
                var next = new bool[w, h];
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        if (exclusions.Contains(new Position(x, y)))
                        { next[x, y] = false; continue; }

                        int walls = 0;
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                                    walls++; // Out-of-bounds counts as wall
                                else if (grid[nx, ny])
                                    walls++;
                            }
                        next[x, y] = walls >= 5;
                    }
                }
                grid = next;
            }

            // Step 3: enforce mirror symmetry (primary half → secondary half)
            if (config.Symmetry == SymmetryType.Mirror)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h / 2; y++)
                    {
                        int mx = w - 1 - x, my = h - 1 - y;
                        grid[mx, my] = grid[x, y];
                    }
                }

                // Re-clear exclusion zones that may have been set by mirroring
                foreach (var pos in exclusions)
                    if (pos.X >= 0 && pos.X < w && pos.Y >= 0 && pos.Y < h)
                        grid[pos.X, pos.Y] = false;
            }

            // Step 4: collect blocked cells and group into groves
            var blocked = new HashSet<Position>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (grid[x, y])
                        blocked.Add(new Position(x, y));

            var groves = FloodFillGroves(blocked, rng);
            return (blocked, groves);
        }

        #endregion

        #region Grove Growth

        /// <summary>
        /// Grow an organic cluster from a seed position using BFS expansion
        /// with distance-based probability falloff. Produces natural-looking shapes.
        /// <paramref name="baseProb"/> and <paramref name="falloff"/> control the shape:
        /// high prob + low falloff → dense, round blobs (Forest);
        /// low prob + high falloff → small, sparse scatters (OpenField).
        /// </summary>
        private List<Position> GrowGrove(Position seed, int targetSize,
            MapGeneratorConfig config, HashSet<Position> exclusions,
            HashSet<Position> existing, HashSet<Position> buffer, Random rng,
            float baseProb, float falloff)
        {
            var grove = new List<Position> { seed };
            var frontier = new List<Position> { seed };
            var visited = new HashSet<Position> { seed };
            int halfH = config.Height / 2;

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

                    if (n.X < 0 || n.X >= config.Width || n.Y < 0 || n.Y >= halfH)
                        continue;
                    if (exclusions.Contains(n) || existing.Contains(n))
                        continue;
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

        /// <summary>Pick a random open position in the primary half (y &lt; height/2).</summary>
        private Position? RandomInPrimaryHalf(MapGeneratorConfig config,
            HashSet<Position> exclusions, HashSet<Position> existing,
            HashSet<Position> buffer, Random rng)
        {
            int halfH = config.Height / 2;
            for (int i = 0; i < 100; i++)
            {
                int x = rng.Next(0, config.Width);
                int y = rng.Next(0, halfH);
                var pos = new Position(x, y);
                if (!exclusions.Contains(pos) && !existing.Contains(pos) && !buffer.Contains(pos))
                    return pos;
            }
            return null;
        }

        #endregion

        #region Symmetry

        private void ApplyMirrorSymmetry(MapGeneratorConfig config,
            ref HashSet<Position> blocked, ref List<Grove> groves)
        {
            var mirrored = new HashSet<Position>(blocked);
            var mirroredGroves = new List<Grove>(groves);

            foreach (var pos in blocked)
                mirrored.Add(MirrorPos(pos, config.Width, config.Height));

            foreach (var grove in groves)
            {
                var mirrorCells = grove.Cells
                    .Select(c => MirrorPos(c, config.Width, config.Height))
                    .ToList();
                mirroredGroves.Add(new Grove(grove.TreeType, mirrorCells));
            }

            blocked = mirrored;
            groves = mirroredGroves;
        }

        /// <summary>Point reflection through map center: (x, y) → (W-1-x, H-1-y).</summary>
        /// <summary>Point reflection for 1x1 cells.</summary>
        internal static Position MirrorPos(Position pos, int width, int height)
        {
            return new Position(width - 1 - pos.X, height - 1 - pos.Y);
        }

        /// <summary>
        /// Mirror a unit anchor accounting for its footprint size.
        /// Produces visual symmetry with the non-walkable pivot formula.
        /// </summary>
        private static Position MirrorUnit(Position pos, int w, int h, int sizeX, int sizeY)
        {
            // Bottom-left anchor. Unit occupies [pos.X..pos.X+sizeX-1, pos.Y..pos.Y+sizeY-1].
            // Mirror across map center: mirrored anchor = (w - sizeX - pos.X, h - sizeY - pos.Y).
            return new Position(w - sizeX - pos.X, h - sizeY - pos.Y);
        }

        #endregion

        #region Validation

        private static SimMap BuildMap(MapGeneratorConfig config, HashSet<Position> blocked)
        {
            var map = new SimMap(config.Width, config.Height);
            foreach (var pos in blocked)
                map.SetCellBlocked(pos);
            return map;
        }

        /// <summary>
        /// Validate that all key positions are connected:
        /// - Each spawn can reach its mine.
        /// - Spawns can reach each other.
        /// - Each spawn can reach the map center.
        /// </summary>
        private static bool ValidateConnectivity(SimMap map, Position[] spawns,
            Position[] mines, MapGeneratorConfig config)
        {
            for (int i = 0; i < spawns.Length; i++)
            {
                if (map.FindPath(spawns[i], mines[i]).Count == 0)
                    return false;
            }

            if (map.FindPath(spawns[0], spawns[1]).Count == 0)
                return false;

            var center = new Position(config.Width / 2, config.Height / 2);
            for (int i = 0; i < spawns.Length; i++)
            {
                if (map.FindPath(spawns[i], center).Count == 0)
                    return false;
            }

            return true;
        }

        #endregion

        #region Helpers

        /// <summary>Group contiguous blocked cells into groves via flood fill.</summary>
        private List<Grove> FloodFillGroves(HashSet<Position> blocked, Random rng)
        {
            var groves = new List<Grove>();
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
                    {
                        if (remaining.Remove(n))
                            queue.Enqueue(n);
                    }
                }

                groves.Add(new Grove(rng.Next(1, 5), cells));
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
