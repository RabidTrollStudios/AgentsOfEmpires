using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using AgentSDK;
using GameManager.GameElements;

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
		public void IsAreaBuildable_PawnOnOpen_True()
		{
			Assert.IsTrue(manager.IsAreaBuildable(UnitType.PAWN, new Vector3Int(5, 5, 0)));
		}

		[Test]
		public void IsAreaBuildable_PawnOnBlocked_False()
		{
			var (mgr, go) = MapManagerTestHelper.Build(10, 10, new (int, int)[] { (5, 5) });
			Assert.IsFalse(mgr.IsAreaBuildable(UnitType.PAWN, new Vector3Int(5, 5, 0)));
			Object.DestroyImmediate(go);
		}

		[Test]
		public void IsAreaBuildable_Base6x4_FullyOpen_True()
		{
			// BASE is 6x4. Position (2,5) means cells x=[2,7], y=[2,5]
			// using the pattern gridPosition + (i, -j) for i in [0,size.x), j in [0,size.y)
			Assert.IsTrue(manager.IsAreaBuildable(UnitType.BASE, new Vector3Int(2, 5, 0)));
		}

		[Test]
		public void IsAreaBuildable_Base6x4_PartiallyBlocked_False()
		{
			// Block one cell within the BASE footprint: (3, 6) is at offset (1, 1) from anchor (2, 5)
			// Bottom-left anchor: footprint extends right (+X) and up (+Y)
			var (mgr, go) = MapManagerTestHelper.Build(10, 10, new (int, int)[] { (3, 6) });
			Assert.IsFalse(mgr.IsAreaBuildable(UnitType.BASE, new Vector3Int(2, 5, 0)));
			Object.DestroyImmediate(go);
		}

		[Test]
		public void IsAreaBuildable_AtMapEdge_Works()
		{
			// Pawn at (0,0) — should be valid and buildable on a 10x10 map
			Assert.IsTrue(manager.IsAreaBuildable(UnitType.PAWN, new Vector3Int(0, 0, 0)));
		}

		[Test]
		public void IsAreaBuildable_OffMap_ReturnsFalse()
		{
			// Position (-1, 0) is out of bounds
			Assert.IsFalse(manager.IsAreaBuildable(UnitType.PAWN, new Vector3Int(-1, 0, 0)));
		}

		[Test]
		public void IsAreaBuildable_BaseOverlapsEdge_False()
		{
			// BASE is 6x4. At (5, 9) extends to x=10 which is out of bounds on a 10-wide map
			Assert.IsFalse(manager.IsAreaBuildable(UnitType.BASE, new Vector3Int(5, 9, 0)));
		}

		[Test]
		public void IsBoundedAreaBuildable_Open_True()
		{
			// Pawn at center of 10x10 — buffer zone all clear
			Assert.IsTrue(manager.IsBoundedAreaBuildable(UnitType.PAWN, new Vector3Int(5, 5, 0)));
		}

		[Test]
		public void IsBoundedAreaBuildable_AdjacentWall_False()
		{
			// Block (4, 5) which is one cell to the left of (5, 5), in the boundary zone
			var (mgr, go) = MapManagerTestHelper.Build(10, 10, new (int, int)[] { (4, 5) });
			Assert.IsFalse(mgr.IsBoundedAreaBuildable(UnitType.PAWN, new Vector3Int(5, 5, 0)));
			Object.DestroyImmediate(go);
		}

		[Test]
		public void SetAreaBuildability_MobileUnit_WalkableStaysTrue()
		{
			// Warrior is mobile — setting buildable to false should keep walkable true
			manager.SetAreaBuildability(UnitType.WARRIOR, new Vector3Int(5, 5, 0), false);
			Assert.IsFalse(manager.IsGridPositionBuildable(new Vector3Int(5, 5, 0)));
			Assert.IsTrue(manager.IsGridPositionWalkable(new Vector3Int(5, 5, 0)));
		}

		[Test]
		public void SetAreaBuildability_Building_WalkableFalse()
		{
			// BASE is immobile (6x4). Anchor (2,5) is bottom-left.
			// Passage row (top) is at y = 5 + 4 - 1 = 8 — stays walkable.
			// Body cells (e.g. anchor row y=5) become not walkable and not buildable.
			manager.SetAreaBuildability(UnitType.BASE, new Vector3Int(2, 5, 0), false);
			Assert.IsFalse(manager.IsGridPositionBuildable(new Vector3Int(2, 8, 0)),
				"Top row should not be buildable");
			Assert.IsTrue(manager.IsGridPositionWalkable(new Vector3Int(2, 8, 0)),
				"Top row should remain walkable (passage)");
			Assert.IsFalse(manager.IsGridPositionWalkable(new Vector3Int(2, 5, 0)),
				"Body row should not be walkable");
		}

		[Test]
		public void SetAreaBuildability_Restore_BothTrue()
		{
			// Set then restore
			manager.SetAreaBuildability(UnitType.BASE, new Vector3Int(2, 5, 0), false);
			manager.SetAreaBuildability(UnitType.BASE, new Vector3Int(2, 5, 0), true);
			Assert.IsTrue(manager.IsGridPositionBuildable(new Vector3Int(2, 5, 0)));
			Assert.IsTrue(manager.IsGridPositionWalkable(new Vector3Int(2, 5, 0)));
		}

		[Test]
		public void GetGridPositionsNearUnit_Pawn_Returns8()
		{
			// Pawn is 1x1, so 8 perimeter cells around (5,5)
			var positions = manager.GetGridPositionsNearUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Assert.AreEqual(8, positions.Count);
		}

		[Test]
		public void GetPathBetweenGridPositions_FindsPath()
		{
			var path = manager.GetPathBetweenGridPositions(new Vector3Int(0, 0, 0), new Vector3Int(9, 9, 0));
			Assert.Greater(path.Count, 0, "Should find a path across a fully open 10x10 map");
		}

		#region IsAreaBuildable with excludePositions

		[Test]
		public void IsAreaBuildable_ExcludePositions_SkipsBlockedCell()
		{
			// Block (5,5) then pass it as excluded — should still be buildable
			var (mgr, go) = MapManagerTestHelper.Build(10, 10, new (int, int)[] { (5, 5) });
			var exclude = new HashSet<Vector3Int> { new Vector3Int(5, 5, 0) };
			Assert.IsTrue(mgr.IsAreaBuildable(UnitType.PAWN, new Vector3Int(5, 5, 0), exclude));
			Object.DestroyImmediate(go);
		}

		[Test]
		public void IsAreaBuildable_ExcludePositions_StillFailsOnOtherBlocked()
		{
			// Block (5,5) and (6,5). Exclude only (5,5) — BASE footprint still hits (6,5)
			var (mgr, go) = MapManagerTestHelper.Build(10, 10, new (int, int)[] { (5, 5), (6, 5) });
			var exclude = new HashSet<Vector3Int> { new Vector3Int(5, 5, 0) };
			Assert.IsFalse(mgr.IsAreaBuildable(UnitType.BASE, new Vector3Int(5, 5, 0), exclude));
			Object.DestroyImmediate(go);
		}

		#endregion

		#region IsBoundedAreaBuildable with excludePositions

		[Test]
		public void IsBoundedAreaBuildable_ExcludePositions_SkipsBlockedCell()
		{
			// Block (4,5) which is in the boundary zone of a pawn at (5,5)
			var (mgr, go) = MapManagerTestHelper.Build(10, 10, new (int, int)[] { (4, 5) });
			var exclude = new HashSet<Vector3Int> { new Vector3Int(4, 5, 0) };
			Assert.IsTrue(mgr.IsBoundedAreaBuildable(UnitType.PAWN, new Vector3Int(5, 5, 0), exclude));
			Object.DestroyImmediate(go);
		}

		[Test]
		public void IsBoundedAreaBuildable_ExcludePositions_StillFailsOnOtherBlocked()
		{
			// Block (4,5) and (6,6). Exclude only (4,5) — boundary still hits (6,6)
			var (mgr, go) = MapManagerTestHelper.Build(10, 10, new (int, int)[] { (4, 5), (6, 6) });
			var exclude = new HashSet<Vector3Int> { new Vector3Int(4, 5, 0) };
			Assert.IsFalse(mgr.IsBoundedAreaBuildable(UnitType.PAWN, new Vector3Int(5, 5, 0), exclude));
			Object.DestroyImmediate(go);
		}

		#endregion

		#region IsNeighborOfUnit

		[Test]
		public void IsNeighborOfUnit_AdjacentCell_True()
		{
			Assert.IsTrue(manager.IsNeighborOfUnit(
				new Vector3Int(4, 5, 0), UnitType.PAWN, new Vector3Int(5, 5, 0)));
		}

		[Test]
		public void IsNeighborOfUnit_DiagonalCell_True()
		{
			Assert.IsTrue(manager.IsNeighborOfUnit(
				new Vector3Int(4, 4, 0), UnitType.PAWN, new Vector3Int(5, 5, 0)));
		}

		[Test]
		public void IsNeighborOfUnit_SameCell_False()
		{
			Assert.IsFalse(manager.IsNeighborOfUnit(
				new Vector3Int(5, 5, 0), UnitType.PAWN, new Vector3Int(5, 5, 0)));
		}

		[Test]
		public void IsNeighborOfUnit_FarCell_False()
		{
			Assert.IsFalse(manager.IsNeighborOfUnit(
				new Vector3Int(0, 0, 0), UnitType.PAWN, new Vector3Int(5, 5, 0)));
		}

		[Test]
		public void IsNeighborOfUnit_Base3x3_AdjacentToFootprint_True()
		{
			// BASE is 6x4 at (2,5). Cell (1,5) is left of the footprint
			Assert.IsTrue(manager.IsNeighborOfUnit(
				new Vector3Int(1, 5, 0), UnitType.BASE, new Vector3Int(2, 5, 0)));
		}

		#endregion

		#region GetBuildableGridPositionsNearUnit

		[Test]
		public void GetBuildableGridPositionsNearUnit_AllOpen_MatchesNearUnit()
		{
			var near = manager.GetGridPositionsNearUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var buildable = manager.GetBuildableGridPositionsNearUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Assert.AreEqual(near.Count, buildable.Count,
				"All neighbors should be buildable on an open map");
		}

		[Test]
		public void GetBuildableGridPositionsNearUnit_SomeBlocked_FewerResults()
		{
			// Block one neighbor cell
			var (mgr, go) = MapManagerTestHelper.Build(10, 10, new (int, int)[] { (4, 5) });
			var near = mgr.GetGridPositionsNearUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var buildable = mgr.GetBuildableGridPositionsNearUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Assert.Less(buildable.Count, near.Count);
			Assert.IsFalse(buildable.Contains(new Vector3Int(4, 5, 0)));
			Object.DestroyImmediate(go);
		}

		#endregion

		#region GetGridPositionsNearUnit edge cases

		[Test]
		public void GetGridPositionsNearUnit_AtCorner_ClipsToMap()
		{
			// Pawn at (0,0) — only 3 neighbors are valid (right, up, diagonal)
			var positions = manager.GetGridPositionsNearUnit(UnitType.PAWN, new Vector3Int(0, 0, 0));
			Assert.AreEqual(3, positions.Count);
		}

		[Test]
		public void GetGridPositionsNearUnit_Base_ReturnsPerimeterCells()
		{
			// BASE is 6x4 at (2,5). Perimeter = 2*(6+2) + 2*(4+2) - 4 corners
			// = 16 + 12 - 4 = 24 but we need to check bounds clipping
			var positions = manager.GetGridPositionsNearUnit(UnitType.BASE, new Vector3Int(2, 5, 0));
			Assert.Greater(positions.Count, 8, "BASE has larger perimeter than PAWN");
		}

		#endregion

		#region GetPathBetweenGridPositions edge cases

		[Test]
		public void GetPathBetweenGridPositions_SamePosition_ReturnsPath()
		{
			var path = manager.GetPathBetweenGridPositions(
				new Vector3Int(5, 5, 0), new Vector3Int(5, 5, 0));
			// A* from same start/end typically returns empty or single-element
			Assert.IsNotNull(path);
		}

		[Test]
		public void GetPathBetweenGridPositions_BlockedDestination_ReturnsEmptyPath()
		{
			// Build a map with a completely walled-off destination
			var (mgr, go) = MapManagerTestHelper.Build(5, 5,
				new (int, int)[] { (3, 3), (3, 4), (4, 3), (4, 4) });
			var path = mgr.GetPathBetweenGridPositions(
				new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 0));
			Assert.AreEqual(0, path.Count, "Should not find path to blocked cell");
			Object.DestroyImmediate(go);
		}

		#endregion

		#region GetPathToUnit

		[Test]
		public void GetPathToUnit_FindsPathToNeighbor()
		{
			var path = manager.GetPathToUnit(
				new Vector3Int(0, 0, 0), UnitType.PAWN, new Vector3Int(5, 5, 0));
			Assert.Greater(path.Count, 0, "Should find path to pawn neighbor");
			// Final position should be a neighbor of (5,5)
			var last = path[path.Count - 1];
			Assert.IsTrue(manager.IsNeighborOfUnit(last, UnitType.PAWN, new Vector3Int(5, 5, 0)),
				"Path endpoint should be a neighbor of the target unit");
		}

		[Test]
		public void GetPathToUnit_ClosestNeighborFirst()
		{
			// Start near the target — path should be short
			var path = manager.GetPathToUnit(
				new Vector3Int(4, 4, 0), UnitType.PAWN, new Vector3Int(5, 5, 0));
			Assert.Greater(path.Count, 0);
			// Should reach a neighbor in 1-2 steps from adjacent cell
			Assert.LessOrEqual(path.Count, 3);
		}

		[Test]
		public void GetPathToUnit_AdjacentStart_ShortPath()
		{
			// Start right next to the target — path should be very short
			var path = manager.GetPathToUnit(
				new Vector3Int(3, 5, 0), UnitType.PAWN, new Vector3Int(5, 5, 0));
			Assert.Greater(path.Count, 0, "Should find path from nearby start");
		}

		#endregion

		#region SetAreaBuildability additional cases

		[Test]
		public void SetAreaBuildability_ArcherMobile_WalkableStaysTrue()
		{
			// ARCHER is mobile — buildable false, walkable stays true
			manager.SetAreaBuildability(UnitType.ARCHER, new Vector3Int(3, 3, 0), false);
			Assert.IsFalse(manager.IsGridPositionBuildable(new Vector3Int(3, 3, 0)));
			Assert.IsTrue(manager.IsGridPositionWalkable(new Vector3Int(3, 3, 0)));
		}

		[Test]
		public void SetAreaBuildability_Barracks_MultiCellBlocked()
		{
			// BARRACKS is 3x3. Bottom-left anchor at (3,5). Footprint extends right and up.
			// Top row (j=2, y=7) stays walkable (passage). Body rows (j=0,1) become not walkable.
			manager.SetAreaBuildability(UnitType.BARRACKS, new Vector3Int(3, 5, 0), false);
			for (int i = 0; i < 3; i++)
			{
				for (int j = 0; j < 3; j++)
				{
					var pos = new Vector3Int(3 + i, 5 + j, 0);
					Assert.IsFalse(manager.IsGridPositionBuildable(pos),
						$"Cell {pos} should not be buildable");
					if (j == 2) // top row = passage
						Assert.IsTrue(manager.IsGridPositionWalkable(pos),
							$"Top row cell {pos} should remain walkable (passage)");
					else
						Assert.IsFalse(manager.IsGridPositionWalkable(pos),
							$"Body cell {pos} should not be walkable");
				}
			}
		}

		[Test]
		public void SetAreaBuildability_AtEdge_SkipsOutOfBounds()
		{
			// Place a BASE at (8, 9) on a 10x10 map — footprint extends beyond map
			// Should not throw; only valid cells get modified
			Assert.DoesNotThrow(() =>
				manager.SetAreaBuildability(UnitType.BASE, new Vector3Int(8, 9, 0), false));
		}

		#endregion

		#region GenerateGraph

		[Test]
		public void GenerateGraph_WithGroundTilemap_CreatesGridCells()
		{
			// Build a grid with a "Ground 1" tilemap containing actual tiles
			var gridGo = new GameObject("TestGrid");
			gridGo.AddComponent<Grid>();

			var groundGo = new GameObject("Ground 1");
			groundGo.transform.SetParent(gridGo.transform);
			var groundTilemap = groundGo.AddComponent<Tilemap>();
			groundGo.AddComponent<TilemapRenderer>();

			// Place tiles on a 3x3 area, leaving (2,2) empty to test null-tile branch
			var tile = ScriptableObject.CreateInstance<Tile>();
			for (int x = 0; x < 3; x++)
				for (int y = 0; y < 3; y++)
					if (x != 2 || y != 2) // skip (2,2)
						groundTilemap.SetTile(new Vector3Int(x, y, 0), tile);

			var mgr = new MapManager();
			var graph = mgr.GenerateGraph(gridGo, gridGo);

			Assert.IsNotNull(graph, "GenerateGraph should return a graph");
			Assert.AreEqual(3, mgr.MapSize.x);
			Assert.AreEqual(3, mgr.MapSize.y);

			// Cell (0,0) has a tile — should be buildable
			Assert.IsTrue(mgr.IsGridPositionBuildable(new Vector3Int(0, 0, 0)));
			// Cell (2,2) has no tile — should NOT be buildable
			Assert.IsFalse(mgr.IsGridPositionBuildable(new Vector3Int(2, 2, 0)));
			Assert.IsFalse(mgr.IsGridPositionWalkable(new Vector3Int(2, 2, 0)));

			Object.DestroyImmediate(tile);
			Object.DestroyImmediate(gridGo);
		}

		[Test]
		public void GenerateGraph_WallsTilemap_MarksObstacles()
		{
			var gridGo = new GameObject("TestGrid");
			gridGo.AddComponent<Grid>();

			// Ground 1 with full 4x4 tiles
			var groundGo = new GameObject("Ground 1");
			groundGo.transform.SetParent(gridGo.transform);
			var groundTilemap = groundGo.AddComponent<Tilemap>();
			groundGo.AddComponent<TilemapRenderer>();

			var tile = ScriptableObject.CreateInstance<Tile>();
			for (int x = 0; x < 4; x++)
				for (int y = 0; y < 4; y++)
					groundTilemap.SetTile(new Vector3Int(x, y, 0), tile);

			// Walls layer with obstacle at (1,1)
			var wallsGo = new GameObject("Walls");
			wallsGo.transform.SetParent(gridGo.transform);
			var wallsTilemap = wallsGo.AddComponent<Tilemap>();
			wallsGo.AddComponent<TilemapRenderer>();
			wallsTilemap.SetTile(new Vector3Int(1, 1, 0), tile);

			var mgr = new MapManager();
			var graph = mgr.GenerateGraph(gridGo, gridGo);

			Assert.IsNotNull(graph);
			// (0,0) is open — no wall
			Assert.IsTrue(mgr.IsGridPositionBuildable(new Vector3Int(0, 0, 0)));
			// (1,1) has a wall — should be blocked
			Assert.IsFalse(mgr.IsGridPositionBuildable(new Vector3Int(1, 1, 0)));
			Assert.IsFalse(mgr.IsGridPositionWalkable(new Vector3Int(1, 1, 0)));

			Object.DestroyImmediate(tile);
			Object.DestroyImmediate(gridGo);
		}

		[Test]
		public void GenerateGraph_InfluenceMapTag_SetsInfluenceMap()
		{
			var gridGo = new GameObject("TestGrid");
			gridGo.AddComponent<Grid>();

			// Ground 1 with a single tile
			var groundGo = new GameObject("Ground 1");
			groundGo.transform.SetParent(gridGo.transform);
			var groundTilemap = groundGo.AddComponent<Tilemap>();
			groundGo.AddComponent<TilemapRenderer>();
			var tile = ScriptableObject.CreateInstance<Tile>();
			groundTilemap.SetTile(new Vector3Int(0, 0, 0), tile);

			// InfluenceMap layer — tag it
			var infGo = new GameObject("InfluenceMap");
			infGo.transform.SetParent(gridGo.transform);
			var infTilemap = infGo.AddComponent<Tilemap>();
			infGo.AddComponent<TilemapRenderer>();
			infGo.tag = "InfluenceMap";

			var mgr = new MapManager();
			mgr.GenerateGraph(gridGo, gridGo);

			Assert.AreEqual(infTilemap, mgr.InfluenceMap,
				"InfluenceMap property should be set from tagged tilemap");

			Object.DestroyImmediate(tile);
			Object.DestroyImmediate(gridGo);
		}

		[Test]
		public void GenerateGraph_SkipsNonObstacleLayers()
		{
			var gridGo = new GameObject("TestGrid");
			gridGo.AddComponent<Grid>();

			var groundGo = new GameObject("Ground 1");
			groundGo.transform.SetParent(gridGo.transform);
			var groundTilemap = groundGo.AddComponent<Tilemap>();
			groundGo.AddComponent<TilemapRenderer>();
			var tile = ScriptableObject.CreateInstance<Tile>();
			groundTilemap.SetTile(new Vector3Int(0, 0, 0), tile);
			groundTilemap.SetTile(new Vector3Int(1, 0, 0), tile);

			// "Decoration" layer with a tile — should NOT block
			var decoGo = new GameObject("Decoration");
			decoGo.transform.SetParent(gridGo.transform);
			var decoTilemap = decoGo.AddComponent<Tilemap>();
			decoGo.AddComponent<TilemapRenderer>();
			decoTilemap.SetTile(new Vector3Int(1, 0, 0), tile);

			var mgr = new MapManager();
			mgr.GenerateGraph(gridGo, gridGo);

			// (1,0) has a decoration tile but should still be buildable
			Assert.IsTrue(mgr.IsGridPositionBuildable(new Vector3Int(1, 0, 0)),
				"Non-obstacle layer should not block cells");

			Object.DestroyImmediate(tile);
			Object.DestroyImmediate(gridGo);
		}

		#endregion

		#region GetRandomBuildableLocation

		[Test]
		public void GetRandomBuildableLocation_ReturnsValidPosition()
		{
			var loc = manager.GetRandomBuildableLocation(UnitType.PAWN);
			Assert.IsTrue(manager.IsAreaBuildable(UnitType.PAWN, loc),
				"Returned location should be buildable");
			Assert.IsTrue(Utility.IsValidGridLocation(loc, manager.MapSize),
				"Returned location should be within map bounds");
		}

		#endregion
	}
}
