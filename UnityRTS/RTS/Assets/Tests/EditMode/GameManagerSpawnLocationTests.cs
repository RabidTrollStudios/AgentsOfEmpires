using System.Collections.Generic;
using System.Reflection;
using AgentSDK;
using NUnit.Framework;
using UnityEngine;

namespace GameManager.Tests
{
	/// <summary>
	/// EditMode tests for GameManager spawn location helpers:
	/// - GetBuildableLocationNearCorner (open map / blocked origin)
	/// - GetRandomBuildableLocationExcludingCorners
	/// </summary>
	[TestFixture]
	public class GameManagerSpawnLocationTests
	{
		private GameManager gm;
		private List<GameObject> createdObjects;

		[SetUp]
		public void SetUp()
		{
			gm = GameManager.Instance;
			createdObjects = new List<GameObject>();
		}

		[TearDown]
		public void TearDown()
		{
			SetField("mapManager", null);

			foreach (var go in createdObjects)
				if (go != null) Object.DestroyImmediate(go);
			createdObjects.Clear();
		}

		// ── Reflection helpers ─────────────────────────────────────────────────

		private void SetField(string name, object value) =>
			typeof(GameManager)
				.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(gm, value);

		private T Invoke<T>(string methodName, params object[] args) =>
			(T)typeof(GameManager)
				.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(gm, args.Length == 0 ? null : args);

		// ── GetBuildableLocationNearCorner ─────────────────────────────────────

		[Test]
		public void GetBuildableLocationNearCorner_OpenMap_ReturnsCornerPositionAtRadiusZero()
		{
			var (mapManager, tilemapGo) = MapManagerTestHelper.Build(30, 30);
			createdObjects.Add(tilemapGo);
			SetField("mapManager", mapManager);

			// On a fully open map at radius 0 the corner cell itself is buildable and valid.
			var result = Invoke<Vector3Int>("GetBuildableLocationNearCorner", 1, 1, UnitType.PAWN);

			Assert.AreEqual(new Vector3Int(1, 1, 0), result,
				"Should return the corner position directly when the map is fully open");
		}

		[Test]
		public void GetBuildableLocationNearCorner_OriginCellBlocked_SearchesOutwardAndReturnsValidCell()
		{
			// Block only (1,1) so radius 0 fails and the search expands to radius 1.
			var (mapManager, tilemapGo) = MapManagerTestHelper.Build(30, 30,
				new[] { (1, 1) });
			createdObjects.Add(tilemapGo);
			SetField("mapManager", mapManager);

			var result = Invoke<Vector3Int>("GetBuildableLocationNearCorner", 1, 1, UnitType.PAWN);

			Assert.AreNotEqual(new Vector3Int(1, 1, 0), result,
				"Should not return the blocked cell");
			bool inBounds = result.x >= 0 && result.x < 30 && result.y >= 0 && result.y < 30;
			Assert.IsTrue(inBounds, $"Result {result} must be within the 30x30 map bounds");
		}

		// ── GetRandomBuildableLocationExcludingCorners ─────────────────────────

		[Test]
		public void GetRandomBuildableLocationExcludingCorners_NeverReturnsCornerLocation()
		{
			var (mapManager, tilemapGo) = MapManagerTestHelper.Build(30, 30);
			createdObjects.Add(tilemapGo);
			SetField("mapManager", mapManager);

			for (int i = 0; i < 5; i++)
			{
				var result = Invoke<Vector3Int>(
					"GetRandomBuildableLocationExcludingCorners", UnitType.PAWN);

				bool inLowerLeftCorner  = result.x < 5 && result.y < 5;
				bool inUpperRightCorner = result.x >= 25 && result.y >= 25;
				Assert.IsFalse(inLowerLeftCorner || inUpperRightCorner,
					$"Iteration {i}: result {result} must not be inside a corner zone");
			}
		}
	}
}
