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
		/// Only mobile unit types (WORKER, SOLDIER, ARCHER) should have CanMove=true.
		/// Buildings and mines are immobile.
		/// </summary>
		[Test]
		public void CanMove_OnlyMobileUnitsAreTrue()
		{
			Assert.IsTrue(Constants.CAN_MOVE[UnitType.WORKER],   "WORKER should be able to move");
			Assert.IsTrue(Constants.CAN_MOVE[UnitType.SOLDIER],  "SOLDIER should be able to move");
			Assert.IsTrue(Constants.CAN_MOVE[UnitType.ARCHER],   "ARCHER should be able to move");

			Assert.IsFalse(Constants.CAN_MOVE[UnitType.BASE],     "BASE should not be able to move");
			Assert.IsFalse(Constants.CAN_MOVE[UnitType.BARRACKS], "BARRACKS should not be able to move");
			Assert.IsFalse(Constants.CAN_MOVE[UnitType.ARCHERY],  "ARCHERY should not be able to move");
			Assert.IsFalse(Constants.CAN_MOVE[UnitType.MINE],     "MINE should not be able to move");
		}

		#endregion

		#region Build Capabilities

		/// <summary>
		/// Only WORKER should be able to build structures.
		/// </summary>
		[Test]
		public void CanBuild_OnlyWorkerIsTrue()
		{
			Assert.IsTrue(Constants.CAN_BUILD[UnitType.WORKER], "WORKER should be able to build");

			Assert.IsFalse(Constants.CAN_BUILD[UnitType.SOLDIER],  "SOLDIER should not be able to build");
			Assert.IsFalse(Constants.CAN_BUILD[UnitType.ARCHER],   "ARCHER should not be able to build");
			Assert.IsFalse(Constants.CAN_BUILD[UnitType.BASE],     "BASE should not be able to build");
			Assert.IsFalse(Constants.CAN_BUILD[UnitType.BARRACKS], "BARRACKS should not be able to build");
			Assert.IsFalse(Constants.CAN_BUILD[UnitType.ARCHERY],  "ARCHERY should not be able to build");
			Assert.IsFalse(Constants.CAN_BUILD[UnitType.MINE],     "MINE should not be able to build");
		}

		/// <summary>
		/// WORKER should be able to build BASE, BARRACKS, and ARCHERY.
		/// </summary>
		[Test]
		public void WorkerBuilds_BaseBarracksArchery()
		{
			var builds = Constants.BUILDS[UnitType.WORKER];

			Assert.Contains(UnitType.BASE,     builds, "WORKER should build BASE");
			Assert.Contains(UnitType.BARRACKS, builds, "WORKER should build BARRACKS");
			Assert.Contains(UnitType.ARCHERY,  builds, "WORKER should build ARCHERY");
		}

		#endregion

		#region Gather Capabilities

		/// <summary>
		/// Only WORKER should be able to gather resources.
		/// </summary>
		[Test]
		public void CanGather_OnlyWorkerIsTrue()
		{
			Assert.IsTrue(Constants.CAN_GATHER[UnitType.WORKER], "WORKER should be able to gather");

			Assert.IsFalse(Constants.CAN_GATHER[UnitType.SOLDIER],  "SOLDIER should not gather");
			Assert.IsFalse(Constants.CAN_GATHER[UnitType.ARCHER],   "ARCHER should not gather");
			Assert.IsFalse(Constants.CAN_GATHER[UnitType.BASE],     "BASE should not gather");
			Assert.IsFalse(Constants.CAN_GATHER[UnitType.BARRACKS], "BARRACKS should not gather");
			Assert.IsFalse(Constants.CAN_GATHER[UnitType.ARCHERY],  "ARCHERY should not gather");
			Assert.IsFalse(Constants.CAN_GATHER[UnitType.MINE],     "MINE should not gather");
		}

		#endregion

		#region Attack Capabilities

		/// <summary>
		/// Only SOLDIER and ARCHER should be able to attack.
		/// </summary>
		[Test]
		public void CanAttack_OnlySoldierAndArcherAreTrue()
		{
			Assert.IsTrue(Constants.CAN_ATTACK[UnitType.SOLDIER], "SOLDIER should be able to attack");
			Assert.IsTrue(Constants.CAN_ATTACK[UnitType.ARCHER],  "ARCHER should be able to attack");

			Assert.IsFalse(Constants.CAN_ATTACK[UnitType.WORKER],   "WORKER should not attack");
			Assert.IsFalse(Constants.CAN_ATTACK[UnitType.BASE],     "BASE should not attack");
			Assert.IsFalse(Constants.CAN_ATTACK[UnitType.BARRACKS], "BARRACKS should not attack");
			Assert.IsFalse(Constants.CAN_ATTACK[UnitType.ARCHERY],  "ARCHERY should not attack");
			Assert.IsFalse(Constants.CAN_ATTACK[UnitType.MINE],     "MINE should not attack");
		}

		/// <summary>
		/// ARCHER should have a strictly greater attack range than SOLDIER.
		/// </summary>
		[Test]
		public void AttackRange_ArcherGreaterThanSoldier()
		{
			Assert.Greater(Constants.ATTACK_RANGE[UnitType.ARCHER],
				Constants.ATTACK_RANGE[UnitType.SOLDIER],
				"ARCHER attack range should exceed SOLDIER attack range");
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

			Assert.IsFalse(Constants.CAN_TRAIN[UnitType.WORKER],   "WORKER should not train");
			Assert.IsFalse(Constants.CAN_TRAIN[UnitType.SOLDIER],  "SOLDIER should not train");
			Assert.IsFalse(Constants.CAN_TRAIN[UnitType.ARCHER],   "ARCHER should not train");
			Assert.IsFalse(Constants.CAN_TRAIN[UnitType.ARCHERY],  "ARCHERY should not train");
			Assert.IsFalse(Constants.CAN_TRAIN[UnitType.MINE],     "MINE should not train");
		}

		/// <summary>
		/// BASE should train WORKER; BARRACKS should train SOLDIER and ARCHER.
		/// </summary>
		[Test]
		public void TrainsMatrix_CorrectUnitTypes()
		{
			Assert.Contains(UnitType.WORKER,  Constants.TRAINS[UnitType.BASE],
				"BASE should train WORKER");
			Assert.Contains(UnitType.SOLDIER, Constants.TRAINS[UnitType.BARRACKS],
				"BARRACKS should train SOLDIER");
			Assert.Contains(UnitType.ARCHER,  Constants.TRAINS[UnitType.BARRACKS],
				"BARRACKS should train ARCHER");
		}

		/// <summary>
		/// BASE should not train SOLDIER or ARCHER.
		/// BARRACKS should not train WORKER.
		/// </summary>
		[Test]
		public void TrainsMatrix_DoesNotContainWrongTypes()
		{
			Assert.IsFalse(Constants.TRAINS[UnitType.BASE].Contains(UnitType.SOLDIER),
				"BASE should not train SOLDIER");
			Assert.IsFalse(Constants.TRAINS[UnitType.BASE].Contains(UnitType.ARCHER),
				"BASE should not train ARCHER");
			Assert.IsFalse(Constants.TRAINS[UnitType.BARRACKS].Contains(UnitType.WORKER),
				"BARRACKS should not train WORKER");
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
		/// so combat units are worth more than workers.
		/// </summary>
		[Test]
		public void UnitValue_CombatUnitsWorthMoreThanWorker()
		{
			Assert.Greater(Constants.UNIT_VALUE[UnitType.SOLDIER],
				Constants.UNIT_VALUE[UnitType.WORKER],
				"SOLDIER should be worth more than WORKER");
			Assert.Greater(Constants.UNIT_VALUE[UnitType.ARCHER],
				Constants.UNIT_VALUE[UnitType.WORKER],
				"ARCHER should be worth more than WORKER");
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
