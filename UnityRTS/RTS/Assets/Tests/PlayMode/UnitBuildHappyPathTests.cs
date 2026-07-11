using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for the unit build happy path and boundary conditions:
	/// IsBuilt transition, footprint walkability, pawn state, gold deduction,
	/// near-edge builds, and adjacent-pawn builds.
	/// </summary>
	[TestFixture]
	public class UnitBuildHappyPathTests : PlayModeTestBase
	{
		// ── Helper ─────────────────────────────────────────────────────────────

		private void TickUnit(Unit unit)
		{
			unit.TickFixedUpdate();
			unit.Update();
		}

		// ── Happy path ─────────────────────────────────────────────────────────

		/// <summary>
		/// At GAME_SPEED=20: CREATION_TIME[BASE] = (1/20)*10 = 0.5 s
		/// </summary>
		[UnityTest]
		public IEnumerator PawnBuildsBase_IsBuiltTransitionsToTrue()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BASE));

			Unit building = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.FirstOrDefault(u => u.UnitType == UnitType.BASE);

			Assert.IsNotNull(building, "Building should be placed immediately on StartBuilding");
			Assert.IsFalse(building.IsBuilt, "Building should start with IsBuilt=false");

			yield return WaitUntil(() =>
			{
				TickUnit(pawn);
				return building.IsBuilt;
			}, timeoutSeconds: 10f, failMessage: "Building never became IsBuilt=true");

			Assert.IsTrue(building.IsBuilt);
		}

		[UnityTest]
		public IEnumerator BuildingPlaced_FootprintCellsBecomeUnwalkable()
		{
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			pawn.StartBuilding(new BuildEventArgs(pawn, buildPos, UnitType.BASE));

			yield return null;

			// Footprint extends UP (anchor + (i, +j)); the TOP row (j == size.y-1) is the
			// walkable passage, the body rows are not walkable.
			Vector3Int size = Constants.UNIT_SIZE[UnitType.BASE];
			for (int i = 0; i < size.x; i++)
			{
				for (int j = 0; j < size.y; j++)
				{
					Vector3Int cell = buildPos + new Vector3Int(i, j, 0);
					Assert.IsFalse(ctx.MapManager.IsGridPositionBuildable(cell),
						$"Cell {cell} in building footprint should not be buildable");
					if (j == size.y - 1 && size.y > 1)
						Assert.IsTrue(ctx.MapManager.IsGridPositionWalkable(cell),
							$"Top row cell {cell} should remain walkable (passage)");
					else
						Assert.IsFalse(ctx.MapManager.IsGridPositionWalkable(cell),
							$"Body cell {cell} in building footprint should not be walkable");
				}
			}
		}

		[UnityTest]
		public IEnumerator PawnBuildsBase_PawnGoesIdleAfterCompletion()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BASE));

			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction,
				"Pawn should be in BUILD action after StartBuilding");

			yield return WaitUntil(() =>
			{
				TickUnit(pawn);
				return pawn.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "Pawn never returned to IDLE after building");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction);
		}

		/// <summary>
		/// Gold is deducted at build start (not at completion).
		/// BASE costs SCALAR_COST * 10 = 50 * 10 = 500.
		/// </summary>
		[UnityTest]
		public IEnumerator BuildBase_GoldDeductedAtStart()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			Agent agent = GetAgent0();

			int goldBefore = agent.Gold;
			int baseCost   = (int)Constants.COST[UnitType.BASE];

			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BASE));

			Assert.AreEqual(goldBefore - baseCost, agent.Gold,
				"Gold should be deducted at build start, not at completion");

			Unit building = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.FirstOrDefault(u => u.UnitType == UnitType.BASE);
			Assert.IsFalse(building.IsBuilt, "Building should not be complete yet");

			yield return null;
		}

		// ── Boundary conditions ────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator BuildNearMapEdge_FitsWithinBounds()
		{
			Vector3Int pawnPos = new Vector3Int(22, 6, 0);
			Vector3Int buildPos  = new Vector3Int(23, 6, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, pawnPos);

			var exclusion = new HashSet<Vector3Int> { pawnPos };
			Assert.IsTrue(ctx.MapManager.IsAreaBuildable(UnitType.BASE, buildPos, exclusion),
				"6x4 area at (23,6) should be buildable within 30x30 map");

			pawn.StartBuilding(new BuildEventArgs(pawn, buildPos, UnitType.BASE));

			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction,
				"Pawn should accept build command near map edge");

			yield return WaitUntil(() =>
			{
				TickUnit(pawn);
				return pawn.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "Build near map edge did not complete");

			Unit building = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.FirstOrDefault(u => u.UnitType == UnitType.BASE);

			Assert.IsNotNull(building);
			Assert.IsTrue(building.IsBuilt, "Building near map edge should complete successfully");
		}

		[UnityTest]
		public IEnumerator PawnAdjacentToBuildSite_BuildsQuickly()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BASE));

			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction);

			yield return WaitUntil(() =>
			{
				TickUnit(pawn);
				return pawn.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 5f, failMessage: "Adjacent pawn did not finish building quickly");

			Unit building = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.FirstOrDefault(u => u.UnitType == UnitType.BASE);

			Assert.IsNotNull(building);
			Assert.IsTrue(building.IsBuilt);
		}
	}
}
