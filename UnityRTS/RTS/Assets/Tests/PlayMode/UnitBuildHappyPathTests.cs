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
	/// IsBuilt transition, footprint walkability, worker state, gold deduction,
	/// near-edge builds, and adjacent-worker builds.
	/// </summary>
	[TestFixture]
	public class UnitBuildHappyPathTests : PlayModeTestBase
	{
		// ── Helper ─────────────────────────────────────────────────────────────

		private void TickUnit(Unit unit)
		{
			unit.FixedUpdate();
			unit.Update();
		}

		// ── Happy path ─────────────────────────────────────────────────────────

		/// <summary>
		/// At GAME_SPEED=20: CREATION_TIME[BASE] = (1/20)*10 = 0.5 s
		/// </summary>
		[UnityTest]
		public IEnumerator WorkerBuildsBase_IsBuiltTransitionsToTrue()
		{
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			worker.StartBuilding(new BuildEventArgs(worker, new Vector3Int(10, 10, 0), UnitType.BASE));

			Unit building = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.FirstOrDefault(u => u.UnitType == UnitType.BASE);

			Assert.IsNotNull(building, "Building should be placed immediately on StartBuilding");
			Assert.IsFalse(building.IsBuilt, "Building should start with IsBuilt=false");

			yield return WaitUntil(() =>
			{
				TickUnit(worker);
				return building.IsBuilt;
			}, timeoutSeconds: 10f, failMessage: "Building never became IsBuilt=true");

			Assert.IsTrue(building.IsBuilt);
		}

		[UnityTest]
		public IEnumerator BuildingPlaced_FootprintCellsBecomeUnwalkable()
		{
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));

			yield return null;

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

		[UnityTest]
		public IEnumerator WorkerBuildsBase_WorkerGoesIdleAfterCompletion()
		{
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			worker.StartBuilding(new BuildEventArgs(worker, new Vector3Int(10, 10, 0), UnitType.BASE));

			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should be in BUILD action after StartBuilding");

			yield return WaitUntil(() =>
			{
				TickUnit(worker);
				return worker.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "Worker never returned to IDLE after building");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction);
		}

		/// <summary>
		/// Gold is deducted at build start (not at completion).
		/// BASE costs SCALAR_COST * 10 = 50 * 10 = 500.
		/// </summary>
		[UnityTest]
		public IEnumerator BuildBase_GoldDeductedAtStart()
		{
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			Agent agent = GetAgent0();

			int goldBefore = agent.Gold;
			int baseCost   = (int)Constants.COST[UnitType.BASE];

			worker.StartBuilding(new BuildEventArgs(worker, new Vector3Int(10, 10, 0), UnitType.BASE));

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
			Vector3Int workerPos = new Vector3Int(24, 4, 0);
			Vector3Int buildPos  = new Vector3Int(25, 4, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);

			var exclusion = new HashSet<Vector3Int> { workerPos };
			Assert.IsTrue(ctx.MapManager.IsAreaBuildable(UnitType.BASE, buildPos, exclusion),
				"4x4 area at (25,4) should be buildable within 30x30 map");

			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));

			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should accept build command near map edge");

			yield return WaitUntil(() =>
			{
				TickUnit(worker);
				return worker.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "Build near map edge did not complete");

			Unit building = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.FirstOrDefault(u => u.UnitType == UnitType.BASE);

			Assert.IsNotNull(building);
			Assert.IsTrue(building.IsBuilt, "Building near map edge should complete successfully");
		}

		[UnityTest]
		public IEnumerator WorkerAdjacentToBuildSite_BuildsQuickly()
		{
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			worker.StartBuilding(new BuildEventArgs(worker, new Vector3Int(10, 10, 0), UnitType.BASE));

			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction);

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
	}
}
