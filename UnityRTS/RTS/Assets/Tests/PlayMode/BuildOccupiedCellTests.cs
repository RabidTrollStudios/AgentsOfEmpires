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
		/// A pawn cannot build at a position occupied by another unit.
		/// The build command is rejected, pawn stays IDLE, gold unchanged.
		/// </summary>
		[UnityTest]
		public IEnumerator BuildAtOccupiedCell_Rejected()
		{
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			Vector3Int occupiedPos = new Vector3Int(10, 10, 0);

			// Place a unit on the target cell
			PlaceUnit(UnitType.WARRIOR, occupiedPos);
			yield return WaitFrames(1);

			// Verify the cell is not buildable
			Assert.IsFalse(ctx.MapManager.IsAreaBuildable(UnitType.BASE, occupiedPos),
				"Cell occupied by a warrior should not be buildable");

			// Try to build at the occupied position
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			pawn.StartBuilding(new BuildEventArgs(pawn, occupiedPos, UnitType.BASE));

			Assert.AreNotEqual(UnitAction.BUILD, pawn.CurrentAction,
				"Build at occupied cell should be rejected; pawn should not enter BUILD state");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted when build command is rejected");
		}

		#endregion

		#region Cell Occupied by Building

		/// <summary>
		/// A pawn cannot build a second structure at a position already occupied
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
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			int goldBefore = agent.Gold;
			pawn.StartBuilding(new BuildEventArgs(pawn, buildPos, UnitType.BARRACKS));

			Assert.AreNotEqual(UnitAction.BUILD, pawn.CurrentAction,
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
			Unit warrior = PlaceUnit(UnitType.WARRIOR, targetPos);

			yield return WaitFrames(1);

			// Cell should be occupied
			Assert.IsFalse(ctx.MapManager.IsAreaBuildable(UnitType.PAWN, targetPos),
				"Cell should not be buildable while warrior is on it");

			// Move warrior away
			warrior.StartMoving(new MoveEventArgs(warrior, warrior.UnitType, new Vector3Int(15, 10, 0)));

			yield return WaitForTick(
				() => warrior.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Warrior did not finish moving");

			// Cell should now be free
			Assert.IsTrue(ctx.MapManager.IsAreaBuildable(UnitType.PAWN, targetPos),
				"Cell should be buildable again after warrior moves away");
		}

		#endregion

		}
}
