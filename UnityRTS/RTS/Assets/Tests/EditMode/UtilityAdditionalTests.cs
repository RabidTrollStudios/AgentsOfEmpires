using NUnit.Framework;
using UnityEngine;
using GameManager.GameElements;
using GameManager.EnumTypes;

namespace GameManager.Tests
{
	/// <summary>
	/// Additional tests for Utility covering WorldToGrid/GridToWorld conversions,
	/// round-trip encoding integrity, non-square map sizes, and non-origin
	/// start positions in ConvertPositionToDirection.
	/// </summary>
	[TestFixture]
	public class UtilityAdditionalTests
	{
		#region WorldToGrid / GridToWorld

		[Test]
		public void WorldToGrid_TruncatesDecimalPart()
		{
			// Fractional world positions should be truncated (int cast)
			var result = Utility.WorldToGrid(new Vector3(3.9f, 7.1f, 0f));
			Assert.AreEqual(new Vector3Int(3, 7, 0), result);
		}

		[Test]
		public void WorldToGrid_NegativeValues_TruncatesCorrectly()
		{
			// C# int cast truncates toward zero, so -1.9f becomes -1
			var result = Utility.WorldToGrid(new Vector3(-1.9f, -0.1f, 0f));
			Assert.AreEqual(new Vector3Int(-1, 0, 0), result);
		}

		[Test]
		public void GridToWorld_PreservesXYSetsZToZero()
		{
			var result = Utility.GridToWorld(new Vector3Int(4, 9, 0));
			Assert.AreEqual(4f, result.x, 0.0001f);
			Assert.AreEqual(9f, result.y, 0.0001f);
			Assert.AreEqual(0f, result.z, 0.0001f);
		}

		[Test]
		public void GridToWorld_ZeroPosition_ReturnsOrigin()
		{
			var result = Utility.GridToWorld(Vector3Int.zero);
			Assert.AreEqual(Vector3.zero, result);
		}

		[Test]
		public void WorldToGrid_GridToWorld_RoundTrip()
		{
			// World → Grid → World should be lossless for integer-valued world coords
			var original = new Vector3(5f, 12f, 0f);
			var grid = Utility.WorldToGrid(original);
			var back = Utility.GridToWorld(grid);
			Assert.AreEqual(original.x, back.x, 0.0001f);
			Assert.AreEqual(original.y, back.y, 0.0001f);
		}

		#endregion

		#region GridToInt / IntToGrid Round-Trips

		[Test]
		public void GridToInt_IntToGrid_RoundTrip_Square()
		{
			var mapSize = new Vector3Int(30, 30, 0);
			var positions = new[]
			{
				new Vector3Int(0, 0, 0),
				new Vector3Int(0, 29, 0),
				new Vector3Int(29, 0, 0),
				new Vector3Int(29, 29, 0),
				new Vector3Int(15, 15, 0),
			};

			foreach (var pos in positions)
			{
				int encoded = Utility.GridToInt(pos, mapSize);
				var decoded = Utility.IntToGrid(encoded, mapSize);
				Assert.AreEqual(pos, decoded,
					$"Round-trip failed for position {pos} on 30x30 map");
			}
		}

		[Test]
		public void GridToInt_IntToGrid_RoundTrip_NonSquare()
		{
			// Non-square map: width=20, height=15
			var mapSize = new Vector3Int(20, 15, 0);
			var positions = new[]
			{
				new Vector3Int(0, 0, 0),
				new Vector3Int(0, 14, 0),
				new Vector3Int(19, 0, 0),
				new Vector3Int(19, 14, 0),
				new Vector3Int(10, 7, 0),
			};

			foreach (var pos in positions)
			{
				int encoded = Utility.GridToInt(pos, mapSize);
				var decoded = Utility.IntToGrid(encoded, mapSize);
				Assert.AreEqual(pos, decoded,
					$"Round-trip failed for {pos} on 20x15 map");
			}
		}

		[Test]
		public void GridToInt_UniquePerCell_SquareMap()
		{
			// Every cell on a 5x5 map should have a unique encoded integer
			var mapSize = new Vector3Int(5, 5, 0);
			var seen = new System.Collections.Generic.HashSet<int>();
			for (int x = 0; x < 5; x++)
			{
				for (int y = 0; y < 5; y++)
				{
					int encoded = Utility.GridToInt(new Vector3Int(x, y, 0), mapSize);
					Assert.IsTrue(seen.Add(encoded),
						$"Duplicate encoding {encoded} at ({x},{y})");
				}
			}
		}

		#endregion

		#region IsValidGridLocation – edge cases

		[Test]
		public void IsValidGridLocation_MaxCorner_Valid()
		{
			var mapSize = new Vector3Int(30, 30, 0);
			Assert.IsTrue(Utility.IsValidGridLocation(new Vector3Int(29, 29, 0), mapSize));
		}

		[Test]
		public void IsValidGridLocation_ExactSizeX_Invalid()
		{
			var mapSize = new Vector3Int(30, 30, 0);
			// x == mapSize.x is out of bounds
			Assert.IsFalse(Utility.IsValidGridLocation(new Vector3Int(30, 15, 0), mapSize));
		}

		[Test]
		public void IsValidGridLocation_ExactSizeY_Invalid()
		{
			var mapSize = new Vector3Int(30, 30, 0);
			Assert.IsFalse(Utility.IsValidGridLocation(new Vector3Int(15, 30, 0), mapSize));
		}

		[Test]
		public void IsValidGridLocation_NonSquareMap_ValidCorners()
		{
			var mapSize = new Vector3Int(20, 10, 0);
			Assert.IsTrue(Utility.IsValidGridLocation(new Vector3Int(0, 0, 0), mapSize));
			Assert.IsTrue(Utility.IsValidGridLocation(new Vector3Int(19, 9, 0), mapSize));
			Assert.IsFalse(Utility.IsValidGridLocation(new Vector3Int(20, 0, 0), mapSize));
			Assert.IsFalse(Utility.IsValidGridLocation(new Vector3Int(0, 10, 0), mapSize));
		}

		#endregion

		#region ConvertPositionToDirection – non-origin start

		[Test]
		public void ConvertPositionToDirection_NonOriginStart_N()
		{
			// Moving north from (5,5) to (5,6)
			var result = Utility.ConvertPositionToDirection(new Vector3Int(5, 5, 0), new Vector3Int(5, 6, 0));
			Assert.AreEqual(Direction.N, result);
		}

		[Test]
		public void ConvertPositionToDirection_NonOriginStart_SE()
		{
			// Moving SE from (10, 8) to (11, 7)
			var result = Utility.ConvertPositionToDirection(new Vector3Int(10, 8, 0), new Vector3Int(11, 7, 0));
			Assert.AreEqual(Direction.SE, result);
		}

		[Test]
		public void ConvertPositionToDirection_NonOriginStart_W()
		{
			// Moving west from (15, 15) to (14, 15)
			var result = Utility.ConvertPositionToDirection(new Vector3Int(15, 15, 0), new Vector3Int(14, 15, 0));
			Assert.AreEqual(Direction.W, result);
		}

		[Test]
		public void ConvertPositionToDirection_LargeDeltaFromNonOrigin_NW()
		{
			// Large delta (-5, +8) clamps to (-1, +1) → NW
			var result = Utility.ConvertPositionToDirection(new Vector3Int(10, 10, 0), new Vector3Int(5, 18, 0));
			Assert.AreEqual(Direction.NW, result);
		}

		[Test]
		public void ConvertPositionToDirection_SamePositionNonOrigin_None()
		{
			var result = Utility.ConvertPositionToDirection(new Vector3Int(7, 3, 0), new Vector3Int(7, 3, 0));
			Assert.AreEqual(Direction.None, result);
		}

		#endregion
	}
}
