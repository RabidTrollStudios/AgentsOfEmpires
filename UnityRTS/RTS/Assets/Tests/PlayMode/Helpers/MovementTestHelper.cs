using System;
using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Helper utilities for movement-focused Play Mode tests.
	/// Provides shorthand for issuing move commands, placing obstacle buildings,
	/// and waiting for unit arrival.
	/// </summary>
	public static class MovementTestHelper
	{
		/// <summary>
		/// Place a built BASE at the given position to act as a movement obstacle.
		/// The building marks its footprint cells as non-walkable.
		/// Returns the obstacle building.
		/// </summary>
		public static Unit PlaceObstacle(PlayModeTestContext ctx, Vector3Int position)
		{
			Unit obstacle = PlayModeTestHelper.PlaceUnit(ctx, ctx.Agent0Go, UnitType.BASE, position);
			obstacle.IsBuilt = true;
			return obstacle;
		}

		/// <summary>
		/// Issue a move command to the unit and wait until it enters IDLE state
		/// (indicating arrival at destination or command completion).
		/// </summary>
		public static IEnumerator MoveAndWaitIdle(Unit unit, Vector3Int destination,
			float timeoutSeconds = 10f)
		{
			unit.StartMoving(new MoveEventArgs(unit, unit.UnitType, destination));

			float elapsed = 0f;
			while (unit.CurrentAction != UnitAction.IDLE)
			{
				elapsed += Time.deltaTime;
				if (elapsed > timeoutSeconds)
				{
					Assert.Fail(
						$"Unit {unit.UnitType} did not go IDLE after move to {destination} " +
						$"within {timeoutSeconds}s (current action: {unit.CurrentAction})");
					yield break;
				}
				yield return null;
			}
		}

		/// <summary>
		/// Issue a move command and wait until the unit enters MOVE state (command accepted).
		/// Times out if the unit does not accept the move within a short window.
		/// </summary>
		public static IEnumerator WaitForMoveStart(Unit unit, Vector3Int destination,
			float timeoutSeconds = 5f)
		{
			unit.StartMoving(new MoveEventArgs(unit, unit.UnitType, destination));

			float elapsed = 0f;
			while (unit.CurrentAction != UnitAction.MOVE)
			{
				elapsed += Time.deltaTime;
				if (elapsed > timeoutSeconds)
				{
					Assert.Fail(
						$"Unit {unit.UnitType} did not enter MOVE state within {timeoutSeconds}s");
					yield break;
				}
				yield return null;
			}
		}

		/// <summary>
		/// Assert that a unit is currently in MOVE state.
		/// </summary>
		public static void AssertMoving(Unit unit)
		{
			Assert.AreEqual(UnitAction.MOVE, unit.CurrentAction,
				$"{unit.UnitType} should be in MOVE state");
		}

		/// <summary>
		/// Assert that a move command was rejected: unit remains IDLE.
		/// </summary>
		public static void AssertMoveRejected(Unit unit)
		{
			Assert.AreEqual(UnitAction.IDLE, unit.CurrentAction,
				$"Move command to {unit.UnitType} should have been rejected");
		}
	}
}
