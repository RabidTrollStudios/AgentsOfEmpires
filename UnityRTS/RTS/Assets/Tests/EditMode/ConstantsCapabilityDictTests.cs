using NUnit.Framework;
using AgentSDK;

namespace GameManager.Tests
{
	/// <summary>
	/// Tests that all capability dictionaries (CAN_MOVE, CAN_BUILD, CAN_TRAIN,
	/// CAN_ATTACK, CAN_GATHER) are complete — containing an entry for every
	/// one of the 7 unit types.
	/// Complements ConstantsCapabilityTests which verifies individual values
	/// but does not check dictionary completeness.
	/// </summary>
	[TestFixture]
	public class ConstantsCapabilityDictTests
	{
		private static readonly UnitType[] AllUnitTypes = {
			UnitType.MINE, UnitType.PAWN, UnitType.WARRIOR,
			UnitType.ARCHER, UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY
		};

		#region CAN_MOVE Completeness

		/// <summary>
		/// CAN_MOVE has an entry for every unit type (7 entries).
		/// </summary>
		[Test]
		public void CanMove_HasAllSevenUnitTypes()
		{
			Assert.AreEqual(7, Constants.CAN_MOVE.Count,
				"CAN_MOVE should have 7 entries");
			foreach (var type in AllUnitTypes)
				Assert.IsTrue(Constants.CAN_MOVE.ContainsKey(type),
					$"CAN_MOVE missing key: {type}");
		}

		#endregion

		#region CAN_BUILD Completeness

		/// <summary>
		/// CAN_BUILD has an entry for every unit type.
		/// </summary>
		[Test]
		public void CanBuild_HasAllSevenUnitTypes()
		{
			Assert.AreEqual(7, Constants.CAN_BUILD.Count,
				"CAN_BUILD should have 7 entries");
			foreach (var type in AllUnitTypes)
				Assert.IsTrue(Constants.CAN_BUILD.ContainsKey(type),
					$"CAN_BUILD missing key: {type}");
		}

		#endregion

		#region CAN_TRAIN Completeness

		/// <summary>
		/// CAN_TRAIN has an entry for every unit type.
		/// </summary>
		[Test]
		public void CanTrain_HasAllSevenUnitTypes()
		{
			Assert.AreEqual(7, Constants.CAN_TRAIN.Count,
				"CAN_TRAIN should have 7 entries");
			foreach (var type in AllUnitTypes)
				Assert.IsTrue(Constants.CAN_TRAIN.ContainsKey(type),
					$"CAN_TRAIN missing key: {type}");
		}

		#endregion

		#region CAN_ATTACK Completeness

		/// <summary>
		/// CAN_ATTACK has an entry for every unit type.
		/// </summary>
		[Test]
		public void CanAttack_HasAllSevenUnitTypes()
		{
			Assert.AreEqual(7, Constants.CAN_ATTACK.Count,
				"CAN_ATTACK should have 7 entries");
			foreach (var type in AllUnitTypes)
				Assert.IsTrue(Constants.CAN_ATTACK.ContainsKey(type),
					$"CAN_ATTACK missing key: {type}");
		}

		#endregion

		#region CAN_GATHER Completeness

		/// <summary>
		/// CAN_GATHER has an entry for every unit type.
		/// </summary>
		[Test]
		public void CanGather_HasAllSevenUnitTypes()
		{
			Assert.AreEqual(7, Constants.CAN_GATHER.Count,
				"CAN_GATHER should have 7 entries");
			foreach (var type in AllUnitTypes)
				Assert.IsTrue(Constants.CAN_GATHER.ContainsKey(type),
					$"CAN_GATHER missing key: {type}");
		}

		#endregion

		#region Non-Overlapping Capabilities

		/// <summary>
		/// No unit type should have both CAN_BUILD and CAN_TRAIN true.
		/// Builders do not train; trainers do not build.
		/// </summary>
		[Test]
		public void NoBuildAndTrainOverlap()
		{
			foreach (var type in AllUnitTypes)
			{
				bool canBuild = Constants.CAN_BUILD[type];
				bool canTrain = Constants.CAN_TRAIN[type];
				Assert.IsFalse(canBuild && canTrain,
					$"{type} should not be able to both build and train");
			}
		}

		/// <summary>
		/// Non-mobile units (CAN_MOVE=false) should also have CAN_GATHER=false.
		/// You cannot gather if you cannot move to the mine.
		/// </summary>
		[Test]
		public void Immobile_AlsoCannotGather()
		{
			foreach (var type in AllUnitTypes)
			{
				if (!Constants.CAN_MOVE[type])
				{
					Assert.IsFalse(Constants.CAN_GATHER[type],
						$"{type} cannot move so it should not be able to gather");
				}
			}
		}

		/// <summary>
		/// Non-mobile units should also have CAN_ATTACK=false.
		/// (Stationary units do not attack in this game.)
		/// </summary>
		[Test]
		public void Immobile_AlsoCannotAttack()
		{
			foreach (var type in AllUnitTypes)
			{
				if (!Constants.CAN_MOVE[type])
				{
					Assert.IsFalse(Constants.CAN_ATTACK[type],
						$"{type} cannot move so it should not be able to attack");
				}
			}
		}

		#endregion
	}
}
