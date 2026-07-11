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

		private void StartGathering(Unit pawn, Unit mine, Unit baseUnit) =>
			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

		// ── Happy path ─────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Gather_PawnCompletesRoundTrip_AgentGoldIncreases()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(20, 10, 0));
			// Pawn must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit pawn   = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			StartGathering(pawn, mine, baseUnit);

			yield return WaitForTick(
				() => agent.Gold > initialGold,
				30f,
				"Pawn did not deposit gold after a full gather round trip");

			Assert.Greater(agent.Gold, initialGold,
				"Agent gold should have increased after pawn deposited resources");
		}

		[UnityTest]
		public IEnumerator Gather_PawnContinuesAfterFirstDeposit_StillGathering()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(20, 10, 0));
			// Pawn must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit pawn   = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			StartGathering(pawn, mine, baseUnit);

			yield return WaitForTick(
				() => agent.Gold > initialGold,
				30f,
				"Pawn did not complete first deposit");

			Assert.AreEqual(UnitAction.GATHER, pawn.CurrentAction,
				"Pawn should remain in GATHER action after depositing and cycling back to mine");
		}

		[UnityTest]
		public IEnumerator Gather_MineHealthDecreases_DuringMining()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(20, 10, 0));
			// Pawn must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit pawn   = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			float initialMineHealth = mine.Health;

			StartGathering(pawn, mine, baseUnit);

			yield return WaitForTick(
				() => mine.Health < initialMineHealth,
				15f,
				"Mine health did not decrease during pawn mining");

			Assert.Less(mine.Health, initialMineHealth,
				"Mine health should decrease as the pawn extracts gold");
		}

		// ── Boundary conditions ────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Gather_MineCloseToBase_GoldStillDeposited()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(20, 10, 0));
			// Pawn must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit pawn   = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			StartGathering(pawn, mine, baseUnit);

			yield return WaitForTick(
				() => agent.Gold > initialGold,
				20f,
				"Pawn did not deposit gold even with mine close to base");

			Assert.Greater(agent.Gold, initialGold,
				"Agent gold should increase even with a short gather cycle");
		}

		[UnityTest]
		public IEnumerator Gather_MineFarFromBase_GatherCompletesEventually()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(2, 2, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(25, 25, 0));
			Unit pawn   = PlaceUnit(UnitType.PAWN, new Vector3Int(3,  3,  0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			StartGathering(pawn, mine, baseUnit);

			yield return WaitForTick(
				() => agent.Gold > initialGold,
				120f,
				"Pawn did not deposit gold when mine is far from base");

			Assert.Greater(agent.Gold, initialGold,
				"Agent gold should increase even when the mine is far from base");
		}

		[UnityTest]
		public IEnumerator Gather_MineLowHealth_PawnGoesIdleAfterDepletion()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(20, 10, 0));
			// Pawn must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit pawn   = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			mine.Health = 50; // deplete quickly

			StartGathering(pawn, mine, baseUnit);

			yield return WaitForTick(
				() => pawn.CurrentAction == UnitAction.IDLE,
				30f,
				"Pawn did not go IDLE after depleting a low-health mine");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should be IDLE after the mine is fully depleted");
		}
	}
}
