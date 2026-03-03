using NUnit.Framework;
using AgentSDK;

namespace GameManager.Tests
{
	/// <summary>
	/// Tests for Constants.UNIT_VALUE — verifies specific integer value assignments
	/// and relative orderings between unit types.
	/// Complements ConstantsCapabilityTests which only checks combat > worker and mine = 0.
	/// </summary>
	[TestFixture]
	public class ConstantsUnitValueTests
	{
		#region Completeness

		/// <summary>
		/// UNIT_VALUE contains entries for all 7 unit types.
		/// </summary>
		[Test]
		public void UnitValue_HasAllSevenUnitTypes()
		{
			Assert.AreEqual(7, Constants.UNIT_VALUE.Count,
				"UNIT_VALUE should have exactly 7 entries");
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.MINE));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.WORKER));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.SOLDIER));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.ARCHER));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.BASE));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.BARRACKS));
			Assert.IsTrue(Constants.UNIT_VALUE.ContainsKey(UnitType.ARCHERY));
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

		#region Exact Values

		/// <summary>
		/// ARCHER has the highest unit value (5), reflecting its strategic importance.
		/// </summary>
		[Test]
		public void UnitValue_Archer_IsFive()
		{
			Assert.AreEqual(5, Constants.UNIT_VALUE[UnitType.ARCHER],
				"ARCHER should have UNIT_VALUE = 5");
		}

		/// <summary>
		/// SOLDIER has unit value 4.
		/// </summary>
		[Test]
		public void UnitValue_Soldier_IsFour()
		{
			Assert.AreEqual(4, Constants.UNIT_VALUE[UnitType.SOLDIER],
				"SOLDIER should have UNIT_VALUE = 4");
		}

		/// <summary>
		/// BARRACKS has unit value 3.
		/// </summary>
		[Test]
		public void UnitValue_Barracks_IsThree()
		{
			Assert.AreEqual(3, Constants.UNIT_VALUE[UnitType.BARRACKS],
				"BARRACKS should have UNIT_VALUE = 3");
		}

		/// <summary>
		/// BASE has unit value 2.
		/// </summary>
		[Test]
		public void UnitValue_Base_IsTwo()
		{
			Assert.AreEqual(2, Constants.UNIT_VALUE[UnitType.BASE],
				"BASE should have UNIT_VALUE = 2");
		}

		/// <summary>
		/// WORKER has unit value 1.
		/// </summary>
		[Test]
		public void UnitValue_Worker_IsOne()
		{
			Assert.AreEqual(1, Constants.UNIT_VALUE[UnitType.WORKER],
				"WORKER should have UNIT_VALUE = 1");
		}

		/// <summary>
		/// ARCHERY has unit value 3 (same as BARRACKS).
		/// </summary>
		[Test]
		public void UnitValue_Archery_IsThree()
		{
			Assert.AreEqual(3, Constants.UNIT_VALUE[UnitType.ARCHERY],
				"ARCHERY should have UNIT_VALUE = 3");
		}

		/// <summary>
		/// MINE has unit value 0 (it is a neutral resource node, not owned).
		/// </summary>
		[Test]
		public void UnitValue_Mine_IsZero()
		{
			Assert.AreEqual(0, Constants.UNIT_VALUE[UnitType.MINE],
				"MINE should have UNIT_VALUE = 0");
		}

		#endregion

		#region Relative Ordering

		/// <summary>
		/// ARCHER value exceeds SOLDIER value.
		/// </summary>
		[Test]
		public void UnitValue_ArcherGreaterThanSoldier()
		{
			Assert.Greater(Constants.UNIT_VALUE[UnitType.ARCHER],
				Constants.UNIT_VALUE[UnitType.SOLDIER],
				"ARCHER should be worth more than SOLDIER");
		}

		/// <summary>
		/// BARRACKS value exceeds BASE value.
		/// </summary>
		[Test]
		public void UnitValue_BarracksGreaterThanBase()
		{
			Assert.Greater(Constants.UNIT_VALUE[UnitType.BARRACKS],
				Constants.UNIT_VALUE[UnitType.BASE],
				"BARRACKS should be worth more than BASE");
		}

		/// <summary>
		/// BASE value exceeds WORKER value.
		/// </summary>
		[Test]
		public void UnitValue_BaseGreaterThanWorker()
		{
			Assert.Greater(Constants.UNIT_VALUE[UnitType.BASE],
				Constants.UNIT_VALUE[UnitType.WORKER],
				"BASE should be worth more than WORKER");
		}

		#endregion
	}
}
