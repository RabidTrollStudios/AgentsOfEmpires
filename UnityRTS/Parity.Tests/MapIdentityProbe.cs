using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentSDK;
using Xunit;
using Xunit.Abstractions;

namespace Parity.Tests
{
    /// <summary>
    /// Fundamental check: does MapGenCore, given the SAME seed/config Unity used,
    /// produce the SAME blocked cells (within the 75x30 playable region) as Unity
    /// recorded in the ParityState CSV? Origin (0,0) is shared; water border outside
    /// 75x30 is ignored on both sides.
    /// </summary>
    public class MapIdentityProbe
    {
        private readonly ITestOutputHelper _out;
        public MapIdentityProbe(ITestOutputHelper o) => _out = o;

        [Fact]
        public void SimMap_Matches_UnityBlockedCells_InPlayableRegion()
        {
            const int PW = 75, PH = 30;

            // Unity ground truth: playable-blocked set extracted from the CSV.
            string unityFile = @"C:\Users\Dana\AppData\Local\Temp\claude\C--Git-Warcrap\9897982d-7c52-44e6-b5da-70850f673837\scratchpad\unity_blocked_playable.txt";
            var unityBlocked = new HashSet<(int, int)>();
            foreach (var tok in File.ReadAllText(unityFile).Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var xy = tok.Split(',');
                unityBlocked.Add((int.Parse(xy[0]), int.Parse(xy[1])));
            }
            _out.WriteLine($"Unity playable-blocked cells: {unityBlocked.Count}");

            // Headless generation with the EXACT config from the CSV header.
            var core = MapGenCore.Generate(new MapGenConfig
            {
                Seed = 1914087774,
                Width = PW,
                Height = PH,
                ObstacleDensity = 0.2000f,
                Template = MapTemplate.OPEN_FIELD,
                Symmetry = SymmetryType.MIRROR,
            });

            var simBlocked = new HashSet<(int, int)>(
                core.BlockedCells.Select(p => (p.X, p.Y)).Where(c => c.X >= 0 && c.X < PW && c.Y >= 0 && c.Y < PH));
            _out.WriteLine($"Sim (MapGenCore) blocked cells in playable: {simBlocked.Count}");
            _out.WriteLine($"Sim spawn positions: {string.Join(", ", core.SpawnPositions.Select(p => $"({p.X},{p.Y})"))}");
            _out.WriteLine($"Sim mine positions:  {string.Join(", ", core.MinePositions.Select(p => $"({p.X},{p.Y})"))}");

            var onlyUnity = unityBlocked.Except(simBlocked).ToList();
            var onlySim = simBlocked.Except(unityBlocked).ToList();
            int common = unityBlocked.Intersect(simBlocked).Count();

            _out.WriteLine($"--- COMPARISON ---");
            _out.WriteLine($"common blocked: {common}");
            _out.WriteLine($"only in Unity (sim thinks walkable): {onlyUnity.Count}");
            _out.WriteLine($"only in Sim (Unity thinks walkable): {onlySim.Count}");
            _out.WriteLine($"  sample only-Unity: {string.Join(" ", onlyUnity.Take(15).Select(c => $"{c.Item1},{c.Item2}"))}");
            _out.WriteLine($"  sample only-Sim:   {string.Join(" ", onlySim.Take(15).Select(c => $"{c.Item1},{c.Item2}"))}");

            // Not a hard assert yet — this is diagnostic. But record the verdict.
            if (onlyUnity.Count == 0 && onlySim.Count == 0)
                _out.WriteLine("VERDICT: MAPS IDENTICAL in playable region.");
            else
                _out.WriteLine($"VERDICT: MAPS DIFFER by {onlyUnity.Count + onlySim.Count} cells.");
        }
    }
}
