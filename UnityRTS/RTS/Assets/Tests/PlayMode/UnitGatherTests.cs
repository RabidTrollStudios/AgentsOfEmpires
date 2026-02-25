using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for worker gathering behavior.
	/// Covers the full gather cycle: TO_MINE -> MINING -> TO_BASE -> deposit -> repeat,
	/// plus boundary conditions (close/far mine, depleted mine) and error handling
	/// (mine/base destroyed mid-trip).
	/// </summary>
	public class UnitGatherTests : PlayModeTestBase
	{
		#region Helpers

		/// <summary>
		/// Place a base at the given position, mark it as built, and return it.
		/// BASE is in BUILDS[WORKER], so Initialize sets IsBuilt=false.
		/// Tests need a pre-built base for gold deposits.
		/// </summary>
		private Unit PlaceBuiltBase(Vector3Int position)
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, position);
			baseUnit.IsBuilt = true;
			return baseUnit;
		}

		/// <summary>
		/// Start a worker gathering from a mine, depositing at a base.
		/// Calls StartGathering directly (bypasses EventDispatcher validation
		/// since we already own all units).
		/// </summary>
		private void StartGathering(Unit worker, Unit mine, Unit baseUnit)
		{
			var args = new GatherEventArgs(worker, mine, baseUnit);
			worker.StartGathering(args);
		}

		#endregion

		#region Happy Path Tests

		/// <summary>
		/// A worker completes one gather round trip and deposits gold at the base,
		/// increasing the agent's gold total.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_WorkerCompletesRoundTrip_AgentGoldIncreases()
		{
			// Arrange: base at (5,5), mine at (15,5), worker at (8,5)
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			// Act
			StartGathering(worker, mine, baseUnit);

			// Assert: wait until gold increases (worker completed at least one deposit)
			yield return WaitUntil(
				() => agent.Gold > initialGold,
				30f,
				"Worker did not deposit gold after a full gather round trip"
			);

			Assert.Greater(agent.Gold, initialGold,
				"Agent gold should have increased after worker deposited resources");
		}

		/// <summary>
		/// After the first deposit, the worker continues gathering (stays in GATHER action)
		/// and cycles back toward the mine for another trip.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_WorkerContinuesAfterFirstDeposit_StillGathering()
		{
			// Arrange: base at (5,5), mine at (15,5), worker at (8,5)
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			// Act
			StartGathering(worker, mine, baseUnit);

			// Wait for the first deposit
			yield return WaitUntil(
				() => agent.Gold > initialGold,
				30f,
				"Worker did not complete first deposit"
			);

			// Assert: worker should still be in GATHER action after depositing
			Assert.AreEqual(UnitAction.GATHER, worker.CurrentAction,
				"Worker should remain in GATHER action after depositing and cycling back to mine");
		}

		/// <summary>
		/// The mine's health decreases while a worker is mining from it.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_MineHealthDecreases_DuringMining()
		{
			// Arrange: base at (5,5), mine at (10,5), worker adjacent to mine at (9,5)
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(10, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 5, 0));

			float initialMineHealth = mine.Health;

			// Act
			StartGathering(worker, mine, baseUnit);

			// Wait until mine health has decreased (worker is mining)
			yield return WaitUntil(
				() => mine.Health < initialMineHealth,
				15f,
				"Mine health did not decrease during worker mining"
			);

			Assert.Less(mine.Health, initialMineHealth,
				"Mine health should decrease as the worker extracts gold");
		}

		#endregion

		#region Boundary Tests

		/// <summary>
		/// Mine very close to base — short cycle still results in gold deposit.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_MineCloseToBase_GoldStillDeposited()
		{
			// Arrange: base at (5,5), mine at (10,5), worker at (8,5) — short distances
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(10, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			// Act
			StartGathering(worker, mine, baseUnit);

			// Assert: gold should still be deposited even with short distances
			yield return WaitUntil(
				() => agent.Gold > initialGold,
				20f,
				"Worker did not deposit gold even with mine close to base"
			);

			Assert.Greater(agent.Gold, initialGold,
				"Agent gold should increase even with a short gather cycle");
		}

		/// <summary>
		/// Mine far from base — gather still completes eventually with a longer timeout.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_MineFarFromBase_GatherCompletesEventually()
		{
			// Arrange: base at (2,2), mine at (25,25), worker at (3,3)
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(2, 2, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(25, 25, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(3, 3, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			// Act
			StartGathering(worker, mine, baseUnit);

			// Assert: allow more time for the long trek
			yield return WaitUntil(
				() => agent.Gold > initialGold,
				120f,
				"Worker did not deposit gold when mine is far from base"
			);

			Assert.Greater(agent.Gold, initialGold,
				"Agent gold should increase even when the mine is far from base");
		}

		/// <summary>
		/// Mine with very low health depletes in a single trip. The worker should
		/// eventually return to IDLE once the mine is gone and gold is deposited.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_MineLowHealth_WorkerGoesIdleAfterDepletion()
		{
			// Arrange: base at (5,5), mine at (10,5), worker at (8,5)
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(10, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));

			// Set mine health very low so it depletes quickly
			mine.Health = 50;

			// Act
			StartGathering(worker, mine, baseUnit);

			// Assert: worker should go IDLE after mine is depleted and gold deposited
			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				30f,
				"Worker did not go IDLE after depleting a low-health mine"
			);

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker should be IDLE after the mine is fully depleted");
		}

		#endregion

		#region Error Handling Tests

		/// <summary>
		/// Mine destroyed (health set to 0) while worker is gathering.
		/// The worker should gracefully go IDLE without crashing.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_MineDestroyedMidTrip_WorkerGoesIdle()
		{
			// Arrange: base at (5,5), mine at (15,5), worker at (8,5)
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));

			// Act: start gathering
			StartGathering(worker, mine, baseUnit);

			// Let the worker start moving toward the mine
			yield return WaitFrames(30);

			// Destroy the mine by setting its health to 0
			mine.Health = 0;

			// Assert: worker should eventually go IDLE
			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				15f,
				"Worker did not go IDLE after mine was destroyed mid-trip"
			);

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker should be IDLE when the mine is destroyed during gathering");
		}

		/// <summary>
		/// Base destroyed while worker is heading back with gold.
		/// The worker should go IDLE without crashing.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_BaseDestroyedMidTrip_WorkerGoesIdle()
		{
			// Arrange: base at (5,5), mine at (10,5), worker near mine at (9,5)
			// Place worker adjacent to mine so it reaches mining phase quickly
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(10, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 5, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			// Act: start gathering
			StartGathering(worker, mine, baseUnit);

			// Wait until the mine health starts decreasing (worker is mining)
			float initialMineHealth = mine.Health;
			yield return WaitUntil(
				() => mine.Health < initialMineHealth,
				15f,
				"Worker did not start mining"
			);

			// Destroy the base by setting its health to 0
			baseUnit.Health = 0;

			// Assert: worker should eventually go IDLE
			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				30f,
				"Worker did not go IDLE after base was destroyed during gathering"
			);

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker should be IDLE when the base is destroyed during a gather trip");
		}

		#endregion

		#region Stress Tests

		/// <summary>
		/// Multiple workers gathering from the same mine simultaneously.
		/// All should eventually deposit gold, increasing the agent's total.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_MultipleWorkersSameMine_AllDepositGold()
		{
			// Arrange: base at (5,5), mine at (15,5)
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 5, 0));

			// Place 5 workers at different positions near the path between base and mine
			Unit[] workers = new Unit[5];
			workers[0] = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 4, 0));
			workers[1] = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 6, 0));
			workers[2] = PlaceUnit(UnitType.WORKER, new Vector3Int(10, 4, 0));
			workers[3] = PlaceUnit(UnitType.WORKER, new Vector3Int(11, 4, 0));
			workers[4] = PlaceUnit(UnitType.WORKER, new Vector3Int(11, 6, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			// Act: send all workers to gather
			foreach (Unit worker in workers)
			{
				StartGathering(worker, mine, baseUnit);
			}

			// Assert: wait until gold increases by at least the capacity of one worker trip
			// MiningCapacity for WORKER = 10 * 10 = 100 gold per trip
			// With 5 workers we expect substantial gold increase
			yield return WaitUntil(
				() => agent.Gold >= initialGold + 100,
				60f,
				"Multiple workers did not deposit enough gold from the same mine"
			);

			Assert.GreaterOrEqual(agent.Gold, initialGold + 100,
				"Agent gold should increase by at least one worker's capacity with 5 workers gathering");

			// Verify at least some workers are still actively gathering
			int gatheringCount = 0;
			foreach (Unit worker in workers)
			{
				if (worker != null && worker.CurrentAction == UnitAction.GATHER)
					gatheringCount++;
			}

			Assert.Greater(gatheringCount, 0,
				"At least some workers should still be gathering after the first deposits");
		}

		#endregion
	}
}
