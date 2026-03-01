using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests verifying that build commands are rejected when
	/// the target cell is already occupied by a unit.
	/// </summary>
	[TestFixture]
	public class BuildOccupiedCellTests : PlayModeTestBase
	{
		#region Cell Occupied by Mobile Unit

		/// <summary>
		/// A worker cannot build at a position occupied by another unit.
		/// The build command is rejected, worker stays IDLE, gold unchanged.
		/// </summary>
		[UnityTest]
		public IEnumerator BuildAtOccupiedCell_Rejected()
		{
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			Vector3Int occupiedPos = new Vector3Int(10, 10, 0);

			// Place a unit on the target cell
			PlaceUnit(UnitType.SOLDIER, occupiedPos);
			yield return WaitFrames(1);

			// Verify the cell is not buildable
			Assert.IsFalse(ctx.MapManager.IsAreaBuildable(UnitType.BASE, occupiedPos),
				"Cell occupied by a soldier should not be buildable");

			// Try to build at the occupied position
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			worker.StartBuilding(new BuildEventArgs(worker, occupiedPos, UnitType.BASE));

			Assert.AreNotEqual(UnitAction.BUILD, worker.CurrentAction,
				"Build at occupied cell should be rejected; worker should not enter BUILD state");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted when build command is rejected");
		}

		#endregion

		#region Cell Occupied by Building

		/// <summary>
		/// A worker cannot build a second structure at a position already occupied
		/// by an existing building.
		/// </summary>
		[UnityTest]
		public IEnumerator BuildAtExistingBuilding_Rejected()
		{
			Agent agent = GetAgent0();
			Vector3Int buildPos = new Vector3Int(10, 10, 0);

			// Place an existing building at the target position
			Unit existingBase = PlaceUnit(UnitType.BASE, buildPos);
			existingBase.IsBuilt = true;
			yield return WaitFrames(1);

			Assert.IsFalse(ctx.MapManager.IsAreaBuildable(UnitType.BARRACKS, buildPos),
				"Position occupied by BASE should not be buildable for BARRACKS");

			// Attempt to build at the same position
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			int goldBefore = agent.Gold;
			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BARRACKS));

			Assert.AreNotEqual(UnitAction.BUILD, worker.CurrentAction,
				"Build over existing structure should be rejected");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted for rejected build");
		}

		#endregion

		#region Cell Freed After Unit Moves Away

		/// <summary>
		/// After a unit moves away from a cell, that cell becomes buildable again.
		/// </summary>
		[UnityTest]
		public IEnumerator UnitMovesAway_CellBecomesBuildable()
		{
			Vector3Int targetPos = new Vector3Int(10, 10, 0);
			Unit soldier = PlaceUnit(UnitType.SOLDIER, targetPos);

			yield return WaitFrames(1);

			// Cell should be occupied
			Assert.IsFalse(ctx.MapManager.IsAreaBuildable(UnitType.WORKER, targetPos),
				"Cell should not be buildable while soldier is on it");

			// Move soldier away
			soldier.StartMoving(new MoveEventArgs(soldier, soldier.UnitType, new Vector3Int(15, 10, 0)));

			yield return WaitUntil(
				() => soldier.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Soldier did not finish moving");

			// Cell should now be free
			Assert.IsTrue(ctx.MapManager.IsAreaBuildable(UnitType.WORKER, targetPos),
				"Cell should be buildable again after soldier moves away");
		}

		#endregion

		}
}
