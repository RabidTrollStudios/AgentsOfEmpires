using System.Collections;
using System.Reflection;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for the repair action.
	/// Covers StartRepairing, UpdateRepair, and edge cases like
	/// non-builder units, building death during repair, and repair completion.
	/// </summary>
	[TestFixture]
	public class UnitRepairTests : PlayModeTestBase
	{
		#region Repair Lifecycle

		/// <summary>
		/// A pawn issued a repair command on a damaged building enters REPAIR state.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_RepairDamagedBuilding_EntersRepairState()
		{
			// Place a built base with reduced health
			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(10, 10, 0));
			float maxHp = Constants.HEALTH[UnitType.BASE];
			baseUnit.Health = maxHp * 0.5f;

			// Place a pawn next to the base
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 10, 0));
			yield return null;

			pawn.StartRepairing(new RepairEventArgs(pawn, baseUnit));

			Assert.AreEqual(UnitAction.REPAIR, pawn.CurrentAction,
				"Pawn should enter REPAIR state");
			yield return null;
		}

		/// <summary>
		/// A pawn repairs a damaged building back to full health and returns to IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_RepairToFull_ReturnsToIdle()
		{
			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(10, 10, 0));
			float maxHp = Constants.HEALTH[UnitType.BASE];
			baseUnit.Health = maxHp * 0.8f; // Only slightly damaged for faster test

			// Place pawn adjacent to the base
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 10, 0));
			yield return null;

			pawn.StartRepairing(new RepairEventArgs(pawn, baseUnit));
			Assert.AreEqual(UnitAction.REPAIR, pawn.CurrentAction);

			// Tick until repair completes
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Tick(pawn);
				return pawn.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 15f, failMessage: "Pawn should return to IDLE after repair completes");

			Assert.AreEqual(maxHp, baseUnit.Health, 0.01f,
				"Building should be at full health after repair");
		}

		/// <summary>
		/// Building health increases during repair.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_RepairingBuilding_HealthIncreases()
		{
			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(10, 10, 0));
			float maxHp = Constants.HEALTH[UnitType.BASE];
			baseUnit.Health = maxHp * 0.5f;
			float healthBefore = baseUnit.Health;

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 10, 0));
			yield return null;

			pawn.StartRepairing(new RepairEventArgs(pawn, baseUnit));

			// Tick a few frames to accumulate some repair
			for (int i = 0; i < 30; i++)
			{
				BuildingTestHelper.Tick(pawn);
				yield return null;
			}

			Assert.Greater(baseUnit.Health, healthBefore,
				"Building health should increase during repair");
		}

		#endregion

		#region Repair Rejection

		/// <summary>
		/// A warrior (non-builder) cannot repair — stays in current state.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_CannotRepair_StaysIdle()
		{
			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(10, 10, 0));
			baseUnit.Health = Constants.HEALTH[UnitType.BASE] * 0.5f;

			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(8, 10, 0));
			yield return null;

			warrior.StartRepairing(new RepairEventArgs(warrior, baseUnit));

			Assert.AreNotEqual(UnitAction.REPAIR, warrior.CurrentAction,
				"Warrior should not enter REPAIR state (can't build/repair)");
		}

		#endregion

		#region Building Destroyed During Repair

		/// <summary>
		/// If the building is destroyed while the pawn is repairing,
		/// the pawn should return to IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_BuildingDestroyedDuringRepair_ReturnsToIdle()
		{
			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(10, 10, 0));
			baseUnit.Health = Constants.HEALTH[UnitType.BASE] * 0.5f;

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 10, 0));
			yield return null;

			pawn.StartRepairing(new RepairEventArgs(pawn, baseUnit));
			Assert.AreEqual(UnitAction.REPAIR, pawn.CurrentAction);

			// Tick a few frames so repair is in progress
			for (int i = 0; i < 10; i++)
			{
				BuildingTestHelper.Tick(pawn);
				yield return null;
			}

			// Destroy the building
			baseUnit.Health = 0;
			baseUnit.Update();
			yield return WaitFrames(2);

			// Pawn should detect building is gone and go idle
			for (int i = 0; i < 5; i++)
			{
				BuildingTestHelper.Tick(pawn);
				yield return null;
			}

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should go IDLE when repaired building is destroyed");
		}

		#endregion

		#region Move Interrupts Repair

		/// <summary>
		/// A move command interrupts an ongoing repair.
		/// </summary>
		[UnityTest]
		public IEnumerator MoveCommand_InterruptsRepair()
		{
			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(10, 10, 0));
			baseUnit.Health = Constants.HEALTH[UnitType.BASE] * 0.5f;

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 10, 0));
			yield return null;

			pawn.StartRepairing(new RepairEventArgs(pawn, baseUnit));
			Assert.AreEqual(UnitAction.REPAIR, pawn.CurrentAction);

			// Issue move command
			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, new Vector3Int(5, 5, 0)));

			Assert.AreEqual(UnitAction.MOVE, pawn.CurrentAction,
				"Move should interrupt repair");
			yield return null;
		}

		#endregion

		#region Repair From Build State

		/// <summary>
		/// If a pawn is in BUILD state and receives a repair command,
		/// it should release the current building and switch to REPAIR.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_InBuildState_SwitchesToRepair()
		{
			// Place a built base (dependency) and a damaged barracks
			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(5, 5, 0));
			Unit barracks = BuildingTestHelper.PlaceBuiltBarracks(ctx, new Vector3Int(15, 15, 0));
			barracks.Health = Constants.HEALTH[UnitType.BARRACKS] * 0.3f;

			// Place pawn and start building something
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			yield return null;

			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BASE));

			if (pawn.CurrentAction == UnitAction.BUILD)
			{
				// Now issue repair on the damaged barracks
				pawn.StartRepairing(new RepairEventArgs(pawn, barracks));

				Assert.AreEqual(UnitAction.REPAIR, pawn.CurrentAction,
					"Pawn should switch from BUILD to REPAIR");
			}

			yield return null;
		}

		#endregion

		#region Debugging Info for Repair

		/// <summary>
		/// When a pawn is repairing and debugging is enabled,
		/// the repair state is reflected in the debugging info.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Repairing_DebuggingInfoShowsRepairState()
		{
			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(10, 10, 0));
			baseUnit.Health = Constants.HEALTH[UnitType.BASE] * 0.5f;

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 10, 0));
			yield return null;

			// Enable debugging via GameManager (Unit.Update reads HasUnitDebugging each tick)
			typeof(GameManager).GetProperty("HasUnitDebugging",
				BindingFlags.Public | BindingFlags.Instance)
				.SetValue(GameManager.Instance, true);

			pawn.StartRepairing(new RepairEventArgs(pawn, baseUnit));
			Assert.AreEqual(UnitAction.REPAIR, pawn.CurrentAction);

			// Tick to trigger UpdateDebuggingInfo with REPAIR case
			for (int i = 0; i < 5; i++)
			{
				BuildingTestHelper.Tick(pawn);
				yield return null;
			}

			// Just verifying no exceptions — the REPAIR branch in UpdateDebuggingInfo was uncovered
			Assert.AreEqual(UnitAction.REPAIR, pawn.CurrentAction);

			typeof(GameManager).GetProperty("HasUnitDebugging",
				BindingFlags.Public | BindingFlags.Instance)
				.SetValue(GameManager.Instance, false);
			yield return null;
		}

		#endregion
	}
}
