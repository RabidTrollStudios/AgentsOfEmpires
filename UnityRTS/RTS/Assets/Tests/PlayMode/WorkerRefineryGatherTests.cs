using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for gather operations when a REFINERY is present.
	/// Verifies that the gather cycle functions correctly with a refinery in the
	/// test environment (no errors or hangs).
	/// </summary>
	[TestFixture]
	public class WorkerRefineryGatherTests : PlayModeTestBase
	{
		private Unit PlaceBuiltRefinery(Vector3Int position)
		{
			Unit refinery = PlaceUnit(UnitType.REFINERY, position);
			refinery.IsBuilt = true;
			return refinery;
		}

		#region Gather With Refinery Present

		/// <summary>
		/// A worker gathers from a mine to a base while a built refinery exists nearby.
		/// Gold should increase — the refinery does not break the gather cycle.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_GathersWithRefineryPresent_GoldIncreases()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			Unit refinery = PlaceBuiltRefinery(new Vector3Int(5, 8, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(14, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			worker.StartGathering(new GatherEventArgs(worker, mine, baseUnit));

			yield return WaitUntil(
				() => agent.Gold > initialGold,
				timeoutSeconds: 30f,
				failMessage: "Gold did not increase when gathering with refinery present");

			Assert.Greater(agent.Gold, initialGold,
				"Agent gold should increase when a worker gathers with refinery present");
		}

		/// <summary>
		/// A worker stays in GATHER state (cycling) when a refinery is present.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_WithRefineryPresent_StaysInGatherCycle()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			PlaceBuiltRefinery(new Vector3Int(5, 8, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(12, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			worker.StartGathering(new GatherEventArgs(worker, mine, baseUnit));

			// Wait for first deposit
			yield return WaitUntil(
				() => agent.Gold > initialGold,
				timeoutSeconds: 30f,
				failMessage: "First deposit did not occur with refinery present");

			// Worker should continue gathering (not go IDLE prematurely)
			Assert.AreEqual(UnitAction.GATHER, worker.CurrentAction,
				"Worker should remain in GATHER state after deposit when refinery is present");
		}

		#endregion

		#region Refinery Is Built and Not Broken

		/// <summary>
		/// A built REFINERY is placed and remains in place with health > 0.
		/// (Verifies that placing a REFINERY does not crash or cause errors.)
		/// </summary>
		[UnityTest]
		public IEnumerator BuiltRefinery_RemainsIntact()
		{
			Unit refinery = PlaceBuiltRefinery(new Vector3Int(8, 8, 0));

			yield return WaitFrames(5);

			Assert.IsTrue(refinery.IsBuilt, "Refinery should remain built");
			Assert.Greater(refinery.Health, 0f, "Refinery health should be > 0");
		}

		/// <summary>
		/// A built REFINERY has the correct health from Constants.HEALTH.
		/// </summary>
		[UnityTest]
		public IEnumerator BuiltRefinery_HasCorrectHealth()
		{
			Unit refinery = PlaceBuiltRefinery(new Vector3Int(8, 8, 0));

			Assert.AreEqual(Constants.HEALTH[UnitType.REFINERY], refinery.Health, 0.1f,
				"REFINERY should spawn with HEALTH[REFINERY] health");
			yield return null;
		}

		#endregion

		#region Multiple Workers With Refinery

		/// <summary>
		/// Two workers gathering to the same base with a refinery present both deposit gold.
		/// </summary>
		[UnityTest]
		public IEnumerator TwoWorkers_WithRefineryPresent_BothDeposit()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			PlaceBuiltRefinery(new Vector3Int(5, 8, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(13, 5, 0));

			Unit w1 = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 4, 0));
			Unit w2 = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 6, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;
			int targetGold = initialGold + (int)(Constants.MINING_CAPACITY[UnitType.WORKER] * 2);

			w1.StartGathering(new GatherEventArgs(w1, mine, baseUnit));
			w2.StartGathering(new GatherEventArgs(w2, mine, baseUnit));

			yield return WaitUntil(
				() => agent.Gold >= targetGold,
				timeoutSeconds: 60f,
				failMessage: "Two workers with refinery did not deposit combined gold");

			Assert.GreaterOrEqual(agent.Gold, targetGold,
				"Two workers with refinery should deposit at least 2x mining capacity");
		}

		#endregion
	}
}
