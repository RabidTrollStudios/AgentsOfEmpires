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
	[TestFixture]
	public class UnitBuildTests : PlayModeTestBase
	{
		/// <summary>
		/// Manually tick a unit's Update (task state machine) and FixedUpdate
		/// (movement along path) so the build pipeline advances.
		/// GameManager.enabled is false in tests, so neither runs automatically.
		/// </summary>
		private void TickUnit(Unit unit)
		{
			unit.FixedUpdate();
			unit.Update();
		}

		// ------------------------------------------------------------------
		// Happy-path tests
		// ------------------------------------------------------------------

		/// <summary>
		/// A worker builds a base. The building starts with IsBuilt=false and
		/// transitions to IsBuilt=true after the build timer completes.
		/// At GAME_SPEED=20: CREATION_TIME[BASE] = (1/20)*10 = 0.5 s
		/// </summary>
		[UnityTest]
		public IEnumerator WorkerBuildsBase_IsBuiltTransitionsToTrue()
		{
			// Place worker adjacent to where the base will go
			Vector3Int workerPos = new Vector3Int(9, 10, 0);
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);

			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));

			// The building should have been placed immediately with IsBuilt=false
			Unit building = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.FirstOrDefault(u => u.UnitType == UnitType.BASE);

			Assert.IsNotNull(building, "Building should be placed immediately on StartBuilding");
			Assert.IsFalse(building.IsBuilt,
				"Building should start with IsBuilt=false");

			// Tick until construction completes
			yield return WaitUntil(() =>
			{
				TickUnit(worker);
				return building.IsBuilt;
			}, timeoutSeconds: 10f, failMessage: "Building never became IsBuilt=true");

			Assert.IsTrue(building.IsBuilt);
		}

		/// <summary>
		/// After a 3x3 building is placed, all 9 footprint cells become
		/// unwalkable (buildings are not mobile, so walkability is set false).
		/// </summary>
		[UnityTest]
		public IEnumerator BuildingPlaced_FootprintCellsBecomeUnwalkable()
		{
			Vector3Int workerPos = new Vector3Int(9, 10, 0);
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);

			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));

			// Yield one frame so placement is fully processed
			yield return null;

			// Check all 3x3 cells of the base footprint
			// Footprint: anchor (10,10), extends right and down
			// Cells: (10,10), (11,10), (12,10), (10,9), (11,9), (12,9), (10,8), (11,8), (12,8)
			Vector3Int size = Constants.UNIT_SIZE[UnitType.BASE];
			for (int i = 0; i < size.x; i++)
			{
				for (int j = 0; j < size.y; j++)
				{
					Vector3Int cell = buildPos + new Vector3Int(i, -j, 0);
					Assert.IsFalse(ctx.MapManager.IsGridPositionBuildable(cell),
						$"Cell {cell} in building footprint should not be buildable");
					Assert.IsFalse(ctx.MapManager.IsGridPositionWalkable(cell),
						$"Cell {cell} in building footprint should not be walkable");
				}
			}
		}

		/// <summary>
		/// The worker returns to IDLE after construction completes.
		/// </summary>
		[UnityTest]
		public IEnumerator WorkerBuildsBase_WorkerGoesIdleAfterCompletion()
		{
			Vector3Int workerPos = new Vector3Int(9, 10, 0);
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);

			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should be in BUILD action after StartBuilding");

			// Wait for worker to finish building and return to IDLE
			yield return WaitUntil(() =>
			{
				TickUnit(worker);
				return worker.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "Worker never returned to IDLE after building");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction);
		}

		/// <summary>
		/// Gold is deducted at build start, not at completion. Verify gold
		/// drops immediately after StartBuilding before the timer completes.
		/// BASE costs 500 (SCALAR_COST * 10 = 50 * 10).
		/// </summary>
		[UnityTest]
		public IEnumerator BuildBase_GoldDeductedAtStart()
		{
			Vector3Int workerPos = new Vector3Int(9, 10, 0);
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);
			Agent agent = GetAgent0();

			int goldBefore = agent.Gold;
			int baseCost = (int)Constants.COST[UnitType.BASE];

			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));

			// Gold should be deducted immediately
			Assert.AreEqual(goldBefore - baseCost, agent.Gold,
				"Gold should be deducted at build start, not at completion");

			// Verify building is not yet complete
			Unit building = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.FirstOrDefault(u => u.UnitType == UnitType.BASE);
			Assert.IsFalse(building.IsBuilt,
				"Building should not be complete yet");

			yield return null;
		}

		// ------------------------------------------------------------------
		// Boundary tests
		// ------------------------------------------------------------------

		/// <summary>
		/// Build a 3x3 base near the map edge at position (27, 2). The
		/// footprint (27..29, 0..2) should fit within the 30x30 map bounds.
		/// </summary>
		[UnityTest]
		public IEnumerator BuildNearMapEdge_FitsWithinBounds()
		{
			Vector3Int workerPos = new Vector3Int(26, 2, 0);
			Vector3Int buildPos = new Vector3Int(27, 2, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);

			// Verify the area is buildable before issuing the command
			// Exclude the worker's own cell (same as StartBuilding does)
			var exclusion = new HashSet<Vector3Int> { workerPos };
			Assert.IsTrue(ctx.MapManager.IsAreaBuildable(UnitType.BASE, buildPos, exclusion),
				"3x3 area at (27,2) should be buildable within 30x30 map");

			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));

			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should accept build command near map edge");

			// Wait for construction to finish
			yield return WaitUntil(() =>
			{
				TickUnit(worker);
				return worker.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "Build near map edge did not complete");

			Unit building = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.FirstOrDefault(u => u.UnitType == UnitType.BASE);

			Assert.IsNotNull(building);
			Assert.IsTrue(building.IsBuilt,
				"Building near map edge should complete successfully");
		}

		/// <summary>
		/// Worker placed one cell away from the build site has a very short
		/// path and should complete the build quickly.
		/// </summary>
		[UnityTest]
		public IEnumerator WorkerAdjacentToBuildSite_BuildsQuickly()
		{
			// Worker at (9,10), build site at (10,10). The worker is already a
			// neighbor of the 3x3 building, so path length should be minimal.
			Vector3Int workerPos = new Vector3Int(9, 10, 0);
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);

			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction);

			// At speed 20, BASE creation time is 0.5 s. With an adjacent worker
			// the travel phase is nearly zero, so total time should be close to 0.5 s.
			yield return WaitUntil(() =>
			{
				TickUnit(worker);
				return worker.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 5f, failMessage: "Adjacent worker did not finish building quickly");

			Unit building = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.FirstOrDefault(u => u.UnitType == UnitType.BASE);

			Assert.IsNotNull(building);
			Assert.IsTrue(building.IsBuilt);
		}

		// ------------------------------------------------------------------
		// Error tests
		// ------------------------------------------------------------------

		/// <summary>
		/// Building on an occupied location (another building already there)
		/// should be rejected. Gold should not change.
		/// </summary>
		[UnityTest]
		public IEnumerator BuildOnOccupiedLocation_CommandRejectedGoldUnchanged()
		{
			Vector3Int buildPos = new Vector3Int(10, 10, 0);

			// Place a base at the target location first
			PlaceUnit(UnitType.BASE, buildPos);

			// Place a worker nearby
			Vector3Int workerPos = new Vector3Int(8, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);
			Agent agent = GetAgent0();

			int goldBefore = agent.Gold;

			// Try to build another base at the same location
			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));

			Assert.AreNotEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should not enter BUILD state when area is occupied");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not change when build is rejected due to occupied area");

			yield return null;
		}

		/// <summary>
		/// When the agent does not have enough gold, the build command should
		/// be rejected. BASE costs 500; set gold to 10 and verify rejection.
		/// </summary>
		[UnityTest]
		public IEnumerator BuildWithInsufficientGold_CommandRejected()
		{
			Vector3Int workerPos = new Vector3Int(9, 10, 0);
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);
			Agent agent = GetAgent0();

			// Set gold below the cost of a BASE (500)
			agent.Gold = 10;
			int goldBefore = agent.Gold;

			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));

			Assert.AreNotEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should not enter BUILD state with insufficient gold");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not change when build is rejected due to insufficient funds");

			yield return null;
		}

		/// <summary>
		/// When there is no path to the build site (worker is completely walled
		/// off), the building should not be placed and gold should not be
		/// deducted. StartBuilding only proceeds when path.Count > 0.
		/// </summary>
		[UnityTest]
		public IEnumerator BuildWithNoPath_BuildingNotPlacedGoldUnchanged()
		{
			// Destroy any units placed by other setup so we start clean
			ctx.UnitManager.DestroyAllUnits();
			yield return null; // let Destroy process

			// Seal the worker inside a ring of non-walkable terrain cells.
			// This simulates the worker being completely walled off with no
			// pathfinding route to the distant build site.
			Vector3Int workerPos = new Vector3Int(5, 5, 0);

			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx == 0 && dy == 0) continue;
					Vector3Int pos = workerPos + new Vector3Int(dx, dy, 0);
					ctx.MapManager.GridCells[pos.x, pos.y].SetBuildable(false);
					ctx.MapManager.GridCells[pos.x, pos.y].SetWalkable(false);
				}
			}

			// Place the worker inside the sealed area
			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;
			int unitCountBefore = ctx.UnitManager.GetAllUnits().Count;

			// Build site is far away and area-buildable, but no walkable path exists
			Vector3Int buildPos = new Vector3Int(20, 20, 0);

			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));

			Assert.AreNotEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should not enter BUILD when no path exists to build site");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted when no path is found");
			Assert.AreEqual(unitCountBefore, ctx.UnitManager.GetAllUnits().Count,
				"No building should be placed when no path exists");
		}
	}
}
