using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests verifying that a building cannot start a second training
	/// session while it is already in TRAIN state.
	/// </summary>
	[TestFixture]
	public class TrainingBusyRejectTests : PlayModeTestBase
	{
		#region Second Train Rejected While Busy

		/// <summary>
		/// Issuing a second train command to BARRACKS while it is already training
		/// does NOT change the current action (still TRAIN).
		/// </summary>
		[UnityTest]
		public IEnumerator Barracks_AlreadyTraining_SecondCommandRejected()
		{
			Agent agent = GetAgent0();
			agent.Gold = (int)(Constants.COST[UnitType.SOLDIER] * 4);

			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			// Start first training
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.SOLDIER));
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction,
				"BARRACKS should enter TRAIN state for first command");

			int goldAfterFirstTrain = agent.Gold;

			// Attempt a second train while still busy
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.ARCHER));

			// Should still be TRAIN (same or rejected — not a new TRAIN for ARCHER)
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction,
				"BARRACKS should remain in TRAIN state during second command attempt");

			// Gold should NOT have been deducted a second time
			Assert.AreEqual(goldAfterFirstTrain, agent.Gold,
				"Gold should not be deducted for a rejected training command");

			yield return null;
		}

		/// <summary>
		/// BASE rejects a second train command while already training.
		/// Gold is not deducted for the rejected command.
		/// </summary>
		[UnityTest]
		public IEnumerator Base_AlreadyTraining_SecondCommandRejected()
		{
			Agent agent = GetAgent0();
			agent.Gold = (int)(Constants.COST[UnitType.WORKER] * 4);

			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));
			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction);

			int goldAfterFirstTrain = agent.Gold;

			// Second train command while busy
			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));

			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction,
				"BASE should remain TRAIN, not start a second training");
			Assert.AreEqual(goldAfterFirstTrain, agent.Gold,
				"Gold should not be deducted for second train command on busy BASE");

			yield return null;
		}

		#endregion

		#region Train After Completing First Training

		/// <summary>
		/// After the first training completes (barracks goes IDLE),
		/// a second training command is accepted.
		/// </summary>
		[UnityTest]
		public IEnumerator Barracks_AfterFirstTrainComplete_AcceptsSecondTrain()
		{
			Agent agent = GetAgent0();
			agent.Gold = (int)(Constants.COST[UnitType.SOLDIER] * 4);

			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.SOLDIER));
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction);

			// Wait for first training to complete
			yield return WaitUntil(
				() => barracks.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "First BARRACKS training did not complete");

			// Now issue second train command
			int goldBeforeSecond = agent.Gold;
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.SOLDIER));

			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction,
				"BARRACKS should accept second train command after first training completes");
			Assert.Less(agent.Gold, goldBeforeSecond,
				"Gold should be deducted for the second (accepted) training command");
		}

		#endregion

		#region Unbuilt Building Rejects Train

		/// <summary>
		/// An unbuilt BARRACKS (IsBuilt=false) rejects train commands.
		/// </summary>
		[UnityTest]
		public IEnumerator UnbuiltBarracks_TrainRejected()
		{
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			// Do NOT set IsBuilt = true

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.SOLDIER));

			Assert.AreNotEqual(UnitAction.TRAIN, barracks.CurrentAction,
				"Unbuilt BARRACKS should not start training");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted when training is rejected on unbuilt BARRACKS");

			yield return null;
		}

		#endregion
	}
}
