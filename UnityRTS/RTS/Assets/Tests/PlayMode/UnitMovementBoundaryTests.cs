using System.Collections;
using System.Collections.Generic;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for unit movement boundary conditions, error handling,
	/// and stress scenarios: map edges, blocked/surrounded starts,
	/// same-position commands, mid-path obstructions, and multiple workers.
	/// </summary>
	[TestFixture]
	public class UnitMovementBoundaryTests : PlayModeTestBase
	{
		// ── Boundary ───────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Worker_AtMapEdge_MovesToInteriorTarget()
		{
			var start  = new Vector3Int(0, 0, 0);
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

		[UnityTest]
		public IEnumerator Worker_MovesToNearMapCorner_ArrivesSuccessfully()
		{
			var start  = new Vector3Int(15, 15, 0);
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

		[UnityTest]
		public IEnumerator Worker_OnUnwalkableStart_PathfindsOut()
		{
			// BASE at (10,12) makes its 3×3 footprint unwalkable
			PlaceUnit(UnitType.BASE, new Vector3Int(10, 12, 0));

			var workerStart = new Vector3Int(13, 12, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerStart);

			var target = new Vector3Int(20, 20, 0);
			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, target));

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Worker near building did not reach target or go IDLE");

			Assert.AreEqual(target, worker.GridPosition,
				"Worker should pathfind around the building to the target");
		}

		[UnityTest]
		public IEnumerator Worker_MoveToSamePosition_StaysIdle()
		{
			var pos = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, pos);

			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, pos));

			yield return WaitFrames(5);

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker should be IDLE when commanded to move to its current position");
			Assert.AreEqual(pos, worker.GridPosition,
				"Worker should remain at its current position");
		}

		// ── Error ──────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Worker_PathBlockedMidTraversal_RepathsOrGoesIdle()
		{
			var start  = new Vector3Int(5, 5, 0);
			var target = new Vector3Int(5, 20, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, start);

			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, target));

			yield return WaitFixedFrames(3);

			// Place a BASE blocking the direct path
			PlaceUnit(UnitType.BASE, new Vector3Int(4, 9, 0));

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "Worker did not resolve blocked path (re-path or go IDLE)");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker should be IDLE after path blockage is resolved");
		}

		[UnityTest]
		public IEnumerator Worker_SurroundedByUnwalkable_GoesIdle()
		{
			var center = new Vector3Int(15, 15, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, center);

			// Surround the worker with 4 BASEs (3×3 each)
			PlaceUnit(UnitType.BASE, new Vector3Int(14, 19, 0));
			PlaceUnit(UnitType.BASE, new Vector3Int(14, 14, 0));
			PlaceUnit(UnitType.BASE, new Vector3Int(11, 17, 0));
			PlaceUnit(UnitType.BASE, new Vector3Int(17, 17, 0));

			var target = new Vector3Int(25, 25, 0);
			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, target));

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Surrounded worker did not go IDLE after path failures");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker surrounded by unwalkable buildings should be IDLE");
		}

		// ── Stress ────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator MultipleWorkers_MovingSimultaneously_AllResolve()
		{
			int workerCount = 10;
			var workers = new List<Unit>();
			var targets = new List<Vector3Int>();

			for (int i = 0; i < workerCount; i++)
			{
				var start  = new Vector3Int(1, 2 + i * 2, 0);
				var target = new Vector3Int(25, 2 + i * 2, 0);
				workers.Add(PlaceUnit(UnitType.WORKER, start));
				targets.Add(target);
			}

			for (int i = 0; i < workerCount; i++)
				workers[i].StartMoving(new MoveEventArgs(workers[i], UnitType.WORKER, targets[i]));

			yield return WaitUntil(
				() =>
				{
					foreach (var w in workers)
						if (w != null && w.CurrentAction != UnitAction.IDLE)
							return false;
					return true;
				},
				timeoutSeconds: 60f,
				failMessage: "Not all workers resolved to IDLE within timeout");

			for (int i = 0; i < workerCount; i++)
			{
				Assert.IsNotNull(workers[i], $"Worker {i} should still exist after movement");
				Assert.AreEqual(UnitAction.IDLE, workers[i].CurrentAction,
					$"Worker {i} should be IDLE after movement completes");
			}
		}
	}
}
