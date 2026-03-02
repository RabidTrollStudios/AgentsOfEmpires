using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for the gather happy path and boundary conditions:
	/// round-trip completion, cycle continuation, mine health decrease,
	/// short/long distances, and mine depletion.
	/// </summary>
	[TestFixture]
	public class UnitGatherLifecycleTests : PlayModeTestBase
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

		// ── Happy path ─────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Gather_WorkerCompletesRoundTrip_AgentGoldIncreases()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(15, 5, 0));
			Unit worker   = PlaceUnit(UnitType.WORKER, new Vector3Int(8,  5, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			StartGathering(worker, mine, baseUnit);

			yield return WaitUntil(
				() => agent.Gold > initialGold,
				30f,
				"Worker did not deposit gold after a full gather round trip");

			Assert.Greater(agent.Gold, initialGold,
				"Agent gold should have increased after worker deposited resources");
		}

		[UnityTest]
		public IEnumerator Gather_WorkerContinuesAfterFirstDeposit_StillGathering()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(15, 5, 0));
			Unit worker   = PlaceUnit(UnitType.WORKER, new Vector3Int(8,  5, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			StartGathering(worker, mine, baseUnit);

			yield return WaitUntil(
				() => agent.Gold > initialGold,
				30f,
				"Worker did not complete first deposit");

			Assert.AreEqual(UnitAction.GATHER, worker.CurrentAction,
				"Worker should remain in GATHER action after depositing and cycling back to mine");
		}

		[UnityTest]
		public IEnumerator Gather_MineHealthDecreases_DuringMining()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(10, 5, 0));
			Unit worker   = PlaceUnit(UnitType.WORKER, new Vector3Int(9,  5, 0));

			float initialMineHealth = mine.Health;

			StartGathering(worker, mine, baseUnit);

			yield return WaitUntil(
				() => mine.Health < initialMineHealth,
				15f,
				"Mine health did not decrease during worker mining");

			Assert.Less(mine.Health, initialMineHealth,
				"Mine health should decrease as the worker extracts gold");
		}

		// ── Boundary conditions ────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Gather_MineCloseToBase_GoldStillDeposited()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(10, 5, 0));
			Unit worker   = PlaceUnit(UnitType.WORKER, new Vector3Int(8,  5, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			StartGathering(worker, mine, baseUnit);

			yield return WaitUntil(
				() => agent.Gold > initialGold,
				20f,
				"Worker did not deposit gold even with mine close to base");

			Assert.Greater(agent.Gold, initialGold,
				"Agent gold should increase even with a short gather cycle");
		}

		[UnityTest]
		public IEnumerator Gather_MineFarFromBase_GatherCompletesEventually()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(2, 2, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(25, 25, 0));
			Unit worker   = PlaceUnit(UnitType.WORKER, new Vector3Int(3,  3,  0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			StartGathering(worker, mine, baseUnit);

			yield return WaitUntil(
				() => agent.Gold > initialGold,
				120f,
				"Worker did not deposit gold when mine is far from base");

			Assert.Greater(agent.Gold, initialGold,
				"Agent gold should increase even when the mine is far from base");
		}

		[UnityTest]
		public IEnumerator Gather_MineLowHealth_WorkerGoesIdleAfterDepletion()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(10, 5, 0));
			Unit worker   = PlaceUnit(UnitType.WORKER, new Vector3Int(8,  5, 0));

			mine.Health = 50; // deplete quickly

			StartGathering(worker, mine, baseUnit);

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				30f,
				"Worker did not go IDLE after depleting a low-health mine");

			Assert.AreEqual(UnitAction.IDLE, worker.CurrentAction,
				"Worker should be IDLE after the mine is fully depleted");
		}
	}
}
