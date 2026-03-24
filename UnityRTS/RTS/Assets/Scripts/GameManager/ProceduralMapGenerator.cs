using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace GameManager
{
    /// <summary>
    /// Result of procedural map generation — blocked cells, spawn/mine positions, and grove data.
    /// Consumed by <see cref="GameManager"/> to create tilemaps at runtime.
    /// </summary>
    internal class ProceduralMapResult
    {
        public int Width;
        public int Height;
        public HashSet<Vector3Int> BlockedCells;
        public Vector3Int[] SpawnPositions;
        public Vector3Int[] MinePositions;
        public List<ProceduralGrove> Groves;
    }

    /// <summary>A cluster of blocked cells sharing one visual tree type (1–4).</summary>
    internal class ProceduralGrove
    {
        public int TreeType;
        public List<Vector3Int> Cells;
        public ProceduralGrove(int treeType, List<Vector3Int> cells)
        { TreeType = treeType; Cells = cells; }
    }

    /// <summary>
    /// Self-contained procedural map generator for the Unity game.
    /// Mirrors the algorithms in AgentTestHarness.MapGenerator but uses Vector3Int
    /// and includes inline BFS connectivity validation (no SimMap dependency).
    /// </summary>
    internal static class ProceduralMapGenerator
    {
        // Hard exclusion: guarantees room for base placement near spawns/mines.
        // Kept small so trees can grow close to starting areas.
        private const int SpawnExclusionRadius = 4;
        private const int MineExclusionRadius = 3;
        private const int CenterExclusionRadius = 2;
        private const int MaxRetries = 50;

        /// <summary>
        /// Generate a procedural map with the given parameters.
        /// Retries on connectivity failure with advancing RNG state.
        /// </summary>
        public static ProceduralMapResult Generate(int width, int height, float density,
            int seed, MapTemplate template, MapSymmetryMode symmetry)
        {
            var rng = new Random(seed);
            var spawns = ComputeSpawnPositions(width, height);
            var mines = ComputeMinePositions(spawns[0], width, height, rng);
            var exclusions = BuildExclusionZones(width, height, spawns, mines);

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var (blocked, groves) = GenerateObstacles(
                    width, height, density, template, symmetry, exclusions, rng);

                if (ValidateConnectivity(width, height, blocked, spawns, mines))
                    return new ProceduralMapResult
                    {
                        Width = width, Height = height,
                        BlockedCells = blocked, SpawnPositions = spawns,
                        MinePositions = mines, Groves = groves
                    };
            }

            // Fallback: empty map
            return new ProceduralMapResult
            {
                Width = width, Height = height,
                BlockedCells = new HashSet<Vector3Int>(),
                SpawnPositions = spawns, MinePositions = mines,
                Groves = new List<ProceduralGrove>()
            };
        }

        #region Spawn & Mine Placement

        private static Vector3Int[] ComputeSpawnPositions(int w, int h)
        {
            int margin = Math.Max(3, w / 10);
            var s0 = new Vector3Int(margin, margin, 0);
            return new[] { s0, Mirror(s0, w, h) };
        }

        private static Vector3Int[] ComputeMinePositions(Vector3Int spawn0, int w, int h, Random rng)
        {
            int maxDist = Math.Min(8, Math.Max(w, h) / 4);
            int minDist = Math.Min(5, maxDist);

            Vector3Int mine0 = Vector3Int.zero;
            for (int i = 0; i < 100; i++)
            {
                int dx = rng.Next(minDist, maxDist + 1);
                int dy = rng.Next(minDist, maxDist + 1);
                int x = spawn0.x + dx, y = spawn0.y + dy;
                if (x >= 2 && x < w / 2 && y >= 2 && y < h / 2)
                { mine0 = new Vector3Int(x, y, 0); break; }
            }
            if (mine0 == Vector3Int.zero)
                mine0 = new Vector3Int(
                    Math.Min(spawn0.x + 7, w / 2 - 2),
                    Math.Min(spawn0.y + 7, h / 2 - 2), 0);

            return new[] { mine0, MirrorUnit(mine0, w, h, 3, 3) };
        }

        #endregion

        #region Exclusion Zones

        private static HashSet<Vector3Int> BuildExclusionZones(int w, int h,
            Vector3Int[] spawns, Vector3Int[] mines)
        {
            var zones = new HashSet<Vector3Int>();
            foreach (var s in spawns) AddCircle(zones, s, SpawnExclusionRadius, w, h);
            foreach (var m in mines)  AddCircle(zones, m, MineExclusionRadius, w, h);
            AddCircle(zones, new Vector3Int(w / 2, h / 2, 0), CenterExclusionRadius, w, h);
            return zones;
        }

        private static void AddCircle(HashSet<Vector3Int> set, Vector3Int center,
            int radius, int w, int h)
        {
            int r2 = radius * radius;
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int x = center.x + dx, y = center.y + dy;
                    if (x >= 0 && x < w && y >= 0 && y < h)
                        set.Add(new Vector3Int(x, y, 0));
                }
        }

        #endregion

        #region Template Dispatch

        private static (HashSet<Vector3Int>, List<ProceduralGrove>) GenerateObstacles(
            int w, int h, float density, MapTemplate template, MapSymmetryMode symmetry,
            HashSet<Vector3Int> exclusions, Random rng)
        {
            HashSet<Vector3Int> blocked;
            List<ProceduralGrove> groves;

            switch (template)
            {
                case MapTemplate.OpenField:
                    // Many small scattered groves — compact individually but spread across the map
                    (blocked, groves) = GenerateGroveBased(w, h, density, exclusions, rng,
                        minGrove: 2, maxGrove: 5, growthProb: 0.80f, growthFalloff: 0.10f,
                        groveSpacing: 0);
                    break;
                case MapTemplate.Forest:
                    // Fewer but much larger, denser groves → thick forests with clearings
                    (blocked, groves) = GenerateGroveBased(w, h, density, exclusions, rng,
                        minGrove: 15, maxGrove: 40, growthProb: 0.90f, growthFalloff: 0.015f,
                        groveSpacing: 3);
                    break;
                case MapTemplate.Maze:
                    return GenerateMaze(w, h, density, symmetry, exclusions, rng);
                default:
                    return (new HashSet<Vector3Int>(), new List<ProceduralGrove>());
            }

            if (symmetry == MapSymmetryMode.Mirror)
                ApplyMirrorSymmetry(w, h, ref blocked, ref groves);

            return (blocked, groves);
        }

        #endregion

        #region Grove-Based Generation (OpenField / Forest)

        /// <param name="growthProb">Base probability a neighbor joins the grove (0–1).</param>
        /// <param name="growthFalloff">Probability reduction per Euclidean distance from seed.</param>
        /// <param name="groveSpacing">Minimum cell gap between groves (0 = no spacing).</param>
        private static (HashSet<Vector3Int>, List<ProceduralGrove>) GenerateGroveBased(
            int w, int h, float density, HashSet<Vector3Int> exclusions, Random rng,
            int minGrove, int maxGrove, float growthProb, float growthFalloff,
            int groveSpacing)
        {
            var blocked = new HashSet<Vector3Int>();
            var groves = new List<ProceduralGrove>();
            // Buffer zone around placed groves — seeds and growth stay out of this area
            var buffer = new HashSet<Vector3Int>();
            // Target for primary half — mirroring will roughly double it
            int targetCells = (int)(w * h * density * 0.5f);
            int cellsPlaced = 0;
            int maxAttempts = Math.Max(targetCells * 4, 50);

            for (int a = 0; a < maxAttempts && cellsPlaced < targetCells; a++)
            {
                int groveSize = rng.Next(minGrove, maxGrove + 1);
                var seed = RandomInPrimaryHalf(w, h, exclusions, blocked, buffer, rng);
                if (!seed.HasValue) continue;

                int treeType = rng.Next(1, 5);
                var cells = GrowGrove(seed.Value, groveSize, w, h, exclusions, blocked, buffer, rng,
                    growthProb, growthFalloff);
                if (cells.Count == 0) continue;

                groves.Add(new ProceduralGrove(treeType, cells));
                foreach (var c in cells)
                {
                    blocked.Add(c);
                    // Expand buffer around each placed cell
                    if (groveSpacing > 0)
                        AddSquareBuffer(buffer, c, groveSpacing, w, h);
                }
                cellsPlaced += cells.Count;
            }

            return (blocked, groves);
        }

        /// <summary>Mark cells within <paramref name="radius"/> of <paramref name="center"/> as buffered.</summary>
        private static void AddSquareBuffer(HashSet<Vector3Int> buffer, Vector3Int center,
            int radius, int w, int h)
        {
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = center.x + dx, y = center.y + dy;
                    if (x >= 0 && x < w && y >= 0 && y < h)
                        buffer.Add(new Vector3Int(x, y, 0));
                }
        }

        /// <summary>
        /// Grow an organic cluster from a seed using BFS with distance-based probability falloff.
        /// <paramref name="baseProb"/> and <paramref name="falloff"/> control the shape:
        /// high prob + low falloff → dense, round blobs (Forest);
        /// low prob + high falloff → small, sparse scatters (OpenField).
        /// Growth avoids the buffer zone so groves don't merge.
        /// </summary>
        private static List<Vector3Int> GrowGrove(Vector3Int seed, int targetSize,
            int w, int h, HashSet<Vector3Int> exclusions,
            HashSet<Vector3Int> existing, HashSet<Vector3Int> buffer, Random rng,
            float baseProb, float falloff)
        {
            var grove = new List<Vector3Int> { seed };
            var frontier = new List<Vector3Int> { seed };
            var visited = new HashSet<Vector3Int> { seed };
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

                    if (n.x < 0 || n.x >= w || n.y < 0 || n.y >= halfH) continue;
                    if (exclusions.Contains(n) || existing.Contains(n)) continue;
                    // Don't grow into another grove's buffer zone
                    if (buffer.Contains(n)) continue;

                    float dx = n.x - seed.x, dy = n.y - seed.y;
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

        /// <summary>
        /// Pick a random open position in the primary half (y &lt; height/2),
        /// distributed evenly across the full width of the map.
        /// </summary>
        private static Vector3Int? RandomInPrimaryHalf(int w, int h,
            HashSet<Vector3Int> exclusions, HashSet<Vector3Int> existing,
            HashSet<Vector3Int> buffer, Random rng)
        {
            int halfH = h / 2;
            for (int i = 0; i < 100; i++)
            {
                int x = rng.Next(0, w), y = rng.Next(0, halfH);
                var pos = new Vector3Int(x, y, 0);
                if (!exclusions.Contains(pos) && !existing.Contains(pos) && !buffer.Contains(pos))
                    return pos;
            }
            return null;
        }

        #endregion

        #region Cellular Automata (Maze)

        private static (HashSet<Vector3Int>, List<ProceduralGrove>) GenerateMaze(
            int w, int h, float density, MapSymmetryMode symmetry,
            HashSet<Vector3Int> exclusions, Random rng)
        {
            float initialFill = Math.Max(0.20f, Math.Min(0.55f, density * 1.3f));

            var grid = new bool[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    grid[x, y] = !exclusions.Contains(new Vector3Int(x, y, 0))
                                 && rng.NextDouble() < initialFill;

            // Smooth with 4-5 rule
            for (int pass = 0; pass < 5; pass++)
            {
                var next = new bool[w, h];
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        if (exclusions.Contains(new Vector3Int(x, y, 0)))
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
            if (symmetry == MapSymmetryMode.Mirror)
            {
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h / 2; y++)
                        grid[w - 1 - x, h - 1 - y] = grid[x, y];

                foreach (var pos in exclusions)
                    if (pos.x >= 0 && pos.x < w && pos.y >= 0 && pos.y < h)
                        grid[pos.x, pos.y] = false;
            }

            var blocked = new HashSet<Vector3Int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (grid[x, y]) blocked.Add(new Vector3Int(x, y, 0));

            return (blocked, FloodFillGroves(blocked, rng));
        }

        #endregion

        #region Symmetry

        private static void ApplyMirrorSymmetry(int w, int h,
            ref HashSet<Vector3Int> blocked, ref List<ProceduralGrove> groves)
        {
            var mirrored = new HashSet<Vector3Int>(blocked);
            var mirroredGroves = new List<ProceduralGrove>(groves);

            foreach (var pos in blocked) mirrored.Add(Mirror(pos, w, h));
            foreach (var grove in groves)
            {
                var mirrorCells = grove.Cells.Select(c => Mirror(c, w, h)).ToList();
                mirroredGroves.Add(new ProceduralGrove(grove.TreeType, mirrorCells));
            }

            blocked = mirrored;
            groves = mirroredGroves;
        }

        /// <summary>Point reflection for 1x1 cells.</summary>
        private static Vector3Int Mirror(Vector3Int pos, int w, int h)
            => new Vector3Int(w - 1 - pos.x, h - 1 - pos.y, 0);

        /// <summary>
        /// Mirror a unit anchor accounting for its footprint size.
        /// Produces visual symmetry with the non-walkable pivot formula (0.5 - sizeY/2).
        /// </summary>
        private static Vector3Int MirrorUnit(Vector3Int pos, int w, int h, int sizeX, int sizeY)
            => new Vector3Int(w - sizeX - pos.x, h - 1 + sizeY - pos.y, 0);

        #endregion

        #region Connectivity Validation

        /// <summary>
        /// BFS flood-fill from spawn 0 to verify all key positions are reachable.
        /// </summary>
        private static bool ValidateConnectivity(int w, int h,
            HashSet<Vector3Int> blocked, Vector3Int[] spawns, Vector3Int[] mines)
        {
            var reachable = new HashSet<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            queue.Enqueue(spawns[0]);
            reachable.Add(spawns[0]);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var n = new Vector3Int(current.x + dx, current.y + dy, 0);
                        if (n.x >= 0 && n.x < w && n.y >= 0 && n.y < h
                            && !blocked.Contains(n) && reachable.Add(n))
                            queue.Enqueue(n);
                    }
            }

            foreach (var s in spawns) if (!reachable.Contains(s)) return false;
            foreach (var m in mines)  if (!reachable.Contains(m)) return false;
            if (!reachable.Contains(new Vector3Int(w / 2, h / 2, 0))) return false;
            return true;
        }

        #endregion

        #region Helpers

        private static List<ProceduralGrove> FloodFillGroves(HashSet<Vector3Int> blocked, Random rng)
        {
            var groves = new List<ProceduralGrove>();
            var remaining = new HashSet<Vector3Int>(blocked);

            while (remaining.Count > 0)
            {
                var seed = remaining.First();
                var cells = new List<Vector3Int>();
                var queue = new Queue<Vector3Int>();
                queue.Enqueue(seed);
                remaining.Remove(seed);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    cells.Add(current);
                    foreach (var n in Get8Neighbors(current))
                        if (remaining.Remove(n)) queue.Enqueue(n);
                }
                groves.Add(new ProceduralGrove(rng.Next(1, 5), cells));
            }
            return groves;
        }

        private static List<Vector3Int> Get8Neighbors(Vector3Int pos)
        {
            return new List<Vector3Int>
            {
                new Vector3Int(pos.x + 1, pos.y, 0),     new Vector3Int(pos.x - 1, pos.y, 0),
                new Vector3Int(pos.x, pos.y + 1, 0),      new Vector3Int(pos.x, pos.y - 1, 0),
                new Vector3Int(pos.x + 1, pos.y + 1, 0),  new Vector3Int(pos.x - 1, pos.y + 1, 0),
                new Vector3Int(pos.x + 1, pos.y - 1, 0),  new Vector3Int(pos.x - 1, pos.y - 1, 0),
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
