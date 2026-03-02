using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for gather error handling and stress scenarios:
	/// mine destroyed mid-trip, base destroyed mid-trip,
	/// and multiple workers on the same mine.
	/// </summary>
	[TestFixture]
	public class UnitGatherErrorTests : PlayModeTestBase
	{
		// ── Helpers ────────────────────────────────────────────────────────────

		private Unit PlaceBuiltBase(Vector3Int position)
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, position);
			baseUnit.IsBuilt = true;
			return baseUnit;
		}

		private void StartGathering(Unit worker, Unit mine, Unit baseUnit) =>
			worker.StartGathering(new GatherEventArgs(worker, mine, baseUnit));

		// ── Error handling ─────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Gather_MineDestroyedMidTrip_WorkerGoesIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(15, 5, 0));
			Unit worker   = PlaceUnit(UnitType.WORKER, new Vector3Int(8,  5, 0));

			StartGathering(worker, mine, baseUnit);

			yield return WaitFrames(30);

			// Kill the mine
			mine.Health = 0;

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				15f,
				"Worker did not go IDLE after mine was destroyed mid-trip");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker should be IDLE when the mine is destroyed during gathering");
		}

		[UnityTest]
		public IEnumerator Gather_BaseDestroyedMidTrip_WorkerGoesIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(10, 5, 0));
			Unit worker   = PlaceUnit(UnitType.WORKER, new Vector3Int(9,  5, 0));

			StartGathering(worker, mine, baseUnit);

			// Wait until mining starts (mine health drops)
			float initialMineHealth = mine.Health;
			yield return WaitUntil(
				() => mine.Health < initialMineHealth,
				15f,
				"Worker did not start mining");

			// Kill the base
			baseUnit.Health = 0;

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				30f,
				"Worker did not go IDLE after base was destroyed during gathering");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker should be IDLE when the base is destroyed during a gather trip");
		}

		// ── Stress ────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Gather_MultipleWorkersSameMine_AllDepositGold()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE, new Vector3Int(15, 5, 0));

			Unit[] workers = new Unit[5];
			workers[0] = PlaceUnit(UnitType.WORKER, new Vector3Int(9,  4, 0));
			workers[1] = PlaceUnit(UnitType.WORKER, new Vector3Int(9,  6, 0));
			workers[2] = PlaceUnit(UnitType.WORKER, new Vector3Int(10, 4, 0));
			workers[3] = PlaceUnit(UnitType.WORKER, new Vector3Int(11, 4, 0));
			workers[4] = PlaceUnit(UnitType.WORKER, new Vector3Int(11, 6, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			foreach (Unit worker in workers)
				StartGathering(worker, mine, baseUnit);

			// MiningCapacity for WORKER = 10 * 10 = 100 gold per trip; 5 workers collectively exceed that
			yield return WaitUntil(
				() => agent.Gold >= initialGold + 100,
				60f,
				"Multiple workers did not deposit enough gold from the same mine");

			Assert.GreaterOrEqual(agent.Gold, initialGold + 100,
				"Agent gold should increase by at least one worker's capacity with 5 workers gathering");

			int gatheringCount = 0;
			foreach (Unit worker in workers)
				if (worker != null && worker.CurrentAction == UnitAction.GATHER)
					gatheringCount++;

			Assert.Greater(gatheringCount, 0,
				"At least some workers should still be gathering after the first deposits");
		}
	}
}
