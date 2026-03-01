using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests verifying that building placement and destruction
	/// correctly toggle cell buildability state.
	/// </summary>
	[TestFixture]
	public class BuildingCellStateTests : PlayModeTestBase
	{
		#region Cells Occupied During Construction

		/// <summary>
		/// When a worker starts building a BASE, the target area is no longer
		/// buildable for another structure (the cell is reserved).
		/// </summary>
		[UnityTest]
		public IEnumerator BuildingUnderConstruction_CellNotBuildable()
		{
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));

			// Before build: area should be buildable
			BuildingTestHelper.AssertAreaBuildable(ctx, UnitType.BASE, buildPos,
				"Area should be buildable before any building is placed");

			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction);

			// After issuing build command: area should NOT be buildable for another BASE
			yield return WaitFrames(2);

			BuildingTestHelper.AssertAreaNotBuildable(ctx, UnitType.BASE, buildPos,
				"Area under active construction should not be buildable for a second BASE");
		}

		/// <summary>
		/// A worker that has started building occupies the target area, preventing
		/// a second worker from issuing the same build command.
		/// </summary>
		[UnityTest]
		public IEnumerator TwoWorkersAtSamePos_SecondBuildRejected()
		{
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Agent agent = GetAgent0();
			agent.Gold = (int)(Constants.COST[UnitType.BASE] * 3);

			Unit w1 = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			Unit w2 = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 11, 0));

			w1.StartBuilding(new BuildEventArgs(w1, buildPos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, w1.CurrentAction, "Worker 1 should be building");

			yield return WaitFrames(2);

			int goldBeforeSecond = agent.Gold;
			w2.StartBuilding(new BuildEventArgs(w2, buildPos, UnitType.BASE));

			// Second build at same position should be rejected
			Assert.AreNotEqual(UnitAction.BUILD, w2.CurrentAction,
				"Second worker should NOT be able to build at an already-occupied position");
			Assert.AreEqual(goldBeforeSecond, agent.Gold,
				"Gold should not be deducted for the rejected second build");
		}

		#endregion

		#region Cells Released After Destruction

		/// <summary>
		/// After a building is destroyed (health = 0), the area becomes buildable again.
		/// </summary>
		[UnityTest]
		public IEnumerator BuildingDestroyed_AreaBecomesRebuildable()
		{
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit barracks = PlaceUnit(UnitType.BARRACKS, buildPos);
			barracks.IsBuilt = true;

			// Area occupied by built barracks should NOT be buildable
			BuildingTestHelper.AssertAreaNotBuildable(ctx, UnitType.BARRACKS, buildPos,
				"Occupied area should not be buildable while building exists");

			// Destroy the building
			barracks.Health = 0;

			yield return WaitUntil(
				() => ctx.UnitManager.GetUnit(barracks.UnitNbr) == null,
				timeoutSeconds: 10f,
				failMessage: "Building was not removed from UnitManager after health=0");

			// Area should now be buildable again
			BuildingTestHelper.AssertAreaBuildable(ctx, UnitType.BARRACKS, buildPos,
				"Area should be buildable again after building is destroyed");
		}

		#endregion

		#region Built vs Unbuilt State

		/// <summary>
		/// A built BASE marks its footprint as non-buildable.
		/// </summary>
		[UnityTest]
		public IEnumerator BuiltBase_FootprintNotBuildable()
		{
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit baseUnit = PlaceUnit(UnitType.BASE, buildPos);
			baseUnit.IsBuilt = true;

			yield return WaitFrames(1);

			BuildingTestHelper.AssertAreaNotBuildable(ctx, UnitType.BASE, buildPos,
				"Built BASE should mark its footprint as non-buildable");
		}

		#endregion
	}
}
