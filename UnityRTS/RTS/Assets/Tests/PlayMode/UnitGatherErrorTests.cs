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
	/// and multiple pawns on the same mine.
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

		private void StartGathering(Unit pawn, Unit mine, Unit baseUnit) =>
			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

		// ── Error handling ─────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Gather_MineDestroyedMidTrip_PawnGoesIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(20, 10, 0));
			// Pawn must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit pawn   = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			StartGathering(pawn, mine, baseUnit);

			yield return WaitFrames(30);

			// Kill the mine
			mine.Health = 0;

			yield return WaitForTick(
				() => pawn.CurrentAction == UnitAction.IDLE,
				15f,
				"Pawn did not go IDLE after mine was destroyed mid-trip");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should be IDLE when the mine is destroyed during gathering");
		}

		[UnityTest]
		public IEnumerator Gather_BaseDestroyedMidTrip_PawnGoesIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			// Mine and pawn must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(20, 10, 0));
			Unit pawn   = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			StartGathering(pawn, mine, baseUnit);

			// Wait until mining starts (mine health drops)
			float initialMineHealth = mine.Health;
			yield return WaitForTick(
				() => mine.Health < initialMineHealth,
				15f,
				"Pawn did not start mining");

			// Kill the base
			baseUnit.Health = 0;

			yield return WaitForTick(
				() => pawn.CurrentAction == UnitAction.IDLE,
				30f,
				"Pawn did not go IDLE after base was destroyed during gathering");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should be IDLE when the base is destroyed during a gather trip");
		}

		// ── Stress ────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Gather_MultiplePawnsSameMine_AllDepositGold()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine     = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));

			// Pawns must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit[] pawns = new Unit[5];
			pawns[0] = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 9, 0));
			pawns[1] = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));
			pawns[2] = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 11, 0));
			pawns[3] = PlaceUnit(UnitType.PAWN, new Vector3Int(13, 9, 0));
			pawns[4] = PlaceUnit(UnitType.PAWN, new Vector3Int(13, 10, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			foreach (Unit pawn in pawns)
				StartGathering(pawn, mine, baseUnit);

			// MiningCapacity for PAWN = 10 * 10 = 100 gold per trip; 5 pawns collectively exceed that
			yield return WaitForTick(
				() => agent.Gold >= initialGold + 100,
				60f,
				"Multiple pawns did not deposit enough gold from the same mine");

			Assert.GreaterOrEqual(agent.Gold, initialGold + 100,
				"Agent gold should increase by at least one pawn's capacity with 5 pawns gathering");

			int gatheringCount = 0;
			foreach (Unit pawn in pawns)
				if (pawn != null && pawn.CurrentAction == UnitAction.GATHER)
					gatheringCount++;

			Assert.Greater(gatheringCount, 0,
				"At least some pawns should still be gathering after the first deposits");
		}
	}
}
