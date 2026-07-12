using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using Xunit;

namespace Parity.Tests
{
    /// <summary>
    /// Self-tests for the #17 derived-state parity digests (grid occupancy / walkability and
    /// PathBudget slot grants). These prove the DIAGNOSTIC itself is sound before it is ever
    /// relied on to flag a real divergence: the digest must MOVE when the state it summarizes
    /// changes, stay STABLE when it doesn't, and be reproducible across independent computations
    /// (the property that lets two engines compare it at all).
    /// </summary>
    public class GridDigestTests
    {
        [Fact]
        public void WalkabilityDigest_StableForIdenticalGrids()
        {
            var a = new GameGrid(10, 8);
            var b = new GameGrid(10, 8);
            Assert.Equal(a.ComputeWalkabilityDigest(), b.ComputeWalkabilityDigest());
        }

        [Fact]
        public void WalkabilityDigest_ChangesWhenACellBlocks()
        {
            var g = new GameGrid(10, 8);
            ulong before = g.ComputeWalkabilityDigest();
            g.SetCellBlocked(3, 4);
            ulong after = g.ComputeWalkabilityDigest();
            Assert.NotEqual(before, after);
        }

        [Fact]
        public void WalkabilityDigest_DistinguishesCellPosition()
        {
            // Blocking DIFFERENT cells must yield DIFFERENT digests — otherwise a footprint
            // placed at the wrong location could hash the same as the right one and hide a bug.
            var g1 = new GameGrid(10, 8);
            var g2 = new GameGrid(10, 8);
            g1.SetCellBlocked(3, 4);
            g2.SetCellBlocked(4, 3);
            Assert.NotEqual(g1.ComputeWalkabilityDigest(), g2.ComputeWalkabilityDigest());
        }

        [Fact]
        public void OccupancyDigest_TracksAdditionalOccupantsIndependently()
        {
            // NOTE: SetCellOccupied couples the FIRST occupant to walkability (OPEN -> WALKABLE
            // so units path through but can't stop). So a second occupant on an already-occupied
            // cell changes ONLY the occupant count — the ideal probe that the occupancy digest
            // tracks the count, not just the cell state.
            var g = new GameGrid(10, 8);
            var p = new Position(2, 2);
            g.SetCellOccupied(p, true);                       // first occupant: also flips to WALKABLE
            ulong occAfterFirst = g.ComputeOccupancyDigest();
            ulong walkAfterFirst = g.ComputeWalkabilityDigest();

            g.SetCellOccupied(p, true);                       // second occupant: count only

            Assert.NotEqual(occAfterFirst, g.ComputeOccupancyDigest());   // occupancy moved
            Assert.Equal(walkAfterFirst, g.ComputeWalkabilityDigest());   // walkability unchanged
        }

        [Fact]
        public void OccupancyDigest_ReturnsToBaselineWhenCleared()
        {
            var g = new GameGrid(6, 6);
            ulong baseline = g.ComputeOccupancyDigest();
            var p = new Position(1, 1);
            g.SetCellOccupied(p, true);
            Assert.NotEqual(baseline, g.ComputeOccupancyDigest());
            g.SetCellOccupied(p, false);
            Assert.Equal(baseline, g.ComputeOccupancyDigest());
        }

        [Fact]
        public void BoundedDigest_IgnoresCellsOutsidePlayableRegion()
        {
            // This is the fix that aligns Unity (large water-bordered grid) with the sim
            // (playable-only grid): a bounded digest over the shared region must be INSENSITIVE to
            // anything outside it. Two grids identical within 5x5 but differing outside must hash
            // equal when bounded to 5x5, and differently when hashed in full.
            var small = new GameGrid(5, 5);
            var large = new GameGrid(12, 12);          // "Unity" grid with extra border
            // Make the extra border area differ wildly (simulating water blocked cells).
            for (int x = 5; x < 12; x++)
                for (int y = 0; y < 12; y++)
                    large.SetCellBlocked(x, y);
            for (int y = 5; y < 12; y++)
                for (int x = 0; x < 12; x++)
                    large.SetCellBlocked(x, y);

            // Bounded to the shared 5x5 playable region → identical.
            Assert.Equal(
                small.ComputeWalkabilityDigest(5, 5),
                large.ComputeWalkabilityDigest(5, 5));
            Assert.Equal(
                small.ComputeOccupancyDigest(5, 5),
                large.ComputeOccupancyDigest(5, 5));

            // Full-grid digests differ (different sizes + border) — proving the bound is doing work.
            Assert.NotEqual(
                small.ComputeWalkabilityDigest(),
                large.ComputeWalkabilityDigest());
        }

        [Fact]
        public void BoundedDigest_ReflectsChangesInsideRegion()
        {
            // A change INSIDE the bounded region must still register — the bound narrows scope, it
            // must not blind the digest to real playable-area changes.
            var g = new GameGrid(12, 12);
            ulong before = g.ComputeWalkabilityDigest(5, 5);
            g.SetCellBlocked(2, 2);                     // inside the 5x5 region
            Assert.NotEqual(before, g.ComputeWalkabilityDigest(5, 5));
        }

        [Fact]
        public void SlotDigest_DeterministicForSameInputs()
        {
            var units = new List<int> { 0, 1, 2, 5, 9 };
            ulong a = PathBudget.ComputeSlotDigest(42, units);
            ulong b = PathBudget.ComputeSlotDigest(42, units);
            Assert.Equal(a, b);
        }

        [Fact]
        public void SlotDigest_ChangesWithTick()
        {
            var units = new List<int> { 0, 1, 2, 5, 9 };
            // Different ticks rotate the grant window, so the grant/defer pattern — and thus the
            // digest — must differ (unless coincidentally identical; pick ticks a window apart).
            Assert.NotEqual(
                PathBudget.ComputeSlotDigest(0, units),
                PathBudget.ComputeSlotDigest(PathBudget.SLOTS_PER_TICK, units));
        }

        [Fact]
        public void SlotDigest_ChangesWithRoster()
        {
            // A roster skew (one engine thinks an extra unit exists) must change the digest —
            // that is exactly the divergence this digest is meant to catch.
            ulong five = PathBudget.ComputeSlotDigest(10, new[] { 0, 1, 2, 3, 4 });
            ulong six = PathBudget.ComputeSlotDigest(10, new[] { 0, 1, 2, 3, 4, 5 });
            Assert.NotEqual(five, six);
        }

        [Fact]
        public void SlotDigest_MatchesGateDecisionsExactly()
        {
            // Sanity that the digest reflects the SAME grant/defer the engines act on: rebuild
            // the digest by hand from CanPathThisTick and confirm it agrees.
            int tick = 7;
            var units = Enumerable.Range(0, 20).ToList();
            ulong viaHelper = PathBudget.ComputeSlotDigest(tick, units);

            // Recompute independently the same way ComputeSlotDigest does.
            const ulong offset = 14695981039346656037UL, prime = 1099511628211UL;
            ulong h = offset;
            unchecked
            {
                foreach (int u in units)
                {
                    int v = (u << 1) | (PathBudget.CanPathThisTick(tick, u) ? 1 : 0);
                    h = (h ^ (byte)v) * prime;
                    h = (h ^ (byte)(v >> 8)) * prime;
                    h = (h ^ (byte)(v >> 16)) * prime;
                    h = (h ^ (byte)(v >> 24)) * prime;
                }
            }
            Assert.Equal(h, viaHelper);
        }
    }
}
