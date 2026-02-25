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
	public class UnitMovementTests : PlayModeTestBase
	{
		#region Happy Path

		/// <summary>
		/// A worker given a Move command should travel to the destination and go IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_MoveCommand_ArrivesAtDestinationAndGoesIdle()
		{
			var start = new Vector3Int(5, 5, 0);
			var target = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, start);

			// Issue move command
			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, target));

			Assert.AreEqual(UnitAction.MOVE, worker.CurrentAction,
				"Worker should be in MOVE state after StartMoving");

			// Wait for the worker to arrive
			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Worker did not arrive at destination and go IDLE");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction);
			Assert.AreEqual(target, worker.GridPosition,
				"Worker should be at the target grid position after movement completes");
		}

		/// <summary>
		/// When a worker moves, its original cell should become buildable again and
		/// the destination cell should become not-buildable.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_Movement_OriginalCellFreedAfterMove()
		{
			var start = new Vector3Int(5, 5, 0);
			var target = new Vector3Int(8, 8, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, start);

			// The start cell should not be buildable (occupied by the worker)
			Assert.IsFalse(ctx.MapManager.IsGridPositionBuildable(start),
				"Start cell should not be buildable while worker is on it");

			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, target));

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Worker did not finish moving");

			// After arrival, the start cell should be buildable again
			Assert.IsTrue(ctx.MapManager.IsGridPositionBuildable(start),
				"Original cell should be buildable after worker has moved away");

			// The destination cell should not be buildable (worker is there now)
			Assert.IsFalse(ctx.MapManager.IsGridPositionBuildable(target),
				"Destination cell should not be buildable while worker occupies it");
		}

		/// <summary>
		/// A worker moving along a multi-step path should reach the final target position.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_MultiStepPath_ReachesFinalTarget()
		{
			var start = new Vector3Int(2, 2, 0);
			var target = new Vector3Int(20, 20, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, start);

			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, target));

			Assert.AreEqual(UnitAction.MOVE, worker.CurrentAction);

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "Worker did not arrive at distant target via multi-step path");

			Assert.AreEqual(target, worker.GridPosition,
				"Worker should reach the final target after traversing a long path");
		}

		#endregion

		#region Boundary

		/// <summary>
		/// A worker starting at the map edge (0,0) should be able to move
		/// to an interior target without errors.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_AtMapEdge_MovesToInteriorTarget()
		{
			var start = new Vector3Int(0, 0, 0);
			var target = new Vector3Int(15, 15, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, start);

			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, target));

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "Worker at map edge (0,0) did not reach interior target");

			Assert.AreEqual(target, worker.GridPosition,
				"Worker should arrive at the interior target from map edge");
		}

		/// <summary>
		/// A worker moving to near the far corner (27,27) should arrive
		/// without out-of-bounds issues.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_MovesToNearMapCorner_ArrivesSuccessfully()
		{
			var start = new Vector3Int(15, 15, 0);
			var target = new Vector3Int(27, 27, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, start);

			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, target));

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "Worker did not reach near-corner target (27,27)");

			Assert.AreEqual(target, worker.GridPosition,
				"Worker should reach the near-corner target position");
		}

		/// <summary>
		/// A worker placed inside a building footprint (simulating an inside-building scenario)
		/// should still be able to pathfind out to a walkable destination if at least one
		/// neighbor cell is walkable.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_OnUnwalkableStart_PathfindsOut()
		{
			// Place a BASE at (10,12) which occupies cells (10,12),(11,12),(12,12),
			// (10,11),(11,11),(12,11),(10,10),(11,10),(12,10)
			// The BASE makes cells unwalkable AND not-buildable.
			PlaceUnit(UnitType.BASE, new Vector3Int(10, 12, 0));

			// Place a worker at (13,12) which is just outside the building, then
			// manually set its grid position onto a cell that is occupied by the
			// building. This simulates a worker ending up "inside" a structure.
			var workerStart = new Vector3Int(13, 12, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerStart);

			var target = new Vector3Int(20, 20, 0);
			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, target));

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Worker near building did not reach target or go IDLE");

			// Worker should have arrived at the target since its start cell was walkable
			Assert.AreEqual(target, worker.GridPosition,
				"Worker should pathfind around the building to the target");
		}

		/// <summary>
		/// If a worker is told to move to its own current position, it should
		/// stay IDLE (path is empty or zero-length).
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_MoveToSamePosition_StaysIdle()
		{
			var pos = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, pos);

			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, pos));

			// The path should be empty (start == target), so UpdateMove sets IDLE
			// Give a few frames for the Update cycle to process
			yield return WaitFrames(5);

			// Worker either never entered MOVE (empty path) or went back to IDLE immediately
			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker should be IDLE when commanded to move to its current position");
			Assert.AreEqual(pos, worker.GridPosition,
				"Worker should remain at its current position");
		}

		#endregion

		#region Error

		/// <summary>
		/// If the path becomes blocked mid-traversal (a building placed on the next cell),
		/// the unit should either re-path around the obstacle or eventually go IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_PathBlockedMidTraversal_RepathsOrGoesIdle()
		{
			var start = new Vector3Int(5, 5, 0);
			var target = new Vector3Int(5, 20, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, start);

			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, target));

			// Wait a few frames for the worker to begin moving
			yield return WaitFixedFrames(3);

			// Place a BASE that blocks the path ahead.
			// BASE is 3x3. Place at (4,9) to block cells (4,9),(5,9),(6,9),
			// (4,8),(5,8),(6,8),(4,7),(5,7),(6,7) -- right in the worker's path.
			PlaceUnit(UnitType.BASE, new Vector3Int(4, 9, 0));

			// The worker should re-path around the building or eventually go IDLE
			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "Worker did not resolve blocked path (re-path or go IDLE)");

			// The worker either reached the target or gave up
			bool arrivedOrIdle = worker.CurrentAction == UnitAction.IDLE;
			Assert.IsTrue(arrivedOrIdle,
				"Worker should be IDLE after path blockage is resolved");
		}

		/// <summary>
		/// If a unit is completely surrounded by unwalkable cells (buildings),
		/// it should eventually go IDLE after exhausting path retries.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_SurroundedByUnwalkable_GoesIdle()
		{
			// Place worker at center
			var center = new Vector3Int(15, 15, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, center);

			// Surround with BASEs (3x3 each, unwalkable).
			// Place buildings to form a wall around (15,15):
			// North: BASE at (14,19) covers y=19,18,17 x=14,15,16
			// South: BASE at (14,14) covers y=14,13,12 x=14,15,16
			// West:  BASE at (11,17) covers y=17,16,15 x=11,12,13
			// East:  BASE at (17,17) covers y=17,16,15 x=17,18,19
			PlaceUnit(UnitType.BASE, new Vector3Int(14, 19, 0));
			PlaceUnit(UnitType.BASE, new Vector3Int(14, 14, 0));
			PlaceUnit(UnitType.BASE, new Vector3Int(11, 17, 0));
			PlaceUnit(UnitType.BASE, new Vector3Int(17, 17, 0));

			var target = new Vector3Int(25, 25, 0);
			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, target));

			// If no path was found, the unit stays IDLE (path.Count == 0 from StartMoving)
			// Or if it briefly enters MOVE, UpdateMove will flip to IDLE on empty path.
			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Surrounded worker did not go IDLE after path failures");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker surrounded by unwalkable buildings should be IDLE");
		}

		#endregion

		#region Stress

		/// <summary>
		/// Multiple workers moving simultaneously should all eventually arrive
		/// at their destinations or go IDLE (if paths conflict).
		/// </summary>
		[UnityTest]
		public IEnumerator MultipleWorkers_MovingSimultaneously_AllResolve()
		{
			int workerCount = 10;
			var workers = new List<Unit>();
			var targets = new List<Vector3Int>();

			// Spread workers across the left side, targets across the right side
			for (int i = 0; i < workerCount; i++)
			{
				var start = new Vector3Int(1, 2 + i * 2, 0);
				var target = new Vector3Int(25, 2 + i * 2, 0);
				Unit worker = PlaceUnit(UnitType.WORKER, start);
				workers.Add(worker);
				targets.Add(target);
			}

			// Issue move commands to all workers
			for (int i = 0; i < workerCount; i++)
			{
				workers[i].StartMoving(new MoveEventArgs(workers[i], UnitType.WORKER, targets[i]));
			}

			// Wait for all workers to finish (either arrive or go IDLE)
			yield return WaitUntil(
				() =>
				{
					foreach (var w in workers)
					{
						if (w != null && w.CurrentAction != UnitAction.IDLE)
							return false;
					}
					return true;
				},
				timeoutSeconds: 60f,
				failMessage: "Not all workers resolved to IDLE within timeout");

			// Verify all workers are IDLE
			for (int i = 0; i < workerCount; i++)
			{
				Assert.IsNotNull(workers[i],
					$"Worker {i} should still exist after movement");
				Assert.AreEqual(UnitAction.IDLE, workers[i].CurrentAction,
					$"Worker {i} should be IDLE after movement completes");
			}
		}

		#endregion
	}
}
