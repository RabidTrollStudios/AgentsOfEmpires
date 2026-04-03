using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace Gameplay.Tests
{
    /// <summary>
    /// Tests for SimMap pathfinding: happy path, boundary cases, and FindPathToUnit.
    /// </summary>
    public class SimMapPathfindingTests
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
    }
}
