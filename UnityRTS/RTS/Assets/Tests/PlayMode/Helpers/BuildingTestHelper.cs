using System.Collections;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Helper utilities for building and training Play Mode tests.
	/// Provides shorthand for placing pre-built structures, issuing build
	/// commands, and waiting for construction or training to complete.
	/// </summary>
	public static class BuildingTestHelper
	{
		/// <summary>
		/// Manually advance a unit's Update and FixedUpdate cycles.
		/// Required when GameManager.enabled is false in tests.
		/// </summary>
		public static void Tick(Unit unit)
		{
			unit.TickFixedUpdate();
			unit.Update();
		}

		/// <summary>
		/// Place a BASE at the given position and immediately mark it as built.
		/// Useful for tests that need a completed BASE as a dependency or depot.
		/// </summary>
		public static Unit PlaceBuiltBase(PlayModeTestContext ctx, Vector3Int position,
			GameObject agentGo = null)
		{
			agentGo = agentGo ?? ctx.Agent0Go;
			Unit baseUnit = PlayModeTestHelper.PlaceUnit(ctx, agentGo, UnitType.BASE, position);
			baseUnit.IsBuilt = true;
			return baseUnit;
		}

		/// <summary>
		/// Place a BARRACKS at the given position and immediately mark it as built.
		/// </summary>
		public static Unit PlaceBuiltBarracks(PlayModeTestContext ctx, Vector3Int position,
			GameObject agentGo = null)
		{
			agentGo = agentGo ?? ctx.Agent0Go;
			Unit barracks = PlayModeTestHelper.PlaceUnit(ctx, agentGo, UnitType.BARRACKS, position);
			barracks.IsBuilt = true;
			return barracks;
		}

		/// <summary>
		/// Place a TOWER at the given position and immediately mark it as built.
		/// </summary>
		public static Unit PlaceBuiltTower(PlayModeTestContext ctx, Vector3Int position,
			GameObject agentGo = null)
		{
			agentGo = agentGo ?? ctx.Agent0Go;
			Unit tower = PlayModeTestHelper.PlaceUnit(ctx, agentGo, UnitType.TOWER, position);
			tower.IsBuilt = true;
			return tower;
		}

		/// <summary>
		/// Assert that the given area is buildable for the specified unit type.
		/// </summary>
		public static void AssertAreaBuildable(PlayModeTestContext ctx, UnitType unitType,
			Vector3Int position, string message = null)
		{
			Assert.IsTrue(ctx.MapManager.IsAreaBuildable(unitType, position),
				message ?? $"Area at {position} should be buildable for {unitType}");
		}

		/// <summary>
		/// Assert that the given area is NOT buildable for the specified unit type.
		/// </summary>
		public static void AssertAreaNotBuildable(PlayModeTestContext ctx, UnitType unitType,
			Vector3Int position, string message = null)
		{
			Assert.IsFalse(ctx.MapManager.IsAreaBuildable(unitType, position),
				message ?? $"Area at {position} should NOT be buildable for {unitType}");
		}

		/// <summary>
		/// Wait until the given unit's IsBuilt property becomes true,
		/// ticking the building pawn each frame.
		/// </summary>
		public static IEnumerator WaitForConstruction(Unit pawn, Unit building,
			float timeoutSeconds = 15f)
		{
			float elapsed = 0f;
			while (!building.IsBuilt)
			{
				Tick(pawn);
				elapsed += Time.deltaTime;
				if (elapsed > timeoutSeconds)
				{
					Assert.Fail($"Building {building.UnitType} did not complete construction within {timeoutSeconds}s");
					yield break;
				}
				yield return null;
			}
		}

		/// <summary>
		/// Wait until the given trainer's CurrentAction returns to IDLE,
		/// ticking the trainer each frame.
		/// </summary>
		public static IEnumerator WaitForTraining(Unit trainer,
			float timeoutSeconds = 15f)
		{
			float elapsed = 0f;
			while (trainer.CurrentAction != UnitAction.IDLE)
			{
				trainer.Update();
				elapsed += Time.deltaTime;
				if (elapsed > timeoutSeconds)
				{
					Assert.Fail($"Trainer {trainer.UnitType} did not complete training within {timeoutSeconds}s");
					yield break;
				}
				yield return null;
			}
		}

		/// <summary>
		/// Issue a build command and assert it was accepted (pawn enters BUILD state).
		/// Also asserts gold was deducted.
		/// </summary>
		public static void StartBuildAndAssert(Unit pawn, Vector3Int buildPos,
			UnitType buildingType, Agent agent)
		{
			int goldBefore = agent.Gold;
			int cost = (int)Constants.COST[buildingType];

			pawn.StartBuilding(new BuildEventArgs(pawn, buildPos, buildingType));

			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction,
				$"Pawn should enter BUILD state when building {buildingType}");
			Assert.AreEqual(goldBefore - cost, agent.Gold,
				$"Gold should be deducted by {cost} when building {buildingType}");
		}

		/// <summary>
		/// Find the most recently created unit of the given type in UnitManager.
		/// Returns null if none found.
		/// </summary>
		public static Unit FindNewestUnitOfType(PlayModeTestContext ctx, UnitType unitType)
		{
			return ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.Where(u => u.UnitType == unitType)
				.OrderByDescending(u => u.UnitNbr)
				.FirstOrDefault();
		}
	}
}
