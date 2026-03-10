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
	/// same-position commands, mid-path obstructions, and multiple pawns.
	/// </summary>
	[TestFixture]
	public class UnitMovementBoundaryTests : PlayModeTestBase
	{
		// ── Boundary ───────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Pawn_AtMapEdge_MovesToInteriorTarget()
		{
			var start  = new Vector3Int(0, 0, 0);
			var target = new Vector3Int(15, 15, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, start);

			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, target));

			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "Pawn at map edge (0,0) did not reach interior target");

			Assert.AreEqual(target, pawn.GridPosition,
				"Pawn should arrive at the interior target from map edge");
		}

		[UnityTest]
		public IEnumerator Pawn_MovesToNearMapCorner_ArrivesSuccessfully()
		{
			var start  = new Vector3Int(15, 15, 0);
			var target = new Vector3Int(27, 27, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, start);

			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, target));

			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "Pawn did not reach near-corner target (27,27)");

			Assert.AreEqual(target, pawn.GridPosition,
				"Pawn should reach the near-corner target position");
		}

		[UnityTest]
		public IEnumerator Pawn_OnUnwalkableStart_PathfindsOut()
		{
			// BASE at (10,12) makes its 3×3 footprint unwalkable
			PlaceUnit(UnitType.BASE, new Vector3Int(10, 12, 0));

			var pawnStart = new Vector3Int(13, 12, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, pawnStart);

			var target = new Vector3Int(20, 20, 0);
			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, target));

			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Pawn near building did not reach target or go IDLE");

			Assert.AreEqual(target, pawn.GridPosition,
				"Pawn should pathfind around the building to the target");
		}

		[UnityTest]
		public IEnumerator Pawn_MoveToSamePosition_StaysIdle()
		{
			var pos = new Vector3Int(10, 10, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, pos);

			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, pos));

			yield return WaitFrames(5);

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should be IDLE when commanded to move to its current position");
			Assert.AreEqual(pos, pawn.GridPosition,
				"Pawn should remain at its current position");
		}

		// ── Error ──────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Pawn_PathBlockedMidTraversal_RepathsOrGoesIdle()
		{
			var start  = new Vector3Int(5, 5, 0);
			var target = new Vector3Int(5, 20, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, start);

			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, target));

			yield return WaitFixedFrames(3);

			// Place a BASE blocking the direct path
			PlaceUnit(UnitType.BASE, new Vector3Int(4, 9, 0));

			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "Pawn did not resolve blocked path (re-path or go IDLE)");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should be IDLE after path blockage is resolved");
		}

		[UnityTest]
		public IEnumerator Pawn_SurroundedByUnwalkable_GoesIdle()
		{
			var center = new Vector3Int(15, 15, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, center);

			// Surround the pawn with 4 BASEs (3×3 each)
			PlaceUnit(UnitType.BASE, new Vector3Int(14, 19, 0));
			PlaceUnit(UnitType.BASE, new Vector3Int(14, 14, 0));
			PlaceUnit(UnitType.BASE, new Vector3Int(11, 17, 0));
			PlaceUnit(UnitType.BASE, new Vector3Int(17, 17, 0));

			var target = new Vector3Int(25, 25, 0);
			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, target));

			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Surrounded pawn did not go IDLE after path failures");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn surrounded by unwalkable buildings should be IDLE");
		}

		// ── Stress ────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator MultiplePawns_MovingSimultaneously_AllResolve()
		{
			int pawnCount = 10;
			var pawns = new List<Unit>();
			var targets = new List<Vector3Int>();

			for (int i = 0; i < pawnCount; i++)
			{
				var start  = new Vector3Int(1, 2 + i * 2, 0);
				var target = new Vector3Int(25, 2 + i * 2, 0);
				pawns.Add(PlaceUnit(UnitType.PAWN, start));
				targets.Add(target);
			}

			for (int i = 0; i < pawnCount; i++)
				pawns[i].StartMoving(new MoveEventArgs(pawns[i], UnitType.PAWN, targets[i]));

			yield return WaitUntil(
				() =>
				{
					foreach (var w in pawns)
						if (w != null && w.CurrentAction != UnitAction.IDLE)
							return false;
					return true;
				},
				timeoutSeconds: 60f,
				failMessage: "Not all pawns resolved to IDLE within timeout");

			for (int i = 0; i < pawnCount; i++)
			{
				Assert.IsNotNull(pawns[i], $"Pawn {i} should still exist after movement");
				Assert.AreEqual(UnitAction.IDLE, pawns[i].CurrentAction,
					$"Pawn {i} should be IDLE after movement completes");
			}
		}
	}
}
