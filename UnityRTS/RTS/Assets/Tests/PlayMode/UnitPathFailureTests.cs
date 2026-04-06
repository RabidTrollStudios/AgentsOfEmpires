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

			// Place mine in the middle of the map
			// MINE is 3x3 at (15,15), occupying (15..17, 15..13)
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));

			// Block all neighbor cells around the mine with terrain walls.
			// The mine occupies x=[15,16,17], y=[15,14,13].
			// Neighbors: ring around that 3x3 area.
			int[] xs = { 14, 15, 16, 17, 18 };
			int[] blockYs = { 16, 12 }; // top row and bottom row
			foreach (int x in xs)
			{
				foreach (int y in blockYs)
				{
					if (x >= 0 && x < 30 && y >= 0 && y < 30)
					{
						ctx.MapManager.GridCells[x, y].SetWalkable(false);
						ctx.MapManager.GridCells[x, y].SetBuildable(false);
						ctx.MapManager.Grid.SetCellBlocked(x, y);
					}
				}
			}
			// Left and right columns
			for (int y = 13; y <= 15; y++)
			{
				ctx.MapManager.GridCells[14, y].SetWalkable(false);
				ctx.MapManager.GridCells[14, y].SetBuildable(false);
				ctx.MapManager.Grid.SetCellBlocked(14, y);
				ctx.MapManager.GridCells[18, y].SetWalkable(false);
				ctx.MapManager.GridCells[18, y].SetBuildable(false);
				ctx.MapManager.Grid.SetCellBlocked(18, y);
			}

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(7, 5, 0));
			yield return null;

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));
			Assert.AreEqual(UnitAction.GATHER, pawn.CurrentAction);

			// Step many times — the pawn should eventually give up and go IDLE
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Step(pawn);
				return pawn.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 15f, failMessage: "Pawn should go IDLE after repeated path failures");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction);
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

			// Step until the pawn either reaches nearby and stops, or goes IDLE
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Step(pawn);
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
