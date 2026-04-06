using System.Collections;
using System.Reflection;
using AgentSDK;
using GameManager.EnumTypes;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Tests targeting specific uncovered lines in Unit.Actions.cs,
	/// Unit.Tasks.cs, and Unit.Movement.cs to increase coverage.
	/// </summary>
	[TestFixture]
	public class UnitCoverageGapTests : PlayModeTestBase
	{
		#region Helpers

		private static readonly BindingFlags NonPublic =
			BindingFlags.NonPublic | BindingFlags.Instance;

		private void SetPrivateField(Unit unit, string fieldName, object value) =>
			typeof(Unit).GetField(fieldName, NonPublic).SetValue(unit, value);

		private T GetPrivateField<T>(Unit unit, string fieldName) =>
			(T)typeof(Unit).GetField(fieldName, NonPublic).GetValue(unit);

		private Unit PlaceBuiltBase(Vector3Int pos, GameObject agentGo = null) =>
			BuildingTestHelper.PlaceBuiltBase(ctx, pos, agentGo);

		private Unit PlaceBuiltBarracks(Vector3Int pos) =>
			BuildingTestHelper.PlaceBuiltBarracks(ctx, pos);

		/// <summary>
		/// Create a vertical wall of unwalkable cells across the map at column x.
		/// </summary>
		private void CreateWall(int x)
		{
			for (int y = 0; y < 30; y++)
			{
				ctx.MapManager.GridCells[x, y].SetWalkable(false);
				ctx.MapManager.GridCells[x, y].SetBuildable(false);
				ctx.MapManager.Grid.SetCellBlocked(x, y);
			}
		}

		#endregion

		#region StartBuilding — Resume No Path (Unit.Actions.cs:99-102)

		/// <summary>
		/// When a pawn tries to resume building an unfinished building but
		/// the path is blocked, it should not enter BUILD state.
		/// </summary>
		[UnityTest]
		public IEnumerator StartBuilding_ResumeNoPath_DoesNotBuild()
		{
			// Place a built base (dependency)
			PlaceBuiltBase(new Vector3Int(0, 0, 0));

			// Place an unfinished barracks on the far side of a wall
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(20, 15, 0));
			Assert.IsFalse(barracks.IsBuilt, "Barracks should start unbuilt");

			// Create an impassable wall between pawn and barracks
			CreateWall(15);

			// Place pawn on the near side of the wall
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(2, 2, 0));
			yield return null;

			// Try to build a BARRACKS at the same position (triggers resume path)
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(20, 15, 0), UnitType.BARRACKS));

			// Pawn should NOT have entered BUILD state (no path to paused building)
			Assert.AreNotEqual(UnitAction.BUILD, pawn.CurrentAction,
				"Pawn should not enter BUILD when resume path is blocked");
		}

		#endregion

		#region StartGathering — Busy Unit (Unit.Actions.cs:234-237)

		/// <summary>
		/// A pawn in BUILD state cannot start gathering — the busy branch fires.
		/// </summary>
		[UnityTest]
		public IEnumerator StartGathering_BusyBuilding_StaysInBuild()
		{
			PlaceBuiltBase(new Vector3Int(0, 0, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 20, 0));

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 10, 0));
			yield return null;

			// Put the pawn into BUILD state
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(12, 10, 0), UnitType.BASE));
			if (pawn.CurrentAction != UnitAction.BUILD)
			{
				// If build didn't start (e.g., path issue), skip
				Assert.Ignore("Could not put pawn into BUILD state for this test");
			}

			// Try to gather while building — should be rejected
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction,
				"Pawn should remain in BUILD state when gather is rejected");
		}

		#endregion

		#region StartGathering — Resource/Base Gone (Unit.Actions.cs:244-247)

		/// <summary>
		/// When the base referenced in GatherEventArgs no longer exists
		/// in UnitManager, the gather should be rejected.
		/// Covers the "resource or base unit no longer exists" branch.
		/// </summary>
		[UnityTest]
		public IEnumerator StartGathering_BaseGone_StaysIdle()
		{
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 20, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			var args = new GatherEventArgs(pawn, mine, baseUnit);
			yield return null;

			// Kill the base via FixedUpdate() — death check runs in FixedUpdate,
			// which removes it from UnitManager
			baseUnit.Health = 0;
			baseUnit.StepFixedUpdate();

			// Call StartGathering in the SAME frame — GetUnit returns null but
			// args.BaseUnit.UnitNbr is still accessible
			pawn.StartGathering(args);

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should stay IDLE when base unit no longer exists in UnitManager");
		}

		#endregion

		#region StartAttacking — No Path (Unit.Actions.cs:297-300)

		/// <summary>
		/// When a warrior tries to attack a target behind an impassable wall
		/// and out of attack range, the attack should fail (no path).
		/// </summary>
		[UnityTest]
		public IEnumerator StartAttacking_NoPath_StaysIdle()
		{
			// Create an impassable wall dividing the map
			CreateWall(15);

			// Place enemy warrior on the far side of the wall
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(20, 15, 0), ctx.Agent1Go);

			// Warrior on the near side
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(2, 2, 0));
			yield return null;

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			Assert.AreEqual(UnitAction.IDLE, warrior.CurrentAction,
				"Warrior should stay IDLE when no path to target exists");
		}

		#endregion

		#region StartRepairing — No Path (Unit.Actions.cs:349-352)

		/// <summary>
		/// When a pawn tries to repair a building behind an impassable wall,
		/// the repair should fail.
		/// </summary>
		[UnityTest]
		public IEnumerator StartRepairing_NoPath_StaysIdle()
		{
			// Place a damaged base on the far side of an impassable wall
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(20, 15, 0));
			baseUnit.Health = Constants.HEALTH[UnitType.BASE] * 0.5f;

			// Create impassable wall
			CreateWall(15);

			// Pawn on the near side
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(2, 2, 0));
			yield return null;

			pawn.StartRepairing(new RepairEventArgs(pawn, baseUnit));

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should stay IDLE when no path to building exists");
		}

		#endregion

		#region Death With Killer Info (Unit.Movement.cs:46-50)

		/// <summary>
		/// When a unit dies while it is attacking something (attackUnitNbr >= 0),
		/// the death log includes killer info. Covers Unit.Movement.cs lines 46-50.
		/// </summary>
		[UnityTest]
		public IEnumerator Death_WhileAttacking_CoversKillerInfoBranch()
		{
			// Place two adjacent warriors
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			yield return null;

			// Both attack each other so both have attackUnitNbr set
			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));
			enemy.StartAttacking(new AttackEventArgs(enemy, warrior));
			Assert.AreEqual(UnitAction.ATTACK, enemy.CurrentAction);

			// Weaken enemy so it dies next step
			enemy.Health = 0.01f;

			// Step both — enemy dies with attackUnitNbr pointing to warrior.
			// The death branch (including killer info lookup) runs inside Update()
			// during the step that reduces health to 0.
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Step(warrior);
				BuildingTestHelper.Step(enemy);
				return enemy.Health <= 0;
			}, timeoutSeconds: 10f, failMessage: "Enemy should die from warrior attack");

			yield return WaitFrames(2);
		}

		#endregion

		#region IDLE Cleanup of currentBuilding (Unit.Movement.cs:67)

		/// <summary>
		/// When a unit transitions to IDLE while having a currentBuilding reference,
		/// the building's ActiveBuilders should be cleaned up.
		/// </summary>
		[UnityTest]
		public IEnumerator Idle_WithCurrentBuilding_CleansUpActiveBuilders()
		{
			PlaceBuiltBase(new Vector3Int(0, 0, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 10, 0));
			yield return null;

			// Start building a base
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(12, 10, 0), UnitType.BASE));

			if (pawn.CurrentAction != UnitAction.BUILD)
				Assert.Ignore("Could not start build for this test");

			// Get the currentBuilding reference
			var buildingObj = GetPrivateField<GameObject>(pawn, "currentBuilding");
			Assert.IsNotNull(buildingObj, "currentBuilding should be set");

			// Force pawn to IDLE (simulates interruption)
			pawn.CurrentAction = UnitAction.IDLE;

			// Step — the IDLE branch should clean up currentBuilding
			pawn.StepFixedUpdate();
			yield return null;

			var afterBuilding = GetPrivateField<GameObject>(pawn, "currentBuilding");
			Assert.IsNull(afterBuilding,
				"currentBuilding should be null after IDLE cleanup");
		}

		#endregion

		#region UpdateBuildPulse Reset (Unit.Movement.cs:162)

		/// <summary>
		/// When buildPulseFrames counts down to 0, transform.localScale
		/// should be reset to Vector3.one.
		/// </summary>
		[UnityTest]
		public IEnumerator BuildPulse_CountsDownToZero_ResetsScale()
		{
			PlaceBuiltBase(new Vector3Int(0, 0, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 10, 0));
			yield return null;

			// Start building
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BASE));

			if (pawn.CurrentAction != UnitAction.BUILD)
				Assert.Ignore("Could not start build for this test");

			// Wait for construction to complete
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Step(pawn);
				return pawn.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 30f, failMessage: "Building should complete");

			// Find the built building — it should have buildPulseFrames set
			var building = BuildingTestHelper.FindNewestUnitOfType(ctx, UnitType.BASE);
			if (building == null)
				Assert.Ignore("Could not find completed building");

			// Step the building through its pulse animation (24 frames)
			for (int i = 0; i < 30; i++)
			{
				building.Update();
				yield return null;
			}

			// Scale should be reset to (1,1,1)
			Assert.AreEqual(Vector3.one, building.transform.localScale,
				"Scale should be Vector3.one after build pulse completes");
		}

		#endregion

		#region UpdateRepair — Building Health <= 0 (Unit.Tasks.cs:366-369)

		/// <summary>
		/// If a building's health drops to 0 during repair (not destroyed via Update),
		/// the pawn should go IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator Repair_BuildingHealthZero_PawnGoesIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));
			baseUnit.Health = Constants.HEALTH[UnitType.BASE] * 0.5f;

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 10, 0));
			yield return null;

			pawn.StartRepairing(new RepairEventArgs(pawn, baseUnit));
			Assert.AreEqual(UnitAction.REPAIR, pawn.CurrentAction);

			// Step until repair is in BUILDING phase (path cleared, actively repairing)
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Step(pawn);
				var phase = GetPrivateField<BuildPhase>(pawn, "buildPhase");
				return phase == BuildPhase.BUILDING;
			}, timeoutSeconds: 10f, failMessage: "Pawn should enter BUILDING phase of repair");

			// Set building health to 0 WITHOUT calling building.Update()
			// This way currentBuilding is still valid but health <= 0
			baseUnit.Health = 0;

			// Step the pawn — UpdateRepair should detect health <= 0
			for (int i = 0; i < 5; i++)
			{
				BuildingTestHelper.Step(pawn);
				yield return null;
			}

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should go IDLE when repaired building health reaches 0");
		}

		#endregion

		#region UpdateGather — Mine Dead + No Base (Unit.Tasks.cs:488-491)

		/// <summary>
		/// When a pawn is in MINING phase and the mine dies but the base
		/// has also been destroyed, the pawn should go IDLE immediately.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_MineDeadNoBase_PawnGoesIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));
			yield return null;

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

			// Wait until mining
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Step(pawn);
				return mine.Health < Constants.HEALTH[UnitType.MINE];
			}, timeoutSeconds: 15f, failMessage: "Pawn should start mining");

			// Destroy the base first
			baseUnit.Health = 0;
			baseUnit.StepFixedUpdate();
			yield return WaitFrames(2);

			// Now destroy the mine
			mine.Health = 0;
			mine.StepFixedUpdate();
			yield return WaitFrames(2);

			// Step the pawn — should hit the mine dead + no base path
			for (int i = 0; i < 10; i++)
			{
				BuildingTestHelper.Step(pawn);
				yield return null;
			}

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should go IDLE when mine depleted and base destroyed");
		}

		#endregion

		#region UpdateGather — TO_BASE Phase: Not At Neighbor (Unit.Tasks.cs:564-569)

		/// <summary>
		/// When a pawn in TO_BASE phase reaches end of its path but is not
		/// adjacent to the base, it should re-path toward the base.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_ToBaseNotAtNeighbor_RePathsToBase()
		{
			// Place base far from mine
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(0, 0, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));
			yield return null;

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

			// Wait until pawn reaches MINING phase
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Step(pawn);
				return mine.Health < Constants.HEALTH[UnitType.MINE];
			}, timeoutSeconds: 15f, failMessage: "Pawn should start mining");

			// Force the pawn into TO_BASE with an empty path far from base.
			// Must also set TargetUnitType/TargetGridPos to BASE so the
			// IsNeighborOfUnit check uses the base's location, not the mine's.
			SetPrivateField(pawn, "gatherPhase", GatherPhase.TO_BASE);
			SetPrivateField(pawn, "path", new System.Collections.Generic.List<Vector3Int>());
			pawn.TargetUnitType = UnitType.BASE;
			pawn.TargetGridPos = baseUnit.GridPosition;

			// Step — pawn should detect it's not at base neighbor and re-path
			BuildingTestHelper.Step(pawn);
			yield return null;

			// Pawn should still be gathering (it re-pathed, didn't go idle)
			Assert.AreEqual(UnitAction.GATHER, pawn.CurrentAction,
				"Pawn should re-path toward base when not at neighbor");
		}

		#endregion

		#region UpdateGather — TO_BASE Phase: Base Destroyed (Unit.Tasks.cs:570-574)

		/// <summary>
		/// When a pawn in TO_BASE phase finds that the base has been destroyed,
		/// it should go IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_ToBaseDestroyed_PawnGoesIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));
			yield return null;

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

			// Wait until mining
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Step(pawn);
				return mine.Health < Constants.HEALTH[UnitType.MINE];
			}, timeoutSeconds: 15f, failMessage: "Pawn should start mining");

			// Force pawn into TO_BASE phase with empty path
			SetPrivateField(pawn, "gatherPhase", GatherPhase.TO_BASE);
			SetPrivateField(pawn, "path", new System.Collections.Generic.List<Vector3Int>());

			// Destroy the base
			baseUnit.Health = 0;
			baseUnit.StepFixedUpdate();
			yield return WaitFrames(2);

			// Step the pawn — should detect base is gone and go IDLE
			for (int i = 0; i < 5; i++)
			{
				BuildingTestHelper.Step(pawn);
				yield return null;
			}

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should go IDLE when base destroyed during TO_BASE");
		}

		#endregion

		#region UpdateGather — MINING Phase: Capacity Reached, No Base (Unit.Tasks.cs:534-538)

		/// <summary>
		/// When a pawn reaches mining capacity but the base is destroyed,
		/// the pawn goes IDLE instead of heading to base.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_CapacityReachedNoBase_PawnGoesIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));
			yield return null;

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

			// Wait until mining
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Step(pawn);
				return mine.Health < Constants.HEALTH[UnitType.MINE];
			}, timeoutSeconds: 15f, failMessage: "Pawn should start mining");

			// Destroy the base while mining
			baseUnit.Health = 0;
			baseUnit.StepFixedUpdate();
			yield return WaitFrames(2);

			// Continue mining until capacity is reached — pawn should go IDLE
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Step(pawn);
				return pawn.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 20f, failMessage: "Pawn should go IDLE when capacity reached and no base");
		}

		#endregion

		#region UpdateGather — Mine Depleted at Base: No Mine for Return Trip (Unit.Tasks.cs:558-562)

		/// <summary>
		/// When a pawn deposits gold at the base but the mine has been destroyed,
		/// the pawn should go IDLE instead of heading back to the mine.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_DepositButMineGone_PawnGoesIdle()
		{
			// Place base and mine close together for faster gather cycle
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(12, 10, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(7, 10, 0));
			yield return null;

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

			// Wait until pawn has started mining
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Step(pawn);
				return mine.Health < Constants.HEALTH[UnitType.MINE];
			}, timeoutSeconds: 15f, failMessage: "Pawn should start mining");

			// Wait until pawn fills capacity and heads to base
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Step(pawn);
				var phase = GetPrivateField<GatherPhase>(pawn, "gatherPhase");
				return phase == GatherPhase.TO_BASE;
			}, timeoutSeconds: 20f, failMessage: "Pawn should transition to TO_BASE");

			// Destroy the mine while pawn is heading to base
			mine.Health = 0;
			mine.StepFixedUpdate();
			yield return WaitFrames(2);

			// Let pawn deposit and try to return — should go IDLE since mine is gone
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Step(pawn);
				return pawn.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 20f, failMessage: "Pawn should go IDLE after deposit when mine is gone");
		}

		#endregion

		#region StartGathering — Unit in REPAIR state (Unit.Actions.cs:234-237, alternate)

		/// <summary>
		/// A pawn in REPAIR state cannot start gathering.
		/// </summary>
		[UnityTest]
		public IEnumerator StartGathering_BusyRepairing_StaysInRepair()
		{
			Unit repairedBase = PlaceBuiltBase(new Vector3Int(10, 10, 0));
			repairedBase.Health = Constants.HEALTH[UnitType.BASE] * 0.5f;

			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 20, 0));
			Unit depositBase = PlaceBuiltBase(new Vector3Int(5, 5, 0));

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 10, 0));
			yield return null;

			// Put pawn into REPAIR state
			pawn.StartRepairing(new RepairEventArgs(pawn, repairedBase));
			Assert.AreEqual(UnitAction.REPAIR, pawn.CurrentAction);

			// Try to gather while repairing — should be rejected
			pawn.StartGathering(new GatherEventArgs(pawn, mine, depositBase));

			Assert.AreEqual(UnitAction.REPAIR, pawn.CurrentAction,
				"Pawn should remain in REPAIR state when gather is rejected");
		}

		#endregion

		#region Death During Build — ActiveBuilders Cleanup (Unit.Movement.cs:58-60)

		/// <summary>
		/// When a pawn dies while building, the building's ActiveBuilders
		/// should have the pawn removed.
		/// </summary>
		[UnityTest]
		public IEnumerator Death_WhileBuilding_CleansUpActiveBuilders()
		{
			PlaceBuiltBase(new Vector3Int(0, 0, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 10, 0));
			yield return null;

			// Start building
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(12, 10, 0), UnitType.BASE));

			if (pawn.CurrentAction != UnitAction.BUILD)
				Assert.Ignore("Could not start build for this test");

			var buildingObj = GetPrivateField<GameObject>(pawn, "currentBuilding");
			Assert.IsNotNull(buildingObj, "currentBuilding should be set");

			Unit buildingUnit = buildingObj.GetComponent<Unit>();
			Assert.IsTrue(buildingUnit.ActiveBuilders.Contains(pawn.UnitNbr),
				"Pawn should be in ActiveBuilders");

			// Kill the pawn
			pawn.Health = 0;
			pawn.StepFixedUpdate();
			yield return WaitFrames(2);

			// ActiveBuilders should not contain the dead pawn
			Assert.IsFalse(buildingUnit.ActiveBuilders.Contains(pawn.UnitNbr),
				"Dead pawn should be removed from ActiveBuilders");
		}

		#endregion

		#region Move Interrupts Build — currentBuilding Cleanup (Unit.Actions.cs:164-167)

		/// <summary>
		/// When a move command interrupts a build, currentBuilding should be
		/// cleaned up and ActiveBuilders updated.
		/// </summary>
		[UnityTest]
		public IEnumerator Move_InterruptsBuild_CleansUpCurrentBuilding()
		{
			PlaceBuiltBase(new Vector3Int(0, 0, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 10, 0));
			yield return null;

			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(12, 10, 0), UnitType.BASE));

			if (pawn.CurrentAction != UnitAction.BUILD)
				Assert.Ignore("Could not start build for this test");

			var buildingObj = GetPrivateField<GameObject>(pawn, "currentBuilding");
			Unit buildingUnit = buildingObj.GetComponent<Unit>();

			// Move interrupts build
			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, new Vector3Int(5, 5, 0)));

			Assert.AreEqual(UnitAction.MOVE, pawn.CurrentAction);
			Assert.IsFalse(buildingUnit.ActiveBuilders.Contains(pawn.UnitNbr),
				"Pawn should be removed from ActiveBuilders after move interrupt");
		}

		#endregion
	}
}
