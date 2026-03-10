using System.Collections.Generic;
using NUnit.Framework;
using AgentSDK;

namespace GameManager.Tests
{
	/// <summary>
	/// Tests that verify the capability and dependency matrices in Constants
	/// match the expected game design. These act as a regression guard against
	/// accidental SDK constant changes.
	/// </summary>
	[TestFixture]
	public class ConstantsCapabilityTests
	{
		#region Movement Capabilities

		/// <summary>
		/// Only mobile unit types (PAWN, WARRIOR, ARCHER) should have CanMove=true.
		/// Buildings and mines are immobile.
		/// </summary>
		[Test]
		public void CanMove_OnlyMobileUnitsAreTrue()
		{
			Assert.IsTrue(Constants.CAN_MOVE[UnitType.PAWN],   "PAWN should be able to move");
			Assert.IsTrue(Constants.CAN_MOVE[UnitType.WARRIOR],  "WARRIOR should be able to move");
			Assert.IsTrue(Constants.CAN_MOVE[UnitType.ARCHER],   "ARCHER should be able to move");

			Assert.IsFalse(Constants.CAN_MOVE[UnitType.BASE],     "BASE should not be able to move");
			Assert.IsFalse(Constants.CAN_MOVE[UnitType.BARRACKS], "BARRACKS should not be able to move");
			Assert.IsFalse(Constants.CAN_MOVE[UnitType.ARCHERY],  "ARCHERY should not be able to move");
			Assert.IsFalse(Constants.CAN_MOVE[UnitType.MINE],     "MINE should not be able to move");
		}

		#endregion

		#region Build Capabilities

		/// <summary>
		/// Only PAWN should be able to build structures.
		/// </summary>
		[Test]
		public void CanBuild_OnlyPawnIsTrue()
		{
			Assert.IsTrue(Constants.CAN_BUILD[UnitType.PAWN], "PAWN should be able to build");

			Assert.IsFalse(Constants.CAN_BUILD[UnitType.WARRIOR],  "WARRIOR should not be able to build");
			Assert.IsFalse(Constants.CAN_BUILD[UnitType.ARCHER],   "ARCHER should not be able to build");
			Assert.IsFalse(Constants.CAN_BUILD[UnitType.BASE],     "BASE should not be able to build");
			Assert.IsFalse(Constants.CAN_BUILD[UnitType.BARRACKS], "BARRACKS should not be able to build");
			Assert.IsFalse(Constants.CAN_BUILD[UnitType.ARCHERY],  "ARCHERY should not be able to build");
			Assert.IsFalse(Constants.CAN_BUILD[UnitType.MINE],     "MINE should not be able to build");
		}

		/// <summary>
		/// PAWN should be able to build BASE, BARRACKS, and ARCHERY.
		/// </summary>
		[Test]
		public void PawnBuilds_BaseBarracksArchery()
		{
			var builds = Constants.BUILDS[UnitType.PAWN];

			Assert.Contains(UnitType.BASE,     builds, "PAWN should build BASE");
			Assert.Contains(UnitType.BARRACKS, builds, "PAWN should build BARRACKS");
			Assert.Contains(UnitType.ARCHERY,  builds, "PAWN should build ARCHERY");
		}

		#endregion

		#region Gather Capabilities

		/// <summary>
		/// Only PAWN should be able to gather resources.
		/// </summary>
		[Test]
		public void CanGather_OnlyPawnIsTrue()
		{
			Assert.IsTrue(Constants.CAN_GATHER[UnitType.PAWN], "PAWN should be able to gather");

			Assert.IsFalse(Constants.CAN_GATHER[UnitType.WARRIOR],  "WARRIOR should not gather");
			Assert.IsFalse(Constants.CAN_GATHER[UnitType.ARCHER],   "ARCHER should not gather");
			Assert.IsFalse(Constants.CAN_GATHER[UnitType.BASE],     "BASE should not gather");
			Assert.IsFalse(Constants.CAN_GATHER[UnitType.BARRACKS], "BARRACKS should not gather");
			Assert.IsFalse(Constants.CAN_GATHER[UnitType.ARCHERY],  "ARCHERY should not gather");
			Assert.IsFalse(Constants.CAN_GATHER[UnitType.MINE],     "MINE should not gather");
		}

		#endregion

		#region Attack Capabilities

		/// <summary>
		/// Only WARRIOR and ARCHER should be able to attack.
		/// </summary>
		[Test]
		public void CanAttack_OnlyWarriorAndArcherAreTrue()
		{
			Assert.IsTrue(Constants.CAN_ATTACK[UnitType.WARRIOR], "WARRIOR should be able to attack");
			Assert.IsTrue(Constants.CAN_ATTACK[UnitType.ARCHER],  "ARCHER should be able to attack");

			Assert.IsFalse(Constants.CAN_ATTACK[UnitType.PAWN],   "PAWN should not attack");
			Assert.IsFalse(Constants.CAN_ATTACK[UnitType.BASE],     "BASE should not attack");
			Assert.IsFalse(Constants.CAN_ATTACK[UnitType.BARRACKS], "BARRACKS should not attack");
			Assert.IsFalse(Constants.CAN_ATTACK[UnitType.ARCHERY],  "ARCHERY should not attack");
			Assert.IsFalse(Constants.CAN_ATTACK[UnitType.MINE],     "MINE should not attack");
		}

		/// <summary>
		/// ARCHER should have a strictly greater attack range than WARRIOR.
		/// </summary>
		[Test]
		public void AttackRange_ArcherGreaterThanWarrior()
		{
			Assert.Greater(Constants.ATTACK_RANGE[UnitType.ARCHER],
				Constants.ATTACK_RANGE[UnitType.WARRIOR],
				"ARCHER attack range should exceed WARRIOR attack range");
		}

		#endregion

		#region Train Capabilities

		/// <summary>
		/// Only BASE and BARRACKS should be able to train units.
		/// </summary>
		[Test]
		public void CanTrain_OnlyBaseAndBarracksAreTrue()
		{
			Assert.IsTrue(Constants.CAN_TRAIN[UnitType.BASE],     "BASE should be able to train");
			Assert.IsTrue(Constants.CAN_TRAIN[UnitType.BARRACKS], "BARRACKS should be able to train");

			Assert.IsFalse(Constants.CAN_TRAIN[UnitType.PAWN],   "PAWN should not train");
			Assert.IsFalse(Constants.CAN_TRAIN[UnitType.WARRIOR],  "WARRIOR should not train");
			Assert.IsFalse(Constants.CAN_TRAIN[UnitType.ARCHER],   "ARCHER should not train");
			Assert.IsFalse(Constants.CAN_TRAIN[UnitType.ARCHERY],  "ARCHERY should not train");
			Assert.IsFalse(Constants.CAN_TRAIN[UnitType.MINE],     "MINE should not train");
		}

		/// <summary>
		/// BASE should train PAWN; BARRACKS should train WARRIOR and ARCHER.
		/// </summary>
		[Test]
		public void TrainsMatrix_CorrectUnitTypes()
		{
			Assert.Contains(UnitType.PAWN,  Constants.TRAINS[UnitType.BASE],
				"BASE should train PAWN");
			Assert.Contains(UnitType.WARRIOR, Constants.TRAINS[UnitType.BARRACKS],
				"BARRACKS should train WARRIOR");
			Assert.Contains(UnitType.ARCHER,  Constants.TRAINS[UnitType.BARRACKS],
				"BARRACKS should train ARCHER");
		}

		/// <summary>
		/// BASE should not train WARRIOR or ARCHER.
		/// BARRACKS should not train PAWN.
		/// </summary>
		[Test]
		public void TrainsMatrix_DoesNotContainWrongTypes()
		{
			Assert.IsFalse(Constants.TRAINS[UnitType.BASE].Contains(UnitType.WARRIOR),
				"BASE should not train WARRIOR");
			Assert.IsFalse(Constants.TRAINS[UnitType.BASE].Contains(UnitType.ARCHER),
				"BASE should not train ARCHER");
			Assert.IsFalse(Constants.TRAINS[UnitType.BARRACKS].Contains(UnitType.PAWN),
				"BARRACKS should not train PAWN");
		}

		#endregion

		#region Dependencies

		/// <summary>
		/// BARRACKS should depend on BASE being built before it can be constructed.
		/// </summary>
		[Test]
		public void Dependency_BarracksDependsOnBase()
		{
			Assert.Contains(UnitType.BASE, Constants.DEPENDENCY[UnitType.BARRACKS],
				"BARRACKS should require BASE as a dependency");
		}

		/// <summary>
		/// BASE has no build/train dependencies (it is the foundational structure).
		/// </summary>
		[Test]
		public void Dependency_BaseHasNoDependencies()
		{
			Assert.AreEqual(0, Constants.DEPENDENCY[UnitType.BASE].Count,
				"BASE should have no dependencies");
		}

		#endregion

		#region Unit Values

		/// <summary>
		/// UNIT_VALUE should be non-negative for all unit types and ordered
		/// so combat units are worth more than pawns.
		/// </summary>
		[Test]
		public void UnitValue_CombatUnitsWorthMoreThanPawn()
		{
			Assert.Greater(Constants.UNIT_VALUE[UnitType.WARRIOR],
				Constants.UNIT_VALUE[UnitType.PAWN],
				"WARRIOR should be worth more than PAWN");
			Assert.Greater(Constants.UNIT_VALUE[UnitType.ARCHER],
				Constants.UNIT_VALUE[UnitType.PAWN],
				"ARCHER should be worth more than PAWN");
		}

		/// <summary>
		/// MINE should have zero unit value (it is a resource node, not a combat asset).
		/// </summary>
		[Test]
		public void UnitValue_MineIsZero()
		{
			Assert.AreEqual(0, Constants.UNIT_VALUE[UnitType.MINE],
				"MINE should have UNIT_VALUE of 0");
		}

		#endregion
	}
}
