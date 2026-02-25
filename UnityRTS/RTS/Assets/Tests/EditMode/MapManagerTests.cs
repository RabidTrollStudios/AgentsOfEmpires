using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AgentSDK;

namespace GameManager.Tests
{
	[TestFixture]
	public class MapManagerTests
	{
		private MapManager manager;
		private GameObject tilemapGo;

		[SetUp]
		public void SetUp()
		{
			(manager, tilemapGo) = MapManagerTestHelper.Build(10, 10);
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(tilemapGo);
		}

		[Test]
		public void IsAreaBuildable_WorkerOnOpen_True()
		{
			Assert.IsTrue(manager.IsAreaBuildable(UnitType.WORKER, new Vector3Int(5, 5, 0)));
		}

		[Test]
		public void IsAreaBuildable_WorkerOnBlocked_False()
		{
			var (mgr, go) = MapManagerTestHelper.Build(10, 10, new (int, int)[] { (5, 5) });
			Assert.IsFalse(mgr.IsAreaBuildable(UnitType.WORKER, new Vector3Int(5, 5, 0)));
			Object.DestroyImmediate(go);
		}

		[Test]
		public void IsAreaBuildable_Base3x3_FullyOpen_True()
		{
			// BASE is 3x3. Position (5,5) means cells (5,5), (6,5), (7,5), (5,4), (6,4), (7,4), (5,3), (6,3), (7,3)
			// using the pattern gridPosition + (i, -j) for i in [0,size.x), j in [0,size.y)
			Assert.IsTrue(manager.IsAreaBuildable(UnitType.BASE, new Vector3Int(5, 5, 0)));
		}

		[Test]
		public void IsAreaBuildable_Base3x3_PartiallyBlocked_False()
		{
			// Block one cell within the BASE footprint: (6, 4) is at offset (1, -1) from (5, 5)
			var (mgr, go) = MapManagerTestHelper.Build(10, 10, new (int, int)[] { (6, 4) });
			Assert.IsFalse(mgr.IsAreaBuildable(UnitType.BASE, new Vector3Int(5, 5, 0)));
			Object.DestroyImmediate(go);
		}

		[Test]
		public void IsAreaBuildable_AtMapEdge_Works()
		{
			// Worker at (0,0) — should be valid and buildable on a 10x10 map
			Assert.IsTrue(manager.IsAreaBuildable(UnitType.WORKER, new Vector3Int(0, 0, 0)));
		}

		[Test]
		public void IsAreaBuildable_OffMap_ReturnsFalse()
		{
			// Position (-1, 0) is out of bounds
			Assert.IsFalse(manager.IsAreaBuildable(UnitType.WORKER, new Vector3Int(-1, 0, 0)));
		}

		[Test]
		public void IsAreaBuildable_BaseOverlapsEdge_False()
		{
			// BASE at (8, 9) — extends to x=10 which is out of bounds on a 10-wide map
			Assert.IsFalse(manager.IsAreaBuildable(UnitType.BASE, new Vector3Int(8, 9, 0)));
		}

		[Test]
		public void IsBoundedAreaBuildable_Open_True()
		{
			// Worker at center of 10x10 — buffer zone all clear
			Assert.IsTrue(manager.IsBoundedAreaBuildable(UnitType.WORKER, new Vector3Int(5, 5, 0)));
		}

		[Test]
		public void IsBoundedAreaBuildable_AdjacentWall_False()
		{
			// Block (4, 5) which is one cell to the left of (5, 5), in the boundary zone
			var (mgr, go) = MapManagerTestHelper.Build(10, 10, new (int, int)[] { (4, 5) });
			Assert.IsFalse(mgr.IsBoundedAreaBuildable(UnitType.WORKER, new Vector3Int(5, 5, 0)));
			Object.DestroyImmediate(go);
		}

		[Test]
		public void SetAreaBuildability_MobileUnit_WalkableStaysTrue()
		{
			// Soldier is mobile — setting buildable to false should keep walkable true
			manager.SetAreaBuildability(UnitType.SOLDIER, new Vector3Int(5, 5, 0), false);
			Assert.IsFalse(manager.IsGridPositionBuildable(new Vector3Int(5, 5, 0)));
			Assert.IsTrue(manager.IsGridPositionWalkable(new Vector3Int(5, 5, 0)));
		}

		[Test]
		public void SetAreaBuildability_Building_WalkableFalse()
		{
			// BASE is immobile — both buildable and walkable should be false
			manager.SetAreaBuildability(UnitType.BASE, new Vector3Int(5, 5, 0), false);
			Assert.IsFalse(manager.IsGridPositionBuildable(new Vector3Int(5, 5, 0)));
			Assert.IsFalse(manager.IsGridPositionWalkable(new Vector3Int(5, 5, 0)));
		}

		[Test]
		public void SetAreaBuildability_Restore_BothTrue()
		{
			// Set then restore
			manager.SetAreaBuildability(UnitType.BASE, new Vector3Int(5, 5, 0), false);
			manager.SetAreaBuildability(UnitType.BASE, new Vector3Int(5, 5, 0), true);
			Assert.IsTrue(manager.IsGridPositionBuildable(new Vector3Int(5, 5, 0)));
			Assert.IsTrue(manager.IsGridPositionWalkable(new Vector3Int(5, 5, 0)));
		}

		[Test]
		public void GetGridPositionsNearUnit_Worker_Returns8()
		{
			// Worker is 1x1, so 8 perimeter cells around (5,5)
			var positions = manager.GetGridPositionsNearUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			Assert.AreEqual(8, positions.Count);
		}

		[Test]
		public void GetPathBetweenGridPositions_FindsPath()
		{
			var path = manager.GetPathBetweenGridPositions(new Vector3Int(0, 0, 0), new Vector3Int(9, 9, 0));
			Assert.Greater(path.Count, 0, "Should find a path across a fully open 10x10 map");
		}
	}
}
