using NUnit.Framework;
using AgentSDK;

namespace GameManager.Tests
{
	/// <summary>
	/// Tests for Constants.HEALTH: dictionary completeness and value sanity.
	/// Constants.HEALTH is a static readonly dictionary initialized from GameConstants
	/// at class load time (the MINE value is updated by CalculateGameConstants but
	/// starts positive regardless).
	/// </summary>
	[TestFixture]
	public class ConstantsHealthTests
	{
		[SetUp]
		public void SetUp()
		{
			Constants.GAME_SPEED = 1;
			Constants.CalculateGameConstants();
		}

		private static readonly UnitType[] AllUnitTypes = {
			UnitType.MINE, UnitType.PAWN, UnitType.WARRIOR,
			UnitType.ARCHER, UnitType.LANCER, UnitType.BASE,
			UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER
		};

		#region Completeness

		/// <summary>
		/// HEALTH dictionary contains entries for all 9 unit types.
		/// </summary>
		[Test]
		public void Health_HasAllNineUnitTypes()
		{
			Assert.AreEqual(9, Constants.HEALTH.Count,
				"HEALTH should have exactly 9 entries");
			foreach (var type in AllUnitTypes)
				Assert.IsTrue(Constants.HEALTH.ContainsKey(type),
					$"HEALTH missing entry for {type}");
		}

		/// <summary>
		/// All HEALTH values should be strictly positive.
		/// </summary>
		[Test]
		public void Health_AllValuesPositive()
		{
			foreach (var kvp in Constants.HEALTH)
			{
				Assert.Greater(kvp.Value, 0f,
					$"HEALTH for {kvp.Key} should be > 0");
			}
		}

		#endregion

		#region Relative Ordering

		/// <summary>
		/// Buildings should be tougher than the basic pawn unit.
		/// BASE health > PAWN health.
		/// </summary>
		[Test]
		public void Health_BaseGreaterThanPawn()
		{
			Assert.Greater(Constants.HEALTH[UnitType.BASE],
				Constants.HEALTH[UnitType.PAWN],
				"BASE should have more health than PAWN");
		}

		/// <summary>
		/// BARRACKS health should be greater than PAWN health.
		/// </summary>
		[Test]
		public void Health_BarracksGreaterThanPawn()
		{
			Assert.Greater(Constants.HEALTH[UnitType.BARRACKS],
				Constants.HEALTH[UnitType.PAWN],
				"BARRACKS should have more health than PAWN");
		}

		/// <summary>
		/// Combat units (WARRIOR, ARCHER, LANCER) should have more health than PAWN.
		/// </summary>
		[Test]
		public void Health_CombatUnitsGreaterThanPawn()
		{
			Assert.Greater(Constants.HEALTH[UnitType.WARRIOR],
				Constants.HEALTH[UnitType.PAWN],
				"WARRIOR should have more health than PAWN");
			Assert.Greater(Constants.HEALTH[UnitType.ARCHER],
				Constants.HEALTH[UnitType.PAWN],
				"ARCHER should have more health than PAWN");
			Assert.Greater(Constants.HEALTH[UnitType.LANCER],
				Constants.HEALTH[UnitType.PAWN],
				"LANCER should have more health than PAWN");
		}

		/// <summary>
		/// TOWER health should be greater than PAWN health.
		/// </summary>
		[Test]
		public void Health_TowerGreaterThanPawn()
		{
			Assert.Greater(Constants.HEALTH[UnitType.TOWER],
				Constants.HEALTH[UnitType.PAWN],
				"TOWER should have more health than PAWN");
		}

		/// <summary>
		/// MINE health (which equals starting gold) should be significantly
		/// larger than any unit's combat health (it represents gold reserves).
		/// </summary>
		[Test]
		public void Health_MineLargerThanCombatUnits()
		{
			Assert.Greater(Constants.HEALTH[UnitType.MINE],
				Constants.HEALTH[UnitType.WARRIOR],
				"MINE health (= starting gold) should exceed WARRIOR combat health");
			Assert.Greater(Constants.HEALTH[UnitType.MINE],
				Constants.HEALTH[UnitType.BASE],
				"MINE health (= starting gold) should exceed BASE health");
		}

		#endregion

		#region HEALTH Dictionary Is Readonly

		/// <summary>
		/// Constants.HEALTH is a static readonly dictionary — it is not null.
		/// </summary>
		[Test]
		public void Health_DictionaryIsNotNull()
		{
			Assert.IsNotNull(Constants.HEALTH,
				"Constants.HEALTH should not be null");
		}

		#endregion
	}
}
