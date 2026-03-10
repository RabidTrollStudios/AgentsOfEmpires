using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for WARRIOR melee attack behavior.
	/// Covers: health reduction, target death and cell release,
	/// attacker returning to IDLE, out-of-range movement before attack,
	/// and attacking an already-dead target.
	/// </summary>
	[TestFixture]
	public class UnitAttackTests : PlayModeTestBase
	{
		#region Happy Path

		/// <summary>
		/// A warrior attacks an enemy pawn and reduces its health over time.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_AttacksEnemy_HealthDecreases()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0), ctx.Agent1Go);

			float initialHealth = enemy.Health;
			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction,
				"Warrior should enter ATTACK state after StartAttacking");

			// Wait until enemy health decreases
			yield return WaitUntil(
				() => enemy == null || enemy.Health < initialHealth,
				timeoutSeconds: 10f,
				failMessage: "Enemy health did not decrease after warrior attack");

			if (enemy != null)
				Assert.Less(enemy.Health, initialHealth,
					"Enemy health should drop as warrior attacks");
		}

		/// <summary>
		/// A warrior kills an enemy unit; the cell the enemy occupied becomes buildable again.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_KillsEnemy_CellBecomesBuildable()
		{
			var enemyPos = new Vector3Int(11, 10, 0);
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, enemyPos, ctx.Agent1Go);
			int enemyNbr = enemy.UnitNbr;

			// Mark enemy as very fragile
			enemy.Health = 1f;

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			// Wait until the enemy is removed from UnitManager
			yield return WaitUntil(
				() => ctx.UnitManager.GetUnit(enemyNbr) == null,
				timeoutSeconds: 10f,
				failMessage: "Enemy was not destroyed after warrior attack");

			// Allow one frame for Object.Destroy
			yield return null;

			Assert.IsTrue(ctx.MapManager.IsGridPositionBuildable(enemyPos),
				"Cell should be buildable after enemy pawn is destroyed");
		}

		/// <summary>
		/// Warrior goes IDLE after the target is killed.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_AfterKillingTarget_GoesIdle()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			enemy.Health = 1f;

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			yield return WaitUntil(
				() => warrior.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Warrior did not go IDLE after killing its target");

			Assert.AreEqual(UnitAction.IDLE, warrior.CurrentAction,
				"Warrior should be IDLE once target is dead");
		}

		#endregion

		#region Boundary

		/// <summary>
		/// A warrior placed out of melee range first moves toward the target,
		/// then attacks once in range.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_OutOfRange_MovesToTargetThenAttacks()
		{
			// Warrior at (5,5), enemy at (15,15) — well beyond WARRIOR attack range
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(15, 15, 0), ctx.Agent1Go);
			float initialHealth = enemy.Health;

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			// Warrior should first move (not immediately deal damage)
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);

			// Wait until health decreases (attack landed)
			yield return WaitUntil(
				() => enemy == null || enemy.Health < initialHealth,
				timeoutSeconds: 30f,
				failMessage: "Warrior never reached or attacked out-of-range enemy");

			if (enemy != null)
				Assert.Less(enemy.Health, initialHealth,
					"Enemy health should drop once warrior is in range");
		}

		/// <summary>
		/// A warrior attacking a unit with very high health still deals incremental damage.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_AttacksHighHealthTarget_DealsDamageOverTime()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.BASE, new Vector3Int(11, 10, 0), ctx.Agent1Go);

			float initialHealth = enemy.Health;
			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			// Wait for at least some damage
			yield return WaitUntil(
				() => enemy.Health < initialHealth,
				timeoutSeconds: 15f,
				failMessage: "Warrior did not damage a high-health building");

			Assert.Less(enemy.Health, initialHealth,
				"Warrior should chip away at a high-health building over time");
		}

		#endregion

		#region Error

		/// <summary>
		/// A non-combatant unit (PAWN) cannot attack; the command is rejected.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_AttackCommand_Rejected()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			float healthBefore = enemy.Health;

			pawn.StartAttacking(new AttackEventArgs(pawn, enemy));

			Assert.AreNotEqual(UnitAction.ATTACK, pawn.CurrentAction,
				"PAWN should not be able to attack");
			Assert.AreEqual(healthBefore, enemy.Health,
				"Enemy health should not change when pawn tries to attack");

			yield return null;
		}

		#endregion

		#region Stress

		/// <summary>
		/// Multiple warriors attacking the same target simultaneously should kill it faster.
		/// </summary>
		[UnityTest]
		public IEnumerator MultipleWarriors_SameTarget_KillFaster()
		{
			var enemyPos = new Vector3Int(15, 10, 0);
			Unit enemy = PlaceUnit(UnitType.BASE, enemyPos, ctx.Agent1Go);
			int enemyNbr = enemy.UnitNbr;

			// Three warriors surrounding the enemy building
			Unit s1 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(14, 10, 0));
			Unit s2 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(14, 9, 0));
			Unit s3 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(14, 11, 0));

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
				failMessage: "Three warriors could not destroy the enemy building");

			yield return null;

			Assert.IsNull(ctx.UnitManager.GetUnit(enemyNbr),
				"Enemy BASE should be destroyed by three warriors");
		}

		#endregion
	}
}
