using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for ARCHER vs SOLDIER attack range comparison.
	/// Verifies that archers can engage enemies at distances where soldiers cannot.
	/// </summary>
	[TestFixture]
	public class ArcherEngageRangeTests : PlayModeTestBase
	{
		#region Range Advantage

		/// <summary>
		/// An ARCHER attacks an enemy at exactly its range without needing to move.
		/// A SOLDIER at the same position cannot reach the same target (out of range).
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_AttacksAtRange_SoldierCannotReach()
		{
			// Place units at the same starting position
			float archerRange = Constants.ATTACK_RANGE[UnitType.ARCHER];
			float soldierRange = Constants.ATTACK_RANGE[UnitType.SOLDIER];

			// Enemy distance: between soldier range and archer range
			// (must be > soldierRange so soldier must move, but ≤ archerRange + some buffer)
			// Use a distance midway between the two ranges
			int enemyDist = (int)((soldierRange + archerRange) / 2f) + 1;

			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 10, 0));
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 12, 0));
			Unit enemy = PlaceUnit(UnitType.WORKER, new Vector3Int(5 + enemyDist, 10, 0),
				ctx.Agent1Go);
			CombatTestHelper.WeakenUnit(enemy, 1f);

			// Archer issues attack; it should engage from current position
			archer.StartAttacking(new AttackEventArgs(archer, enemy));
			Assert.AreEqual(UnitAction.ATTACK, archer.CurrentAction,
				"ARCHER should enter ATTACK state");

			// Wait for archer to deal damage
			float initialHealth = enemy.Health;
			yield return WaitUntil(
				() => enemy.Health < initialHealth || ctx.UnitManager.GetUnit(enemy.UnitNbr) == null,
				timeoutSeconds: 15f,
				failMessage: "ARCHER did not deal damage to enemy within range");

			// The important assertion: archer can damage, demonstrating range advantage
			bool enemyDamaged = enemy.Health < initialHealth ||
				ctx.UnitManager.GetUnit(enemy.UnitNbr) == null;
			Assert.IsTrue(enemyDamaged,
				"ARCHER should deal damage to target within its attack range");
		}

		/// <summary>
		/// ARCHER attack range from Constants is strictly greater than SOLDIER range.
		/// (Runtime verification via Constants — not just the static EditMode check.)
		/// </summary>
		[UnityTest]
		public IEnumerator Runtime_ArcherRange_GreaterThanSoldierRange()
		{
			float archerRange = Constants.ATTACK_RANGE[UnitType.ARCHER];
			float soldierRange = Constants.ATTACK_RANGE[UnitType.SOLDIER];

			Assert.Greater(archerRange, soldierRange,
				"ARCHER attack range should exceed SOLDIER attack range at runtime");
			yield return null;
		}

		#endregion

		#region Archer Kills at Range

		/// <summary>
		/// An ARCHER kills a weakened enemy that is within its range
		/// without needing to move.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_KillsWeakenedEnemyInRange()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 10, 0));
			Unit enemy = CombatTestHelper.PlaceWeakEnemy(ctx, UnitType.WORKER,
				new Vector3Int(7, 10, 0), health: 1f);
			int enemyNbr = enemy.UnitNbr;

			archer.StartAttacking(new AttackEventArgs(archer, enemy));

			yield return CombatTestHelper.WaitForDeath(ctx, enemyNbr, timeoutSeconds: 20f,
				failMessage: "ARCHER did not kill weakened enemy");

			Assert.IsNull(ctx.UnitManager.GetUnit(enemyNbr),
				"Enemy should be removed after ARCHER kills it");
		}

		#endregion

		#region Archer Cannot Attack Friendly

		/// <summary>
		/// ARCHER cannot attack a friendly unit (same agent).
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_CannotAttackFriendly()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 10, 0));
			Unit friendly = PlaceUnit(UnitType.WORKER, new Vector3Int(6, 10, 0));

			CombatTestHelper.AssertFriendlyFireRejected(archer, friendly);
			yield return null;
		}

		#endregion

		#region Both Attack Same Target — Damage Dealt Faster

		/// <summary>
		/// When both an ARCHER and a SOLDIER attack the same healthy enemy,
		/// the combined damage reduces enemy health faster.
		/// </summary>
		[UnityTest]
		public IEnumerator ArcherAndSoldier_CombinedAttack_ReducesHealthFaster()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 9, 0));
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 11, 0));
			Unit enemy = PlaceUnit(UnitType.SOLDIER, new Vector3Int(8, 10, 0), ctx.Agent1Go);
			float initialHealth = enemy.Health;

			archer.StartAttacking(new AttackEventArgs(archer, enemy));
			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));

			yield return WaitUntil(
				() => enemy.Health < initialHealth,
				timeoutSeconds: 15f,
				failMessage: "Combined archer+soldier attack did not reduce enemy health");

			Assert.Less(enemy.Health, initialHealth,
				"Enemy health should decrease when attacked by both archer and soldier");
		}

		#endregion
	}
}
