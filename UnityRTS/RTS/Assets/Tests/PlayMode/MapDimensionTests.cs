using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for MapManager spatial queries:
	/// map dimensions are accessible and valid, and cell positions are queryable.
	/// </summary>
	[TestFixture]
	public class MapDimensionTests : PlayModeTestBase
	{
		#region Map Size

		/// <summary>
		/// MapManager.MapSize.x is positive in the test environment.
		/// </summary>
		[UnityTest]
		public IEnumerator MapManager_WidthIsPositive()
		{
			Assert.Greater(ctx.MapManager.MapSize.x, 0,
				"Map width (MapSize.x) should be greater than 0");
			yield return null;
		}

		/// <summary>
		/// MapManager.MapSize.y is positive in the test environment.
		/// </summary>
		[UnityTest]
		public IEnumerator MapManager_HeightIsPositive()
		{
			Assert.Greater(ctx.MapManager.MapSize.y, 0,
				"Map height (MapSize.y) should be greater than 0");
			yield return null;
		}

		/// <summary>
		/// Map width and height are equal (the test environment uses a square map).
		/// </summary>
		[UnityTest]
		public IEnumerator MapManager_WidthEqualsHeight()
		{
			Assert.AreEqual(ctx.MapManager.MapSize.x, ctx.MapManager.MapSize.y,
				"The test map should be square (width == height)");
			yield return null;
		}

		#endregion

		#region Buildable Area Queries

		/// <summary>
		/// Cells near the center of the map are buildable by default (no units placed).
		/// </summary>
		[UnityTest]
		public IEnumerator EmptyMap_CenterCellsBuildable()
		{
			Vector3Int center = new Vector3Int(
				ctx.MapManager.MapSize.x / 2,
				ctx.MapManager.MapSize.y / 2, 0);

			Assert.IsTrue(ctx.MapManager.IsAreaBuildable(UnitType.PAWN, center),
				"Center cell of an empty map should be buildable");
			yield return null;
		}

		/// <summary>
		/// Placing a unit on a cell makes that cell non-buildable.
		/// </summary>
		[UnityTest]
		public IEnumerator PlacedUnit_CellNotBuildable()
		{
			Vector3Int pos = new Vector3Int(5, 5, 0);
			PlaceUnit(UnitType.PAWN, pos);

			yield return WaitFrames(1);

			Assert.IsFalse(ctx.MapManager.IsAreaBuildable(UnitType.PAWN, pos),
				"Cell occupied by a unit should not be buildable");
		}

		/// <summary>
		/// MapManager.MapSize is not the zero vector.
		/// </summary>
		[UnityTest]
		public IEnumerator MapSize_IsNotZero()
		{
			Assert.AreNotEqual(Vector3Int.zero, ctx.MapManager.MapSize,
				"MapSize should not be the zero vector in a valid test environment");
			yield return null;
		}

		#endregion

		#region Multiple Queries Consistent

		/// <summary>
		/// Calling MapSize multiple times returns the same value (it is not recalculated).
		/// </summary>
		[UnityTest]
		public IEnumerator MapSize_ConsistentAcrossMultipleCalls()
		{
			Vector3Int first = ctx.MapManager.MapSize;
			Vector3Int second = ctx.MapManager.MapSize;

			Assert.AreEqual(first, second,
				"MapSize should return the same value on repeated calls");
			yield return null;
		}

		#endregion
	}
}
