using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AgentSDK;
using Xunit;
using Xunit.Abstractions;

namespace Parity.Tests
{
    /// <summary>
    /// Realistic pathfinding load test — informs whether Unity's per-agent path
    /// budget (MAX_PATH_CALLS_PER_FRAME = 20) actually protects framerate.
    ///
    /// Uses the REAL procedural map from the divergent parity seed. Units MOVE:
    /// each picks a random OPEN destination, pathfinds to it, follows the path one
    /// cell per tick, and re-paths to a new random destination only when it arrives
    /// or its path is exhausted — matching real play (units re-path on exhaustion,
    /// with varied path lengths), not the worst-case-every-tick pathological case.
    ///
    /// We grow the population (adding units over time), and each tick measure the
    /// pathfinding time spent by ONLY the units that needed a new path that tick.
    /// We report how many concurrent moving units it takes for a single tick's
    /// pathfinding to exceed one 60fps frame (16.6ms).
    /// </summary>
    public class PathfindingStressTest
    {
        private const int Seed = 1914087774;

        private readonly ITestOutputHelper _output;
        public PathfindingStressTest(ITestOutputHelper output) { _output = output; }

        private sealed class Mover
        {
            public Position Pos;
            public List<Position> Path;
            public int Idx;
        }

        [Fact]
        public void MovingUnits_RandomDestinations_HowManyExceedAFrame()
        {
            var config = new MapGenConfig
            {
                Seed = Seed, Width = 75, Height = 30,
                ObstacleDensity = 0.20f,
                Template = MapTemplate.OPEN_FIELD, Symmetry = SymmetryType.MIRROR,
            };
            var result = MapGenCore.Generate(config);
            var grid = new GameGrid(config.Width, config.Height);
            foreach (var b in result.BlockedCells) grid.SetCellBlocked(b);

            // Precompute all walkable cells for random-destination picks.
            var walkable = new List<Position>();
            for (int x = 0; x < config.Width; x++)
                for (int y = 0; y < config.Height; y++)
                {
                    var p = new Position(x, y);
                    if (grid.IsPositionWalkable(p)) walkable.Add(p);
                }
            _output.WriteLine($"Map {config.Width}x{config.Height}, blocked={result.BlockedCells.Count}, walkable={walkable.Count}");

            // Deterministic RNG so the run is reproducible.
            var rng = new Random(12345);
            Position RandDest() => walkable[rng.Next(walkable.Count)];

            // Warm the JIT / caches with real re-paths so numbers are steady-state.
            for (int w = 0; w < 2000; w++) grid.FindPath(RandDest(), RandDest());

            var movers = new List<Mover>();
            int pathLenSum = 0, pathCount = 0;
            int at60 = -1, at30 = -1, at20 = -1;
            const double FRAME_60 = 1000.0 / 60.0, FRAME_30 = 1000.0 / 30.0, TICK_20 = 50.0;
            int maxPathsInATick = 0;

            _output.WriteLine("units | pathing units/tick | pathfind ms/tick | avg path len");
            _output.WriteLine("------+--------------------+------------------+-------------");

            // Grow to 500 moving units; add ~1 per tick. Each tick: move everyone one
            // step; units that arrived/exhausted re-path (that's the measured load).
            const int MAX_UNITS = 500;
            for (int tick = 1; tick <= MAX_UNITS + 50; tick++)
            {
                if (movers.Count < MAX_UNITS)
                {
                    var start = RandDest();
                    movers.Add(new Mover { Pos = start, Path = null, Idx = 0 });
                }

                int pathingThisTick = 0;
                var sw = Stopwatch.StartNew();
                foreach (var m in movers)
                {
                    bool needsPath = m.Path == null || m.Idx >= m.Path.Count;
                    if (needsPath)
                    {
                        var dest = RandDest();
                        m.Path = grid.FindPath(m.Pos, dest);
                        m.Idx = 0;
                        pathingThisTick++;
                        if (m.Path.Count > 0) { pathLenSum += m.Path.Count; pathCount++; }
                    }
                    // Advance one cell along the path (units move ~1 cell/tick).
                    if (m.Path != null && m.Idx < m.Path.Count)
                    {
                        m.Pos = m.Path[m.Idx];
                        m.Idx++;
                    }
                }
                sw.Stop();
                double ms = sw.Elapsed.TotalMilliseconds;
                if (pathingThisTick > maxPathsInATick) maxPathsInATick = pathingThisTick;

                if (at60 < 0 && ms > FRAME_60) at60 = movers.Count;
                if (at30 < 0 && ms > FRAME_30) at30 = movers.Count;
                if (at20 < 0 && ms > TICK_20)  at20 = movers.Count;

                if (movers.Count <= 20 || movers.Count % 50 == 0)
                    _output.WriteLine($"{movers.Count,5} | {pathingThisTick,18} | {ms,16:F2} | {(pathCount>0?(double)pathLenSum/pathCount:0),12:F1}");

                if (at20 > 0 && movers.Count >= MAX_UNITS) break;
            }

            _output.WriteLine("");
            _output.WriteLine($"Avg path length: {(pathCount>0?(double)pathLenSum/pathCount:0):F1} cells (real random-destination mix)");
            _output.WriteLine($"Max units that re-pathed in a single tick: {maxPathsInATick}");
            _output.WriteLine($"Exceeds 60fps frame (16.6ms) at: {(at60<0?">"+MAX_UNITS:at60.ToString())} moving units");
            _output.WriteLine($"Exceeds 30fps frame (33.3ms) at: {(at30<0?">"+MAX_UNITS:at30.ToString())} units");
            _output.WriteLine($"Exceeds 20Hz tick   (50.0ms) at: {(at20<0?">"+MAX_UNITS:at20.ToString())} units");
            _output.WriteLine($"(Unity's per-agent budget is 20 path calls/tick; realistic per-tick re-paths peaked at {maxPathsInATick}.)");
        }
    }
}
