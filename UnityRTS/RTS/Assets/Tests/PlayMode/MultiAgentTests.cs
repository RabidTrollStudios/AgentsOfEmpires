using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for multi-agent interactions:
	/// cross-agent attacks, ownership verification, and competing actions.
	/// </summary>
	[TestFixture]
	public class MultiAgentTests : PlayModeTestBase
	{
		#region Ownership

		/// <summary>
		/// Agent 1 units should have a different AgentNbr than agent 0 units.
		/// </summary>
		[UnityTest]
		public IEnumerator Agent1Units_HaveDifferentOwner()
		{
			Unit agent0Unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			Unit agent1Unit = PlaceUnit(UnitType.WORKER, new Vector3Int(15, 15, 0), ctx.Agent1Go);

			int agent0Nbr = agent0Unit.Agent.GetComponent<AgentController>().Agent.AgentNbr;
			int agent1Nbr = agent1Unit.Agent.GetComponent<AgentController>().Agent.AgentNbr;

			Assert.AreNotEqual(agent0Nbr, agent1Nbr,
				"Agent 0 and Agent 1 units should have different owner IDs");

			yield return null;
		}

		/// <summary>
		/// GetUnitNbrsOfType with agent number filter returns only that agent's units.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnitNbrsOfType_AgentFilter_ReturnsSeparate()
		{
			Unit a0w1 = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			Unit a0w2 = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 6, 0));
			Unit a1w1 = PlaceUnit(UnitType.WORKER, new Vector3Int(15, 5, 0), ctx.Agent1Go);

			int agent0Nbr = a0w1.Agent.GetComponent<AgentController>().Agent.AgentNbr;
			int agent1Nbr = a1w1.Agent.GetComponent<AgentController>().Agent.AgentNbr;

			var agent0Workers = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WORKER, agent0Nbr);
			var agent1Workers = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WORKER, agent1Nbr);

			Assert.AreEqual(2, agent0Workers.Count,
				"Agent 0 should have exactly 2 workers");
			Assert.AreEqual(1, agent1Workers.Count,
				"Agent 1 should have exactly 1 worker");

			yield return null;
		}

		#endregion

		#region Cross-Agent Combat

		/// <summary>
		/// Agent 0 soldier attacks agent 1 worker — health decreases.
		/// </summary>
		[UnityTest]
		public IEnumerator Agent0Soldier_AttacksAgent1Worker_HealthDecreases()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WORKER, new Vector3Int(11, 10, 0), ctx.Agent1Go);

			float initialHealth = enemy.Health;
			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));

			Assert.AreEqual(UnitAction.ATTACK, soldier.CurrentAction,
				"Agent 0 soldier should attack agent 1 worker");

			yield return WaitUntil(
				() => enemy == null || enemy.Health < initialHealth,
				timeoutSeconds: 10f,
				failMessage: "Agent 1 enemy health did not decrease from agent 0 soldier attack");

			if (enemy != null)
				Assert.Less(enemy.Health, initialHealth,
					"Cross-agent attack should deal damage");
		}

		/// <summary>
		/// Agent 1 soldier attacks agent 0 worker successfully.
		/// </summary>
		[UnityTest]
		public IEnumerator Agent1Soldier_AttacksAgent0Worker_HealthDecreases()
		{
			Unit friendlyWorker = PlaceUnit(UnitType.WORKER, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.SOLDIER, new Vector3Int(11, 10, 0), ctx.Agent1Go);

			float initialHealth = friendlyWorker.Health;
			enemy.StartAttacking(new AttackEventArgs(enemy, friendlyWorker));

			Assert.AreEqual(UnitAction.ATTACK, enemy.CurrentAction,
				"Agent 1 soldier should attack agent 0 worker");

			yield return WaitUntil(
				() => friendlyWorker == null || friendlyWorker.Health < initialHealth,
				timeoutSeconds: 10f,
				failMessage: "Agent 0 worker health did not decrease from agent 1 attack");

			if (friendlyWorker != null)
				Assert.Less(friendlyWorker.Health, initialHealth,
					"Reverse cross-agent attack should deal damage");
		}

		/// <summary>
		/// Agent 0 archer and agent 0 soldier cannot attack each other.
		/// </summary>
		[UnityTest]
		public IEnumerator SameAgent_ArcherAndSoldier_CannotAttackEachOther()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(10, 10, 0));
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(11, 10, 0));

			float soldierHealth = soldier.Health;
			float archerHealth = archer.Health;

			archer.StartAttacking(new AttackEventArgs(archer, soldier));
			soldier.StartAttacking(new AttackEventArgs(soldier, archer));

			Assert.AreNotEqual(UnitAction.ATTACK, archer.CurrentAction,
				"Archer should not attack a friendly soldier");
			Assert.AreNotEqual(UnitAction.ATTACK, soldier.CurrentAction,
				"Soldier should not attack a friendly archer");
			Assert.AreEqual(soldierHealth, soldier.Health,
				"Friendly soldier health should not change");
			Assert.AreEqual(archerHealth, archer.Health,
				"Friendly archer health should not change");

			yield return null;
		}

		#endregion

		#region Competing Resources

		/// <summary>
		/// Workers from both agents can gather from the same mine independently.
		/// </summary>
		[UnityTest]
		public IEnumerator BothAgents_GatherSameMine_BothGainGold()
		{
			Unit base0 = PlaceUnit(UnitType.BASE, new Vector3Int(2, 10, 0));
			base0.IsBuilt = true;

			Unit base1 = PlaceUnit(UnitType.BASE, new Vector3Int(24, 10, 0), ctx.Agent1Go);
			base1.IsBuilt = true;

			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 10, 0));

			Unit worker0 = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 10, 0));
			Unit worker1 = PlaceUnit(UnitType.WORKER, new Vector3Int(20, 10, 0), ctx.Agent1Go);

			Agent agent0 = GetAgent0();
			Agent agent1 = ctx.GetAgent(1);

			int gold0Before = agent0.Gold;
			int gold1Before = agent1.Gold;

			worker0.StartGathering(new GatherEventArgs(worker0, mine, base0));
			worker1.StartGathering(new GatherEventArgs(worker1, mine, base1));

			// Wait until both agents gain gold
			yield return WaitUntil(
				() => agent0.Gold > gold0Before && agent1.Gold > gold1Before,
				timeoutSeconds: 60f,
				failMessage: "Both agents did not gain gold from competing gather");

			Assert.Greater(agent0.Gold, gold0Before, "Agent 0 should gain gold");
			Assert.Greater(agent1.Gold, gold1Before, "Agent 1 should gain gold");
		}

		#endregion
	}
}
