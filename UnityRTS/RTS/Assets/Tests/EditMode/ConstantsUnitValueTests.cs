using NUnit.Framework;
using AgentSDK;

namespace GameManager.Tests
{
	/// <summary>
	/// Tests for Constants.UNIT_VALUE — verifies values match the formula
	/// ceil(COST / SCORING_SCALAR) and relative orderings between unit types.
	/// Complements ConstantsCapabilityTests which only checks combat > pawn and mine = 0.
	/// </summary>
	[TestFixture]
	public class ConstantsUnitValueTests
	{
		#region Completeness

		/// <summary>
		/// UNIT_VALUE contains entries for all 11 unit types.
		/// </summary>
		[Test]
		public void UnitValue_HasAllElevenUnitTypes()
		{
			Assert.AreEqual(11, Constants.UNIT_VALUE.Count,
				"UNIT_VALUE should have exactly 11 entries");
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.MINE));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.PAWN));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.WARRIOR));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.ARCHER));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.LANCER));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.BASE));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.BARRACKS));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.ARCHERY));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.TOWER));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.MONASTERY));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.MONK));
		}

		/// <summary>
		/// All UNIT_VALUE entries should be non-negative.
		/// </summary>
		[Test]
		public void UnitValue_AllNonNegative()
		{
			foreach (var kvp in Constants.UNIT_VALUE)
			{
				Assert.GreaterOrEqual(kvp.Value, 0,
					$"UNIT_VALUE for {kvp.Key} should be >= 0");
			}
		}

		#endregion

		#region Formula Verification — ceil(COST / SCORING_SCALAR)

		// SCORING_SCALAR = 20. Values: MINE=0, PAWN=3, WARRIOR=5, ARCHER=4,
		// LANCER=4, MONK=5, BASE=25, BARRACKS=20, ARCHERY=18, TOWER=15, MONASTERY=18.

		[Test] public void UnitValue_Mine_IsZero() =>
			Assert.AreEqual(0, Constants.UNIT_VALUE[UnitType.MINE], "ceil(0/20) = 0");

		[Test] public void UnitValue_Pawn_IsThree() =>
			Assert.AreEqual(3, Constants.UNIT_VALUE[UnitType.PAWN], "ceil(50/20) = 3");

		[Test] public void UnitValue_Warrior_IsFive() =>
			Assert.AreEqual(5, Constants.UNIT_VALUE[UnitType.WARRIOR], "ceil(100/20) = 5");

		[Test] public void UnitValue_Archer_IsFour() =>
			Assert.AreEqual(4, Constants.UNIT_VALUE[UnitType.ARCHER], "ceil(80/20) = 4");

		[Test] public void UnitValue_Lancer_IsFour() =>
			Assert.AreEqual(4, Constants.UNIT_VALUE[UnitType.LANCER], "ceil(70/20) = 4");

		[Test] public void UnitValue_Monk_IsFive() =>
			Assert.AreEqual(5, Constants.UNIT_VALUE[UnitType.MONK], "ceil(90/20) = 5");

		[Test] public void UnitValue_Base_IsTwentyFive() =>
			Assert.AreEqual(25, Constants.UNIT_VALUE[UnitType.BASE], "ceil(500/20) = 25");

		[Test] public void UnitValue_Barracks_IsTwenty() =>
			Assert.AreEqual(20, Constants.UNIT_VALUE[UnitType.BARRACKS], "ceil(400/20) = 20");

		[Test] public void UnitValue_Archery_IsEighteen() =>
			Assert.AreEqual(18, Constants.UNIT_VALUE[UnitType.ARCHERY], "ceil(350/20) = 18");

		[Test] public void UnitValue_Tower_IsFifteen() =>
			Assert.AreEqual(15, Constants.UNIT_VALUE[UnitType.TOWER], "ceil(300/20) = 15");

		[Test] public void UnitValue_Monastery_IsEighteen() =>
			Assert.AreEqual(18, Constants.UNIT_VALUE[UnitType.MONASTERY], "ceil(350/20) = 18");

		#endregion

		#region Relative Ordering

		/// <summary>
		/// All combat units (WARRIOR, ARCHER, LANCER, MONK) worth more than PAWN.
		/// </summary>
		[Test]
		public void UnitValue_CombatUnitsGreaterThanPawn()
		{
			Assert.Greater(Constants.UNIT_VALUE[UnitType.WARRIOR],
				Constants.UNIT_VALUE[UnitType.PAWN], "WARRIOR > PAWN");
			Assert.Greater(Constants.UNIT_VALUE[UnitType.ARCHER],
				Constants.UNIT_VALUE[UnitType.PAWN], "ARCHER > PAWN");
			Assert.Greater(Constants.UNIT_VALUE[UnitType.LANCER],
				Constants.UNIT_VALUE[UnitType.PAWN], "LANCER > PAWN");
			Assert.Greater(Constants.UNIT_VALUE[UnitType.MONK],
				Constants.UNIT_VALUE[UnitType.PAWN], "MONK > PAWN");
		}

		/// <summary>
		/// Buildings are high-value targets — all worth more than any mobile unit.
		/// </summary>
		[Test]
		public void UnitValue_BuildingsGreaterThanMobileUnits()
		{
			Assert.Greater(Constants.UNIT_VALUE[UnitType.BASE],
				Constants.UNIT_VALUE[UnitType.WARRIOR], "BASE > WARRIOR");
			Assert.Greater(Constants.UNIT_VALUE[UnitType.BARRACKS],
				Constants.UNIT_VALUE[UnitType.WARRIOR], "BARRACKS > WARRIOR");
			Assert.Greater(Constants.UNIT_VALUE[UnitType.TOWER],
				Constants.UNIT_VALUE[UnitType.WARRIOR], "TOWER > WARRIOR");
		}

		/// <summary>
		/// LANCER and ARCHER have equal value (both cost-proportional).
		/// </summary>
		[Test]
		public void UnitValue_LancerEqualsArcher()
		{
			Assert.AreEqual(Constants.UNIT_VALUE[UnitType.LANCER],
				Constants.UNIT_VALUE[UnitType.ARCHER],
				"LANCER and ARCHER should have equal value");
		}

		/// <summary>
		/// BASE is the highest-value target.
		/// </summary>
		[Test]
		public void UnitValue_BaseIsHighestValue()
		{
			foreach (var kvp in Constants.UNIT_VALUE)
			{
				Assert.GreaterOrEqual(Constants.UNIT_VALUE[UnitType.BASE], kvp.Value,
					$"BASE should be >= {kvp.Key}");
			}
		}

		#endregion
	}
}
