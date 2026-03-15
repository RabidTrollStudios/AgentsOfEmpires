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
	/// a warrior far from its target pathfinds into attack range and engages.
	/// </summary>
	[TestFixture]
	public class WarriorChaseTests : PlayModeTestBase
	{
		#region Long-Range Chase

		/// <summary>
		/// A WARRIOR issued an attack command against a distant weakened enemy
		/// eventually kills it (unit removed from UnitManager).
		/// The warrior must pathfind into melee range first.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_FarFromEnemy_PathfindsAndKills()
		{
			// Enemy is 8 cells away — beyond melee range
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 10, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(13, 10, 0), ctx.Agent1Go);
			CombatTestHelper.WeakenUnit(enemy, 1f);

			int enemyNbr = enemy.UnitNbr;
			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction,
				"Warrior should enter ATTACK state after issuing attack command");

			// Wait for enemy to be destroyed
			yield return CombatTestHelper.WaitForDeath(ctx, enemyNbr, timeoutSeconds: 10f,
				failMessage: "Warrior did not kill distant enemy within 10s");

			Assert.IsNull(ctx.UnitManager.GetUnit(enemyNbr),
				"Enemy should be removed from UnitManager after being killed");
		}

		/// <summary>
		/// After killing a distant enemy, the WARRIOR returns to IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_AfterKillingDistantEnemy_GoesIdle()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 10, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			CombatTestHelper.WeakenUnit(enemy, 1f);

			int enemyNbr = enemy.UnitNbr;
			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			yield return CombatTestHelper.WaitForDeath(ctx, enemyNbr, timeoutSeconds: 10f);

			yield return WaitUntil(
				() => warrior.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 10f,
				failMessage: "Warrior did not return to IDLE after killing enemy");

			Assert.AreEqual(UnitAction.IDLE, warrior.CurrentAction,
				"Warrior should be IDLE after its target is destroyed");
		}

		#endregion

		#region Chase with Full-Health Target

		/// <summary>
		/// A WARRIOR attacks a healthy enemy from a distance; enemy's health decreases
		/// after the warrior closes range.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_ChasesHealthyEnemy_DealsAtLeastOneDamage()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(12, 10, 0), ctx.Agent1Go);
			float initialEnemyHealth = enemy.Health;

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			// Wait for any damage to occur
			yield return WaitUntil(
				() => enemy.Health < initialEnemyHealth,
				timeoutSeconds: 20f,
				failMessage: "Warrior did not deal damage to distant enemy after chasing");

			Assert.Less(enemy.Health, initialEnemyHealth,
				"Enemy health should decrease after warrior chases and attacks");
		}

		#endregion

		#region Archer Chase

		/// <summary>
		/// An ARCHER issues an attack command from a distance and eventually deals damage.
		/// Archer has longer range so may not need to travel as far as a warrior.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_ChasesDistantEnemy_DealsAtLeastOneDamage()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(14, 10, 0), ctx.Agent1Go);
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
