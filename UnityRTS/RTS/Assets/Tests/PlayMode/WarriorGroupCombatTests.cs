using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for group combat:
	/// multiple warriors vs. multiple weakened enemies.
	/// </summary>
	[TestFixture]
	public class WarriorGroupCombatTests : PlayModeTestBase
	{
		#region Three Warriors vs Three Enemies

		/// <summary>
		/// Three warriors each attack one weakened enemy.
		/// All three enemies should be killed and removed from UnitManager.
		/// </summary>
		[UnityTest]
		public IEnumerator ThreeWarriors_VsThreeWeakenedEnemies_AllEnemiesDie()
		{
			// Place three weakened enemy pawns
			Unit e1 = CombatTestHelper.PlaceWeakEnemy(ctx, UnitType.PAWN, new Vector3Int(10, 8, 0));
			Unit e2 = CombatTestHelper.PlaceWeakEnemy(ctx, UnitType.PAWN, new Vector3Int(10, 10, 0));
			Unit e3 = CombatTestHelper.PlaceWeakEnemy(ctx, UnitType.PAWN, new Vector3Int(10, 12, 0));
			int n1 = e1.UnitNbr, n2 = e2.UnitNbr, n3 = e3.UnitNbr;

			// Place three warriors and assign each to one enemy
			Unit s1 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 8, 0));
			Unit s2 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 10, 0));
			Unit s3 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 12, 0));

			s1.StartAttacking(new AttackEventArgs(s1, e1));
			s2.StartAttacking(new AttackEventArgs(s2, e2));
			s3.StartAttacking(new AttackEventArgs(s3, e3));

			Assert.AreEqual(UnitAction.ATTACK, s1.CurrentAction, "Warrior 1 should enter ATTACK");
			Assert.AreEqual(UnitAction.ATTACK, s2.CurrentAction, "Warrior 2 should enter ATTACK");
			Assert.AreEqual(UnitAction.ATTACK, s3.CurrentAction, "Warrior 3 should enter ATTACK");

			// Wait for all enemies to die
			yield return CombatTestHelper.WaitForDeath(ctx, n1, timeoutSeconds: 10f,
				failMessage: "Enemy 1 was not killed");
			yield return CombatTestHelper.WaitForDeath(ctx, n2, timeoutSeconds: 10f,
				failMessage: "Enemy 2 was not killed");
			yield return CombatTestHelper.WaitForDeath(ctx, n3, timeoutSeconds: 10f,
				failMessage: "Enemy 3 was not killed");

			Assert.IsNull(ctx.UnitManager.GetUnit(n1), "Enemy 1 should be gone from UnitManager");
			Assert.IsNull(ctx.UnitManager.GetUnit(n2), "Enemy 2 should be gone from UnitManager");
			Assert.IsNull(ctx.UnitManager.GetUnit(n3), "Enemy 3 should be gone from UnitManager");
		}

		#endregion

		#region Focus Fire

		/// <summary>
		/// Two warriors both assigned to one weakened enemy — enemy dies faster.
		/// All warriors enter ATTACK state.
		/// </summary>
		[UnityTest]
		public IEnumerator TwoWarriors_FocusOneEnemy_EnemyDies()
		{
			Unit enemy = CombatTestHelper.PlaceWeakEnemy(ctx, UnitType.PAWN,
				new Vector3Int(8, 10, 0), health: 5f);
			int enemyNbr = enemy.UnitNbr;

			Unit s1 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 9, 0));
			Unit s2 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 11, 0));

			s1.StartAttacking(new AttackEventArgs(s1, enemy));
			s2.StartAttacking(new AttackEventArgs(s2, enemy));

			yield return CombatTestHelper.WaitForDeath(ctx, enemyNbr, timeoutSeconds: 20f,
				failMessage: "Two warriors did not kill focused enemy");

			Assert.IsNull(ctx.UnitManager.GetUnit(enemyNbr),
				"Focus-fired enemy should be removed from UnitManager");
		}

		#endregion

		#region Mixed Group

		/// <summary>
		/// A mix of WARRIOR and ARCHER both attacking the same weakened enemy.
		/// The enemy is eliminated.
		/// </summary>
		[UnityTest]
		public IEnumerator WarriorAndArcher_AttackSameEnemy_EnemyDies()
		{
			Unit enemy = CombatTestHelper.PlaceWeakEnemy(ctx, UnitType.PAWN,
				new Vector3Int(10, 10, 0), health: 10f);
			int enemyNbr = enemy.UnitNbr;

			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(7, 10, 0));
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(6, 10, 0));

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));
			archer.StartAttacking(new AttackEventArgs(archer, enemy));

			yield return CombatTestHelper.WaitForDeath(ctx, enemyNbr, timeoutSeconds: 20f,
				failMessage: "Warrior+Archer focus fire did not kill enemy");

			Assert.IsNull(ctx.UnitManager.GetUnit(enemyNbr),
				"Enemy should be removed after warrior+archer focus fire");
		}

		/// <summary>
		/// After killing all targets, warriors return to IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator AllWarriors_AfterGroupKill_ReturnToIdle()
		{
			Unit enemy = CombatTestHelper.PlaceWeakEnemy(ctx, UnitType.WARRIOR,
				new Vector3Int(8, 10, 0), health: 1f);

			Unit s1 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 9, 0));
			Unit s2 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 11, 0));

			s1.StartAttacking(new AttackEventArgs(s1, enemy));
			s2.StartAttacking(new AttackEventArgs(s2, enemy));

			yield return CombatTestHelper.WaitForDeath(ctx, enemy.UnitNbr, timeoutSeconds: 20f);

			yield return WaitUntil(
				() => s1.CurrentAction == UnitAction.IDLE && s2.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 10f,
				failMessage: "Warriors did not return to IDLE after group kill");

			Assert.AreEqual(UnitAction.IDLE, s1.CurrentAction, "Warrior 1 should be IDLE");
			Assert.AreEqual(UnitAction.IDLE, s2.CurrentAction, "Warrior 2 should be IDLE");
		}

		#endregion
	}
}
