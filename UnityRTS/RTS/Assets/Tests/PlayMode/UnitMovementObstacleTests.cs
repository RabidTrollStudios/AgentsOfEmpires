using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for unit movement with obstacles in the path.
	/// Verifies that units can pathfind around buildings to reach their destination.
	/// </summary>
	[TestFixture]
	public class UnitMovementObstacleTests : PlayModeTestBase
	{
		#region Movement Around Obstacle

		/// <summary>
		/// A WORKER starts a move command and enters MOVE state immediately.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_MoveCommand_EntersMoveState()
		{
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			worker.StartMoving(new MoveEventArgs(worker, worker.UnitType, new Vector3Int(15, 5, 0)));

			Assert.AreEqual(UnitAction.MOVE, worker.CurrentAction,
				"Worker should enter MOVE state when given a move command");

			yield return null;
		}

		/// <summary>
		/// A SOLDIER issued a move command eventually returns to IDLE,
		/// indicating it completed the move.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_Move_CompletesAndGoesIdle()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));
			soldier.StartMoving(new MoveEventArgs(soldier, soldier.UnitType, new Vector3Int(7, 5, 0)));

			Assert.AreEqual(UnitAction.MOVE, soldier.CurrentAction,
				"Soldier should enter MOVE state");

			yield return WaitUntil(
				() => soldier.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Soldier did not complete its move and return to IDLE");

			Assert.AreEqual(UnitAction.IDLE, soldier.CurrentAction,
				"Soldier should be IDLE after completing its move");
		}

		/// <summary>
		/// A WORKER with a BASE obstacle between it and its destination still
		/// completes the move (pathfinding routes around the obstacle).
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_WithObstacle_CompletesMove()
		{
			// Place an obstacle building in the direct horizontal path
			Unit obstacle = MovementTestHelper.PlaceObstacle(ctx, new Vector3Int(10, 5, 0));

			// Worker on left side of obstacle, destination on right side
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(7, 5, 0));
			worker.StartMoving(new MoveEventArgs(worker, worker.UnitType, new Vector3Int(14, 5, 0)));

			Assert.AreEqual(UnitAction.MOVE, worker.CurrentAction,
				"Worker should accept the move command");

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "Worker with obstacle in path did not complete its move");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker should be IDLE after navigating around the obstacle");
		}

		/// <summary>
		/// An ARCHER can move to an open destination without obstacles.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_OpenPath_CompletesMove()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(3, 3, 0));
			archer.StartMoving(new MoveEventArgs(archer, archer.UnitType, new Vector3Int(6, 3, 0)));

			Assert.AreEqual(UnitAction.MOVE, archer.CurrentAction);

			yield return WaitUntil(
				() => archer.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Archer did not complete its open-path move");
		}

		#endregion

		#region Multiple Sequential Moves

		/// <summary>
		/// A unit can accept a second move command after completing the first.
		/// </summary>
		[UnityTest]
		public IEnumerator Unit_CanMoveAgainAfterFirstMoveCompletes()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));
			soldier.StartMoving(new MoveEventArgs(soldier, soldier.UnitType, new Vector3Int(7, 5, 0)));

			// Wait for first move to complete
			yield return WaitUntil(
				() => soldier.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Soldier did not complete first move");

			// Issue second move
			soldier.StartMoving(new MoveEventArgs(soldier, soldier.UnitType, new Vector3Int(9, 5, 0)));
			Assert.AreEqual(UnitAction.MOVE, soldier.CurrentAction,
				"Soldier should accept a second move command after completing the first");

			yield return WaitUntil(
				() => soldier.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Soldier did not complete second move");
		}

		#endregion
	}
}
