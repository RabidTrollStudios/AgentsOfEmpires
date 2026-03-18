using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Tests for Agent.Commands.cs — the command validation error paths
	/// (Move, Build, Gather, Train, Attack, Repair).
	/// Each command has multiple early-return branches for null units,
	/// capability checks, invalid positions, missing dependencies, etc.
	/// </summary>
	[TestFixture]
	public class AgentCommandTests : PlayModeTestBase
	{
		private Agent agent;

		private Agent SetUpAgent()
		{
			if (agent == null)
				agent = GetAgent0();
			return agent;
		}

		// ── Move ──────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Move_NullUnit_ReturnsUnitNotFound()
		{
			yield return null;
			var a = SetUpAgent();
			var result = a.Move(null, new Vector3Int(5, 5, 0));
			Assert.AreEqual(CommandResult.UNIT_NOT_FOUND, result);
		}

		[UnityTest]
		public IEnumerator Move_BuildingUnit_ReturnsCannotPerformAction()
		{
			yield return null;
			var a = SetUpAgent();
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;

			var result = a.Move(baseUnit, new Vector3Int(5, 5, 0));
			Assert.AreEqual(CommandResult.UNIT_CANNOT_PERFORM_ACTION, result);
		}

		[UnityTest]
		public IEnumerator Move_InvalidPosition_ReturnsInvalidPosition()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			var result = a.Move(pawn, new Vector3Int(-1, -1, 0));
			Assert.AreEqual(CommandResult.INVALID_POSITION, result);
		}

		[UnityTest]
		public IEnumerator Move_UnwalkablePosition_ReturnsPositionNotWalkable()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			// Place a building to make a cell unwalkable
			var blocker = PlaceUnit(UnitType.BASE, new Vector3Int(8, 8, 0));
			blocker.IsBuilt = true;

			var result = a.Move(pawn, new Vector3Int(8, 8, 0));
			Assert.AreEqual(CommandResult.POSITION_NOT_WALKABLE, result);
		}

		[UnityTest]
		public IEnumerator Move_ValidPawn_ReturnsSuccess()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			var result = a.Move(pawn, new Vector3Int(10, 10, 0));
			Assert.AreEqual(CommandResult.SUCCESS, result);
		}

		// ── Build ─────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Build_NullUnit_ReturnsUnitNotFound()
		{
			yield return null;
			var a = SetUpAgent();
			var result = a.Build(null, new Vector3Int(5, 5, 0), UnitType.BASE);
			Assert.AreEqual(CommandResult.UNIT_NOT_FOUND, result);
		}

		[UnityTest]
		public IEnumerator Build_WarriorUnit_ReturnsCannotPerformAction()
		{
			yield return null;
			var a = SetUpAgent();
			var warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));

			var result = a.Build(warrior, new Vector3Int(10, 10, 0), UnitType.BASE);
			Assert.AreEqual(CommandResult.UNIT_CANNOT_PERFORM_ACTION, result);
		}

		[UnityTest]
		public IEnumerator Build_PawnCannotBuildWarrior_ReturnsCannotPerformAction()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			// Pawns can build buildings, not mobile units
			var result = a.Build(pawn, new Vector3Int(10, 10, 0), UnitType.WARRIOR);
			Assert.AreEqual(CommandResult.UNIT_CANNOT_PERFORM_ACTION, result);
		}

		[UnityTest]
		public IEnumerator Build_InvalidPosition_ReturnsInvalidPosition()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			var result = a.Build(pawn, new Vector3Int(-1, -1, 0), UnitType.BASE);
			Assert.AreEqual(CommandResult.INVALID_POSITION, result);
		}

		[UnityTest]
		public IEnumerator Build_AreaNotBuildable_ReturnsAreaNotBuildable()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			// Place a building to block the target area
			var blocker = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			blocker.IsBuilt = true;

			var result = a.Build(pawn, new Vector3Int(10, 10, 0), UnitType.BASE);
			Assert.AreEqual(CommandResult.AREA_NOT_BUILDABLE, result);
		}

		[UnityTest]
		public IEnumerator Build_MissingDependency_ReturnsMissingDependency()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			// BARRACKS requires a built BASE — no base exists
			var result = a.Build(pawn, new Vector3Int(10, 10, 0), UnitType.BARRACKS);
			Assert.AreEqual(CommandResult.MISSING_DEPENDENCY, result);
		}

		[UnityTest]
		public IEnumerator Build_ValidPawnAndBase_ReturnsSuccess()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			// BASE has no dependencies
			var result = a.Build(pawn, new Vector3Int(10, 10, 0), UnitType.BASE);
			Assert.AreEqual(CommandResult.SUCCESS, result);
		}

		// ── Gather ────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Gather_NullUnit_ReturnsUnitNotFound()
		{
			yield return null;
			var a = SetUpAgent();
			var result = a.Gather(null, null, null);
			Assert.AreEqual(CommandResult.UNIT_NOT_FOUND, result);
		}

		[UnityTest]
		public IEnumerator Gather_NullResource_ReturnsTargetNotFound()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			var result = a.Gather(pawn, null, null);
			Assert.AreEqual(CommandResult.TARGET_NOT_FOUND, result);
		}

		[UnityTest]
		public IEnumerator Gather_ResourceNotMine_ReturnsInvalidTarget()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;

			// Pass a BASE as the resource — should fail
			var result = a.Gather(pawn, baseUnit, null);
			Assert.AreEqual(CommandResult.INVALID_TARGET, result);
		}

		[UnityTest]
		public IEnumerator Gather_NullBase_ReturnsTargetNotFound()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));

			var result = a.Gather(pawn, mine, null);
			Assert.AreEqual(CommandResult.TARGET_NOT_FOUND, result);
		}

		[UnityTest]
		public IEnumerator Gather_BaseNotBaseType_ReturnsInvalidTarget()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));
			var barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(20, 20, 0));

			// Pass BARRACKS as the base — should fail
			var result = a.Gather(pawn, mine, barracks);
			Assert.AreEqual(CommandResult.INVALID_TARGET, result);
		}

		[UnityTest]
		public IEnumerator Gather_WarriorCannotGather_ReturnsCannotPerformAction()
		{
			yield return null;
			var a = SetUpAgent();
			var warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			var mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(20, 20, 0));
			baseUnit.IsBuilt = true;

			var result = a.Gather(warrior, mine, baseUnit);
			Assert.AreEqual(CommandResult.UNIT_CANNOT_PERFORM_ACTION, result);
		}

		[UnityTest]
		public IEnumerator Gather_ValidPawn_ReturnsSuccess()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(20, 20, 0));
			baseUnit.IsBuilt = true;

			var result = a.Gather(pawn, mine, baseUnit);
			Assert.AreEqual(CommandResult.SUCCESS, result);
		}

		// ── Train ─────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Train_NullUnit_ReturnsUnitNotFound()
		{
			yield return null;
			var a = SetUpAgent();
			var result = a.Train(null, UnitType.PAWN);
			Assert.AreEqual(CommandResult.UNIT_NOT_FOUND, result);
		}

		[UnityTest]
		public IEnumerator Train_PawnCannotTrain_ReturnsCannotPerformAction()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			var result = a.Train(pawn, UnitType.WARRIOR);
			Assert.AreEqual(CommandResult.UNIT_CANNOT_PERFORM_ACTION, result);
		}

		[UnityTest]
		public IEnumerator Train_UnbuiltBuilding_ReturnsBuildingNotFinished()
		{
			yield return null;
			var a = SetUpAgent();
			var barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			// Not setting IsBuilt = true

			var result = a.Train(barracks, UnitType.WARRIOR);
			Assert.AreEqual(CommandResult.BUILDING_NOT_FINISHED, result);
		}

		[UnityTest]
		public IEnumerator Train_BarracksCannotTrainPawn_ReturnsCannotPerformAction()
		{
			yield return null;
			var a = SetUpAgent();
			var barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			// BARRACKS can't train PAWN
			var result = a.Train(barracks, UnitType.PAWN);
			Assert.AreEqual(CommandResult.UNIT_CANNOT_PERFORM_ACTION, result);
		}

		[UnityTest]
		public IEnumerator Train_ValidBarracksWarrior_ReturnsSuccess()
		{
			yield return null;
			var a = SetUpAgent();
			var barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			var result = a.Train(barracks, UnitType.WARRIOR);
			Assert.AreEqual(CommandResult.SUCCESS, result);
		}

		// ── Attack ────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Attack_NullUnit_ReturnsUnitNotFound()
		{
			yield return null;
			var a = SetUpAgent();
			var result = a.Attack(null, null);
			Assert.AreEqual(CommandResult.UNIT_NOT_FOUND, result);
		}

		[UnityTest]
		public IEnumerator Attack_NullTarget_ReturnsTargetNotFound()
		{
			yield return null;
			var a = SetUpAgent();
			var warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));

			var result = a.Attack(warrior, null);
			Assert.AreEqual(CommandResult.TARGET_NOT_FOUND, result);
		}

		[UnityTest]
		public IEnumerator Attack_PawnCannotAttack_ReturnsCannotPerformAction()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(6, 6, 0), ctx.Agent1Go);

			var result = a.Attack(pawn, enemy);
			Assert.AreEqual(CommandResult.UNIT_CANNOT_PERFORM_ACTION, result);
		}

		[UnityTest]
		public IEnumerator Attack_TargetIsMine_ReturnsInvalidTarget()
		{
			yield return null;
			var a = SetUpAgent();
			var warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			var mine = PlaceUnit(UnitType.MINE, new Vector3Int(10, 10, 0));

			var result = a.Attack(warrior, mine);
			Assert.AreEqual(CommandResult.INVALID_TARGET, result);
		}

		[UnityTest]
		public IEnumerator Attack_FriendlyFire_ReturnsFriendlyFire()
		{
			yield return null;
			var a = SetUpAgent();
			var warrior1 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			var warrior2 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(6, 6, 0));

			var result = a.Attack(warrior1, warrior2);
			Assert.AreEqual(CommandResult.FRIENDLY_FIRE, result);
		}

		[UnityTest]
		public IEnumerator Attack_ValidWarriorVsEnemy_ReturnsSuccess()
		{
			yield return null;
			var a = SetUpAgent();
			var warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			var enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(6, 6, 0), ctx.Agent1Go);

			var result = a.Attack(warrior, enemy);
			Assert.AreEqual(CommandResult.SUCCESS, result);
		}

		// ── Repair ────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Repair_NullUnit_ReturnsUnitNotFound()
		{
			yield return null;
			var a = SetUpAgent();
			var result = a.Repair(null, null);
			Assert.AreEqual(CommandResult.UNIT_NOT_FOUND, result);
		}

		[UnityTest]
		public IEnumerator Repair_NullBuilding_ReturnsTargetNotFound()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			var result = a.Repair(pawn, null);
			Assert.AreEqual(CommandResult.TARGET_NOT_FOUND, result);
		}

		[UnityTest]
		public IEnumerator Repair_WarriorCannotRepair_ReturnsCannotPerformAction()
		{
			yield return null;
			var a = SetUpAgent();
			var warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;

			var result = a.Repair(warrior, baseUnit);
			Assert.AreEqual(CommandResult.UNIT_CANNOT_PERFORM_ACTION, result);
		}

		[UnityTest]
		public IEnumerator Repair_TargetIsMobileUnit_ReturnsInvalidTarget()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(6, 6, 0));

			// Warrior is mobile — can't be repaired
			var result = a.Repair(pawn, warrior);
			Assert.AreEqual(CommandResult.INVALID_TARGET, result);
		}

		[UnityTest]
		public IEnumerator Repair_TargetIsMine_ReturnsInvalidTarget()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var mine = PlaceUnit(UnitType.MINE, new Vector3Int(10, 10, 0));

			var result = a.Repair(pawn, mine);
			Assert.AreEqual(CommandResult.INVALID_TARGET, result);
		}

		[UnityTest]
		public IEnumerator Repair_UnbuiltBuilding_ReturnsBuildingNotFinished()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			// Not setting IsBuilt

			var result = a.Repair(pawn, baseUnit);
			Assert.AreEqual(CommandResult.BUILDING_NOT_FINISHED, result);
		}

		[UnityTest]
		public IEnumerator Repair_EnemyBuilding_ReturnsFriendlyFire()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var enemyBase = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0), ctx.Agent1Go);
			enemyBase.IsBuilt = true;

			var result = a.Repair(pawn, enemyBase);
			Assert.AreEqual(CommandResult.FRIENDLY_FIRE, result);
		}

		[UnityTest]
		public IEnumerator Repair_ValidPawnAndOwnBuilding_ReturnsSuccess()
		{
			yield return null;
			var a = SetUpAgent();
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;

			var result = a.Repair(pawn, baseUnit);
			Assert.AreEqual(CommandResult.SUCCESS, result);
		}
	}
}
