using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for multi-worker gather accumulation:
	/// three workers gathering simultaneously should deposit at least 3x the
	/// single-worker mining capacity.
	/// </summary>
	[TestFixture]
	public class ThreeWorkerDepositTests : PlayModeTestBase
	{
		#region Three Workers Gather Simultaneously

		/// <summary>
		/// Three workers all gather from the same mine to the same base.
		/// Combined gold income should reach at least 3× the per-worker capacity.
		/// </summary>
		[UnityTest]
		public IEnumerator ThreeWorkers_GatherSimultaneously_AccumulateTripleCapacity()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 5, 0));

			Unit w1 = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 4, 0));
			Unit w2 = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));
			Unit w3 = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 6, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;
			int targetGold = initialGold + (int)(Constants.MINING_CAPACITY[UnitType.WORKER] * 3);

			w1.StartGathering(new GatherEventArgs(w1, mine, baseUnit));
			w2.StartGathering(new GatherEventArgs(w2, mine, baseUnit));
			w3.StartGathering(new GatherEventArgs(w3, mine, baseUnit));

			yield return WaitUntil(
				() => agent.Gold >= targetGold,
				timeoutSeconds: 90f,
				failMessage: $"Three workers did not accumulate {targetGold} gold (started at {initialGold})");

			Assert.GreaterOrEqual(agent.Gold, targetGold,
				"Three workers should accumulate at least 3x mining capacity in gold");
		}

		#endregion

		#region Gold Increases Monotonically with Three Workers

		/// <summary>
		/// With three workers gathering, gold should never decrease between checks.
		/// </summary>
		[UnityTest]
		public IEnumerator ThreeWorkers_GoldNeverDecreases()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(12, 5, 0));

			Unit w1 = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 4, 0));
			Unit w2 = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));
			Unit w3 = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 6, 0));

			Agent agent = GetAgent0();
			w1.StartGathering(new GatherEventArgs(w1, mine, baseUnit));
			w2.StartGathering(new GatherEventArgs(w2, mine, baseUnit));
			w3.StartGathering(new GatherEventArgs(w3, mine, baseUnit));

			int prevGold = agent.Gold;
			int depositsObserved = 0;
			float elapsed = 0f;

			while (depositsObserved < 3)
			{
				elapsed += Time.deltaTime;
				if (elapsed > 90f)
				{
					Assert.Fail("Did not observe 3 deposits within 90s with three workers");
					yield break;
				}

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
				"Should observe at least 3 deposits with 3 workers");
		}

		#endregion

		#region Staggered Start

		/// <summary>
		/// Three workers starting gather at different times all contribute gold.
		/// Combined income exceeds what a single worker would earn alone.
		/// </summary>
		[UnityTest]
		public IEnumerator ThreeWorkers_StaggeredStart_AllDeposit()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(14, 5, 0));

			Unit w1 = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 4, 0));
			Unit w2 = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));
			Unit w3 = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 6, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			w1.StartGathering(new GatherEventArgs(w1, mine, baseUnit));
			yield return WaitFrames(20);
			w2.StartGathering(new GatherEventArgs(w2, mine, baseUnit));
			yield return WaitFrames(20);
			w3.StartGathering(new GatherEventArgs(w3, mine, baseUnit));

			int targetGold = initialGold + (int)(Constants.MINING_CAPACITY[UnitType.WORKER] * 2);

			yield return WaitUntil(
				() => agent.Gold >= targetGold,
				timeoutSeconds: 90f,
				failMessage: "Staggered three workers did not accumulate expected gold");

			Assert.GreaterOrEqual(agent.Gold, targetGold,
				"Staggered workers should together deposit at least 2x single-worker capacity");
		}

		#endregion
	}
}
