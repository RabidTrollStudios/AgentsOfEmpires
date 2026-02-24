using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for SimMap: pathfinding, buildability checks, and footprint logic.
    /// </summary>
    public class SimMapTests
    {
        // ------------------------------------------------------------------
        // Pathfinding — happy path
        // ------------------------------------------------------------------

        [Fact]
        public void FindPath_StraightLine_ReturnsDirectPath()
        {
            var map = new SimMap(10, 10);
            var path = map.FindPath(new Position(0, 0), new Position(5, 0));

            Assert.NotEmpty(path);
            Assert.Equal(new Position(5, 0), path[path.Count - 1]);
        }

        [Fact]
        public void FindPath_Diagonal_ReturnsPath()
        {
            var map = new SimMap(10, 10);
            var path = map.FindPath(new Position(0, 0), new Position(5, 5));

            Assert.NotEmpty(path);
            Assert.Equal(new Position(5, 5), path[path.Count - 1]);
        }

        [Fact]
        public void FindPath_ExcludesStartPosition()
        {
            var map = new SimMap(10, 10);
            var path = map.FindPath(new Position(2, 2), new Position(5, 2));

            Assert.NotEmpty(path);
            Assert.DoesNotContain(new Position(2, 2), path);
        }

        [Fact]
        public void FindPath_SameStartAndEnd_ReturnsEmpty()
        {
            var map = new SimMap(10, 10);
            var path = map.FindPath(new Position(3, 3), new Position(3, 3));

            Assert.Empty(path);
        }

        [Fact]
        public void FindPath_AroundObstacle_FindsAlternatePath()
        {
            var map = new SimMap(10, 10);
            // Wall across y=5, x=0..7 (gap at x=8,9)
            for (int x = 0; x <= 7; x++)
                map.SetCellBlocked(new Position(x, 5));

            var path = map.FindPath(new Position(5, 3), new Position(5, 7));
            Assert.NotEmpty(path);
            Assert.Equal(new Position(5, 7), path[path.Count - 1]);
        }

        // ------------------------------------------------------------------
        // Pathfinding — boundary cases
        // ------------------------------------------------------------------

        [Fact]
        public void FindPath_CompletelyBlocked_ReturnsEmpty()
        {
            var map = new SimMap(10, 10);
            // Complete wall across y=5
            for (int x = 0; x < 10; x++)
                map.SetCellBlocked(new Position(x, 5));

            var path = map.FindPath(new Position(5, 3), new Position(5, 7));
            Assert.Empty(path);
        }

        [Fact]
        public void FindPath_EndBlocked_ReturnsEmpty()
        {
            var map = new SimMap(10, 10);
            map.SetCellBlocked(new Position(5, 5));

            var path = map.FindPath(new Position(0, 0), new Position(5, 5));
            Assert.Empty(path);
        }

        [Fact]
        public void FindPath_MapCornerToCorner_Succeeds()
        {
            var map = new SimMap(30, 30);
            var path = map.FindPath(new Position(0, 0), new Position(29, 29));

            Assert.NotEmpty(path);
            Assert.Equal(new Position(29, 29), path[path.Count - 1]);
        }

        [Fact]
        public void FindPath_OutOfBounds_ReturnsEmpty()
        {
            var map = new SimMap(10, 10);
            var path = map.FindPath(new Position(0, 0), new Position(15, 15));
            Assert.Empty(path);
        }

        [Fact]
        public void FindPath_AdjacentCells_ReturnsSingleStep()
        {
            var map = new SimMap(10, 10);
            var path = map.FindPath(new Position(5, 5), new Position(6, 5));

            Assert.Single(path);
            Assert.Equal(new Position(6, 5), path[0]);
        }

        // ------------------------------------------------------------------
        // Pathfinding — FindPathToUnit
        // ------------------------------------------------------------------

        [Fact]
        public void FindPathToUnit_FindsPathToNeighborOfBuilding()
        {
            var map = new SimMap(30, 30);
            // Place a 3x3 building at (10, 10)
            map.SetAreaBuildability(UnitType.BASE, new Position(10, 10), false);

            var path = map.FindPathToUnit(new Position(5, 5), UnitType.BASE, new Position(10, 10));
            Assert.NotEmpty(path);

            // Path should end at a cell adjacent to the building, not inside it
            var endpoint = path[path.Count - 1];
            var neighbors = map.GetPositionsNearUnit(UnitType.BASE, new Position(10, 10));
            Assert.Contains(endpoint, neighbors);
        }

        // ------------------------------------------------------------------
        // Buildability checks
        // ------------------------------------------------------------------

        [Fact]
        public void IsAreaBuildable_EmptyMap_ReturnsTrue()
        {
            var map = new SimMap(30, 30);
            Assert.True(map.IsAreaBuildable(UnitType.BASE, new Position(10, 10)));
        }

        [Fact]
        public void IsAreaBuildable_OccupiedCell_ReturnsFalse()
        {
            var map = new SimMap(30, 30);
            map.SetAreaBuildability(UnitType.BASE, new Position(10, 10), false);

            // Overlapping position should not be buildable
            Assert.False(map.IsAreaBuildable(UnitType.BASE, new Position(11, 10)));
        }

        [Fact]
        public void IsAreaBuildable_AtMapEdge_ReturnsFalse()
        {
            var map = new SimMap(30, 30);
            // 3x3 building at (29, 1) would need cells (29,1), (30,1), (31,1) — out of bounds
            Assert.False(map.IsAreaBuildable(UnitType.BASE, new Position(29, 1)));
        }

        [Fact]
        public void IsBoundedAreaBuildable_NeedsClearance()
        {
            var map = new SimMap(30, 30);
            // Place a building at (10, 10)
            map.SetAreaBuildability(UnitType.BASE, new Position(10, 10), false);

            // Right next to it (at 13, 10) should be buildable for area, but might fail bounded
            // The building footprint covers (10,10), (11,10), (12,10), (10,9), (11,9), (12,9), etc.
            // Bounded check at (13, 10) includes border at (12, 10) which is occupied
            Assert.False(map.IsBoundedAreaBuildable(UnitType.BASE, new Position(13, 10)));
        }

        [Fact]
        public void IsBoundedAreaBuildable_WithSpace_ReturnsTrue()
        {
            var map = new SimMap(30, 30);
            map.SetAreaBuildability(UnitType.BASE, new Position(5, 10), false);

            // Far enough away (15, 10) should pass bounded check
            Assert.True(map.IsBoundedAreaBuildable(UnitType.BASE, new Position(15, 15)));
        }

        // ------------------------------------------------------------------
        // Neighbor ring
        // ------------------------------------------------------------------

        [Fact]
        public void GetPositionsNearUnit_ReturnsRingAround3x3()
        {
            var map = new SimMap(30, 30);
            var positions = map.GetPositionsNearUnit(UnitType.BASE, new Position(10, 10));

            // 4x4 building (BASE): ring around it should have (4+2)*2 + (4)*2 = 20 cells
            // Top row (6 cells) + bottom row (6 cells) + left col (4 cells) + right col (4 cells) = 20
            Assert.Equal(20, positions.Count);

            // None should be inside the footprint
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    Assert.DoesNotContain(new Position(10 + i, 10 - j), positions);
        }

        [Fact]
        public void GetPositionsNearUnit_1x1Worker_Returns8Neighbors()
        {
            var map = new SimMap(30, 30);
            var positions = map.GetPositionsNearUnit(UnitType.WORKER, new Position(10, 10));

            // 1x1 unit: ring is 8 surrounding cells
            Assert.Equal(8, positions.Count);
        }

        [Fact]
        public void GetBuildablePositionsNearUnit_ExcludesOccupied()
        {
            var map = new SimMap(30, 30);
            map.SetAreaBuildability(UnitType.BASE, new Position(10, 10), false);

            var allNeighbors = map.GetPositionsNearUnit(UnitType.BASE, new Position(10, 10));
            var buildableNeighbors = map.GetBuildablePositionsNearUnit(UnitType.BASE, new Position(10, 10));

            Assert.Equal(allNeighbors.Count, buildableNeighbors.Count);

            // Block one neighbor
            map.SetCellBlocked(allNeighbors[0]);
            buildableNeighbors = map.GetBuildablePositionsNearUnit(UnitType.BASE, new Position(10, 10));
            Assert.Equal(allNeighbors.Count - 1, buildableNeighbors.Count);
        }

        // ------------------------------------------------------------------
        // SetAreaBuildability — mobile vs building
        // ------------------------------------------------------------------

        [Fact]
        public void SetAreaBuildability_Building_BlocksWalkability()
        {
            var map = new SimMap(30, 30);
            map.SetAreaBuildability(UnitType.BASE, new Position(10, 10), false);

            Assert.False(map.IsPositionBuildable(new Position(10, 10)));
            Assert.False(map.IsPositionWalkable(new Position(10, 10)));
        }

        [Fact]
        public void SetAreaBuildability_MobileUnit_KeepsWalkable()
        {
            var map = new SimMap(30, 30);
            map.SetAreaBuildability(UnitType.WORKER, new Position(10, 10), false);

            Assert.False(map.IsPositionBuildable(new Position(10, 10)));
            // Mobile units keep cells walkable for pathfinding
            Assert.True(map.IsPositionWalkable(new Position(10, 10)));
        }

        [Fact]
        public void SetAreaBuildability_FreesOnRemoval()
        {
            var map = new SimMap(30, 30);
            map.SetAreaBuildability(UnitType.BASE, new Position(10, 10), false);
            Assert.False(map.IsPositionBuildable(new Position(10, 10)));

            map.SetAreaBuildability(UnitType.BASE, new Position(10, 10), true);
            Assert.True(map.IsPositionBuildable(new Position(10, 10)));
            Assert.True(map.IsPositionWalkable(new Position(10, 10)));
        }

        // ------------------------------------------------------------------
        // FindProspectiveBuildPositions
        // ------------------------------------------------------------------

        [Fact]
        public void FindProspectiveBuildPositions_OnCrowdedMap_ReturnsFewerPositions()
        {
            var map = new SimMap(15, 15);
            int positionsBefore = map.FindProspectiveBuildPositions(UnitType.BASE).Count;

            // Place a building — reduces available positions
            map.SetAreaBuildability(UnitType.BASE, new Position(7, 7), false);
            int positionsAfter = map.FindProspectiveBuildPositions(UnitType.BASE).Count;

            Assert.True(positionsAfter < positionsBefore);
        }
    }
}
