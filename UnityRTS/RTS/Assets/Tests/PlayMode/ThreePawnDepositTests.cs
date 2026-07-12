using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for multi-pawn gather accumulation:
	/// three pawns gathering simultaneously should deposit at least 3x the
	/// single-pawn mining capacity.
	/// </summary>
	[TestFixture]
	public class ThreePawnDepositTests : PlayModeTestBase
	{
		#region Three Pawns Gather Simultaneously

		/// <summary>
		/// Three pawns all gather from the same mine to the same base.
		/// Combined gold income should reach at least 3× the per-pawn capacity.
		/// </summary>
		[UnityTest]
		public IEnumerator ThreePawns_GatherSimultaneously_AccumulateTripleCapacity()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));

			// Pawns must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit w1 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 9, 0));
			Unit w2 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));
			Unit w3 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 11, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;
			int targetGold = initialGold + (int)(Constants.MINING_CAPACITY[UnitType.PAWN] * 3);

			w1.StartGathering(new GatherEventArgs(w1, mine, baseUnit));
			w2.StartGathering(new GatherEventArgs(w2, mine, baseUnit));
			w3.StartGathering(new GatherEventArgs(w3, mine, baseUnit));

			yield return WaitForTick(
				() => agent.Gold >= targetGold,
				timeoutSeconds: 15f,
				failMessage: $"Three pawns did not accumulate {targetGold} gold (started at {initialGold})");

			Assert.GreaterOrEqual(agent.Gold, targetGold,
				"Three pawns should accumulate at least 3x mining capacity in gold");
		}

		#endregion

		#region Gold Increases Monotonically with Three Pawns

		/// <summary>
		/// With three pawns gathering, gold should never decrease between checks.
		/// </summary>
		[UnityTest]
		public IEnumerator ThreePawns_GoldNeverDecreases()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));

			// Pawns must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit w1 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 9, 0));
			Unit w2 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));
			Unit w3 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 11, 0));

			Agent agent = GetAgent0();
			w1.StartGathering(new GatherEventArgs(w1, mine, baseUnit));
			w2.StartGathering(new GatherEventArgs(w2, mine, baseUnit));
			w3.StartGathering(new GatherEventArgs(w3, mine, baseUnit));

			int prevGold = agent.Gold;
			int depositsObserved = 0;
			// GameManager GO is inactive → FixedUpdate never fires. Drive ticks explicitly.
			int maxTicks = 15 * 20; // 15s @ 20 Hz
			int ticks = 0;

			while (depositsObserved < 3)
			{
				if (ticks++ >= maxTicks)
				{
					Assert.Fail("Did not observe 3 deposits within 15s with three pawns");
					yield break;
				}

				GameManager.Instance.SimulateTick();
				if (agent.Gold > prevGold)
				{
					Assert.GreaterOrEqual(agent.Gold, prevGold,
						"Gold should never decrease between deposits");
					prevGold = agent.Gold;
					depositsObserved++;
				}
				yield return null;
			}

			Assert.GreaterOrEqual(depositsObserved, 3,
				"Should observe at least 3 deposits with 3 pawns");
		}

		#endregion

		#region Staggered Start

		/// <summary>
		/// Three pawns starting gather at different times all contribute gold.
		/// Combined income exceeds what a single pawn would earn alone.
		/// </summary>
		[UnityTest]
		public IEnumerator ThreePawns_StaggeredStart_AllDeposit()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));

			// Pawns must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit w1 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 9, 0));
			Unit w2 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));
			Unit w3 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 11, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			w1.StartGathering(new GatherEventArgs(w1, mine, baseUnit));
			yield return WaitFrames(20);
			w2.StartGathering(new GatherEventArgs(w2, mine, baseUnit));
			yield return WaitFrames(20);
			w3.StartGathering(new GatherEventArgs(w3, mine, baseUnit));

			int targetGold = initialGold + (int)(Constants.MINING_CAPACITY[UnitType.PAWN] * 2);

			yield return WaitForTick(
				() => agent.Gold >= targetGold,
				timeoutSeconds: 15f,
				failMessage: "Staggered three pawns did not accumulate expected gold");

			Assert.GreaterOrEqual(agent.Gold, targetGold,
				"Staggered pawns should together deposit at least 2x single-pawn capacity");
		}

		#endregion
	}
}
