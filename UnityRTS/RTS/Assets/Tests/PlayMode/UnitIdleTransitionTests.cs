using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests verifying that units correctly transition to IDLE
	/// after each action type completes naturally.
	/// </summary>
	[TestFixture]
	public class UnitIdleTransitionTests : PlayModeTestBase
	{
		#region Freshly Placed Units

		/// <summary>
		/// A freshly placed unit starts in IDLE state.
		/// </summary>
		[UnityTest]
		public IEnumerator FreshUnit_StartsIdle()
		{
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(6, 5, 0));
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(7, 5, 0));

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction, "Fresh WORKER should be IDLE");
			Assert.AreEqual(UnitAction.IDLE, soldier.CurrentAction, "Fresh SOLDIER should be IDLE");
			Assert.AreEqual(UnitAction.IDLE, archer.CurrentAction, "Fresh ARCHER should be IDLE");

			yield return null;
		}

		#endregion

		#region IDLE After Move

		/// <summary>
		/// A unit returns to IDLE after completing a short move.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_AfterMove_GoesIdle()
		{
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			worker.StartMoving(new MoveEventArgs(worker, worker.UnitType, new Vector3Int(7, 5, 0)));

			Assert.AreEqual(UnitAction.MOVE, worker.CurrentAction);

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Worker did not return to IDLE after completing move");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker should be IDLE after arriving at destination");
		}

		#endregion

		#region IDLE After Kill

		/// <summary>
		/// A SOLDIER returns to IDLE after its weakened target is killed.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_AfterKill_GoesIdle()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(8, 10, 0));
			Unit enemy = CombatTestHelper.PlaceWeakEnemy(ctx, UnitType.WORKER,
				new Vector3Int(9, 10, 0));
			int enemyNbr = enemy.UnitNbr;

			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));

			yield return CombatTestHelper.WaitForDeath(ctx, enemyNbr, timeoutSeconds: 20f);

			yield return WaitUntil(
				() => soldier.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 10f,
				failMessage: "Soldier did not return to IDLE after killing enemy");

			Assert.AreEqual(UnitAction.IDLE, soldier.CurrentAction,
				"Soldier should be IDLE after its target is destroyed");
		}

		/// <summary>
		/// An ARCHER returns to IDLE after killing a weakened enemy.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_AfterKill_GoesIdle()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(7, 10, 0));
			Unit enemy = CombatTestHelper.PlaceWeakEnemy(ctx, UnitType.WORKER,
				new Vector3Int(9, 10, 0));
			int enemyNbr = enemy.UnitNbr;

			archer.StartAttacking(new AttackEventArgs(archer, enemy));

			yield return CombatTestHelper.WaitForDeath(ctx, enemyNbr, timeoutSeconds: 20f);

			yield return WaitUntil(
				() => archer.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 10f,
				failMessage: "Archer did not return to IDLE after kill");

			Assert.AreEqual(UnitAction.IDLE, archer.CurrentAction,
				"Archer should be IDLE after killing its target");
		}

		#endregion

		#region IDLE After Training

		/// <summary>
		/// BARRACKS returns to IDLE after completing SOLDIER training.
		/// </summary>
		[UnityTest]
		public IEnumerator Barracks_AfterTraining_GoesIdle()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.SOLDIER));
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction,
				"BARRACKS should enter TRAIN state");

			yield return WaitUntil(
				() => barracks.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "BARRACKS did not return to IDLE after training completed");

			Assert.AreEqual(UnitAction.IDLE, barracks.CurrentAction,
				"BARRACKS should be IDLE after training completes");
		}

		/// <summary>
		/// BASE returns to IDLE after completing WORKER training.
		/// </summary>
		[UnityTest]
		public IEnumerator Base_AfterTraining_GoesIdle()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));
			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction);

			yield return WaitUntil(
				() => baseUnit.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "BASE did not return to IDLE after training");

			Assert.AreEqual(UnitAction.IDLE, baseUnit.CurrentAction,
				"BASE should be IDLE after WORKER training completes");
		}

		#endregion

		#region IDLE After Gather to Depleted Mine

		/// <summary>
		/// Worker goes to IDLE when the mine it was gathering from is depleted.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_MineDepletedDuringGather_GoesIdle()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(10, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));

			worker.StartGathering(new GatherEventArgs(worker, mine, baseUnit));

			// Deplete the mine immediately
			mine.Health = 0;

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Worker did not go IDLE after mine was depleted");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker should be IDLE when its target mine is depleted");
		}

		#endregion
	}
}
