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
		public IEnumerator Pawn_MoveCommand_ArrivesAtDestinationAndGoesIdle()
		{
			var start  = new Vector3Int(5, 5, 0);
			var target = new Vector3Int(10, 10, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, start);

			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, target));

			Assert.AreEqual(UnitAction.MOVE, pawn.CurrentAction,
				"Pawn should be in MOVE state after StartMoving");

			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Pawn did not arrive at destination and go IDLE");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction);
			Assert.AreEqual(target, pawn.GridPosition,
				"Pawn should be at the target grid position after movement completes");
		}

		[UnityTest]
		public IEnumerator Pawn_Movement_OriginalCellFreedAfterMove()
		{
			var start  = new Vector3Int(5, 5, 0);
			var target = new Vector3Int(8, 8, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, start);

			Assert.IsFalse(ctx.MapManager.IsGridPositionBuildable(start),
				"Start cell should not be buildable while pawn is on it");

			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, target));

			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Pawn did not finish moving");

			Assert.IsTrue(ctx.MapManager.IsGridPositionBuildable(start),
				"Original cell should be buildable after pawn has moved away");
			Assert.IsFalse(ctx.MapManager.IsGridPositionBuildable(target),
				"Destination cell should not be buildable while pawn occupies it");
		}

		[UnityTest]
		public IEnumerator Pawn_MultiStepPath_ReachesFinalTarget()
		{
			var start  = new Vector3Int(2, 2, 0);
			var target = new Vector3Int(20, 20, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, start);

			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, target));

			Assert.AreEqual(UnitAction.MOVE, pawn.CurrentAction);

			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "Pawn did not arrive at distant target via multi-step path");

			Assert.AreEqual(target, pawn.GridPosition,
				"Pawn should reach the final target after traversing a long path");
		}
	}
}
