using NUnit.Framework;
using UnityEngine;
using GameManager.EnumTypes;

namespace GameManager.Tests
{
	/// <summary>
	/// Tests for Constants.directions — verifies that each cardinal and
	/// diagonal Direction enum value maps to the correct Vector3Int offset.
	/// </summary>
	[TestFixture]
	public class DirectionMappingTests
	{
		#region Dictionary Completeness

		/// <summary>
		/// Constants.directions should contain exactly 8 entries (one per non-None direction).
		/// </summary>
		[Test]
		public void Directions_HasExactlyEightEntries()
		{
			Assert.AreEqual(8, Constants.directions.Count,
				"directions dictionary should have exactly 8 entries (N, NE, E, SE, S, SW, W, NW)");
		}

		/// <summary>
		/// Direction.None should NOT be in the directions dictionary
		/// (None means stationary — no offset).
		/// </summary>
		[Test]
		public void Directions_DoesNotContainNone()
		{
			Assert.IsFalse(Constants.directions.ContainsKey(Direction.None),
				"Direction.None should not have an entry in the directions dictionary");
		}

		/// <summary>
		/// All eight non-None Direction values are present in the dictionary.
		/// </summary>
		[Test]
		public void Directions_ContainsAllEightDirections()
		{
			Assert.IsTrue(Constants.directions.ContainsKey(Direction.N),  "N should be present");
			Assert.IsTrue(Constants.directions.ContainsKey(Direction.NE), "NE should be present");
			Assert.IsTrue(Constants.directions.ContainsKey(Direction.E),  "E should be present");
			Assert.IsTrue(Constants.directions.ContainsKey(Direction.SE), "SE should be present");
			Assert.IsTrue(Constants.directions.ContainsKey(Direction.S),  "S should be present");
			Assert.IsTrue(Constants.directions.ContainsKey(Direction.SW), "SW should be present");
			Assert.IsTrue(Constants.directions.ContainsKey(Direction.W),  "W should be present");
			Assert.IsTrue(Constants.directions.ContainsKey(Direction.NW), "NW should be present");
		}

		#endregion

		#region Cardinal Directions

		/// <summary>
		/// N maps to (0, 1, 0) — one step up.
		/// </summary>
		[Test]
		public void Direction_N_MapsToUp()
		{
			Assert.AreEqual(Vector3Int.up, Constants.directions[Direction.N],
				"N should map to Vector3Int.up (0,1,0)");
		}

		/// <summary>
		/// S maps to (0, -1, 0) — one step down.
		/// </summary>
		[Test]
		public void Direction_S_MapsToDown()
		{
			Assert.AreEqual(Vector3Int.down, Constants.directions[Direction.S],
				"S should map to Vector3Int.down (0,-1,0)");
		}

		/// <summary>
		/// E maps to (1, 0, 0) — one step right.
		/// </summary>
		[Test]
		public void Direction_E_MapsToRight()
		{
			Assert.AreEqual(Vector3Int.right, Constants.directions[Direction.E],
				"E should map to Vector3Int.right (1,0,0)");
		}

		/// <summary>
		/// W maps to (-1, 0, 0) — one step left.
		/// </summary>
		[Test]
		public void Direction_W_MapsToLeft()
		{
			Assert.AreEqual(Vector3Int.left, Constants.directions[Direction.W],
				"W should map to Vector3Int.left (-1,0,0)");
		}

		#endregion

		#region Diagonal Directions

		/// <summary>
		/// NE maps to (1, 1, 0).
		/// </summary>
		[Test]
		public void Direction_NE_MapsToUpperRight()
		{
			Assert.AreEqual(new Vector3Int(1, 1, 0), Constants.directions[Direction.NE],
				"NE should map to (1,1,0)");
		}

		/// <summary>
		/// NW maps to (-1, 1, 0).
		/// </summary>
		[Test]
		public void Direction_NW_MapsToUpperLeft()
		{
			Assert.AreEqual(new Vector3Int(-1, 1, 0), Constants.directions[Direction.NW],
				"NW should map to (-1,1,0)");
		}

		/// <summary>
		/// SE maps to (1, -1, 0).
		/// </summary>
		[Test]
		public void Direction_SE_MapsToLowerRight()
		{
			Assert.AreEqual(new Vector3Int(1, -1, 0), Constants.directions[Direction.SE],
				"SE should map to (1,-1,0)");
		}

		/// <summary>
		/// SW maps to (-1, -1, 0).
		/// </summary>
		[Test]
		public void Direction_SW_MapsToLowerLeft()
		{
			Assert.AreEqual(new Vector3Int(-1, -1, 0), Constants.directions[Direction.SW],
				"SW should map to (-1,-1,0)");
		}

		#endregion

		#region Opposite Directions

		/// <summary>
		/// Opposite directions should sum to (0, 0, 0).
		/// </summary>
		[Test]
		public void OppositeDirections_SumToZero()
		{
			Assert.AreEqual(Vector3Int.zero,
				Constants.directions[Direction.N] + Constants.directions[Direction.S],
				"N + S should be zero vector");

			Assert.AreEqual(Vector3Int.zero,
				Constants.directions[Direction.E] + Constants.directions[Direction.W],
				"E + W should be zero vector");

			Assert.AreEqual(Vector3Int.zero,
				Constants.directions[Direction.NE] + Constants.directions[Direction.SW],
				"NE + SW should be zero vector");

			Assert.AreEqual(Vector3Int.zero,
				Constants.directions[Direction.NW] + Constants.directions[Direction.SE],
				"NW + SE should be zero vector");
		}

		#endregion
	}
}
