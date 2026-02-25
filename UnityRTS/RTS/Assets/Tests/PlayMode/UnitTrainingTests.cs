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
	public class UnitTrainingTests : PlayModeTestBase
	{
		/// <summary>
		/// Place a base at the given position and mark it as built.
		/// BASE is in BUILDS[WORKER], so Initialize sets IsBuilt=false.
		/// Tests need a pre-built base for training commands to be accepted.
		/// </summary>
		private Unit PlaceBuiltBase(Vector3Int position)
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, position);
			baseUnit.IsBuilt = true;
			return baseUnit;
		}

		/// <summary>
		/// Manually tick a unit's Update so the task state machine advances.
		/// GameManager.enabled is false in tests, so Update does not run automatically.
		/// </summary>
		private void TickUnit(Unit unit)
		{
			unit.Update();
		}

		// ------------------------------------------------------------------
		// Happy-path tests
		// ------------------------------------------------------------------

		/// <summary>
		/// A base trains a worker. After CREATION_TIME elapses the new worker
		/// should appear in UnitManager.
		/// At GAME_SPEED=20: CREATION_TIME[WORKER] = (1/20)*2 = 0.1 s
		/// </summary>
		[UnityTest]
		public IEnumerator BaseTrainsWorker_NewWorkerAppearsAfterCreationTime()
		{
			// Place a built base at (10, 10)
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));

			int unitsBefore = ctx.UnitManager.GetAllUnits().Count;

			// Issue train command
			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));

			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction,
				"Base should be in TRAIN action after StartTraining");

			// Wait enough real time for the training timer to complete.
			// CREATION_TIME for WORKER at speed 20 is 0.1 s. Give generous margin.
			yield return WaitUntil(() =>
			{
				TickUnit(baseUnit);
				return ctx.UnitManager.GetAllUnits().Count > unitsBefore;
			}, timeoutSeconds: 5f, failMessage: "New worker never appeared after training");

			int unitsAfter = ctx.UnitManager.GetAllUnits().Count;
			Assert.AreEqual(unitsBefore + 1, unitsAfter,
				"Exactly one new unit should have been created");
		}

		/// <summary>
		/// While training the base is in TRAIN state; after the worker spawns it
		/// returns to IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator BaseTrainsWorker_ActionIsTrainThenIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));
			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction);

			// Tick until training completes
			yield return WaitUntil(() =>
			{
				TickUnit(baseUnit);
				return baseUnit.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 5f, failMessage: "Base never returned to IDLE after training");

			Assert.AreEqual(UnitAction.IDLE, baseUnit.CurrentAction);
		}

		/// <summary>
		/// The newly trained worker has UnitType.WORKER and occupies a cell that
		/// is no longer buildable (because the worker stands on it).
		/// </summary>
		[UnityTest]
		public IEnumerator TrainedWorker_HasCorrectTypeAndOccupiesCell()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));
			int unitsBefore = ctx.UnitManager.GetAllUnits().Count;

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));

			// Wait for the new unit to appear
			yield return WaitUntil(() =>
			{
				TickUnit(baseUnit);
				return ctx.UnitManager.GetAllUnits().Count > unitsBefore;
			}, timeoutSeconds: 5f, failMessage: "Trained worker never appeared");

			// Find the newly created unit (highest UnitNbr)
			Unit newWorker = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.Where(u => u.UnitType == UnitType.WORKER)
				.OrderByDescending(u => u.UnitNbr)
				.First();

			Assert.AreEqual(UnitType.WORKER, newWorker.UnitType,
				"Trained unit should be a WORKER");

			// The cell the worker spawned on should no longer be buildable
			Assert.IsFalse(ctx.MapManager.IsGridPositionBuildable(newWorker.GridPosition),
				"Cell occupied by the new worker should not be buildable");
		}

		// ------------------------------------------------------------------
		// Boundary tests
		// ------------------------------------------------------------------

		/// <summary>
		/// At GAME_SPEED=30 (max) training completes almost instantly.
		/// CREATION_TIME[WORKER] = (1/30)*2 ~ 0.067 s
		/// </summary>
		[UnityTest]
		public IEnumerator TrainAtMaxSpeed_CompletesNearlyInstantly()
		{
			// Reconfigure game speed to maximum
			int originalSpeed = Constants.GAME_SPEED;
			Constants.GAME_SPEED = 30;
			Constants.CalculateGameConstants();

			try
			{
				Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));
				int unitsBefore = ctx.UnitManager.GetAllUnits().Count;

				baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));

				// At speed 30, creation time is ~0.067 s. Should complete very quickly.
				yield return WaitUntil(() =>
				{
					TickUnit(baseUnit);
					return ctx.UnitManager.GetAllUnits().Count > unitsBefore;
				}, timeoutSeconds: 3f, failMessage: "Training at max speed did not complete quickly");

				Assert.AreEqual(unitsBefore + 1, ctx.UnitManager.GetAllUnits().Count);
			}
			finally
			{
				// Restore original game speed
				Constants.GAME_SPEED = originalSpeed;
				Constants.CalculateGameConstants();
			}
		}

		/// <summary>
		/// The newly trained worker spawns on a cell outside the base footprint.
		/// BASE is 3x3 anchored at its grid position, so the worker must not
		/// share any of those 9 cells.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainedWorker_SpawnsOutsideBuildingFootprint()
		{
			Vector3Int basePos = new Vector3Int(10, 10, 0);
			Unit baseUnit = PlaceBuiltBase(basePos);
			int unitsBefore = ctx.UnitManager.GetAllUnits().Count;

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));

			yield return WaitUntil(() =>
			{
				TickUnit(baseUnit);
				return ctx.UnitManager.GetAllUnits().Count > unitsBefore;
			}, timeoutSeconds: 5f, failMessage: "Trained worker never appeared");

			Unit newWorker = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.Where(u => u.UnitType == UnitType.WORKER)
				.OrderByDescending(u => u.UnitNbr)
				.First();

			// Collect all 3x3 cells of the base (anchor + offsets with y going down)
			Vector3Int baseSize = Constants.UNIT_SIZE[UnitType.BASE];
			HashSet<Vector3Int> baseCells = new HashSet<Vector3Int>();
			for (int i = 0; i < baseSize.x; i++)
				for (int j = 0; j < baseSize.y; j++)
					baseCells.Add(basePos + new Vector3Int(i, -j, 0));

			Assert.IsFalse(baseCells.Contains(newWorker.GridPosition),
				$"Worker spawned at {newWorker.GridPosition} which is inside the base footprint");
		}

		// ------------------------------------------------------------------
		// Error tests
		// ------------------------------------------------------------------

		/// <summary>
		/// When all cells neighboring the base are occupied, the base stays in
		/// TRAIN indefinitely (the trained unit cannot be placed). It should not
		/// crash and should remain in TRAIN state.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainWithAllNeighborsOccupied_StaysInTrain()
		{
			Vector3Int basePos = new Vector3Int(15, 15, 0);
			Unit baseUnit = PlaceBuiltBase(basePos);

			// Fill every neighboring cell with workers so there is no spawn location.
			// Neighbors of a 3x3 base at (15,15) surround the footprint:
			//   footprint cells: x in [15,17], y in [13,15]  (y goes down)
			//   neighbors: ring around that footprint
			List<Vector3Int> neighborPositions =
				ctx.MapManager.GetBuildableGridPositionsNearUnit(UnitType.BASE, basePos);

			foreach (Vector3Int pos in neighborPositions)
			{
				PlaceUnit(UnitType.WORKER, pos);
			}

			// Verify no buildable positions remain
			var remaining = ctx.MapManager.GetBuildableGridPositionsNearUnit(UnitType.BASE, basePos);
			Assert.AreEqual(0, remaining.Count,
				"All neighbor cells should be occupied before training");

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));
			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction);

			int unitCountBefore = ctx.UnitManager.GetAllUnits().Count;

			// Wait several seconds and verify the base remains stuck in TRAIN
			float waited = 0f;
			while (waited < 3f)
			{
				TickUnit(baseUnit);
				waited += Time.deltaTime;
				yield return null;
			}

			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction,
				"Base should remain in TRAIN when no spawn cell is available");
			Assert.AreEqual(unitCountBefore, ctx.UnitManager.GetAllUnits().Count,
				"No new unit should have been created");
		}

		/// <summary>
		/// Issuing a second train command while already training should be
		/// rejected. Gold is deducted once (first command) but not again.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainWhileAlreadyTraining_SecondCommandRejected()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();

			int goldBefore = agent.Gold;
			int workerCost = (int)Constants.COST[UnitType.WORKER];

			// First train command — should succeed
			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));
			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction);
			Assert.AreEqual(goldBefore - workerCost, agent.Gold,
				"Gold should be deducted for the first train command");

			int goldAfterFirst = agent.Gold;

			// Second train command while still training — should be rejected
			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));
			Assert.AreEqual(goldAfterFirst, agent.Gold,
				"Gold should NOT be deducted for a rejected second train command");

			// Yield one frame to keep the test valid as a UnityTest
			yield return null;
		}

		/// <summary>
		/// Training a unit type that the building cannot train (e.g., BASE
		/// training SOLDIER) should be rejected with gold unchanged.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainInvalidUnitType_CommandRejectedGoldUnchanged()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();

			int goldBefore = agent.Gold;

			// BASE can only train WORKER, not SOLDIER
			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.SOLDIER));

			Assert.AreEqual(UnitAction.IDLE, baseUnit.CurrentAction,
				"Base should remain IDLE after invalid train command");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not change when training an invalid unit type");

			yield return null;
		}

		// ------------------------------------------------------------------
		// Stress / back-to-back test
		// ------------------------------------------------------------------

		/// <summary>
		/// Train 5 workers sequentially from the same base. After all 5 complete,
		/// there should be 5 new workers, each on a distinct cell.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainFiveWorkersSequentially_AllExistOnDistinctCells()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));
			int initialUnitCount = ctx.UnitManager.GetAllUnits().Count;

			for (int i = 0; i < 5; i++)
			{
				baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));
				Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction,
					$"Training iteration {i}: base should be in TRAIN");

				// Wait for training to complete (base returns to IDLE)
				yield return WaitUntil(() =>
				{
					TickUnit(baseUnit);
					return baseUnit.CurrentAction == UnitAction.IDLE;
				}, timeoutSeconds: 5f, failMessage: $"Training iteration {i} did not complete");
			}

			// Verify 5 new units were created (base + 5 workers)
			int finalUnitCount = ctx.UnitManager.GetAllUnits().Count;
			Assert.AreEqual(initialUnitCount + 5, finalUnitCount,
				"5 additional workers should exist after training");

			// Verify all workers are on distinct cells
			List<Unit> workers = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.Where(u => u.UnitType == UnitType.WORKER)
				.ToList();

			HashSet<Vector3Int> positions = new HashSet<Vector3Int>();
			foreach (Unit w in workers)
			{
				Assert.IsTrue(positions.Add(w.GridPosition),
					$"Worker at {w.GridPosition} shares a cell with another worker");
			}
		}
	}
}
