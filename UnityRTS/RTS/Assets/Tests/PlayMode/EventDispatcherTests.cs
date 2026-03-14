using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for EventDispatcher validation logic.
	/// Covers all six event handlers' happy paths and error branches:
	/// null parameters, unit not found, wrong agent (cheater), capability checks,
	/// invalid positions, missing dependencies, and friendly fire.
	/// </summary>
	[TestFixture]
	public class EventDispatcherTests : PlayModeTestBase
	{
		private EventDispatcher dispatcher;
		private Agent agent0;
		private Agent agent1;

		[UnitySetUp]
		public IEnumerator SetUpDispatcher()
		{
			dispatcher = GameManager.Instance.Events;
			agent0 = ctx.GetAgent(0);
			agent1 = ctx.GetAgent(1);
			yield return null;
		}

		#region MoveEventHandler

		[UnityTest]
		public IEnumerator Move_NullUnit_LogsErrorAndReturns()
		{
			var args = new MoveEventArgs(null, UnitType.PAWN, new Vector3Int(5, 5, 0));
			dispatcher.MoveEventHandler(agent0, args);
			yield return null;
			// No exception thrown = null guard works
		}

		[UnityTest]
		public IEnumerator Move_UnitNotFound_LogsErrorAndReturns()
		{
			// Create a unit then destroy it so GetUnit returns null
			var tempUnit = PlaceUnit(UnitType.PAWN, new Vector3Int(6, 6, 0));
			ctx.UnitManager.DestroyUnit(tempUnit.gameObject);
			yield return null; // Let Destroy process

			var args = new MoveEventArgs(tempUnit, UnitType.PAWN, new Vector3Int(10, 10, 0));
			dispatcher.MoveEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Move_WrongAgent_LogsCheatError()
		{
			// Agent 0's unit, but agent 1 sends the command
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var args = new MoveEventArgs(pawn, UnitType.PAWN, new Vector3Int(10, 10, 0));
			dispatcher.MoveEventHandler(agent1, args);
			yield return null;
			// Pawn should NOT be moving (command was rejected)
			Assert.AreNotEqual(UnitAction.MOVE, pawn.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Move_UnitCannotMove_LogsError()
		{
			// BASE can't move
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			var args = new MoveEventArgs(baseUnit, UnitType.BASE, new Vector3Int(10, 10, 0));
			dispatcher.MoveEventHandler(agent0, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.MOVE, baseUnit.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Move_InvalidGridLocation_LogsError()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			// Target outside map bounds
			var args = new MoveEventArgs(pawn, UnitType.PAWN, new Vector3Int(-1, -1, 0));
			dispatcher.MoveEventHandler(agent0, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.MOVE, pawn.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Move_ValidCommand_UnitStartsMoving()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			yield return null;
			var args = new MoveEventArgs(pawn, UnitType.PAWN, new Vector3Int(10, 10, 0));
			dispatcher.MoveEventHandler(agent0, args);
			yield return null;
			Assert.AreEqual(UnitAction.MOVE, pawn.CurrentAction);
		}

		#endregion

		#region BuildEventHandler

		[UnityTest]
		public IEnumerator Build_NullUnit_LogsErrorAndReturns()
		{
			var args = new BuildEventArgs(null, new Vector3Int(10, 10, 0), UnitType.BASE);
			dispatcher.BuildEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Build_UnitNotFound_LogsError()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			ctx.UnitManager.DestroyUnit(pawn.gameObject);
			yield return null;

			var args = new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BASE);
			dispatcher.BuildEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Build_WrongAgent_LogsCheatError()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var args = new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BASE);
			dispatcher.BuildEventHandler(agent1, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.BUILD, pawn.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Build_UnitCannotBuild_LogsError()
		{
			// WARRIOR can't build
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			var args = new BuildEventArgs(warrior, new Vector3Int(10, 10, 0), UnitType.BASE);
			dispatcher.BuildEventHandler(agent0, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.BUILD, warrior.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Build_UnitCannotBuildType_LogsError()
		{
			// PAWN can build, but not WARRIOR (not in BUILDS[PAWN])
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var args = new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.WARRIOR);
			dispatcher.BuildEventHandler(agent0, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.BUILD, pawn.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Build_InvalidPosition_LogsError()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var args = new BuildEventArgs(pawn, new Vector3Int(-1, -1, 0), UnitType.BASE);
			dispatcher.BuildEventHandler(agent0, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.BUILD, pawn.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Build_AreaNotBuildable_LogsError()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			// Place a building on the target area so it's not buildable
			Unit existing = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			yield return null;

			var args = new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BASE);
			dispatcher.BuildEventHandler(agent0, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.BUILD, pawn.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Build_MissingDependency_LogsError()
		{
			// BARRACKS requires a built BASE — don't place one
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			yield return null;
			var args = new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BARRACKS);
			dispatcher.BuildEventHandler(agent0, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.BUILD, pawn.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Build_ValidCommand_UnitStartsBuilding()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			yield return null;
			var args = new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BASE);
			dispatcher.BuildEventHandler(agent0, args);
			yield return null;
			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction);
		}

		#endregion

		#region GatherEventHandler

		[UnityTest]
		public IEnumerator Gather_NullUnit_LogsErrorAndReturns()
		{
			var args = new GatherEventArgs(null, null, null);
			dispatcher.GatherEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Gather_UnitNotFound_LogsError()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(0, 5, 0));
			baseUnit.IsBuilt = true;
			ctx.UnitManager.DestroyUnit(pawn.gameObject);
			yield return null;

			var args = new GatherEventArgs(pawn, mine, baseUnit);
			dispatcher.GatherEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Gather_WrongAgent_LogsCheatError()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(0, 5, 0));
			baseUnit.IsBuilt = true;
			yield return null;

			var args = new GatherEventArgs(pawn, mine, baseUnit);
			dispatcher.GatherEventHandler(agent1, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.GATHER, pawn.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Gather_UnitCannotGather_LogsError()
		{
			// WARRIOR can't gather
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(0, 5, 0));
			baseUnit.IsBuilt = true;
			yield return null;

			var args = new GatherEventArgs(warrior, mine, baseUnit);
			dispatcher.GatherEventHandler(agent0, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.GATHER, warrior.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Gather_NotFromMine_LogsError()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			// Try to gather from a BARRACKS instead of MINE
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(15, 15, 0));
			barracks.IsBuilt = true;
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(0, 5, 0));
			baseUnit.IsBuilt = true;
			yield return null;

			var args = new GatherEventArgs(pawn, barracks, baseUnit);
			dispatcher.GatherEventHandler(agent0, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.GATHER, pawn.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Gather_BaseNotBase_LogsError()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));
			// Try to deposit at a BARRACKS instead of BASE
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(0, 5, 0));
			barracks.IsBuilt = true;
			yield return null;

			var args = new GatherEventArgs(pawn, mine, barracks);
			dispatcher.GatherEventHandler(agent0, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.GATHER, pawn.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Gather_WrongBaseOwner_LogsError()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));
			// Base owned by agent 1
			Unit enemyBase = PlaceUnit(UnitType.BASE, new Vector3Int(0, 5, 0), ctx.Agent1Go);
			enemyBase.IsBuilt = true;
			yield return null;

			var args = new GatherEventArgs(pawn, mine, enemyBase);
			dispatcher.GatherEventHandler(agent0, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.GATHER, pawn.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Gather_ValidCommand_UnitStartsGathering()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(0, 5, 0));
			baseUnit.IsBuilt = true;
			yield return null;

			var args = new GatherEventArgs(pawn, mine, baseUnit);
			dispatcher.GatherEventHandler(agent0, args);
			yield return null;
			Assert.AreEqual(UnitAction.GATHER, pawn.CurrentAction);
		}

		#endregion

		#region TrainEventHandler

		[UnityTest]
		public IEnumerator Train_NullUnit_LogsErrorAndReturns()
		{
			var args = new TrainEventArgs(null, UnitType.PAWN);
			dispatcher.TrainEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Train_UnitNotFound_LogsError()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;
			ctx.UnitManager.DestroyUnit(barracks.gameObject);
			yield return null;

			var args = new TrainEventArgs(barracks, UnitType.WARRIOR);
			dispatcher.TrainEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Train_WrongAgent_LogsCheatError()
		{
			// Place a built BASE (dependency for BARRACKS)
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(0, 5, 0));
			baseUnit.IsBuilt = true;
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;
			yield return null;

			var args = new TrainEventArgs(barracks, UnitType.WARRIOR);
			dispatcher.TrainEventHandler(agent1, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Train_UnitCannotTrain_LogsError()
		{
			// PAWN can't train
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			yield return null;

			var args = new TrainEventArgs(pawn, UnitType.WARRIOR);
			dispatcher.TrainEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Train_MissingDependency_LogsError()
		{
			// WARRIOR dep=[BARRACKS]. BASE CAN train PAWN (PAWN dep=[BASE]).
			// For every trainable unit, the trainer IS the dependency, so the branch
			// is unreachable with current game constants. We exercise it by temporarily
			// mutating DEPENDENCY to add an unsatisfied dep.
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(0, 5, 0));
			baseUnit.IsBuilt = true;
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;
			yield return null;

			// Temporarily add ARCHERY as a WARRIOR dependency (not present on the map)
			Constants.DEPENDENCY[UnitType.WARRIOR].Add(UnitType.ARCHERY);
			try
			{
				var args = new TrainEventArgs(barracks, UnitType.WARRIOR);
				dispatcher.TrainEventHandler(agent0, args);
				yield return null;
				Assert.AreNotEqual(UnitAction.TRAIN, barracks.CurrentAction);
			}
			finally
			{
				Constants.DEPENDENCY[UnitType.WARRIOR].Remove(UnitType.ARCHERY);
			}
		}

		[UnityTest]
		public IEnumerator Train_ValidCommand_UnitStartsTraining()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(0, 5, 0));
			baseUnit.IsBuilt = true;
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;
			yield return null;

			var args = new TrainEventArgs(barracks, UnitType.WARRIOR);
			dispatcher.TrainEventHandler(agent0, args);
			yield return null;
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction);
		}

		#endregion

		#region RepairEventHandler

		[UnityTest]
		public IEnumerator Repair_NullPawn_LogsErrorAndReturns()
		{
			var args = new RepairEventArgs(null, null);
			dispatcher.RepairEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Repair_UnitNotFound_LogsError()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;
			ctx.UnitManager.DestroyUnit(pawn.gameObject);
			yield return null;

			var args = new RepairEventArgs(pawn, baseUnit);
			dispatcher.RepairEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Repair_WrongAgent_LogsCheatError()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;
			yield return null;

			var args = new RepairEventArgs(pawn, baseUnit);
			dispatcher.RepairEventHandler(agent1, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Repair_UnitCannotRepair_LogsError()
		{
			// WARRIOR can't repair (CanBuild = false)
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;
			yield return null;

			var args = new RepairEventArgs(warrior, baseUnit);
			dispatcher.RepairEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Repair_BuildingNotFound_LogsError()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			ctx.UnitManager.DestroyUnit(baseUnit.gameObject);
			yield return null;

			var args = new RepairEventArgs(pawn, baseUnit);
			dispatcher.RepairEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Repair_BuildingNotFinished_LogsError()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			// Don't set IsBuilt = true
			yield return null;

			var args = new RepairEventArgs(pawn, baseUnit);
			dispatcher.RepairEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Repair_ValidCommand_UnitStartsRepairing()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;
			yield return null;

			var args = new RepairEventArgs(pawn, baseUnit);
			dispatcher.RepairEventHandler(agent0, args);
			yield return null;
			Assert.AreEqual(UnitAction.REPAIR, pawn.CurrentAction);
		}

		#endregion

		#region AttackEventHandler

		[UnityTest]
		public IEnumerator Attack_NullUnit_LogsErrorAndReturns()
		{
			var args = new AttackEventArgs(null, null);
			dispatcher.AttackEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Attack_UnitNotFound_LogsError()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(15, 15, 0), ctx.Agent1Go);
			ctx.UnitManager.DestroyUnit(warrior.gameObject);
			yield return null;

			var args = new AttackEventArgs(warrior, enemy);
			dispatcher.AttackEventHandler(agent0, args);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Attack_WrongAgent_LogsCheatError()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(15, 15, 0), ctx.Agent1Go);
			yield return null;

			var args = new AttackEventArgs(warrior, enemy);
			dispatcher.AttackEventHandler(agent1, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.ATTACK, warrior.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Attack_TargetNotFound_LogsError()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(15, 15, 0), ctx.Agent1Go);
			ctx.UnitManager.DestroyUnit(enemy.gameObject);
			yield return null;

			var args = new AttackEventArgs(warrior, enemy);
			dispatcher.AttackEventHandler(agent0, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.ATTACK, warrior.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Attack_FriendlyFire_LogsError()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit friendlyPawn = PlaceUnit(UnitType.PAWN, new Vector3Int(15, 15, 0));
			yield return null;

			var args = new AttackEventArgs(warrior, friendlyPawn);
			dispatcher.AttackEventHandler(agent0, args);
			yield return null;
			Assert.AreNotEqual(UnitAction.ATTACK, warrior.CurrentAction);
		}

		[UnityTest]
		public IEnumerator Attack_ValidCommand_UnitStartsAttacking()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(15, 15, 0), ctx.Agent1Go);
			yield return null;

			var args = new AttackEventArgs(warrior, enemy);
			dispatcher.AttackEventHandler(agent0, args);
			yield return null;
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);
		}

		#endregion
	}
}
