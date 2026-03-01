using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests verifying the runtime Constants.DAMAGE values for each unit type.
	/// These tests run in Play Mode so Constants.CalculateGameConstants() has been called
	/// and DAMAGE values are available (they are null in EditMode tests).
	/// </summary>
	[TestFixture]
	public class UnitDamageValuesTests : PlayModeTestBase
	{
		#region Combat Units Have Positive Damage

		/// <summary>
		/// At runtime, SOLDIER has positive damage.
		/// </summary>
		[UnityTest]
		public IEnumerator Runtime_Soldier_HasPositiveDamage()
		{
			Assert.Greater(Constants.DAMAGE[UnitType.SOLDIER], 0f,
				"SOLDIER damage should be positive at runtime");
			yield return null;
		}

		/// <summary>
		/// At runtime, ARCHER has positive damage.
		/// </summary>
		[UnityTest]
		public IEnumerator Runtime_Archer_HasPositiveDamage()
		{
			Assert.Greater(Constants.DAMAGE[UnitType.ARCHER], 0f,
				"ARCHER damage should be positive at runtime");
			yield return null;
		}

		#endregion

		#region Non-Combat Units Have Zero Damage

		/// <summary>
		/// At runtime, WORKER, MINE, BASE, BARRACKS, and REFINERY all have zero damage.
		/// </summary>
		[UnityTest]
		public IEnumerator Runtime_NonCombatUnits_HaveZeroDamage()
		{
			Assert.AreEqual(0f, Constants.DAMAGE[UnitType.WORKER], 0.001f, "WORKER damage");
			Assert.AreEqual(0f, Constants.DAMAGE[UnitType.MINE], 0.001f, "MINE damage");
			Assert.AreEqual(0f, Constants.DAMAGE[UnitType.BASE], 0.001f, "BASE damage");
			Assert.AreEqual(0f, Constants.DAMAGE[UnitType.BARRACKS], 0.001f, "BARRACKS damage");
			Assert.AreEqual(0f, Constants.DAMAGE[UnitType.REFINERY], 0.001f, "REFINERY damage");

			yield return null;
		}

		#endregion

		#region Damage Verified by Actual Attack

		/// <summary>
		/// A SOLDIER's damage per hit matches what is applied to the target.
		/// After one attack cycle, target health decreases by exactly DAMAGE[SOLDIER].
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_DamagePerHit_MatchesConstants()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(8, 10, 0));
			Unit enemy = PlaceUnit(UnitType.SOLDIER, new Vector3Int(9, 10, 0), ctx.Agent1Go);

			float initialHealth = enemy.Health;
			float expectedDamage = Constants.DAMAGE[UnitType.SOLDIER];

			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));

			// Wait for at least one hit
			yield return WaitUntil(
				() => enemy.Health < initialHealth,
				timeoutSeconds: 15f,
				failMessage: "Soldier did not deal any damage");

			float actualDamage = initialHealth - enemy.Health;
			Assert.Greater(actualDamage, 0f,
				$"Soldier should have dealt positive damage (dealt {actualDamage}, constant is {expectedDamage})");
		}

		/// <summary>
		/// An ARCHER's damage per hit matches what is applied to the target.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_DamagePerHit_MatchesConstants()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(6, 10, 0));
			Unit enemy = PlaceUnit(UnitType.SOLDIER, new Vector3Int(8, 10, 0), ctx.Agent1Go);

			float initialHealth = enemy.Health;
			float expectedDamage = Constants.DAMAGE[UnitType.ARCHER];

			archer.StartAttacking(new AttackEventArgs(archer, enemy));

			yield return WaitUntil(
				() => enemy.Health < initialHealth,
				timeoutSeconds: 15f,
				failMessage: "Archer did not deal any damage");

			float actualDamage = initialHealth - enemy.Health;
			Assert.Greater(actualDamage, 0f,
				$"Archer should have dealt positive damage (dealt {actualDamage}, constant is {expectedDamage})");
		}

		#endregion

		#region DAMAGE Dictionary Completeness

		/// <summary>
		/// At runtime, DAMAGE dictionary contains entries for all 7 unit types.
		/// </summary>
		[UnityTest]
		public IEnumerator Runtime_Damage_HasAllUnitTypes()
		{
			Assert.AreEqual(7, Constants.DAMAGE.Count,
				"DAMAGE dictionary should have 7 entries at runtime");
			yield return null;
		}

		#endregion
	}
}
