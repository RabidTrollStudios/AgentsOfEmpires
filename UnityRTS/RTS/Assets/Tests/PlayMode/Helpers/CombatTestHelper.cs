using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Helper utilities for combat-focused Play Mode tests.
	/// Provides shorthand for setting up attacker/target pairs,
	/// waiting for a unit to die, and verifying damage thresholds.
	/// </summary>
	public static class CombatTestHelper
	{
		/// <summary>
		/// Issue an attack command from attacker to target and assert the
		/// attacker enters ATTACK state immediately.
		/// </summary>
		public static void StartAttackAndAssert(Unit attacker, Unit target)
		{
			attacker.StartAttacking(new AttackEventArgs(attacker, target));
			Assert.AreEqual(UnitAction.ATTACK, attacker.CurrentAction,
				$"{attacker.UnitType} should enter ATTACK state after issuing attack command");
		}

		/// <summary>
		/// Set a unit's health to a specified low value to make it
		/// easy to kill in a short test.
		/// </summary>
		/// <param name="unit">The unit to weaken.</param>
		/// <param name="health">Remaining health (default 1).</param>
		public static void WeakenUnit(Unit unit, float health = 1f)
		{
			unit.Health = health;
		}

		/// <summary>
		/// Returns a coroutine that waits until the given unit is removed
		/// from the UnitManager (i.e., its health reached 0 and it was destroyed).
		/// Fails if the timeout elapses.
		/// </summary>
		/// <param name="ctx">Test context providing access to UnitManager.</param>
		/// <param name="unitNbr">UnitNbr of the unit expected to die.</param>
		/// <param name="timeoutSeconds">Maximum wait time in seconds.</param>
		/// <param name="failMessage">Message shown on timeout failure.</param>
		public static IEnumerator WaitForDeath(
			PlayModeTestContext ctx, int unitNbr,
			float timeoutSeconds = 15f,
			string failMessage = null)
		{
			failMessage = failMessage ?? $"Unit {unitNbr} was not destroyed within {timeoutSeconds}s";
			// The test GameManager GO is inactive, so FixedUpdate never fires — drive
			// ticks explicitly. Combat/death only advances when we call SimulateTick.
			// timeoutSeconds is reinterpreted as a tick budget (×20 at the 20 Hz rate).
			int maxTicks = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds * 20f));
			int ticks = 0;
			while (ctx.UnitManager.GetUnit(unitNbr) != null)
			{
				if (ticks++ >= maxTicks)
				{
					Assert.Fail(failMessage);
					yield break;
				}
				GameManager.Instance.SimulateTick();
				yield return null;
			}
		}

		/// <summary>
		/// Assert that the target's health dropped by at least the specified amount.
		/// </summary>
		public static void AssertDamageTaken(Unit target, float initialHealth, float minimumDamage)
		{
			float damage = initialHealth - target.Health;
			Assert.GreaterOrEqual(damage, minimumDamage,
				$"Expected at least {minimumDamage} damage but only {damage} was dealt");
		}

		/// <summary>
		/// Assert that a unit's health has not changed (no damage taken).
		/// </summary>
		public static void AssertNoDamageTaken(Unit unit, float initialHealth)
		{
			Assert.AreEqual(initialHealth, unit.Health,
				$"{unit.UnitType} should not have taken any damage");
		}

		/// <summary>
		/// Place and immediately weaken an enemy unit owned by agent 1.
		/// Returns the weakened enemy.
		/// </summary>
		public static Unit PlaceWeakEnemy(PlayModeTestContext ctx, UnitType unitType,
			Vector3Int position, float health = 1f)
		{
			Unit enemy = PlayModeTestHelper.PlaceUnit(ctx, ctx.Agent1Go, unitType, position);
			enemy.Health = health;
			return enemy;
		}

		/// <summary>
		/// Verify that attacking a friendly unit (same agent) is rejected:
		/// attacker stays IDLE and target health is unchanged.
		/// </summary>
		public static void AssertFriendlyFireRejected(Unit attacker, Unit friendly)
		{
			float friendlyHealthBefore = friendly.Health;
			attacker.StartAttacking(new AttackEventArgs(attacker, friendly));
			Assert.AreNotEqual(UnitAction.ATTACK, attacker.CurrentAction,
				"Attacker should not enter ATTACK when targeting a friendly unit");
			Assert.AreEqual(friendlyHealthBefore, friendly.Health,
				"Friendly health should be unchanged when friendly fire is rejected");
		}
	}
}
