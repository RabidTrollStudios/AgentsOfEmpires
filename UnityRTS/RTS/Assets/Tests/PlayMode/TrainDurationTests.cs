using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests verifying that unit training completes within the
	/// time bounds defined by Constants.CREATION_TIME.
	/// </summary>
	[TestFixture]
	public class TrainDurationTests : PlayModeTestBase
	{
		/// <summary>
		/// Training a WARRIOR completes within a reasonable upper time bound
		/// (2x the nominal CREATION_TIME).
		/// </summary>
		[UnityTest]
		public IEnumerator TrainWarrior_CompletesWithinTimeBound()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			float startTime = Time.time;
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.WARRIOR));
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction);

			yield return WaitForTick(
				() => barracks.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "WARRIOR training did not complete within 15s");

			float elapsed = Time.time - startTime;
			float expectedTime = Constants.CREATION_TIME[UnitType.WARRIOR];

			// Should complete within 2x the nominal time (allows for game speed effects)
			Assert.LessOrEqual(elapsed, expectedTime * 2f + 1f,
				$"WARRIOR training took {elapsed:F2}s, expected <= {expectedTime * 2f + 1f:F2}s");
		}

		/// <summary>
		/// Training a PAWN completes within a reasonable time bound.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainPawn_CompletesWithinTimeBound()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;

			float startTime = Time.time;
			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.PAWN));
			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction);

			yield return WaitForTick(
				() => baseUnit.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "PAWN training did not complete within 15s");

			float elapsed = Time.time - startTime;
			float expectedTime = Constants.CREATION_TIME[UnitType.PAWN];

			Assert.LessOrEqual(elapsed, expectedTime * 2f + 1f,
				$"PAWN training took {elapsed:F2}s, expected <= {expectedTime * 2f + 1f:F2}s");
		}

		/// <summary>
		/// Training an ARCHER completes within a reasonable time bound.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainArcher_CompletesWithinTimeBound()
		{
			Unit archery = PlaceUnit(UnitType.ARCHERY, new Vector3Int(10, 10, 0));
			archery.IsBuilt = true;

			float startTime = Time.time;
			archery.StartTraining(new TrainEventArgs(archery, UnitType.ARCHER));
			Assert.AreEqual(UnitAction.TRAIN, archery.CurrentAction);

			yield return WaitForTick(
				() => archery.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "ARCHER training did not complete within 15s");

			float elapsed = Time.time - startTime;
			float expectedTime = Constants.CREATION_TIME[UnitType.ARCHER];

			Assert.LessOrEqual(elapsed, expectedTime * 2f + 1f,
				$"ARCHER training took {elapsed:F2}s, expected <= {expectedTime * 2f + 1f:F2}s");
		}

		/// <summary>
		/// WARRIOR training takes longer than zero seconds (it is not instantaneous).
		/// </summary>
		[UnityTest]
		public IEnumerator TrainWarrior_TakesNonZeroTime()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			float startTime = Time.time;
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.WARRIOR));

			// After a single frame, should still be training (not already done)
			yield return null;
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction,
				"WARRIOR training should not complete in a single frame");
		}

		/// <summary>
		/// Constants.CREATION_TIME has a positive value for WARRIOR.
		/// </summary>
		[UnityTest]
		public IEnumerator CreationTime_Warrior_IsPositive()
		{
			Assert.Greater(Constants.CREATION_TIME[UnitType.WARRIOR], 0f,
				"CREATION_TIME[WARRIOR] should be positive");
			yield return null;
		}

		/// <summary>
		/// ARCHER trains in less time than WARRIOR if ARCHER creation time < WARRIOR creation time,
		/// or at least both complete without error.
		/// </summary>
		[UnityTest]
		public IEnumerator BothCombatUnits_TrainingTimesArePositive()
		{
			Assert.Greater(Constants.CREATION_TIME[UnitType.WARRIOR], 0f);
			Assert.Greater(Constants.CREATION_TIME[UnitType.ARCHER], 0f);
			yield return null;
		}
	}
}
