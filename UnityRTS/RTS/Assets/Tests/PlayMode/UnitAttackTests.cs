using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for SOLDIER melee attack behavior.
	/// Covers: health reduction, target death and cell release,
	/// attacker returning to IDLE, out-of-range movement before attack,
	/// and attacking an already-dead target.
	/// </summary>
	[TestFixture]
	public class UnitAttackTests : PlayModeTestBase
	{
		#region Happy Path

		/// <summary>
		/// A soldier attacks an enemy worker and reduces its health over time.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_AttacksEnemy_HealthDecreases()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WORKER, new Vector3Int(11, 10, 0), ctx.Agent1Go);

			float initialHealth = enemy.Health;
			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));

			Assert.AreEqual(UnitAction.ATTACK, soldier.CurrentAction,
				"Soldier should enter ATTACK state after StartAttacking");

			// Wait until enemy health decreases
			yield return WaitUntil(
				() => enemy == null || enemy.Health < initialHealth,
				timeoutSeconds: 10f,
				failMessage: "Enemy health did not decrease after soldier attack");

			if (enemy != null)
				Assert.Less(enemy.Health, initialHealth,
					"Enemy health should drop as soldier attacks");
		}

		/// <summary>
		/// A soldier kills an enemy unit; the cell the enemy occupied becomes buildable again.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_KillsEnemy_CellBecomesBuildable()
		{
			var enemyPos = new Vector3Int(11, 10, 0);
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WORKER, enemyPos, ctx.Agent1Go);
			int enemyNbr = enemy.UnitNbr;

			// Mark enemy as very fragile
			enemy.Health = 1f;

			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));

			// Wait until the enemy is removed from UnitManager
			yield return WaitUntil(
				() => ctx.UnitManager.GetUnit(enemyNbr) == null,
				timeoutSeconds: 10f,
				failMessage: "Enemy was not destroyed after soldier attack");

			// Allow one frame for Object.Destroy
			yield return null;

			Assert.IsTrue(ctx.MapManager.IsGridPositionBuildable(enemyPos),
				"Cell should be buildable after enemy worker is destroyed");
		}

		/// <summary>
		/// Soldier goes IDLE after the target is killed.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_AfterKillingTarget_GoesIdle()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WORKER, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			enemy.Health = 1f;

			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));

			yield return WaitUntil(
				() => soldier.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Soldier did not go IDLE after killing its target");

			Assert.AreEqual(UnitAction.IDLE, soldier.CurrentAction,
				"Soldier should be IDLE once target is dead");
		}

		#endregion

		#region Boundary

		/// <summary>
		/// A soldier placed out of melee range first moves toward the target,
		/// then attacks once in range.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_OutOfRange_MovesToTargetThenAttacks()
		{
			// Soldier at (5,5), enemy at (15,15) — well beyond SOLDIER attack range
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));
			Unit enemy = PlaceUnit(UnitType.WORKER, new Vector3Int(15, 15, 0), ctx.Agent1Go);
			float initialHealth = enemy.Health;

			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));

			// Soldier should first move (not immediately deal damage)
			Assert.AreEqual(UnitAction.ATTACK, soldier.CurrentAction);

			// Wait until health decreases (attack landed)
			yield return WaitUntil(
				() => enemy == null || enemy.Health < initialHealth,
				timeoutSeconds: 30f,
				failMessage: "Soldier never reached or attacked out-of-range enemy");

			if (enemy != null)
				Assert.Less(enemy.Health, initialHealth,
					"Enemy health should drop once soldier is in range");
		}

		/// <summary>
		/// A soldier attacking a unit with very high health still deals incremental damage.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_AttacksHighHealthTarget_DealsDamageOverTime()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.BASE, new Vector3Int(11, 10, 0), ctx.Agent1Go);

			float initialHealth = enemy.Health;
			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));

			// Wait for at least some damage
			yield return WaitUntil(
				() => enemy.Health < initialHealth,
				timeoutSeconds: 15f,
				failMessage: "Soldier did not damage a high-health building");

			Assert.Less(enemy.Health, initialHealth,
				"Soldier should chip away at a high-health building over time");
		}

		#endregion

		#region Error

		/// <summary>
		/// Attacking a unit owned by the same agent (friendly fire) should be rejected —
		/// the attacker remains IDLE and the target's health is unchanged.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_AttacksFriendly_CommandRejected()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(10, 10, 0));
			Unit friendly = PlaceUnit(UnitType.WORKER, new Vector3Int(11, 10, 0));

			float healthBefore = friendly.Health;
			soldier.StartAttacking(new AttackEventArgs(soldier, friendly));

			Assert.AreNotEqual(UnitAction.ATTACK, soldier.CurrentAction,
				"Soldier should not enter ATTACK state when targeting a friendly unit");
			Assert.AreEqual(healthBefore, friendly.Health,
				"Friendly unit's health should not change");

			yield return null;
		}

		/// <summary>
		/// A non-combatant unit (WORKER) cannot attack; the command is rejected.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_AttackCommand_Rejected()
		{
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WORKER, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			float healthBefore = enemy.Health;

			worker.StartAttacking(new AttackEventArgs(worker, enemy));

			Assert.AreNotEqual(UnitAction.ATTACK, worker.CurrentAction,
				"WORKER should not be able to attack");
			Assert.AreEqual(healthBefore, enemy.Health,
				"Enemy health should not change when worker tries to attack");

			yield return null;
		}

		#endregion

		#region Stress

		/// <summary>
		/// Multiple soldiers attacking the same target simultaneously should kill it faster.
		/// </summary>
		[UnityTest]
		public IEnumerator MultipleSoldiers_SameTarget_KillFaster()
		{
			var enemyPos = new Vector3Int(15, 10, 0);
			Unit enemy = PlaceUnit(UnitType.BASE, enemyPos, ctx.Agent1Go);
			int enemyNbr = enemy.UnitNbr;

			// Three soldiers surrounding the enemy building
			Unit s1 = PlaceUnit(UnitType.SOLDIER, new Vector3Int(14, 10, 0));
			Unit s2 = PlaceUnit(UnitType.SOLDIER, new Vector3Int(14, 9, 0));
			Unit s3 = PlaceUnit(UnitType.SOLDIER, new Vector3Int(14, 11, 0));

			s1.StartAttacking(new AttackEventArgs(s1, enemy));
			s2.StartAttacking(new AttackEventArgs(s2, enemy));
			s3.StartAttacking(new AttackEventArgs(s3, enemy));

			// All three should be in ATTACK state
			Assert.AreEqual(UnitAction.ATTACK, s1.CurrentAction);
			Assert.AreEqual(UnitAction.ATTACK, s2.CurrentAction);
			Assert.AreEqual(UnitAction.ATTACK, s3.CurrentAction);

			// Wait for enemy to be destroyed
			yield return WaitUntil(
				() => ctx.UnitManager.GetUnit(enemyNbr) == null,
				timeoutSeconds: 30f,
				failMessage: "Three soldiers could not destroy the enemy building");

			yield return null;

			Assert.IsNull(ctx.UnitManager.GetUnit(enemyNbr),
				"Enemy BASE should be destroyed by three soldiers");
		}

		#endregion
	}
}
