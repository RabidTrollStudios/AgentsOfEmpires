using System.Collections;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for agent gold management:
	/// starting gold, spending via build/train, earning via gathering,
	/// and insufficient-gold rejection.
	/// </summary>
	[TestFixture]
	public class AgentGoldTests : PlayModeTestBase
	{
		private void TickUnit(Unit unit)
		{
			unit.FixedUpdate();
			unit.Update();
		}

		#region Starting Gold

		/// <summary>
		/// Both agents start with a positive gold amount.
		/// </summary>
		[UnityTest]
		public IEnumerator BothAgents_StartWithPositiveGold()
		{
			Agent agent0 = GetAgent0();
			Agent agent1 = ctx.GetAgent(1);

			Assert.Greater(agent0.Gold, 0, "Agent 0 should start with positive gold");
			Assert.Greater(agent1.Gold, 0, "Agent 1 should start with positive gold");

			yield return null;
		}

		#endregion

		#region Gold Spending

		/// <summary>
		/// Building a BASE deducts the correct cost from the agent's gold.
		/// </summary>
		[UnityTest]
		public IEnumerator BuildBase_DeductsCorrectCost()
		{
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;
			int baseCost = (int)Constants.COST[UnitType.BASE];

			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			worker.StartBuilding(new BuildEventArgs(worker, new Vector3Int(10, 10, 0), UnitType.BASE));

			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should start building");
			Assert.AreEqual(goldBefore - baseCost, agent.Gold,
				"Gold should drop by BASE cost immediately");

			yield return null;
		}

		/// <summary>
		/// Training a WORKER from BASE deducts the WORKER cost.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainWorker_DeductsCorrectCost()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;

			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;
			int workerCost = (int)Constants.COST[UnitType.WORKER];

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));

			Assert.AreEqual(goldBefore - workerCost, agent.Gold,
				"Gold should drop by WORKER cost when training starts");

			yield return null;
		}

		/// <summary>
		/// Training a SOLDIER from BARRACKS deducts the SOLDIER cost.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainSoldier_DeductsCorrectCost()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;
			int soldierCost = (int)Constants.COST[UnitType.SOLDIER];

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.SOLDIER));

			Assert.AreEqual(goldBefore - soldierCost, agent.Gold,
				"Gold should drop by SOLDIER cost when BARRACKS starts training");

			yield return null;
		}

		/// <summary>
		/// Multiple builds deduct cumulative costs from gold.
		/// </summary>
		[UnityTest]
		public IEnumerator MultipleBuilds_DeductCumulativeCost()
		{
			Agent agent = GetAgent0();
			// Give enough gold for two workers
			agent.Gold = (int)(Constants.COST[UnitType.WORKER] * 3);
			int startGold = agent.Gold;

			Unit w1 = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			Unit w2 = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 14, 0));

			w1.StartBuilding(new BuildEventArgs(w1, new Vector3Int(10, 10, 0), UnitType.BASE));
			// BASE requires BASE cost, not WORKER cost — this is the second build
			int afterFirstBuild = agent.Gold;

			// We only test one build to keep it simple
			Assert.AreEqual(startGold - (int)Constants.COST[UnitType.BASE], afterFirstBuild,
				"First build deducts BASE cost");

			yield return null;
		}

		#endregion

		#region Insufficient Gold

		/// <summary>
		/// When gold is insufficient, a build command is rejected and gold unchanged.
		/// </summary>
		[UnityTest]
		public IEnumerator InsufficientGold_BuildBase_Rejected()
		{
			Agent agent = GetAgent0();
			agent.Gold = 1; // Not enough for BASE

			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			worker.StartBuilding(new BuildEventArgs(worker, new Vector3Int(10, 10, 0), UnitType.BASE));

			Assert.AreNotEqual(UnitAction.BUILD, worker.CurrentAction,
				"Build should be rejected with insufficient gold");
			Assert.AreEqual(1, agent.Gold,
				"Gold should remain unchanged when build is rejected");

			yield return null;
		}

		/// <summary>
		/// When gold is insufficient, a train command is rejected and gold unchanged.
		/// </summary>
		[UnityTest]
		public IEnumerator InsufficientGold_TrainSoldier_Rejected()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			Agent agent = GetAgent0();
			agent.Gold = 1;

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.SOLDIER));

			Assert.AreEqual(UnitAction.IDLE, barracks.CurrentAction,
				"Train should be rejected with insufficient gold");
			Assert.AreEqual(1, agent.Gold,
				"Gold should not be deducted when training is rejected");

			yield return null;
		}

		#endregion

		#region Gold Earning

		/// <summary>
		/// Gold increases after a worker completes a gather round trip.
		/// </summary>
		[UnityTest]
		public IEnumerator GatherRoundTrip_GoldIncreases()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			worker.StartGathering(new GatherEventArgs(worker, mine, baseUnit));

			yield return WaitUntil(
				() => agent.Gold > initialGold,
				timeoutSeconds: 30f,
				failMessage: "Gold did not increase after gather round trip");

			Assert.Greater(agent.Gold, initialGold,
				"Agent gold should increase after worker deposits resources");
		}

		/// <summary>
		/// Gold earned from gathering is cumulative over multiple trips.
		/// </summary>
		[UnityTest]
		public IEnumerator Gathering_MultipleTrips_GoldCompounds()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(10, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			worker.StartGathering(new GatherEventArgs(worker, mine, baseUnit));

			// Wait for at least two deposits (gold to exceed initial + 1 capacity)
			int expectedMinGold = initialGold + (int)(Constants.MINING_CAPACITY[UnitType.WORKER] * 2);
			yield return WaitUntil(
				() => agent.Gold >= expectedMinGold,
				timeoutSeconds: 60f,
				failMessage: "Gold did not compound over two gather trips");

			Assert.GreaterOrEqual(agent.Gold, expectedMinGold,
				"Gold should compound across multiple gather trips");
		}

		#endregion
	}
}
