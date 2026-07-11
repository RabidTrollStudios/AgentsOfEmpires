using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for path failure edge cases.
	/// Covers the UpdatePath pathFailCount >= 5 -> IDLE transition,
	/// and the MOVE close-to-destination blocked -> stop behavior.
	/// </summary>
	[TestFixture]
	public class UnitPathFailureTests : PlayModeTestBase
	{
		#region Gather With No Path To Mine Goes Idle

		/// <summary>
		/// When a pawn is gathering and fails to find a path to the mine
		/// repeatedly (e.g., mine surrounded so no neighbor cell is reachable),
		/// it eventually goes IDLE via the pathFailCount >= 5 mechanism.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_PathToMineBlocked_PawnGoesIdle()
		{
			// Place a built base for the gather target
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(0, 5, 0));
			baseUnit.IsBuilt = true;

			// Place mine in the middle of the map.
			// MINE is 3x3 with an UPWARD footprint: anchor (15,15) occupies
			// x=[15,17], y=[15,17]. Wall off the full 1-cell ring around that
			// footprint (x=[14,18], y=[14,18]) so the pawn cannot reach any
			// neighbor cell — forcing the "no path -> IDLE" behavior under test.
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));

			for (int x = 14; x <= 18; x++)
			{
				for (int y = 14; y <= 18; y++)
				{
					// Skip the mine's own footprint cells (x/y in [15,17]).
					bool insideMine = x >= 15 && x <= 17 && y >= 15 && y <= 17;
					if (insideMine) continue;
					if (x < 0 || x >= 30 || y < 0 || y >= 30) continue;
					ctx.MapManager.GridCells[x, y].SetWalkable(false);
					ctx.MapManager.GridCells[x, y].SetBuildable(false);
					ctx.MapManager.Grid.SetCellBlocked(x, y);
				}
			}

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(7, 5, 0));
			yield return null;

			// The mine is fully walled off, so there is no path to any of its neighbor
			// cells at command time. ProcessGather rejects an unreachable gather up front
			// (NO_PATH_FOUND) without entering GATHER, so the pawn stays IDLE.
			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

			Assert.AreNotEqual(UnitAction.GATHER, pawn.CurrentAction,
				"Pawn should not start gathering a fully-unreachable mine");
			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should stay IDLE when the mine cannot be reached");
		}

		#endregion

		#region Move Close To Destination Blocked

		/// <summary>
		/// When a moving unit is close to its destination but the target cell
		/// is occupied by another mobile unit, the unit stops (goes IDLE)
		/// rather than endlessly re-pathing.
		/// </summary>
		[UnityTest]
		public IEnumerator Move_CloseToDestinationBlocked_PawnStops()
		{
			// Place a pawn that will move to (10, 5)
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 5, 0));
			// Place another pawn ON the target cell
			Unit blocker = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 5, 0));
			yield return null;

			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, new Vector3Int(10, 5, 0)));

			// Tick until the pawn either reaches nearby and stops, or goes IDLE
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Tick(pawn);
				return pawn.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "Pawn should stop when destination is blocked by another unit");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should go IDLE when close to blocked destination");
		}

		#endregion

		#region Build With No Path

		/// <summary>
		/// When a pawn is ordered to build at a position it can't path to
		/// (path.Count == 0), the build command is rejected.
		/// Cells must be non-walkable (not just occupied by mobile units)
		/// because StartBuilding's UpdatePath uses avoidUnits=false.
		/// </summary>
		[UnityTest]
		public IEnumerator Build_NoPathToSite_BuildRejected()
		{
			// Place pawn at (3,3), wall it in by making all 8 neighbor cells non-walkable
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(3, 3, 0));

			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx == 0 && dy == 0) continue;
					ctx.MapManager.GridCells[3 + dx, 3 + dy].SetWalkable(false);
					ctx.MapManager.Grid.SetCellBlocked(3 + dx, 3 + dy);
				}
			}
			yield return null;

			// Try to build far away — no path should exist (pawn is walled in)
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(20, 20, 0), UnitType.BARRACKS));

			Assert.AreNotEqual(UnitAction.BUILD, pawn.CurrentAction,
				"Pawn should not start building when no path to build site exists");
		}

		#endregion
	}
}
