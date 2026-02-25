using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using GameManager.GameElements;

namespace GameManager.Tests
{
	[TestFixture]
	public class GridCellTests
	{
		private GameObject go;
		private Tilemap tilemap;

		[SetUp]
		public void SetUp()
		{
			go = new GameObject("TestGridCellTilemap");
			go.AddComponent<Grid>();
			tilemap = go.AddComponent<Tilemap>();
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(go);
		}

		[Test]
		public void Constructor_DefaultsBothTrue()
		{
			var cell = new GridCell(tilemap, new Vector3Int(1, 2, 0));
			Assert.IsTrue(cell.IsBuildable());
			Assert.IsTrue(cell.IsWalkable());
		}

		[Test]
		public void SetBuildableFalse_WalkableUnchanged()
		{
			var cell = new GridCell(tilemap, new Vector3Int(0, 0, 0));
			cell.SetBuildable(false);
			Assert.IsFalse(cell.IsBuildable());
			Assert.IsTrue(cell.IsWalkable());
		}

		[Test]
		public void SetWalkableFalse_BuildableUnchanged()
		{
			var cell = new GridCell(tilemap, new Vector3Int(0, 0, 0));
			cell.SetWalkable(false);
			Assert.IsTrue(cell.IsBuildable());
			Assert.IsFalse(cell.IsWalkable());
		}

		[Test]
		public void SetBuildable_Toggle_RestoresState()
		{
			var cell = new GridCell(tilemap, new Vector3Int(0, 0, 0));
			cell.SetBuildable(false);
			Assert.IsFalse(cell.IsBuildable());
			cell.SetBuildable(true);
			Assert.IsTrue(cell.IsBuildable());
		}

		[Test]
		public void SetWalkable_Toggle_RestoresState()
		{
			var cell = new GridCell(tilemap, new Vector3Int(0, 0, 0));
			cell.SetWalkable(false);
			Assert.IsFalse(cell.IsWalkable());
			cell.SetWalkable(true);
			Assert.IsTrue(cell.IsWalkable());
		}

		[Test]
		public void GetPosition_ReturnsConstructorPosition()
		{
			var pos = new Vector3Int(3, 7, 0);
			var cell = new GridCell(tilemap, pos);
			Assert.AreEqual(new Vector3(3, 7, 0), cell.GetPosition());
		}

		[Test]
		public void IndependentState_NoCrossAffect()
		{
			var cell = new GridCell(tilemap, new Vector3Int(0, 0, 0));

			// Set buildable false, walkable should stay true
			cell.SetBuildable(false);
			Assert.IsTrue(cell.IsWalkable());

			// Now set walkable false too, then restore buildable
			cell.SetWalkable(false);
			cell.SetBuildable(true);
			Assert.IsTrue(cell.IsBuildable());
			Assert.IsFalse(cell.IsWalkable());
		}

		[Test]
		public void Constructor_ZeroPosition_Works()
		{
			var cell = new GridCell(tilemap, Vector3Int.zero);
			Assert.AreEqual(Vector3.zero, cell.GetPosition());
			Assert.IsTrue(cell.IsBuildable());
			Assert.IsTrue(cell.IsWalkable());
		}
	}
}
