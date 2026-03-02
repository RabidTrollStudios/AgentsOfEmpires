using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for unit movement happy path:
	/// arrival at destination, original cell freed, multi-step path.
	/// </summary>
	[TestFixture]
	public class UnitMovementHappyPathTests : PlayModeTestBase
	{
		[UnityTest]
		public IEnumerator Worker_MoveCommand_ArrivesAtDestinationAndGoesIdle()
		{
			var start  = new Vector3Int(5, 5, 0);
			var target = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, start);

			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, target));

			Assert.AreEqual(UnitAction.MOVE, worker.CurrentAction,
				"Worker should be in MOVE state after StartMoving");

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Worker did not arrive at destination and go IDLE");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction);
			Assert.AreEqual(target, worker.GridPosition,
				"Worker should be at the target grid position after movement completes");
		}

		[UnityTest]
		public IEnumerator Worker_Movement_OriginalCellFreedAfterMove()
		{
			var start  = new Vector3Int(5, 5, 0);
			var target = new Vector3Int(8, 8, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, start);

			Assert.IsFalse(ctx.MapManager.IsGridPositionBuildable(start),
				"Start cell should not be buildable while worker is on it");

			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, target));

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Worker did not finish moving");

			Assert.IsTrue(ctx.MapManager.IsGridPositionBuildable(start),
				"Original cell should be buildable after worker has moved away");
			Assert.IsFalse(ctx.MapManager.IsGridPositionBuildable(target),
				"Destination cell should not be buildable while worker occupies it");
		}

		[UnityTest]
		public IEnumerator Worker_MultiStepPath_ReachesFinalTarget()
		{
			var start  = new Vector3Int(2, 2, 0);
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
	}
}
