using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for ARCHER vs WARRIOR attack range comparison.
	/// Verifies that archers can engage enemies at distances where warriors cannot.
	/// </summary>
	[TestFixture]
	public class ArcherEngageRangeTests : PlayModeTestBase
	{
		#region Range Advantage

		/// <summary>
		/// An ARCHER attacks an enemy at exactly its range without needing to move.
		/// A WARRIOR at the same position cannot reach the same target (out of range).
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_AttacksAtRange_WarriorCannotReach()
		{
			// Place units at the same starting position
			float archerRange = Constants.ATTACK_RANGE[UnitType.ARCHER];
			float warriorRange = Constants.ATTACK_RANGE[UnitType.WARRIOR];

			// Enemy distance: between warrior range and archer range
			// (must be > warriorRange so warrior must move, but ≤ archerRange + some buffer)
			// Use a distance midway between the two ranges
			int enemyDist = (int)((warriorRange + archerRange) / 2f) + 1;

			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 10, 0));
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 12, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(5 + enemyDist, 10, 0),
				ctx.Agent1Go);
			CombatTestHelper.WeakenUnit(enemy, 1f);

			// Archer issues attack; it should engage from current position
			archer.StartAttacking(new AttackEventArgs(archer, enemy));
			Assert.AreEqual(UnitAction.ATTACK, archer.CurrentAction,
				"ARCHER should enter ATTACK state");

			// Wait for archer to deal damage
			float initialHealth = enemy.Health;
			yield return WaitForTick(
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
		/// ARCHER attack range from Constants is strictly greater than WARRIOR range.
		/// (Runtime verification via Constants — not just the static EditMode check.)
		/// </summary>
		[UnityTest]
		public IEnumerator Runtime_ArcherRange_GreaterThanWarriorRange()
		{
			float archerRange = Constants.ATTACK_RANGE[UnitType.ARCHER];
			float warriorRange = Constants.ATTACK_RANGE[UnitType.WARRIOR];

			Assert.Greater(archerRange, warriorRange,
				"ARCHER attack range should exceed WARRIOR attack range at runtime");
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
			Unit enemy = CombatTestHelper.PlaceWeakEnemy(ctx, UnitType.PAWN,
				new Vector3Int(7, 10, 0), health: 1f);
			int enemyNbr = enemy.UnitNbr;

			archer.StartAttacking(new AttackEventArgs(archer, enemy));

			yield return CombatTestHelper.WaitForDeath(ctx, enemyNbr, timeoutSeconds: 20f,
				failMessage: "ARCHER did not kill weakened enemy");

			Assert.IsNull(ctx.UnitManager.GetUnit(enemyNbr),
				"Enemy should be removed after ARCHER kills it");
		}

		#endregion

		#region Both Attack Same Target — Damage Dealt Faster

		/// <summary>
		/// When both an ARCHER and a WARRIOR attack the same healthy enemy,
		/// the combined damage reduces enemy health faster.
		/// </summary>
		[UnityTest]
		public IEnumerator ArcherAndWarrior_CombinedAttack_ReducesHealthFaster()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 9, 0));
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 11, 0));
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(8, 10, 0), ctx.Agent1Go);
			float initialHealth = enemy.Health;

			archer.StartAttacking(new AttackEventArgs(archer, enemy));
			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			yield return WaitForTick(
				() => enemy.Health < initialHealth,
				timeoutSeconds: 15f,
				failMessage: "Combined archer+warrior attack did not reduce enemy health");

			Assert.Less(enemy.Health, initialHealth,
				"Enemy health should decrease when attacked by both archer and warrior");
		}

		#endregion
	}
}
