using NUnit.Framework;
using UnityEngine;
using AgentSDK;

namespace GameManager.Tests
{
	/// <summary>
	/// Tests for Constants.ATTACK_RANGE and Constants.UNIT_SIZE:
	/// - Attack ranges are positive for combat units and zero for non-combatants.
	/// - Unit size dictionary is complete and buildings have multi-cell footprints.
	/// </summary>
	[TestFixture]
	public class ConstantsAttackSizeTests
	{
		private static readonly UnitType[] AllUnitTypes = {
			UnitType.MINE, UnitType.PAWN, UnitType.WARRIOR,
			UnitType.ARCHER, UnitType.LANCER, UnitType.BASE,
			UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER
		};

		#region ATTACK_RANGE Completeness

		/// <summary>
		/// ATTACK_RANGE contains entries for all 9 unit types.
		/// </summary>
		[Test]
		public void AttackRange_HasAllNineUnitTypes()
		{
			Assert.AreEqual(9, Constants.ATTACK_RANGE.Count,
				"ATTACK_RANGE should have 9 entries");
			foreach (var type in AllUnitTypes)
				Assert.IsTrue(Constants.ATTACK_RANGE.ContainsKey(type),
					$"ATTACK_RANGE missing entry for {type}");
		}

		/// <summary>
		/// All ATTACK_RANGE values should be non-negative.
		/// </summary>
		[Test]
		public void AttackRange_AllNonNegative()
		{
			foreach (var kvp in Constants.ATTACK_RANGE)
			{
				Assert.GreaterOrEqual(kvp.Value, 0f,
					$"ATTACK_RANGE for {kvp.Key} should be >= 0");
			}
		}

		/// <summary>
		/// WARRIOR, ARCHER, and LANCER (CAN_ATTACK=true) should have strictly positive range.
		/// </summary>
		[Test]
		public void AttackRange_CombatUnits_HavePositiveRange()
		{
			Assert.Greater(Constants.ATTACK_RANGE[UnitType.WARRIOR], 0f,
				"WARRIOR should have positive attack range");
			Assert.Greater(Constants.ATTACK_RANGE[UnitType.ARCHER], 0f,
				"ARCHER should have positive attack range");
			Assert.Greater(Constants.ATTACK_RANGE[UnitType.LANCER], 0f,
				"LANCER should have positive attack range");
		}

		/// <summary>
		/// Non-combat units should have zero attack range.
		/// </summary>
		[Test]
		public void AttackRange_NonCombatUnits_HaveZeroRange()
		{
			Assert.AreEqual(0f, Constants.ATTACK_RANGE[UnitType.PAWN], 0.001f,
				"PAWN should have zero attack range");
			Assert.AreEqual(0f, Constants.ATTACK_RANGE[UnitType.MINE], 0.001f,
				"MINE should have zero attack range");
			Assert.AreEqual(0f, Constants.ATTACK_RANGE[UnitType.BASE], 0.001f,
				"BASE should have zero attack range");
			Assert.AreEqual(0f, Constants.ATTACK_RANGE[UnitType.BARRACKS], 0.001f,
				"BARRACKS should have zero attack range");
			Assert.AreEqual(0f, Constants.ATTACK_RANGE[UnitType.ARCHERY], 0.001f,
				"ARCHERY should have zero attack range");
			Assert.AreEqual(0f, Constants.ATTACK_RANGE[UnitType.TOWER], 0.001f,
				"TOWER should have zero attack range");
		}

		/// <summary>
		/// Attack range and CAN_ATTACK are consistent: every unit with CAN_ATTACK=true
		/// has positive range, and every unit with CAN_ATTACK=false has zero range.
		/// </summary>
		[Test]
		public void AttackRange_ConsistentWithCanAttack()
		{
			foreach (var type in AllUnitTypes)
			{
				bool canAttack = Constants.CAN_ATTACK[type];
				float range = Constants.ATTACK_RANGE[type];

				if (canAttack)
					Assert.Greater(range, 0f,
						$"{type} has CAN_ATTACK=true so should have positive attack range");
				else
					Assert.AreEqual(0f, range, 0.001f,
						$"{type} has CAN_ATTACK=false so should have zero attack range");
			}
		}

		#endregion

		#region UNIT_SIZE Completeness

		/// <summary>
		/// UNIT_SIZE contains entries for all 9 unit types.
		/// </summary>
		[Test]
		public void UnitSize_HasAllNineUnitTypes()
		{
			Assert.AreEqual(9, Constants.UNIT_SIZE.Count,
				"UNIT_SIZE should have 9 entries");
			foreach (var type in AllUnitTypes)
				Assert.IsTrue(Constants.UNIT_SIZE.ContainsKey(type),
					$"UNIT_SIZE missing entry for {type}");
		}

		/// <summary>
		/// All UNIT_SIZE values should have non-negative x and y components.
		/// </summary>
		[Test]
		public void UnitSize_AllNonNegativeDimensions()
		{
			foreach (var kvp in Constants.UNIT_SIZE)
			{
				Assert.GreaterOrEqual(kvp.Value.x, 0,
					$"UNIT_SIZE[{kvp.Key}].x should be >= 0");
				Assert.GreaterOrEqual(kvp.Value.y, 0,
					$"UNIT_SIZE[{kvp.Key}].y should be >= 0");
			}
		}

		/// <summary>
		/// Building types (BASE, BARRACKS, ARCHERY, TOWER) have a size greater than 1x1.
		/// </summary>
		[Test]
		public void UnitSize_Buildings_LargerThanOneByOne()
		{
			int baseArea = Constants.UNIT_SIZE[UnitType.BASE].x * Constants.UNIT_SIZE[UnitType.BASE].y;
			int barracksArea = Constants.UNIT_SIZE[UnitType.BARRACKS].x * Constants.UNIT_SIZE[UnitType.BARRACKS].y;
			int archeryArea = Constants.UNIT_SIZE[UnitType.ARCHERY].x * Constants.UNIT_SIZE[UnitType.ARCHERY].y;
			int towerArea = Constants.UNIT_SIZE[UnitType.TOWER].x * Constants.UNIT_SIZE[UnitType.TOWER].y;

			Assert.Greater(baseArea, 1, "BASE footprint should span more than 1 cell");
			Assert.Greater(barracksArea, 1, "BARRACKS footprint should span more than 1 cell");
			Assert.Greater(archeryArea, 1, "ARCHERY footprint should span more than 1 cell");
			Assert.Greater(towerArea, 1, "TOWER footprint should span more than 1 cell");
		}

		/// <summary>
		/// Mobile units (PAWN, WARRIOR, ARCHER, LANCER) occupy a 1×1 footprint.
		/// </summary>
		[Test]
		public void UnitSize_MobileUnits_AreOneByOne()
		{
			Assert.AreEqual(new Vector3Int(1, 1, 0), Constants.UNIT_SIZE[UnitType.PAWN],
				"PAWN should have 1x1 footprint");
			Assert.AreEqual(new Vector3Int(1, 1, 0), Constants.UNIT_SIZE[UnitType.WARRIOR],
				"WARRIOR should have 1x1 footprint");
			Assert.AreEqual(new Vector3Int(1, 1, 0), Constants.UNIT_SIZE[UnitType.ARCHER],
				"ARCHER should have 1x1 footprint");
			Assert.AreEqual(new Vector3Int(1, 1, 0), Constants.UNIT_SIZE[UnitType.LANCER],
				"LANCER should have 1x1 footprint");
		}

		#endregion
	}
}
