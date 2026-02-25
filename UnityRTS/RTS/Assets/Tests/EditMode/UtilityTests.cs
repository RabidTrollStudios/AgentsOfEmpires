using NUnit.Framework;
using UnityEngine;
using GameManager.GameElements;
using GameManager.EnumTypes;

namespace GameManager.Tests
{
	[TestFixture]
	public class UtilityTests
	{
		[Test]
		public void SafeNormalize_ZeroVector_ReturnsZero()
		{
			var result = Utility.SafeNormalize(Vector3.zero);
			Assert.AreEqual(Vector3.zero, result);
		}

		[Test]
		public void SafeNormalize_PureX_ReturnsUnitX()
		{
			var result = Utility.SafeNormalize(new Vector3(5, 0, 0));
			Assert.AreEqual(1f, result.x, 0.001f);
			Assert.AreEqual(0f, result.y, 0.001f);
		}

		[Test]
		public void SafeNormalize_PureY_ReturnsUnitY()
		{
			var result = Utility.SafeNormalize(new Vector3(0, -3, 0));
			Assert.AreEqual(0f, result.x, 0.001f);
			Assert.AreEqual(-1f, result.y, 0.001f);
		}

		[Test]
		public void SafeNormalize_Diagonal_ReturnsNormalized()
		{
			var result = Utility.SafeNormalize(new Vector3(3, 4, 0));
			Assert.AreEqual(0.6f, result.x, 0.001f);
			Assert.AreEqual(0.8f, result.y, 0.001f);
		}

		[Test]
		public void ConvertDirection_AllEight_Correct()
		{
			var origin = Vector3Int.zero;
			Assert.AreEqual(Direction.S, Utility.ConvertPositionToDirection(origin, new Vector3Int(0, -1, 0)));
			Assert.AreEqual(Direction.SE, Utility.ConvertPositionToDirection(origin, new Vector3Int(1, -1, 0)));
			Assert.AreEqual(Direction.E, Utility.ConvertPositionToDirection(origin, new Vector3Int(1, 0, 0)));
			Assert.AreEqual(Direction.NE, Utility.ConvertPositionToDirection(origin, new Vector3Int(1, 1, 0)));
			Assert.AreEqual(Direction.N, Utility.ConvertPositionToDirection(origin, new Vector3Int(0, 1, 0)));
			Assert.AreEqual(Direction.NW, Utility.ConvertPositionToDirection(origin, new Vector3Int(-1, 1, 0)));
			Assert.AreEqual(Direction.W, Utility.ConvertPositionToDirection(origin, new Vector3Int(-1, 0, 0)));
			Assert.AreEqual(Direction.SW, Utility.ConvertPositionToDirection(origin, new Vector3Int(-1, -1, 0)));
		}

		[Test]
		public void ConvertDirection_LargeDelta_ClampsCorrectly()
		{
			// Large positive delta (2,3) clamps to (1,1) → NE
			var result = Utility.ConvertPositionToDirection(Vector3Int.zero, new Vector3Int(2, 3, 0));
			Assert.AreEqual(Direction.NE, result);
		}

		[Test]
		public void ConvertDirection_LargeNegativeSteps_SW()
		{
			// Large negative delta (-100,-200) clamps to (-1,-1) → SW
			var result = Utility.ConvertPositionToDirection(Vector3Int.zero, new Vector3Int(-100, -200, 0));
			Assert.AreEqual(Direction.SW, result);
		}

		[Test]
		public void ConvertDirection_SamePosition_ReturnsNone()
		{
			// Zero delta returns Direction.None
			var result = Utility.ConvertPositionToDirection(new Vector3Int(5, 5, 0), new Vector3Int(5, 5, 0));
			Assert.AreEqual(Direction.None, result);
		}

		[Test]
		public void ConvertDirection_MatchesConstantsMap()
		{
			var origin = Vector3Int.zero;
			foreach (var kvp in Constants.directions)
			{
				var dir = Utility.ConvertPositionToDirection(origin, kvp.Value);
				Assert.AreEqual(kvp.Key, dir, "Mismatch for direction {0} with vector {1}", kvp.Key, kvp.Value);
			}
		}

		[Test]
		public void GridToInt_Overload_EncodesCorrectly()
		{
			var mapSize = new Vector3Int(10, 10, 0);
			int result = Utility.GridToInt(new Vector3Int(3, 7, 0), mapSize);
			Assert.AreEqual(37, result); // 3 * 10 + 7
		}

		[Test]
		public void IntToGrid_Overload_DecodesCorrectly()
		{
			var mapSize = new Vector3Int(10, 10, 0);
			var result = Utility.IntToGrid(37, mapSize);
			Assert.AreEqual(new Vector3Int(3, 7, 0), result);
		}

		[Test]
		public void IsValidGridLocation_BoundaryValues()
		{
			var mapSize = new Vector3Int(5, 5, 0);

			// Valid corners
			Assert.IsTrue(Utility.IsValidGridLocation(new Vector3Int(0, 0, 0), mapSize));
			Assert.IsTrue(Utility.IsValidGridLocation(new Vector3Int(4, 4, 0), mapSize));

			// Just outside each edge
			Assert.IsFalse(Utility.IsValidGridLocation(new Vector3Int(-1, 0, 0), mapSize));
			Assert.IsFalse(Utility.IsValidGridLocation(new Vector3Int(0, -1, 0), mapSize));
			Assert.IsFalse(Utility.IsValidGridLocation(new Vector3Int(5, 0, 0), mapSize));
			Assert.IsFalse(Utility.IsValidGridLocation(new Vector3Int(0, 5, 0), mapSize));

			// Interior
			Assert.IsTrue(Utility.IsValidGridLocation(new Vector3Int(2, 3, 0), mapSize));
		}
	}
}
