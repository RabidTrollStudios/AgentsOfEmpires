using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for gather continuation and cycle behavior:
	/// worker cycling back to mine after deposit, handling mine depletion
	/// mid-cycle, and refinery boost on gold collection.
	/// </summary>
	[TestFixture]
	public class GatherContinuationTests : PlayModeTestBase
	{
		private Unit PlaceBuiltBase(Vector3Int position)
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, position);
			baseUnit.IsBuilt = true;
			return baseUnit;
		}

		private Unit PlaceBuiltRefinery(Vector3Int position)
		{
			Unit refinery = PlaceUnit(UnitType.REFINERY, position);
			refinery.IsBuilt = true;
			return refinery;
		}

		#region Happy Path – Cycle Continuation

		/// <summary>
		/// After the first deposit, the worker automatically cycles back toward the mine
		/// (stays in GATHER, not IDLE).
		/// </summary>
		[UnityTest]
		public IEnumerator AfterDeposit_WorkerCyclesBackToMine()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			worker.StartGathering(new GatherEventArgs(worker, mine, baseUnit));

			// Wait for first deposit
			yield return WaitUntil(
				() => agent.Gold > initialGold,
				timeoutSeconds: 30f,
				failMessage: "First deposit did not occur");

			// Worker should still be in GATHER action
			Assert.AreEqual(UnitAction.GATHER, worker.CurrentAction,
				"Worker should continue gathering after first deposit");
		}

		/// <summary>
		/// Gold accumulates monotonically across multiple gather trips.
		/// </summary>
		[UnityTest]
		public IEnumerator MultipleCycles_GoldMonotonicallyIncreases()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(10, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));

			Agent agent = GetAgent0();
			int lastGold = agent.Gold;
			worker.StartGathering(new GatherEventArgs(worker, mine, baseUnit));

			// Wait for 3 deposits
			int depositCount = 0;
			yield return WaitUntil(() =>
			{
				if (agent.Gold > lastGold)
				{
					Assert.Greater(agent.Gold, lastGold,
						"Gold should only ever increase between deposits");
					lastGold = agent.Gold;
					depositCount++;
				}
				return depositCount >= 3;
			}, timeoutSeconds: 60f, failMessage: "Did not complete 3 gather deposits");

			Assert.GreaterOrEqual(depositCount, 3,
				"Worker should complete at least 3 gather cycles");
		}

		/// <summary>
		/// A second worker starting to gather after the first has already started
		/// both eventually deposit gold.
		/// </summary>
		[UnityTest]
		public IEnumerator TwoWorkers_SequentialStart_BothDeposit()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 5, 0));

			Unit worker1 = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));
			Unit worker2 = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 6, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			// Start first worker
			worker1.StartGathering(new GatherEventArgs(worker1, mine, baseUnit));

			// Wait a few frames before starting second
			yield return WaitFrames(10);
			worker2.StartGathering(new GatherEventArgs(worker2, mine, baseUnit));

			// Wait for gold to increase by at least 2 mining capacities
			int targetGold = initialGold + (int)(Constants.MINING_CAPACITY[UnitType.WORKER] * 2);
			yield return WaitUntil(
				() => agent.Gold >= targetGold,
				timeoutSeconds: 60f,
				failMessage: "Two workers did not together deposit enough gold");

			Assert.GreaterOrEqual(agent.Gold, targetGold,
				"Two workers gathering sequentially should deposit combined gold");
		}

		#endregion

		#region Error – Mid-cycle Interruption

		/// <summary>
		/// If the mine is depleted (health = 0) during the second gather cycle,
		/// the worker eventually goes IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator Mine_DepletedDuringSecondCycle_WorkerGoesIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(10, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;
			worker.StartGathering(new GatherEventArgs(worker, mine, baseUnit));

			// Wait for first deposit (one complete cycle)
			yield return WaitUntil(
				() => agent.Gold > initialGold,
				timeoutSeconds: 30f,
				failMessage: "First gather cycle did not complete");

			// Deplete the mine on the second cycle
			mine.Health = 0;

			// Worker should eventually go IDLE when it can't mine
			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Worker did not go IDLE after mine was depleted");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker should be IDLE after mine is depleted mid-cycle");
		}

		/// <summary>
		/// If the base is destroyed mid-cycle while the worker is heading back,
		/// the worker eventually goes IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator Base_DestroyedMidSecondCycle_WorkerGoesIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(10, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 5, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;
			worker.StartGathering(new GatherEventArgs(worker, mine, baseUnit));

			// Wait for first deposit
			yield return WaitUntil(
				() => agent.Gold > initialGold,
				timeoutSeconds: 20f,
				failMessage: "First gather deposit did not occur");

			// Destroy the base
			baseUnit.Health = 0;

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Worker did not go IDLE after base was destroyed");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker should be IDLE when target base is destroyed");
		}

		#endregion
	}
}
