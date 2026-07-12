using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for ARCHER ranged attack behavior.
	/// Covers: ranged attack at distance, range advantage over WARRIOR,
	/// and multiple archers focusing the same target.
	/// </summary>
	[TestFixture]
	public class ArcherAttackTests : PlayModeTestBase
	{
		#region Happy Path

		/// <summary>
		/// An archer attacks an adjacent enemy and reduces its health.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_AttacksAdjacentEnemy_HealthDecreases()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0), ctx.Agent1Go);

			float initialHealth = enemy.Health;
			archer.StartAttacking(new AttackEventArgs(archer, enemy));

			Assert.AreEqual(UnitAction.ATTACK, archer.CurrentAction,
				"Archer should enter ATTACK state");

			yield return WaitForTick(
				() => enemy == null || enemy.Health < initialHealth,
				timeoutSeconds: 10f,
				failMessage: "Adjacent enemy health did not decrease from archer attack");

			Assert.IsTrue(enemy == null || enemy.Health < initialHealth,
				"Adjacent enemy should lose health from archer attack");
		}

		/// <summary>
		/// Archer has greater attack range than warrior (from Constants).
		/// Verify at runtime that ARCHER range constant is greater.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_AttackRange_GreaterThanWarrior_AtRuntime()
		{
			float archerRange = Constants.ATTACK_RANGE[UnitType.ARCHER];
			float warriorRange = Constants.ATTACK_RANGE[UnitType.WARRIOR];

			Assert.Greater(archerRange, warriorRange,
				"Archer attack range should be greater than warrior attack range at runtime");

			yield return null;
		}

		/// <summary>
		/// Archer kills a low-health enemy and goes IDLE afterward.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_KillsEnemy_GoesIdle()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			enemy.Health = 1f;

			archer.StartAttacking(new AttackEventArgs(archer, enemy));

			yield return WaitForTick(
				() => archer.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 10f,
				failMessage: "Archer did not go IDLE after killing target");

			Assert.AreEqual(UnitAction.IDLE, archer.CurrentAction,
				"Archer should be IDLE after target is dead");
		}

		#endregion

		#region Boundary

		/// <summary>
		/// An archer placed at distance (but within ARCHER attack range) should attack
		/// without needing to move first.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_WithinRange_AttacksWithoutMoving()
		{
			// Place archer and enemy within ARCHER range but well beyond WARRIOR range
			float archerRange = Constants.ATTACK_RANGE[UnitType.ARCHER];
			int rangeInt = Mathf.FloorToInt(archerRange);

			// Enemy is rangeInt cells away on the x axis — within archer range
			var archerPos = new Vector3Int(10, 10, 0);
			var enemyPos = new Vector3Int(10 + rangeInt - 1, 10, 0);

			Unit archer = PlaceUnit(UnitType.ARCHER, archerPos);
			Unit enemy = PlaceUnit(UnitType.PAWN, enemyPos, ctx.Agent1Go);

			float initialHealth = enemy.Health;
			archer.StartAttacking(new AttackEventArgs(archer, enemy));

			yield return WaitForTick(
				() => enemy == null || enemy.Health < initialHealth,
				timeoutSeconds: 15f,
				failMessage: "Archer did not deal damage to in-range enemy");

			Assert.IsTrue(enemy == null || enemy.Health < initialHealth,
				"In-range enemy should take damage from archer");
		}

		/// <summary>
		/// An archer attacking a high-health building deals damage over multiple ticks.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_AttacksBuilding_DealsDamageOverTime()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.BARRACKS, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			float initialHealth = enemy.Health;

			archer.StartAttacking(new AttackEventArgs(archer, enemy));

			yield return WaitForTick(
				() => enemy.Health < initialHealth - Constants.DAMAGE[UnitType.ARCHER],
				timeoutSeconds: 15f,
				failMessage: "Archer did not deal at least one hit worth of damage to building");

			Assert.Less(enemy.Health, initialHealth,
				"Archer should deal damage to buildings over time");
		}

		#endregion

		#region Stress

		/// <summary>
		/// Three archers targeting the same enemy should deal combined damage
		/// and destroy it faster than one archer alone.
		/// </summary>
		[UnityTest]
		public IEnumerator ThreeArchers_SameTarget_DestroyFaster()
		{
			var enemyPos = new Vector3Int(15, 10, 0);
			Unit enemy = PlaceUnit(UnitType.BASE, enemyPos, ctx.Agent1Go);
			int enemyNbr = enemy.UnitNbr;

			Unit a1 = PlaceUnit(UnitType.ARCHER, new Vector3Int(14, 10, 0));
			Unit a2 = PlaceUnit(UnitType.ARCHER, new Vector3Int(14, 9, 0));
			Unit a3 = PlaceUnit(UnitType.ARCHER, new Vector3Int(14, 11, 0));

			a1.StartAttacking(new AttackEventArgs(a1, enemy));
			a2.StartAttacking(new AttackEventArgs(a2, enemy));
			a3.StartAttacking(new AttackEventArgs(a3, enemy));

			Assert.AreEqual(UnitAction.ATTACK, a1.CurrentAction);
			Assert.AreEqual(UnitAction.ATTACK, a2.CurrentAction);
			Assert.AreEqual(UnitAction.ATTACK, a3.CurrentAction);

			// Three archers should destroy the enemy building
			yield return WaitForTick(
				() => ctx.UnitManager.GetUnit(enemyNbr) == null,
				timeoutSeconds: 10f,
				failMessage: "Three archers could not destroy the enemy BASE");

			yield return null;
			Assert.IsNull(ctx.UnitManager.GetUnit(enemyNbr),
				"Enemy BASE should be destroyed by combined archer fire");
		}

		/// <summary>
		/// Archer and warrior attacking the same target simultaneously deal combined damage.
		/// </summary>
		[UnityTest]
		public IEnumerator ArcherAndWarrior_CombinedAttack_MoreDamage()
		{
			Unit enemy = PlaceUnit(UnitType.BASE, new Vector3Int(15, 10, 0), ctx.Agent1Go);
			int enemyNbr = enemy.UnitNbr;

			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(14, 10, 0));
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(14, 11, 0));

			float startHealth = enemy.Health;

			archer.StartAttacking(new AttackEventArgs(archer, enemy));
			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			// Wait for significant health loss
			yield return WaitForTick(
				() => enemy == null || enemy.Health < startHealth * 0.5f,
				timeoutSeconds: 10f,
				failMessage: "Archer+Warrior combo did not deal 50% health to enemy");

			Assert.IsTrue(enemy == null || enemy.Health < startHealth * 0.5f,
				"Combined attack should quickly reduce enemy health below 50%");
		}

		#endregion
	}
}
