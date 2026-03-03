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
			UnitType.MINE, UnitType.WORKER, UnitType.SOLDIER,
			UnitType.ARCHER, UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY
		};

		#region Completeness

		/// <summary>
		/// HEALTH dictionary contains entries for all 7 unit types.
		/// </summary>
		[Test]
		public void Health_HasAllSevenUnitTypes()
		{
			Assert.AreEqual(7, Constants.HEALTH.Count,
				"HEALTH should have exactly 7 entries");
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
		/// Buildings should be tougher than the basic worker unit.
		/// BASE health > WORKER health.
		/// </summary>
		[Test]
		public void Health_BaseGreaterThanWorker()
		{
			Assert.Greater(Constants.HEALTH[UnitType.BASE],
				Constants.HEALTH[UnitType.WORKER],
				"BASE should have more health than WORKER");
		}

		/// <summary>
		/// BARRACKS health should be greater than WORKER health.
		/// </summary>
		[Test]
		public void Health_BarracksGreaterThanWorker()
		{
			Assert.Greater(Constants.HEALTH[UnitType.BARRACKS],
				Constants.HEALTH[UnitType.WORKER],
				"BARRACKS should have more health than WORKER");
		}

		/// <summary>
		/// Combat units (SOLDIER, ARCHER) should have more health than WORKER.
		/// </summary>
		[Test]
		public void Health_CombatUnitsGreaterThanWorker()
		{
			Assert.Greater(Constants.HEALTH[UnitType.SOLDIER],
				Constants.HEALTH[UnitType.WORKER],
				"SOLDIER should have more health than WORKER");
			Assert.Greater(Constants.HEALTH[UnitType.ARCHER],
				Constants.HEALTH[UnitType.WORKER],
				"ARCHER should have more health than WORKER");
		}

		/// <summary>
		/// MINE health (which equals starting gold) should be significantly
		/// larger than any unit's combat health (it represents gold reserves).
		/// </summary>
		[Test]
		public void Health_MineLargerThanCombatUnits()
		{
			Assert.Greater(Constants.HEALTH[UnitType.MINE],
				Constants.HEALTH[UnitType.SOLDIER],
				"MINE health (= starting gold) should exceed SOLDIER combat health");
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
