using System;
using System.Collections.Generic;

namespace AgentSDK
{
    /// <summary>
    /// Shared A* pathfinder for grid-based maps. Used by both the Unity game engine
    /// and the AgentTestHarness simulation to ensure identical pathfinding results.
    ///
    /// Algorithm: A* with Euclidean heuristic, 8-directional movement.
    /// Edge costs: 1.0 for cardinal, sqrt(2) for diagonal.
    /// Tie-breaking: lowest f, then highest g (prefer progress), then lowest key (deterministic).
    /// </summary>
    public static class Pathfinder
    {
        private const float SQRT2 = 1.41421356f;
        private const int DEFAULT_MAX_EXPANSIONS = 2000;

        /// <summary>
        /// Callback to check if a cell is passable.
        /// </summary>
        /// <param name="x">Grid X coordinate</param>
        /// <param name="y">Grid Y coordinate</param>
        /// <returns>True if the cell can be traversed</returns>
        public delegate bool CellPassable(int x, int y);

        /// <summary>
        /// Find the shortest path between two positions on a grid.
        ///
        /// The start position is allowed to be impassable (unit may be inside a building).
        /// The end position must be passable.
        /// Returns the path excluding the start position, including the end position.
        /// Returns an empty list if no path exists.
        /// </summary>
        /// <param name="start">Start position</param>
        /// <param name="end">End position</param>
        /// <param name="width">Grid width</param>
        /// <param name="height">Grid height</param>
        /// <param name="isPassable">Callback to check cell passability</param>
        /// <param name="maxExpansions">Maximum node expansions before giving up</param>
        public static List<Position> FindPath(Position start, Position end,
            int width, int height, CellPassable isPassable,
            int maxExpansions = DEFAULT_MAX_EXPANSIONS)
        {
            if (start == end)
                return new List<Position>();

            // End must be passable
            if (end.X < 0 || end.X >= width || end.Y < 0 || end.Y >= height)
                return new List<Position>();
            if (!isPassable(end.X, end.Y))
                return new List<Position>();

            // Start is allowed to be impassable (unit inside building)

            int startKey = PosToKey(start.X, start.Y, height);
            int endKey = PosToKey(end.X, end.Y, height);

            // Open set: sorted by (f ascending, g descending, key ascending) for deterministic tie-breaking
            var openSet = new SortedSet<AStarNode>(AStarNodeComparer.Instance);
            var gScore = new Dictionary<int, float>();
            var cameFrom = new Dictionary<int, int>();
            var inOpen = new Dictionary<int, AStarNode>();

            float startF = Heuristic(start, end);
            var startNode = new AStarNode(startKey, 0f, startF);
            openSet.Add(startNode);
            gScore[startKey] = 0f;
            inOpen[startKey] = startNode;

            int expansions = 0;

            while (openSet.Count > 0)
            {
                if (++expansions > maxExpansions)
                    return new List<Position>();

                // Dequeue node with lowest f (ties broken by highest g, then lowest key)
                var current = Min(openSet);
                openSet.Remove(current);
                int curKey = current.Key;
                inOpen.Remove(curKey);

                if (curKey == endKey)
                    return ReconstructPath(cameFrom, curKey, height);

                int cx = curKey / height;
                int cy = curKey % height;

                // Expand 8 neighbors
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;

                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        if (!isPassable(nx, ny)) continue;

                        float edgeCost = (dx != 0 && dy != 0) ? SQRT2 : 1.0f;
                        float tentativeG = gScore[curKey] + edgeCost;
                        int neighborKey = PosToKey(nx, ny, height);

                        if (gScore.TryGetValue(neighborKey, out float existingG) && tentativeG >= existingG)
                            continue;

                        gScore[neighborKey] = tentativeG;
                        cameFrom[neighborKey] = curKey;

                        float h = Heuristic(new Position(nx, ny), end);
                        float f = tentativeG + h;

                        // Remove old entry if exists (SortedSet needs re-insertion for updated priority)
                        if (inOpen.TryGetValue(neighborKey, out var oldNode))
                        {
                            openSet.Remove(oldNode);
                        }

                        var newNode = new AStarNode(neighborKey, tentativeG, f);
                        openSet.Add(newNode);
                        inOpen[neighborKey] = newNode;
                    }
                }
            }

            return new List<Position>(); // no path found
        }

        #region Internals

        private static float Heuristic(Position a, Position b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static int PosToKey(int x, int y, int height)
        {
            return x * height + y;
        }

        private static List<Position> ReconstructPath(Dictionary<int, int> cameFrom, int endKey, int height)
        {
            var path = new List<Position>();
            int current = endKey;
            while (cameFrom.ContainsKey(current))
            {
                path.Add(new Position(current / height, current % height));
                current = cameFrom[current];
            }
            path.Reverse();
            return path; // excludes start, includes end
        }

        private static AStarNode Min(SortedSet<AStarNode> set)
        {
            using (var e = set.GetEnumerator())
            {
                e.MoveNext();
                return e.Current;
            }
        }

        private struct AStarNode
        {
            public readonly int Key;
            public readonly float G;
            public readonly float F;

            public AStarNode(int key, float g, float f)
            {
                Key = key; G = g; F = f;
            }
        }

        private class AStarNodeComparer : IComparer<AStarNode>
        {
            public static readonly AStarNodeComparer Instance = new AStarNodeComparer();

            public int Compare(AStarNode a, AStarNode b)
            {
                // Primary: lowest f value
                int cmp = a.F.CompareTo(b.F);
                if (cmp != 0) return cmp;
                // Secondary: highest g value (prefer nodes closer to goal)
                cmp = b.G.CompareTo(a.G);
                if (cmp != 0) return cmp;
                // Tertiary: lowest key (deterministic)
                return a.Key.CompareTo(b.Key);
            }
        }

        #endregion
    }
}
