using System.Collections;
using System.Collections.Generic;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	[TestFixture]
	public class UnitDestructionTests : PlayModeTestBase
	{
		#region Happy Path

		/// <summary>
		/// A unit whose health drops to zero should be destroyed and removed
		/// from the UnitManager on the next Update cycle.
		/// </summary>
		[UnityTest]
		public IEnumerator Unit_HealthZero_DestroyedAndRemovedFromUnitManager()
		{
			var pos = new Vector3Int(10, 10, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, pos);
			int unitNbr = pawn.UnitNbr;

			Assert.IsNotNull(ctx.UnitManager.GetUnit(unitNbr),
				"Unit should exist in UnitManager before destruction");

			// Set health to zero to trigger destruction
			pawn.Health = 0;
			pawn.FixedUpdate();

			// Yield a frame so Object.Destroy is processed
			yield return null;

			Assert.IsNull(ctx.UnitManager.GetUnit(unitNbr),
				"Unit should be removed from UnitManager after health drops to zero");
		}

		/// <summary>
		/// When a pawn is destroyed, its occupied cell should become buildable again.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Destroyed_CellBecomesBuildable()
		{
			var pos = new Vector3Int(12, 12, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, pos);

			Assert.IsFalse(ctx.MapManager.IsGridPositionBuildable(pos),
				"Cell should not be buildable while pawn is alive on it");

			pawn.Health = 0;
			pawn.FixedUpdate();

			yield return null;

			Assert.IsTrue(ctx.MapManager.IsGridPositionBuildable(pos),
				"Cell should be buildable after pawn is destroyed");
		}

		/// <summary>
		/// When a BASE (3x3 building) is destroyed, all of its occupied cells
		/// should become buildable and walkable again.
		/// </summary>
		[UnityTest]
		public IEnumerator Building_Destroyed_AllOccupiedCellsFreed()
		{
			// BASE occupies 3x3: position (10,12) covers cells
			// (10,12),(11,12),(12,12) for j=0
			// (10,11),(11,11),(12,11) for j=1
			// (10,10),(11,10),(12,10) for j=2
			var basePos = new Vector3Int(10, 12, 0);
			Unit building = PlaceUnit(UnitType.BASE, basePos);

			// Verify all 9 cells are not buildable
			var occupiedCells = new List<Vector3Int>();
			for (int i = 0; i < 3; i++)
			{
				for (int j = 0; j < 3; j++)
				{
					var cell = basePos + new Vector3Int(i, -j, 0);
					occupiedCells.Add(cell);
					Assert.IsFalse(ctx.MapManager.IsGridPositionBuildable(cell),
						$"Cell {cell} should not be buildable while BASE is alive");
				}
			}

			building.Health = 0;
			building.FixedUpdate();

			yield return null;

			// All 9 cells should now be buildable
			foreach (var cell in occupiedCells)
			{
				Assert.IsTrue(ctx.MapManager.IsGridPositionBuildable(cell),
					$"Cell {cell} should be buildable after BASE is destroyed");
			}
		}

		#endregion

		#region Boundary

		/// <summary>
		/// Setting health to exactly 0 (not negative) should still trigger destruction.
		/// </summary>
		[UnityTest]
		public IEnumerator Unit_HealthExactlyZero_TriggersDestruction()
		{
			var pos = new Vector3Int(8, 8, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, pos);
			int unitNbr = pawn.UnitNbr;

			pawn.Health = 0f;
			pawn.FixedUpdate();

			yield return null;

			Assert.IsNull(ctx.UnitManager.GetUnit(unitNbr),
				"Unit with health exactly 0 should be destroyed");
		}

		/// <summary>
		/// Setting health to a large negative value should still trigger destruction
		/// without errors (the <= 0 check handles any negative).
		/// </summary>
		[UnityTest]
		public IEnumerator Unit_HealthLargeNegative_TriggersDestruction()
		{
			var pos = new Vector3Int(8, 8, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, pos);
			int unitNbr = pawn.UnitNbr;

			pawn.Health = -1000f;
			pawn.FixedUpdate();

			yield return null;

			Assert.IsNull(ctx.UnitManager.GetUnit(unitNbr),
				"Unit with health -1000 should be destroyed without error");
		}

		/// <summary>
		/// Destroying the last unit of a given type should result in an empty list
		/// when querying the UnitManager for that type.
		/// </summary>
		[UnityTest]
		public IEnumerator DestroyLastUnitOfType_QueryReturnsEmpty()
		{
			var pos = new Vector3Int(14, 14, 0);
			Unit warrior = PlaceUnit(UnitType.WARRIOR, pos);
			int unitNbr = warrior.UnitNbr;

			// Verify the warrior is registered
			List<int> warriorsBefore = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WARRIOR);
			Assert.AreEqual(1, warriorsBefore.Count,
				"There should be exactly one WARRIOR before destruction");

			warrior.Health = 0;
			warrior.FixedUpdate();

			yield return null;

			List<int> warriorsAfter = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WARRIOR);
			Assert.AreEqual(0, warriorsAfter.Count,
				"There should be no WARRIORs after the last one is destroyed");
			Assert.IsNull(ctx.UnitManager.GetUnit(unitNbr),
				"GetUnit should return null for the destroyed warrior");
		}

		#endregion

		#region Error

		/// <summary>
		/// If an attacker's target is destroyed externally (by setting health to 0),
		/// the attacker should detect the null/dead target and go IDLE without crashing.
		/// </summary>
		[UnityTest]
		public IEnumerator Attacker_TargetDestroyedExternally_GoesIdleNoCrash()
		{
			// Place an attacker (warrior owned by agent 0) and a target (pawn owned by agent 1)
			var attackerPos = new Vector3Int(10, 10, 0);
			var targetPos = new Vector3Int(12, 10, 0);
			Unit attacker = PlaceUnit(UnitType.WARRIOR, attackerPos);
			Unit target = PlaceUnit(UnitType.PAWN, targetPos, ctx.Agent1Go);

			// Start attacking
			attacker.StartAttacking(new AttackEventArgs(attacker, target));

			// Let the attack begin for a couple of frames
			yield return WaitFrames(3);

			// Externally destroy the target by setting health to 0 and calling Update
			target.Health = 0;
			target.FixedUpdate();

			// Yield a frame so Object.Destroy on the target is processed
			yield return null;

			// Now call the attacker's FixedUpdate; it should detect the dead target and go IDLE
			// The UpdateAttack method checks AttackUnit == null and Health <= 0.
			attacker.FixedUpdate();

			yield return null;

			Assert.AreEqual(UnitAction.IDLE, attacker.CurrentAction,
				"Attacker should go IDLE when its target is destroyed externally");
		}

		#endregion

		#region Stress

		/// <summary>
		/// Creating and destroying many units in rapid succession should leave
		/// the UnitManager in a consistent state with no lingering references.
		/// </summary>
		[UnityTest]
		public IEnumerator ManyUnits_CreatedAndDestroyed_UnitManagerConsistent()
		{
			int unitCount = 15;
			var unitNbrs = new List<int>();
			var positions = new List<Vector3Int>();

			// Create 15 units spread across the map
			for (int i = 0; i < unitCount; i++)
			{
				var pos = new Vector3Int(1 + i, 1 + i, 0);
				positions.Add(pos);
				Unit pawn = PlaceUnit(UnitType.PAWN, pos);
				unitNbrs.Add(pawn.UnitNbr);
			}

			// Verify all units exist
			foreach (int nbr in unitNbrs)
			{
				Assert.IsNotNull(ctx.UnitManager.GetUnit(nbr),
					$"Unit {nbr} should exist after creation");
			}

			// Destroy all units in rapid succession by setting health to 0
			// and calling Update on each
			for (int i = 0; i < unitCount; i++)
			{
				Unit unit = ctx.UnitManager.GetUnit(unitNbrs[i]);
				if (unit != null)
				{
					unit.Health = 0;
					unit.FixedUpdate();
				}
			}

			// Yield a frame for Object.Destroy to process
			yield return null;

			// All units should be gone
			foreach (int nbr in unitNbrs)
			{
				Assert.IsNull(ctx.UnitManager.GetUnit(nbr),
					$"Unit {nbr} should be removed from UnitManager after destruction");
			}

			// All cells should be buildable again
			foreach (var pos in positions)
			{
				Assert.IsTrue(ctx.MapManager.IsGridPositionBuildable(pos),
					$"Cell {pos} should be buildable after unit destruction");
			}

			// UnitManager should report zero pawns
			List<int> remainingPawns = ctx.UnitManager.GetUnitNbrsOfType(UnitType.PAWN);
			Assert.AreEqual(0, remainingPawns.Count,
				"UnitManager should have zero PAWNs after all are destroyed");
		}

		#endregion
	}
}
