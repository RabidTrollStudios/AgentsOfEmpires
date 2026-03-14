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
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(6, 5, 0));
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(7, 5, 0));

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction, "Fresh PAWN should be IDLE");
			Assert.AreEqual(UnitAction.IDLE, warrior.CurrentAction, "Fresh WARRIOR should be IDLE");
			Assert.AreEqual(UnitAction.IDLE, archer.CurrentAction, "Fresh ARCHER should be IDLE");

			yield return null;
		}

		#endregion

		#region IDLE After Move

		/// <summary>
		/// A unit returns to IDLE after completing a short move.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_AfterMove_GoesIdle()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			pawn.StartMoving(new MoveEventArgs(pawn, pawn.UnitType, new Vector3Int(7, 5, 0)));

			Assert.AreEqual(UnitAction.MOVE, pawn.CurrentAction);

			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Pawn did not return to IDLE after completing move");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should be IDLE after arriving at destination");
		}

		#endregion

		#region IDLE After Kill

		/// <summary>
		/// A WARRIOR returns to IDLE after its weakened target is killed.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_AfterKill_GoesIdle()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(8, 10, 0));
			Unit enemy = CombatTestHelper.PlaceWeakEnemy(ctx, UnitType.PAWN,
				new Vector3Int(9, 10, 0));
			int enemyNbr = enemy.UnitNbr;

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			yield return CombatTestHelper.WaitForDeath(ctx, enemyNbr, timeoutSeconds: 20f);

			yield return WaitUntil(
				() => warrior.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 10f,
				failMessage: "Warrior did not return to IDLE after killing enemy");

			Assert.AreEqual(UnitAction.IDLE, warrior.CurrentAction,
				"Warrior should be IDLE after its target is destroyed");
		}

		/// <summary>
		/// An ARCHER returns to IDLE after killing a weakened enemy.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_AfterKill_GoesIdle()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(7, 10, 0));
			Unit enemy = CombatTestHelper.PlaceWeakEnemy(ctx, UnitType.PAWN,
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
		/// BARRACKS returns to IDLE after completing WARRIOR training.
		/// </summary>
		[UnityTest]
		public IEnumerator Barracks_AfterTraining_GoesIdle()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.WARRIOR));
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
		/// BASE returns to IDLE after completing PAWN training.
		/// </summary>
		[UnityTest]
		public IEnumerator Base_AfterTraining_GoesIdle()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.PAWN));
			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction);

			yield return WaitUntil(
				() => baseUnit.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "BASE did not return to IDLE after training");

			Assert.AreEqual(UnitAction.IDLE, baseUnit.CurrentAction,
				"BASE should be IDLE after PAWN training completes");
		}

		#endregion

		#region IDLE After Gather to Depleted Mine

		/// <summary>
		/// Pawn goes to IDLE when the mine it was gathering from is depleted.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_MineDepletedDuringGather_GoesIdle()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			// Mine and pawn must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

			// Deplete the mine immediately
			mine.Health = 0;

			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Pawn did not go IDLE after mine was depleted");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should be IDLE when its target mine is depleted");
		}

		#endregion
	}
}
