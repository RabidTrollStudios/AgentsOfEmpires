using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AgentSDK;

namespace GameManager.Tests
{
	[TestFixture]
	public class MapManagerGetPathTests
	{
		private MapManager manager;
		private GameObject tilemapGo;

		[SetUp]
		public void SetUp()
		{
			(manager, tilemapGo) = MapManagerTestHelper.Build(20, 20);
		}

		[TearDown]
		public void TearDown()
		{
			if (tilemapGo != null)
				UnityEngine.Object.DestroyImmediate(tilemapGo);
		}

		#region Happy Path

		/// <summary>
		/// A path between two nearby cells on an open grid should return
		/// a non-empty list ending at the target position.
		/// </summary>
		[Test]
		public void GetPathBetweenGridPositions_OpenGrid_ReturnsPathEndingAtTarget()
		{
			var start = new Vector3Int(0, 0, 0);
			var end   = new Vector3Int(5, 5, 0);

			List<Vector3Int> path = manager.GetPathBetweenGridPositions(start, end);

			Assert.Greater(path.Count, 0, "Path should be non-empty on an open grid");
			Assert.AreEqual(end, path[path.Count - 1],
				"Path should end at the target position");
		}

		/// <summary>
		/// When start equals end, GetPathBetweenGridPositions should return an empty path.
		/// </summary>
		[Test]
		public void GetPathBetweenGridPositions_SamePosition_ReturnsEmptyPath()
		{
			var pos = new Vector3Int(5, 5, 0);

			List<Vector3Int> path = manager.GetPathBetweenGridPositions(pos, pos);

			Assert.AreEqual(0, path.Count,
				"Path from a cell to itself should be empty");
		}

		/// <summary>
		/// A path across the full diagonal of the map should find a route.
		/// </summary>
		[Test]
		public void GetPathBetweenGridPositions_FullDiagonal_FindsPath()
		{
			var start = new Vector3Int(0, 0, 0);
			var end   = new Vector3Int(19, 19, 0);

			List<Vector3Int> path = manager.GetPathBetweenGridPositions(start, end);

			Assert.Greater(path.Count, 0, "Should find a path across the map diagonal");
			Assert.AreEqual(end, path[path.Count - 1]);
		}

		/// <summary>
		/// GetPathBetweenGridPositions with a horizontal wall of blocked cells
		/// should route around the obstacle and still find the target.
		/// </summary>
		[Test]
		public void GetPathBetweenGridPositions_WithWall_RouteGoesAround()
		{
			// Build a map with a horizontal wall at y=5 from x=1 to x=15
			var blocked = new System.Collections.Generic.List<(int, int)>();
			for (int x = 1; x <= 15; x++)
				blocked.Add((x, 5));

			(var wallMap, var wallGo) = MapManagerTestHelper.Build(20, 20, blocked.ToArray());

			var start = new Vector3Int(5, 2, 0);
			var end   = new Vector3Int(5, 10, 0);

			List<Vector3Int> path = wallMap.GetPathBetweenGridPositions(start, end);

			Assert.Greater(path.Count, 0, "Should find a path around the horizontal wall");
			Assert.AreEqual(end, path[path.Count - 1]);

			// Verify no cell in path is in the wall
			foreach (var cell in path)
			{
				bool inWall = cell.y == 5 && cell.x >= 1 && cell.x <= 15;
				Assert.IsFalse(inWall,
					$"Path should not pass through walled cell {cell}");
			}

			UnityEngine.Object.DestroyImmediate(wallGo);
		}

		/// <summary>
		/// GetPathBetweenGridPositions should not include the start position in the returned list.
		/// </summary>
		[Test]
		public void GetPathBetweenGridPositions_DoesNotIncludeStart()
		{
			var start = new Vector3Int(0, 0, 0);
			var end   = new Vector3Int(4, 4, 0);

			List<Vector3Int> path = manager.GetPathBetweenGridPositions(start, end);

			Assert.Greater(path.Count, 0);
			Assert.AreNotEqual(start, path[0],
				"Start position should not be included in the returned path");
		}

		#endregion

		#region Boundary

		/// <summary>
		/// When the target cell is completely surrounded by impassable cells,
		/// GetPathBetweenGridPositions should return an empty path.
		/// </summary>
		[Test]
		public void GetPathBetweenGridPositions_TargetEnclosed_ReturnsEmpty()
		{
			// Enclose (10,10) with walls on all 8 neighbors
			var blocked = new (int, int)[]
			{
				(9,9),(10,9),(11,9),
				(9,10),      (11,10),
				(9,11),(10,11),(11,11)
			};
			(var enclosedMap, var enclosedGo) = MapManagerTestHelper.Build(20, 20, blocked);

			var start = new Vector3Int(0, 0, 0);
			var end   = new Vector3Int(10, 10, 0);

			List<Vector3Int> path = enclosedMap.GetPathBetweenGridPositions(start, end);

			Assert.AreEqual(0, path.Count,
				"Path to enclosed target should be empty");

			UnityEngine.Object.DestroyImmediate(enclosedGo);
		}

		/// <summary>
		/// A path to a cell adjacent to the start (1 step away) should have exactly 1 element.
		/// </summary>
		[Test]
		public void GetPathBetweenGridPositions_AdjacentTarget_ReturnsOneStep()
		{
			var start = new Vector3Int(5, 5, 0);
			var end   = new Vector3Int(6, 5, 0);

			List<Vector3Int> path = manager.GetPathBetweenGridPositions(start, end);

			Assert.AreEqual(1, path.Count,
				"Path to adjacent cell should have exactly one step");
			Assert.AreEqual(end, path[0]);
		}

		#endregion

		#region GetBuildableGridPositionsNearUnit

		/// <summary>
		/// On a fully open map, all cells adjacent to a 1x1 unit should be buildable.
		/// </summary>
		[Test]
		public void GetBuildableGridPositionsNearUnit_OpenMap_ReturnsAllNeighbors()
		{
			var pos = new Vector3Int(5, 5, 0);

			List<Vector3Int> buildable = manager.GetBuildableGridPositionsNearUnit(UnitType.WORKER, pos);

			Assert.Greater(buildable.Count, 0,
				"Should return buildable positions near a WORKER on an open map");

			foreach (var cell in buildable)
				Assert.IsTrue(manager.IsGridPositionBuildable(cell),
					$"Cell {cell} returned by GetBuildableGridPositionsNearUnit should be buildable");
		}

		/// <summary>
		/// Blocking all cells around a unit should return an empty list.
		/// </summary>
		[Test]
		public void GetBuildableGridPositionsNearUnit_AllNeighborsBlocked_ReturnsEmpty()
		{
			// Block all 8 neighbors of (5,5)
			var blocked = new (int, int)[]
			{
				(4,4),(5,4),(6,4),
				(4,5),      (6,5),
				(4,6),(5,6),(6,6)
			};
			(var blockedMap, var blockedGo) = MapManagerTestHelper.Build(20, 20, blocked);

			var pos = new Vector3Int(5, 5, 0);
			List<Vector3Int> buildable = blockedMap.GetBuildableGridPositionsNearUnit(UnitType.WORKER, pos);

			Assert.AreEqual(0, buildable.Count,
				"Should return empty list when all neighbors are blocked");

			UnityEngine.Object.DestroyImmediate(blockedGo);
		}

		#endregion
	}
}
