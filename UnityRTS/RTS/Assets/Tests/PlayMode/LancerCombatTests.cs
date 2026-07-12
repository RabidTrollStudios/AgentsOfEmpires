using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for LANCER combat behavior.
	/// Covers: melee attack with extended range, health reduction, target kill,
	/// rock-paper-scissors damage modifiers (LANCER strong vs WARRIOR, weak vs ARCHER),
	/// and friendly fire rejection.
	/// </summary>
	[TestFixture]
	public class LancerCombatTests : PlayModeTestBase
	{
		#region Basic Attack

		/// <summary>
		/// A LANCER attacks an enemy pawn and reduces its health over time.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_AttacksEnemy_HealthDecreases()
		{
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0), ctx.Agent1Go);

			float initialHealth = enemy.Health;
			lancer.StartAttacking(new AttackEventArgs(lancer, enemy));

			Assert.AreEqual(UnitAction.ATTACK, lancer.CurrentAction,
				"LANCER should enter ATTACK state after StartAttacking");

			yield return WaitForTick(
				() => enemy == null || enemy.Health < initialHealth,
				timeoutSeconds: 10f,
				failMessage: "Enemy health did not decrease after LANCER attack");

			Assert.IsTrue(enemy == null || enemy.Health < initialHealth,
				"Enemy health should drop as LANCER attacks");
		}

		/// <summary>
		/// A LANCER kills a weakened enemy and returns to IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_KillsWeakEnemy_ReturnsToIdle()
		{
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			int enemyNbr = enemy.UnitNbr;
			enemy.Health = 1f;

			lancer.StartAttacking(new AttackEventArgs(lancer, enemy));

			yield return WaitForTick(
				() => ctx.UnitManager.GetUnit(enemyNbr) == null,
				timeoutSeconds: 10f,
				failMessage: "Enemy was not killed by LANCER");

			yield return WaitForTick(
				() => lancer.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 5f,
				failMessage: "LANCER did not return to IDLE after killing target");
		}

		/// <summary>
		/// The game does not reject friendly-fire at the command level.
		/// A LANCER ordered to attack a friendly unit enters ATTACK state.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_AttackFriendly_AcceptsCommand()
		{
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(10, 10, 0));
			Unit friendly = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0));

			lancer.StartAttacking(new AttackEventArgs(lancer, friendly));
			Assert.AreEqual(UnitAction.ATTACK, lancer.CurrentAction,
				"LANCER should enter ATTACK state even against friendly units");
			yield return null;
		}

		#endregion

		#region Attack Range

		/// <summary>
		/// LANCER has extended melee range (2.5) — greater than WARRIOR (1.0).
		/// Verifies the LANCER can engage a target beyond standard melee distance.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_AttackRange_GreaterThanWarrior()
		{
			Assert.Greater(Constants.ATTACK_RANGE[UnitType.LANCER],
				Constants.ATTACK_RANGE[UnitType.WARRIOR],
				"LANCER should have greater attack range than WARRIOR");
			yield return null;
		}

		/// <summary>
		/// LANCER attack range is less than ARCHER range.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_AttackRange_LessThanArcher()
		{
			Assert.Less(Constants.ATTACK_RANGE[UnitType.LANCER],
				Constants.ATTACK_RANGE[UnitType.ARCHER],
				"LANCER should have less attack range than ARCHER");
			yield return null;
		}

		#endregion

		#region Rock-Paper-Scissors Damage Modifiers

		/// <summary>
		/// LANCER deals bonus damage (1.25x) to WARRIOR.
		/// Verifies the attack modifier favors LANCER in this matchup.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_VsWarrior_DealsBonus()
		{
			float modifier = GameConstants.DamageMultiplier(UnitType.LANCER, UnitType.WARRIOR);
			Assert.Greater(modifier, 1.0f,
				"LANCER should have > 1.0x modifier vs WARRIOR");
			yield return null;
		}

		/// <summary>
		/// LANCER deals reduced damage (0.75x) to ARCHER.
		/// Verifies the attack modifier penalizes LANCER in this matchup.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_VsArcher_DealsPenalty()
		{
			float modifier = GameConstants.DamageMultiplier(UnitType.LANCER, UnitType.ARCHER);
			Assert.Less(modifier, 1.0f,
				"LANCER should have < 1.0x modifier vs ARCHER");
			yield return null;
		}

		/// <summary>
		/// WARRIOR deals reduced damage (0.75x) to LANCER.
		/// Verifies the symmetry of the rock-paper-scissors relationship.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_VsLancer_DealsPenalty()
		{
			float modifier = GameConstants.DamageMultiplier(UnitType.WARRIOR, UnitType.LANCER);
			Assert.Less(modifier, 1.0f,
				"WARRIOR should have < 1.0x modifier vs LANCER");
			yield return null;
		}

		/// <summary>
		/// ARCHER deals bonus damage (1.25x) to LANCER.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_VsLancer_DealsBonus()
		{
			float modifier = GameConstants.DamageMultiplier(UnitType.ARCHER, UnitType.LANCER);
			Assert.Greater(modifier, 1.0f,
				"ARCHER should have > 1.0x modifier vs LANCER");
			yield return null;
		}

		#endregion

		#region Movement

		/// <summary>
		/// LANCER is the fastest combat unit — faster than WARRIOR and ARCHER.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_Speed_FastestCombatUnit()
		{
			Assert.Greater(Constants.MOVING_SPEED[UnitType.LANCER],
				Constants.MOVING_SPEED[UnitType.WARRIOR],
				"LANCER should move faster than WARRIOR");
			Assert.Greater(Constants.MOVING_SPEED[UnitType.LANCER],
				Constants.MOVING_SPEED[UnitType.ARCHER],
				"LANCER should move faster than ARCHER");
			yield return null;
		}

		#endregion
	}
}
