using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for unit build error conditions:
	/// occupied location, insufficient gold, and no pathfinding route.
	/// </summary>
	[TestFixture]
	public class UnitBuildErrorTests : PlayModeTestBase
	{
		[UnityTest]
		public IEnumerator BuildOnOccupiedLocation_CommandRejectedGoldUnchanged()
		{
			Vector3Int buildPos = new Vector3Int(10, 10, 0);

			// Place a complete base at the target location
			Unit blocker = PlaceUnit(UnitType.BASE, buildPos);
			blocker.IsBuilt = true;

			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 10, 0));
			Agent agent = GetAgent0();

			int goldBefore = agent.Gold;

			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));

			Assert.AreNotEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should not enter BUILD state when area is occupied");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not change when build is rejected due to occupied area");

			yield return null;
		}

		[UnityTest]
		public IEnumerator BuildWithInsufficientGold_CommandRejected()
		{
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			Agent agent = GetAgent0();

			// Set gold below BASE cost (500)
			agent.Gold = 10;
			int goldBefore = agent.Gold;

			worker.StartBuilding(new BuildEventArgs(worker, new Vector3Int(10, 10, 0), UnitType.BASE));

			Assert.AreNotEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should not enter BUILD state with insufficient gold");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not change when build is rejected due to insufficient funds");

			yield return null;
		}

		/// <summary>
		/// A worker completely walled off cannot path to the build site; no building
		/// is placed and gold is not deducted.
		/// </summary>
		[UnityTest]
		public IEnumerator BuildWithNoPath_BuildingNotPlacedGoldUnchanged()
		{
			ctx.UnitManager.DestroyAllUnits();
			yield return null;

			// Seal the worker inside a 1-cell ring of non-walkable terrain
			Vector3Int workerPos = new Vector3Int(5, 5, 0);

			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx == 0 && dy == 0) continue;
					Vector3Int pos = workerPos + new Vector3Int(dx, dy, 0);
					ctx.MapManager.GridCells[pos.x, pos.y].SetBuildable(false);
					ctx.MapManager.GridCells[pos.x, pos.y].SetWalkable(false);
				}
			}

			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);
			Agent agent = GetAgent0();
			int goldBefore       = agent.Gold;
			int unitCountBefore  = ctx.UnitManager.GetAllUnits().Count;

			worker.StartBuilding(new BuildEventArgs(worker, new Vector3Int(20, 20, 0), UnitType.BASE));

			Assert.AreNotEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should not enter BUILD when no path exists to build site");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted when no path is found");
			Assert.AreEqual(unitCountBefore, ctx.UnitManager.GetAllUnits().Count,
				"No building should be placed when no path exists");
		}
	}
}
