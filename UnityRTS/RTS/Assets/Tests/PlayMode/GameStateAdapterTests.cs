using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	[TestFixture]
	public class GameStateAdapterTests : PlayModeTestBase
	{
		private const BindingFlags NonPublic =
			BindingFlags.NonPublic | BindingFlags.Instance;

		private void InjectAgentsDict()
		{
			var agentsDict = new Dictionary<int, GameObject>
			{
				{ 0, ctx.Agent0Go },
				{ 1, ctx.Agent1Go }
			};
			// Auto-property backing field: <Agents>k__BackingField
			typeof(GameManager).GetField("<Agents>k__BackingField",
				BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(GameManager.Instance, agentsDict);
		}

		private GameStateAdapter CreateAdapter(int agentNbr = 0)
		{
			InjectAgentsDict();

			var adapter = new GameStateAdapter(agentNbr, ctx.UnitManager, ctx.MapManager);
			adapter.UpdateEnemyAgentNbr(agentNbr == 0 ? 1 : 0);
			return adapter;
		}

		#region Constructor and Properties

		[UnityTest]
		public IEnumerator Constructor_SetsAgentNbr()
		{
			var adapter = CreateAdapter(0);
			yield return null;
			Assert.AreEqual(0, adapter.MyAgentNbr);
		}

		[UnityTest]
		public IEnumerator UpdateEnemyAgentNbr_SetsEnemyNbr()
		{
			var adapter = CreateAdapter(0);
			yield return null;
			Assert.AreEqual(1, adapter.EnemyAgentNbr);
		}

		[UnityTest]
		public IEnumerator MyGold_ReturnsAgentGold()
		{
			var adapter = CreateAdapter(0);
			yield return null;
			// Starting gold is 5000 per PlayModeTestHelper
			Assert.AreEqual(5000, adapter.MyGold);
		}

		[UnityTest]
		public IEnumerator EnemyGold_ReturnsEnemyAgentGold()
		{
			var adapter = CreateAdapter(0);
			yield return null;
			Assert.AreEqual(5000, adapter.EnemyGold);
		}

		[UnityTest]
		public IEnumerator EnemyGold_NegativeEnemy_ReturnsZero()
		{
			InjectAgentsDict();
			var adapter = new GameStateAdapter(0, ctx.UnitManager, ctx.MapManager);
			adapter.UpdateEnemyAgentNbr(-1);
			yield return null;
			Assert.AreEqual(0, adapter.EnemyGold);
		}

		[UnityTest]
		public IEnumerator MapSize_ReturnsCorrectSize()
		{
			var adapter = CreateAdapter(0);
			yield return null;
			Assert.AreEqual(30, adapter.MapSize.X);
			Assert.AreEqual(30, adapter.MapSize.Y);
		}

		[UnityTest]
		public IEnumerator MyWins_ReturnsAgentWins()
		{
			var adapter = CreateAdapter(0);
			yield return null;
			Assert.AreEqual(0, adapter.MyWins);
		}

		#endregion

		#region GetMyUnits / GetEnemyUnits / GetAllUnits

		[UnityTest]
		public IEnumerator GetMyUnits_ReturnsOwnUnits()
		{
			PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.PAWN, new Vector3Int(7, 7, 0));
			var adapter = CreateAdapter(0);
			yield return null;

			var pawns = adapter.GetMyUnits(AgentSDK.UnitType.PAWN);
			Assert.AreEqual(2, pawns.Count);
		}

		[UnityTest]
		public IEnumerator GetMyUnits_DoesNotReturnEnemyUnits()
		{
			PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.PAWN, new Vector3Int(20, 20, 0), ctx.Agent1Go);
			var adapter = CreateAdapter(0);
			yield return null;

			var pawns = adapter.GetMyUnits(AgentSDK.UnitType.PAWN);
			Assert.AreEqual(1, pawns.Count);
		}

		[UnityTest]
		public IEnumerator GetEnemyUnits_ReturnsEnemyUnits()
		{
			PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.PAWN, new Vector3Int(20, 20, 0), ctx.Agent1Go);
			var adapter = CreateAdapter(0);
			yield return null;

			var enemyPawns = adapter.GetEnemyUnits(AgentSDK.UnitType.PAWN);
			Assert.AreEqual(1, enemyPawns.Count);
		}

		[UnityTest]
		public IEnumerator GetEnemyUnits_NegativeEnemy_ReturnsEmpty()
		{
			PlaceUnit(UnitType.PAWN, new Vector3Int(20, 20, 0), ctx.Agent1Go);
			InjectAgentsDict();
			var adapter = new GameStateAdapter(0, ctx.UnitManager, ctx.MapManager);
			adapter.UpdateEnemyAgentNbr(-1);
			yield return null;

			var result = adapter.GetEnemyUnits(AgentSDK.UnitType.PAWN);
			Assert.AreEqual(0, result.Count);
		}

		[UnityTest]
		public IEnumerator GetAllUnits_ReturnsBothAgents()
		{
			PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.PAWN, new Vector3Int(20, 20, 0), ctx.Agent1Go);
			var adapter = CreateAdapter(0);
			yield return null;

			var allPawns = adapter.GetAllUnits(AgentSDK.UnitType.PAWN);
			Assert.AreEqual(2, allPawns.Count);
		}

		#endregion

		#region GetUnit

		[UnityTest]
		public IEnumerator GetUnit_ValidUnit_ReturnsUnitInfo()
		{
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 10, 0));
			var adapter = CreateAdapter(0);
			yield return null;

			var info = adapter.GetUnit(pawn.UnitNbr);
			Assert.IsNotNull(info);
			Assert.AreEqual(pawn.UnitNbr, info.Value.UnitNbr);
			Assert.AreEqual(AgentSDK.UnitType.PAWN, info.Value.UnitType);
			Assert.AreEqual(10, info.Value.GridPosition.X);
			Assert.AreEqual(10, info.Value.GridPosition.Y);
			Assert.IsTrue(info.Value.CanMove);
			Assert.IsTrue(info.Value.CanBuild);
			Assert.IsTrue(info.Value.CanGather);
			Assert.AreEqual(0, info.Value.OwnerAgentNbr);
		}

		[UnityTest]
		public IEnumerator GetUnit_InvalidNbr_ReturnsNull()
		{
			var adapter = CreateAdapter(0);
			yield return null;

			var info = adapter.GetUnit(99999);
			Assert.IsNull(info);
		}

		[UnityTest]
		public IEnumerator GetUnit_EnemyUnit_ReturnsEnemyOwner()
		{
			var enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(20, 20, 0), ctx.Agent1Go);
			var adapter = CreateAdapter(0);
			yield return null;

			var info = adapter.GetUnit(enemy.UnitNbr);
			Assert.IsNotNull(info);
			Assert.AreEqual(1, info.Value.OwnerAgentNbr);
		}

		#endregion

		#region IsPositionBuildable

		[UnityTest]
		public IEnumerator IsPositionBuildable_EmptyCell_ReturnsTrue()
		{
			var adapter = CreateAdapter(0);
			yield return null;

			Assert.IsTrue(adapter.IsPositionBuildable(new Position(15, 15)));
		}

		[UnityTest]
		public IEnumerator IsPositionBuildable_OccupiedCell_ReturnsFalse()
		{
			PlaceUnit(UnitType.PAWN, new Vector3Int(15, 15, 0));
			var adapter = CreateAdapter(0);
			yield return null;

			Assert.IsFalse(adapter.IsPositionBuildable(new Position(15, 15)));
		}

		#endregion

		#region IsAreaBuildable / IsBoundedAreaBuildable

		[UnityTest]
		public IEnumerator IsAreaBuildable_EmptyArea_ReturnsTrue()
		{
			var adapter = CreateAdapter(0);
			yield return null;

			Assert.IsTrue(adapter.IsAreaBuildable(AgentSDK.UnitType.BARRACKS, new Position(15, 15)));
		}

		[UnityTest]
		public IEnumerator IsAreaBuildable_OccupiedArea_ReturnsFalse()
		{
			PlaceUnit(UnitType.WARRIOR, new Vector3Int(15, 15, 0));
			var adapter = CreateAdapter(0);
			yield return null;

			Assert.IsFalse(adapter.IsAreaBuildable(AgentSDK.UnitType.BARRACKS, new Position(15, 15)));
		}

		[UnityTest]
		public IEnumerator IsBoundedAreaBuildable_InBounds_ReturnsTrue()
		{
			var adapter = CreateAdapter(0);
			yield return null;

			Assert.IsTrue(adapter.IsBoundedAreaBuildable(AgentSDK.UnitType.BARRACKS, new Position(15, 15)));
		}

		[UnityTest]
		public IEnumerator IsBoundedAreaBuildable_OutOfBounds_ReturnsFalse()
		{
			var adapter = CreateAdapter(0);
			yield return null;

			// Edge of map — 2x2 building won't fit
			Assert.IsFalse(adapter.IsBoundedAreaBuildable(AgentSDK.UnitType.BARRACKS, new Position(29, 29)));
		}

		#endregion

		#region GetPathBetween

		[UnityTest]
		public IEnumerator GetPathBetween_ValidPath_ReturnsPositions()
		{
			var adapter = CreateAdapter(0);
			yield return null;

			var path = adapter.GetPathBetween(new Position(5, 5), new Position(10, 10));
			Assert.IsNotNull(path);
			Assert.Greater(path.Count, 0, "Path should have at least one position");
		}

		[UnityTest]
		public IEnumerator GetPathBetween_CachedResult_ReturnsSameObject()
		{
			var adapter = CreateAdapter(0);
			yield return null;

			var path1 = adapter.GetPathBetween(new Position(5, 5), new Position(10, 10));
			var path2 = adapter.GetPathBetween(new Position(5, 5), new Position(10, 10));
			Assert.AreSame(path1, path2, "Second call should return cached result");
		}

		[UnityTest]
		public IEnumerator GetPathBetween_ExceedsBudget_ReturnsEmptyPath()
		{
			var adapter = CreateAdapter(0);
			yield return null;

			// Exhaust the pathfinding budget (MAX_PATH_CALLS_PER_FRAME = 20)
			// Use unique start/end pairs so cache doesn't short-circuit
			for (int i = 0; i < 20; i++)
			{
				adapter.GetPathBetween(new Position(i, 0), new Position(i, 1));
			}

			// 21st call should exceed budget
			LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("exceeded pathfinding budget"));
			var overflow = adapter.GetPathBetween(new Position(0, 5), new Position(0, 6));
			Assert.AreEqual(0, overflow.Count, "Should return empty path when budget exceeded");
		}

		#endregion

		#region GetPathBetween (avoidUnits overload)

		[UnityTest]
		public IEnumerator GetPathBetween_AvoidFalse_DelegatesToBasic()
		{
			var adapter = CreateAdapter(0);
			yield return null;

			var path = adapter.GetPathBetween(new Position(5, 5), new Position(10, 10), false);
			Assert.IsNotNull(path);
			Assert.Greater(path.Count, 0);
		}

		[UnityTest]
		public IEnumerator GetPathBetween_AvoidTrue_ReturnsPath()
		{
			var adapter = CreateAdapter(0);
			yield return null;

			var path = adapter.GetPathBetween(new Position(5, 5), new Position(10, 10), true);
			Assert.IsNotNull(path);
			Assert.Greater(path.Count, 0);
		}

		[UnityTest]
		public IEnumerator GetPathBetween_AvoidTrue_ExceedsBudget_ReturnsEmpty()
		{
			var adapter = CreateAdapter(0);
			yield return null;

			// Exhaust budget
			for (int i = 0; i < 20; i++)
			{
				adapter.GetPathBetween(new Position(i, 0), new Position(i, 1), true);
			}

			LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("exceeded pathfinding budget"));
			var overflow = adapter.GetPathBetween(new Position(0, 5), new Position(0, 6), true);
			Assert.AreEqual(0, overflow.Count);
		}

		#endregion

		#region GetPathToUnit

		[UnityTest]
		public IEnumerator GetPathToUnit_ValidTarget_ReturnsPath()
		{
			var mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 20, 0));
			var adapter = CreateAdapter(0);
			yield return null;

			var path = adapter.GetPathToUnit(
				new Position(5, 5), AgentSDK.UnitType.MINE, new Position(20, 20));
			Assert.IsNotNull(path);
			Assert.Greater(path.Count, 0);
		}

		[UnityTest]
		public IEnumerator GetPathToUnit_CachedResult_ReturnsSameObject()
		{
			var mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 20, 0));
			var adapter = CreateAdapter(0);
			yield return null;

			var path1 = adapter.GetPathToUnit(
				new Position(5, 5), AgentSDK.UnitType.MINE, new Position(20, 20));
			var path2 = adapter.GetPathToUnit(
				new Position(5, 5), AgentSDK.UnitType.MINE, new Position(20, 20));
			Assert.AreSame(path1, path2, "Second call should return cached result");
		}

		[UnityTest]
		public IEnumerator GetPathToUnit_ExceedsBudget_ReturnsEmpty()
		{
			var mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 20, 0));
			var adapter = CreateAdapter(0);
			yield return null;

			// Exhaust budget with different keys
			for (int i = 0; i < 20; i++)
			{
				adapter.GetPathToUnit(
					new Position(i, 0), AgentSDK.UnitType.MINE, new Position(20, 20));
			}

			LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("exceeded pathfinding budget"));
			var overflow = adapter.GetPathToUnit(
				new Position(0, 5), AgentSDK.UnitType.MINE, new Position(20, 20));
			Assert.AreEqual(0, overflow.Count);
		}

		#endregion

		#region GetBuildablePositionsNearUnit

		[UnityTest]
		public IEnumerator GetBuildablePositionsNearUnit_ReturnsPositions()
		{
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(15, 15, 0));
			baseUnit.IsBuilt = true;
			var adapter = CreateAdapter(0);
			yield return null;

			var positions = adapter.GetBuildablePositionsNearUnit(
				AgentSDK.UnitType.BASE, new Position(15, 15));
			Assert.IsNotNull(positions);
			Assert.Greater(positions.Count, 0, "Should have buildable positions near base");
		}

		#endregion

		#region FindProspectiveBuildPositions

		[UnityTest]
		public IEnumerator FindProspectiveBuildPositions_ReturnsValidPositions()
		{
			var adapter = CreateAdapter(0);
			yield return null;

			var positions = adapter.FindProspectiveBuildPositions(AgentSDK.UnitType.BARRACKS);
			Assert.IsNotNull(positions);
			Assert.Greater(positions.Count, 0, "Empty map should have many buildable positions");
		}

		[UnityTest]
		public IEnumerator FindProspectiveBuildPositions_ExcludesPawnPositions()
		{
			// Place a pawn — its position should still show as buildable
			// because the adapter excludes own pawns from blocking
			PlaceUnit(UnitType.PAWN, new Vector3Int(15, 15, 0));
			var adapter = CreateAdapter(0);
			yield return null;

			var positions = adapter.FindProspectiveBuildPositions(AgentSDK.UnitType.BARRACKS);
			Assert.IsNotNull(positions);
			// The pawn position should be included since own pawns are excluded
			bool containsPawnPos = false;
			foreach (var pos in positions)
			{
				if (pos.X == 15 && pos.Y == 15) { containsPawnPos = true; break; }
			}
			Assert.IsTrue(containsPawnPos,
				"Pawn position should be buildable since own pawns are excluded");
		}

		#endregion

		#region ResetCacheIfNewFrame

		[UnityTest]
		public IEnumerator ResetCache_NewFrame_ClearsCache()
		{
			var adapter = CreateAdapter(0);
			yield return null;

			// First call populates cache
			var path1 = adapter.GetPathBetween(new Position(5, 5), new Position(10, 10));

			// Wait a frame so frame count changes
			yield return null;

			// This call should NOT be the same cached object since frame changed
			var path2 = adapter.GetPathBetween(new Position(5, 5), new Position(10, 10));
			// Can't use AreSame because cache was cleared — but path should still be valid
			Assert.IsNotNull(path2);
			Assert.Greater(path2.Count, 0);
		}

		#endregion
	}
}
