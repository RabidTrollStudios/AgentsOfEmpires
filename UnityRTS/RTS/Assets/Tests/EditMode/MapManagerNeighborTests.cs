using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AgentSDK;

namespace GameManager.Tests
{
	/// <summary>
	/// Tests for MapManager neighbor-querying methods:
	/// GetGridPositionsNearUnit, GetBuildableGridPositionsNearUnit, IsNeighborOfUnit,
	/// and GetPathToUnit with simple layouts.
	/// </summary>
	[TestFixture]
	public class MapManagerNeighborTests
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
			Object.DestroyImmediate(tilemapGo);
		}

		#region GetGridPositionsNearUnit

		/// <summary>
		/// A 3x3 BASE at an interior position has 16 perimeter neighbors.
		/// The formula: 2*(size.x+2) + 2*size.y = 2*5 + 2*3 = 16.
		/// </summary>
		[Test]
		public void GetGridPositionsNearUnit_Base3x3_Interior_Returns16()
		{
			var positions = manager.GetGridPositionsNearUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			Assert.AreEqual(16, positions.Count,
				"3x3 BASE at (5,5) should have 16 perimeter cells (all within 20x20 map)");
		}

		/// <summary>
		/// A 1x1 WORKER at a map corner (0,0) should only have 3 valid neighbors
		/// (those that lie within the map bounds).
		/// </summary>
		[Test]
		public void GetGridPositionsNearUnit_Worker_AtCorner_Returns3()
		{
			// WORKER is 1x1. At (0,0) only (1,0), (0,1), (1,1) neighbors are valid.
			// But the perimeter ring is: top row [(-1,1),(0,1),(1,1)], bottom row [(-1,-1),(0,-1),(1,-1)],
			// left col [(-1,0)] and right col [(1,0)].
			// Valid ones: (0,1),(1,1),(1,0) = 3
			var positions = manager.GetGridPositionsNearUnit(UnitType.WORKER, new Vector3Int(0, 0, 0));
			Assert.AreEqual(3, positions.Count,
				"WORKER at corner (0,0) should have 3 valid neighbors");
		}

		/// <summary>
		/// A 1x1 WORKER at a map edge (0,5) — against the left wall.
		/// Expected neighbors: (0,6),(1,6) from top; (0,4),(1,4) from bottom; (1,5) from right.
		/// = 5 valid neighbors.
		/// </summary>
		[Test]
		public void GetGridPositionsNearUnit_Worker_AtLeftEdge_Returns5()
		{
			var positions = manager.GetGridPositionsNearUnit(UnitType.WORKER, new Vector3Int(0, 5, 0));
			Assert.AreEqual(5, positions.Count,
				"WORKER at left edge (0,5) should have 5 valid neighbors");
		}

		/// <summary>
		/// A 3x3 BASE clipped by the map boundary should have fewer than 16 neighbors.
		/// </summary>
		[Test]
		public void GetGridPositionsNearUnit_Base3x3_AtEdge_FewerNeighbors()
		{
			// BASE at (0,5): left edge clips columns at x=-1, reducing count
			var positions = manager.GetGridPositionsNearUnit(UnitType.BASE, new Vector3Int(0, 5, 0));
			Assert.Less(positions.Count, 16,
				"3x3 BASE at left edge should have fewer than 16 valid perimeter cells");
		}

		/// <summary>
		/// No position in the returned list should be inside the building footprint itself.
		/// </summary>
		[Test]
		public void GetGridPositionsNearUnit_NoneInsideFootprint()
		{
			var basePos = new Vector3Int(5, 5, 0);
			var size = Constants.UNIT_SIZE[UnitType.BASE];

			// Build the footprint set
			var footprint = new HashSet<Vector3Int>();
			for (int i = 0; i < size.x; i++)
				for (int j = 0; j < size.y; j++)
					footprint.Add(basePos + new Vector3Int(i, -j, 0));

			var neighbors = manager.GetGridPositionsNearUnit(UnitType.BASE, basePos);
			foreach (var pos in neighbors)
			{
				Assert.IsFalse(footprint.Contains(pos),
					$"Neighbor position {pos} is inside the building footprint");
			}
		}

		#endregion

		#region GetBuildableGridPositionsNearUnit

		/// <summary>
		/// On a fully open map all perimeter cells of a 3x3 BASE are buildable.
		/// </summary>
		[Test]
		public void GetBuildableGridPositionsNearUnit_OpenMap_EqualsAll()
		{
			var basePos = new Vector3Int(5, 5, 0);
			var all = manager.GetGridPositionsNearUnit(UnitType.BASE, basePos);
			var buildable = manager.GetBuildableGridPositionsNearUnit(UnitType.BASE, basePos);
			Assert.AreEqual(all.Count, buildable.Count,
				"On an open map all perimeter cells should be buildable");
		}

		/// <summary>
		/// Blocking one perimeter cell reduces GetBuildable count by exactly 1.
		/// </summary>
		[Test]
		public void GetBuildableGridPositionsNearUnit_OneBlocked_ReducedByOne()
		{
			var basePos = new Vector3Int(5, 5, 0);
			var all = manager.GetGridPositionsNearUnit(UnitType.BASE, basePos);
			int originalCount = manager.GetBuildableGridPositionsNearUnit(UnitType.BASE, basePos).Count;

			// Block the first neighbor
			var toBlock = all[0];
			manager.GridCells[toBlock.x, toBlock.y].SetBuildable(false);

			var afterBlock = manager.GetBuildableGridPositionsNearUnit(UnitType.BASE, basePos);
			Assert.AreEqual(originalCount - 1, afterBlock.Count,
				"Blocking one cell should reduce buildable neighbor count by 1");
		}

		/// <summary>
		/// Blocking all perimeter cells leaves an empty buildable list.
		/// </summary>
		[Test]
		public void GetBuildableGridPositionsNearUnit_AllBlocked_ReturnsEmpty()
		{
			var basePos = new Vector3Int(5, 5, 0);
			var all = manager.GetGridPositionsNearUnit(UnitType.BASE, basePos);
			foreach (var pos in all)
				manager.GridCells[pos.x, pos.y].SetBuildable(false);

			var buildable = manager.GetBuildableGridPositionsNearUnit(UnitType.BASE, basePos);
			Assert.AreEqual(0, buildable.Count,
				"All neighbors blocked should yield an empty buildable list");
		}

		#endregion

		#region IsNeighborOfUnit

		/// <summary>
		/// A cell immediately adjacent to a 1x1 WORKER is a neighbor.
		/// </summary>
		[Test]
		public void IsNeighborOfUnit_AdjacentCell_ReturnsTrue()
		{
			var unitPos = new Vector3Int(10, 10, 0);
			var adjacent = new Vector3Int(10, 11, 0); // directly north
			Assert.IsTrue(manager.IsNeighborOfUnit(adjacent, UnitType.WORKER, unitPos),
				"Cell directly north of WORKER should be a neighbor");
		}

		/// <summary>
		/// A cell two steps away from the unit is not a neighbor.
		/// </summary>
		[Test]
		public void IsNeighborOfUnit_TwoStepsAway_ReturnsFalse()
		{
			var unitPos = new Vector3Int(10, 10, 0);
			var farCell = new Vector3Int(10, 12, 0); // two cells north
			Assert.IsFalse(manager.IsNeighborOfUnit(farCell, UnitType.WORKER, unitPos),
				"Cell two steps away should not be a neighbor of a 1x1 WORKER");
		}

		/// <summary>
		/// The anchor cell of the unit itself is not in the neighbor list.
		/// </summary>
		[Test]
		public void IsNeighborOfUnit_SameCell_ReturnsFalse()
		{
			var unitPos = new Vector3Int(10, 10, 0);
			Assert.IsFalse(manager.IsNeighborOfUnit(unitPos, UnitType.WORKER, unitPos),
				"The unit's own cell should not be reported as a neighbor");
		}

		/// <summary>
		/// Cells adjacent to any side of a 3x3 BASE are neighbors.
		/// </summary>
		[Test]
		public void IsNeighborOfUnit_Base3x3_VariousSides_AllTrue()
		{
			var basePos = new Vector3Int(5, 5, 0);
			// Cells just outside each side of the 3x3 footprint
			var testCells = new[]
			{
				new Vector3Int(5, 6, 0),   // north of top row
				new Vector3Int(5, 2, 0),   // south of bottom row
				new Vector3Int(4, 5, 0),   // west of leftmost col
				new Vector3Int(8, 5, 0),   // east of rightmost col
			};

			foreach (var cell in testCells)
			{
				Assert.IsTrue(manager.IsNeighborOfUnit(cell, UnitType.BASE, basePos),
					$"Cell {cell} should be a neighbor of 3x3 BASE at {basePos}");
			}
		}

		#endregion

		#region GetPathToUnit

		/// <summary>
		/// GetPathToUnit should find a non-empty path to a reachable unit on an open map.
		/// </summary>
		[Test]
		public void GetPathToUnit_OpenMap_FindsPath()
		{
			var from = new Vector3Int(2, 2, 0);
			var targetPos = new Vector3Int(10, 10, 0);
			var path = manager.GetPathToUnit(from, UnitType.BASE, targetPos);
			Assert.Greater(path.Count, 0,
				"Should find a path to BASE at (10,10) from (2,2) on open 20x20 map");
		}

		/// <summary>
		/// When the caller is already adjacent to the target, the returned path
		/// should be very short (just the adjacent cell itself).
		/// </summary>
		[Test]
		public void GetPathToUnit_CallerAlreadyAdjacent_ShortPath()
		{
			var targetPos = new Vector3Int(10, 10, 0);
			var from = new Vector3Int(9, 10, 0); // directly west, a neighbor of a 3x3 BASE at (10,10)

			// Mark the building footprint as not-buildable (as if a building were there)
			var size = Constants.UNIT_SIZE[UnitType.BASE];
			for (int i = 0; i < size.x; i++)
				for (int j = 0; j < size.y; j++)
					manager.GridCells[targetPos.x + i, targetPos.y - j].SetBuildable(false);

			var path = manager.GetPathToUnit(from, UnitType.BASE, targetPos);
			// Path should end at the adjacent cell (from is already a neighbor)
			Assert.LessOrEqual(path.Count, 2,
				"Caller already adjacent to target should yield a very short path");
		}

		#endregion
	}
}
