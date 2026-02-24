using System;
using System.Collections.Generic;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// A* pathfinding with Euclidean heuristic, matching the real game's Graph.AStarSearch.
    /// </summary>
    public partial class SimMap
    {
        /// <summary>
        /// Find shortest path between two positions using A* with Euclidean heuristic.
        /// Cardinal edges cost 1.0, diagonal edges cost sqrt(2).
        /// Returns empty list if no path exists. The start position is excluded from the result.
        /// </summary>
        public List<Position> FindPath(Position start, Position end)
        {
            if (!IsPositionValid(start) || !IsPositionValid(end))
                return new List<Position>();

            if (start == end)
                return new List<Position>();

            // End must be walkable
            if (!walkable[end.X, end.Y])
                return new List<Position>();

            // Start node is allowed to be unwalkable (unit may be inside a building)

            var openSet = new SortedSet<AStarNode>(new AStarNodeComparer());
            var gScore = new Dictionary<int, float>();
            var cameFrom = new Dictionary<int, int>();
            var inOpen = new Dictionary<int, AStarNode>();

            int startKey = PosToKey(start);
            int endKey = PosToKey(end);

            float startG = 0f;
            float startF = Heuristic(start, end);
            var startNode = new AStarNode(startKey, startG, startF);
            openSet.Add(startNode);
            gScore[startKey] = 0f;
            inOpen[startKey] = startNode;

            while (openSet.Count > 0)
            {
                var current = openSet.Min;
                openSet.Remove(current);
                inOpen.Remove(current.Key);

                if (current.Key == endKey)
                    return ReconstructPath(cameFrom, endKey);

                var currentPos = KeyToPos(current.Key);

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = currentPos.X + dx;
                        int ny = currentPos.Y + dy;

                        if (nx < 0 || nx >= Width || ny < 0 || ny >= Height)
                            continue;

                        if (!walkable[nx, ny])
                            continue;

                        int neighborKey = nx * Height + ny;
                        float edgeCost = (dx != 0 && dy != 0) ? 1.41421356f : 1.0f;
                        float tentativeG = gScore[current.Key] + edgeCost;

                        if (gScore.TryGetValue(neighborKey, out float existingG) && tentativeG >= existingG)
                            continue;

                        gScore[neighborKey] = tentativeG;
                        cameFrom[neighborKey] = current.Key;

                        var neighborPos = new Position(nx, ny);
                        float f = tentativeG + Heuristic(neighborPos, end);

                        // Remove old entry if exists
                        if (inOpen.TryGetValue(neighborKey, out var oldNode))
                        {
                            openSet.Remove(oldNode);
                            inOpen.Remove(neighborKey);
                        }

                        var newNode = new AStarNode(neighborKey, tentativeG, f);
                        openSet.Add(newNode);
                        inOpen[neighborKey] = newNode;
                    }
                }
            }

            return new List<Position>();
        }

        /// <summary>
        /// Find shortest path using buildable checks instead of walkable, so cells
        /// occupied by mobile units are treated as impassable.
        /// </summary>
        public List<Position> FindPath(Position start, Position end, bool avoidUnits)
        {
            if (!avoidUnits)
                return FindPath(start, end);

            if (!IsPositionValid(start) || !IsPositionValid(end))
                return new List<Position>();

            if (start == end)
                return new List<Position>();

            if (!IsPositionBuildable(end))
                return new List<Position>();

            var openSet = new SortedSet<AStarNode>(new AStarNodeComparer());
            var gScore = new Dictionary<int, float>();
            var cameFrom = new Dictionary<int, int>();
            var inOpen = new Dictionary<int, AStarNode>();

            int startKey = PosToKey(start);
            int endKey = PosToKey(end);

            float startG = 0f;
            float startF = Heuristic(start, end);
            var startNode = new AStarNode(startKey, startG, startF);
            openSet.Add(startNode);
            gScore[startKey] = 0f;
            inOpen[startKey] = startNode;

            while (openSet.Count > 0)
            {
                var current = openSet.Min;
                openSet.Remove(current);
                inOpen.Remove(current.Key);

                if (current.Key == endKey)
                    return ReconstructPath(cameFrom, endKey);

                var currentPos = KeyToPos(current.Key);

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = currentPos.X + dx;
                        int ny = currentPos.Y + dy;

                        if (nx < 0 || nx >= Width || ny < 0 || ny >= Height)
                            continue;

                        if (!IsPositionBuildable(new Position(nx, ny)))
                            continue;

                        int neighborKey = nx * Height + ny;
                        float edgeCost = (dx != 0 && dy != 0) ? 1.41421356f : 1.0f;
                        float tentativeG = gScore[current.Key] + edgeCost;

                        if (gScore.TryGetValue(neighborKey, out float existingG) && tentativeG >= existingG)
                            continue;

                        gScore[neighborKey] = tentativeG;
                        cameFrom[neighborKey] = current.Key;

                        var neighborPos = new Position(nx, ny);
                        float f = tentativeG + Heuristic(neighborPos, end);

                        if (inOpen.TryGetValue(neighborKey, out var oldNode))
                        {
                            openSet.Remove(oldNode);
                            inOpen.Remove(neighborKey);
                        }

                        var newNode = new AStarNode(neighborKey, tentativeG, f);
                        openSet.Add(newNode);
                        inOpen[neighborKey] = newNode;
                    }
                }
            }

            return new List<Position>();
        }

        /// <summary>
        /// Find a path from start to any walkable cell adjacent to the given unit.
        /// Tries each buildable neighbor; returns the first successful path.
        /// </summary>
        public List<Position> FindPathToUnit(Position start, UnitType unitType, Position unitAnchor)
        {
            var neighbors = GetBuildablePositionsNearUnit(unitType, unitAnchor);
            foreach (var neighbor in neighbors)
            {
                var path = FindPath(start, neighbor);
                if (path.Count > 0)
                    return path;
            }
            return new List<Position>();
        }

        private float Heuristic(Position a, Position b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private int PosToKey(Position p) => p.X * Height + p.Y;

        private Position KeyToPos(int key) => new Position(key / Height, key % Height);

        private List<Position> ReconstructPath(Dictionary<int, int> cameFrom, int endKey)
        {
            var path = new List<Position>();
            int current = endKey;
            while (cameFrom.ContainsKey(current))
            {
                path.Add(KeyToPos(current));
                current = cameFrom[current];
            }
            path.Reverse();
            return path;
        }

        /// <summary>Node used in the A* open set.</summary>
        private struct AStarNode
        {
            public readonly int Key;
            public readonly float G;
            public readonly float F;

            public AStarNode(int key, float g, float f)
            {
                Key = key;
                G = g;
                F = f;
            }
        }

        /// <summary>
        /// Comparer for A* nodes: sort by F, break ties by G (prefer higher G = closer to goal),
        /// then by key for deterministic ordering.
        /// </summary>
        private class AStarNodeComparer : IComparer<AStarNode>
        {
            public int Compare(AStarNode a, AStarNode b)
            {
                int cmp = a.F.CompareTo(b.F);
                if (cmp != 0) return cmp;
                // Break ties: prefer higher G (closer to goal)
                cmp = b.G.CompareTo(a.G);
                if (cmp != 0) return cmp;
                return a.Key.CompareTo(b.Key);
            }
        }
    }
}
