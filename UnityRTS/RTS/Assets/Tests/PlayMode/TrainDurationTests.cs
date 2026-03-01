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
		/// Training a SOLDIER completes within a reasonable upper time bound
		/// (2x the nominal CREATION_TIME).
		/// </summary>
		[UnityTest]
		public IEnumerator TrainSoldier_CompletesWithinTimeBound()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			float startTime = Time.time;
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.SOLDIER));
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction);

			yield return WaitUntil(
				() => barracks.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 60f,
				failMessage: "SOLDIER training did not complete within 60s");

			float elapsed = Time.time - startTime;
			float expectedTime = Constants.CREATION_TIME[UnitType.SOLDIER];

			// Should complete within 2x the nominal time (allows for game speed effects)
			Assert.LessOrEqual(elapsed, expectedTime * 2f + 1f,
				$"SOLDIER training took {elapsed:F2}s, expected <= {expectedTime * 2f + 1f:F2}s");
		}

		/// <summary>
		/// Training a WORKER completes within a reasonable time bound.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainWorker_CompletesWithinTimeBound()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;

			float startTime = Time.time;
			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));
			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction);

			yield return WaitUntil(
				() => baseUnit.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 60f,
				failMessage: "WORKER training did not complete within 60s");

			float elapsed = Time.time - startTime;
			float expectedTime = Constants.CREATION_TIME[UnitType.WORKER];

			Assert.LessOrEqual(elapsed, expectedTime * 2f + 1f,
				$"WORKER training took {elapsed:F2}s, expected <= {expectedTime * 2f + 1f:F2}s");
		}

		/// <summary>
		/// Training an ARCHER completes within a reasonable time bound.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainArcher_CompletesWithinTimeBound()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			float startTime = Time.time;
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.ARCHER));
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction);

			yield return WaitUntil(
				() => barracks.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 60f,
				failMessage: "ARCHER training did not complete within 60s");

			float elapsed = Time.time - startTime;
			float expectedTime = Constants.CREATION_TIME[UnitType.ARCHER];

			Assert.LessOrEqual(elapsed, expectedTime * 2f + 1f,
				$"ARCHER training took {elapsed:F2}s, expected <= {expectedTime * 2f + 1f:F2}s");
		}

		/// <summary>
		/// SOLDIER training takes longer than zero seconds (it is not instantaneous).
		/// </summary>
		[UnityTest]
		public IEnumerator TrainSoldier_TakesNonZeroTime()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			float startTime = Time.time;
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.SOLDIER));

			// After a single frame, should still be training (not already done)
			yield return null;
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction,
				"SOLDIER training should not complete in a single frame");
		}

		/// <summary>
		/// Constants.CREATION_TIME has a positive value for SOLDIER.
		/// </summary>
		[UnityTest]
		public IEnumerator CreationTime_Soldier_IsPositive()
		{
			Assert.Greater(Constants.CREATION_TIME[UnitType.SOLDIER], 0f,
				"CREATION_TIME[SOLDIER] should be positive");
			yield return null;
		}

		/// <summary>
		/// ARCHER trains in less time than SOLDIER if ARCHER creation time < SOLDIER creation time,
		/// or at least both complete without error.
		/// </summary>
		[UnityTest]
		public IEnumerator BothCombatUnits_TrainingTimesArePositive()
		{
			Assert.Greater(Constants.CREATION_TIME[UnitType.SOLDIER], 0f);
			Assert.Greater(Constants.CREATION_TIME[UnitType.ARCHER], 0f);
			yield return null;
		}
	}
}
