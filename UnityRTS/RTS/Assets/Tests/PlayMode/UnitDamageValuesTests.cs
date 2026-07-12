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
		/// At runtime, WARRIOR has positive damage.
		/// </summary>
		[UnityTest]
		public IEnumerator Runtime_Warrior_HasPositiveDamage()
		{
			Assert.Greater(Constants.DAMAGE[UnitType.WARRIOR], 0f,
				"WARRIOR damage should be positive at runtime");
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
		/// At runtime, PAWN, MINE, BASE, and BARRACKS all have zero damage.
		/// </summary>
		[UnityTest]
		public IEnumerator Runtime_NonCombatUnits_HaveZeroDamage()
		{
			Assert.AreEqual(0f, Constants.DAMAGE[UnitType.PAWN], 0.001f, "PAWN damage");
			Assert.AreEqual(0f, Constants.DAMAGE[UnitType.MINE], 0.001f, "MINE damage");
			Assert.AreEqual(0f, Constants.DAMAGE[UnitType.BASE], 0.001f, "BASE damage");
			Assert.AreEqual(0f, Constants.DAMAGE[UnitType.BARRACKS], 0.001f, "BARRACKS damage");

			yield return null;
		}

		#endregion

		#region Damage Verified by Actual Attack

		/// <summary>
		/// A WARRIOR's damage per hit matches what is applied to the target.
		/// After one attack cycle, target health decreases by exactly DAMAGE[WARRIOR].
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_DamagePerHit_MatchesConstants()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(8, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(9, 10, 0), ctx.Agent1Go);

			float initialHealth = enemy.Health;
			float expectedDamage = Constants.DAMAGE[UnitType.WARRIOR];

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			// Wait for at least one hit
			yield return WaitForTick(
				() => enemy.Health < initialHealth,
				timeoutSeconds: 15f,
				failMessage: "Warrior did not deal any damage");

			float actualDamage = initialHealth - enemy.Health;
			Assert.Greater(actualDamage, 0f,
				$"Warrior should have dealt positive damage (dealt {actualDamage}, constant is {expectedDamage})");
		}

		/// <summary>
		/// An ARCHER's damage per hit matches what is applied to the target.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_DamagePerHit_MatchesConstants()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(6, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(8, 10, 0), ctx.Agent1Go);

			float initialHealth = enemy.Health;
			float expectedDamage = Constants.DAMAGE[UnitType.ARCHER];

			archer.StartAttacking(new AttackEventArgs(archer, enemy));

			yield return WaitForTick(
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
		/// At runtime, DAMAGE dictionary contains entries for all 9 unit types.
		/// </summary>
		[UnityTest]
		public IEnumerator Runtime_Damage_HasAllUnitTypes()
		{
			Assert.AreEqual(11, Constants.DAMAGE.Count,
				"DAMAGE dictionary should have 11 entries at runtime");
			yield return null;
		}

		#endregion
	}
}
