using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for the move-then-attack pattern:
	/// a soldier far from its target pathfinds into attack range and engages.
	/// </summary>
	[TestFixture]
	public class SoldierChaseTests : PlayModeTestBase
	{
		#region Long-Range Chase

		/// <summary>
		/// A SOLDIER issued an attack command against a distant weakened enemy
		/// eventually kills it (unit removed from UnitManager).
		/// The soldier must pathfind into melee range first.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_FarFromEnemy_PathfindsAndKills()
		{
			// Enemy is 8 cells away — beyond melee range
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WORKER, new Vector3Int(13, 10, 0), ctx.Agent1Go);
			CombatTestHelper.WeakenUnit(enemy, 1f);

			int enemyNbr = enemy.UnitNbr;
			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));

			Assert.AreEqual(UnitAction.ATTACK, soldier.CurrentAction,
				"Soldier should enter ATTACK state after issuing attack command");

			// Wait for enemy to be destroyed
			yield return CombatTestHelper.WaitForDeath(ctx, enemyNbr, timeoutSeconds: 30f,
				failMessage: "Soldier did not kill distant enemy within 30s");

			Assert.IsNull(ctx.UnitManager.GetUnit(enemyNbr),
				"Enemy should be removed from UnitManager after being killed");
		}

		/// <summary>
		/// After killing a distant enemy, the SOLDIER returns to IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_AfterKillingDistantEnemy_GoesIdle()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WORKER, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			CombatTestHelper.WeakenUnit(enemy, 1f);

			int enemyNbr = enemy.UnitNbr;
			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));

			yield return CombatTestHelper.WaitForDeath(ctx, enemyNbr, timeoutSeconds: 30f);

			yield return WaitUntil(
				() => soldier.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 10f,
				failMessage: "Soldier did not return to IDLE after killing enemy");

			Assert.AreEqual(UnitAction.IDLE, soldier.CurrentAction,
				"Soldier should be IDLE after its target is destroyed");
		}

		#endregion

		#region Chase with Full-Health Target

		/// <summary>
		/// A SOLDIER attacks a healthy enemy from a distance; enemy's health decreases
		/// after the soldier closes range.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_ChasesHealthyEnemy_DealsAtLeastOneDamage()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 10, 0));
			Unit enemy = PlaceUnit(UnitType.SOLDIER, new Vector3Int(12, 10, 0), ctx.Agent1Go);
			float initialEnemyHealth = enemy.Health;

			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));

			// Wait for any damage to occur
			yield return WaitUntil(
				() => enemy.Health < initialEnemyHealth,
				timeoutSeconds: 20f,
				failMessage: "Soldier did not deal damage to distant enemy after chasing");

			Assert.Less(enemy.Health, initialEnemyHealth,
				"Enemy health should decrease after soldier chases and attacks");
		}

		#endregion

		#region Archer Chase

		/// <summary>
		/// An ARCHER issues an attack command from a distance and eventually deals damage.
		/// Archer has longer range so may not need to travel as far as a soldier.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_ChasesDistantEnemy_DealsAtLeastOneDamage()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 10, 0));
			Unit enemy = PlaceUnit(UnitType.SOLDIER, new Vector3Int(14, 10, 0), ctx.Agent1Go);
			float initialEnemyHealth = enemy.Health;

			archer.StartAttacking(new AttackEventArgs(archer, enemy));

			yield return WaitUntil(
				() => enemy.Health < initialEnemyHealth,
				timeoutSeconds: 20f,
				failMessage: "Archer did not deal damage to distant enemy");

			Assert.Less(enemy.Health, initialEnemyHealth,
				"Enemy health should decrease after archer engages");
		}

		#endregion
	}
}
