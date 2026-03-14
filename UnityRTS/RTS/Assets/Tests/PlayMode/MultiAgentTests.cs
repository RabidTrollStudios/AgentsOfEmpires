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
			Unit agent0Unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit agent1Unit = PlaceUnit(UnitType.PAWN, new Vector3Int(15, 15, 0), ctx.Agent1Go);

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
			Unit a0w1 = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit a0w2 = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 6, 0));
			Unit a1w1 = PlaceUnit(UnitType.PAWN, new Vector3Int(15, 5, 0), ctx.Agent1Go);

			int agent0Nbr = a0w1.Agent.GetComponent<AgentController>().Agent.AgentNbr;
			int agent1Nbr = a1w1.Agent.GetComponent<AgentController>().Agent.AgentNbr;

			var agent0Pawns = ctx.UnitManager.GetUnitNbrsOfType(UnitType.PAWN, agent0Nbr);
			var agent1Pawns = ctx.UnitManager.GetUnitNbrsOfType(UnitType.PAWN, agent1Nbr);

			Assert.AreEqual(2, agent0Pawns.Count,
				"Agent 0 should have exactly 2 pawns");
			Assert.AreEqual(1, agent1Pawns.Count,
				"Agent 1 should have exactly 1 pawn");

			yield return null;
		}

		#endregion

		#region Cross-Agent Combat

		/// <summary>
		/// Agent 0 warrior attacks agent 1 pawn — health decreases.
		/// </summary>
		[UnityTest]
		public IEnumerator Agent0Warrior_AttacksAgent1Pawn_HealthDecreases()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0), ctx.Agent1Go);

			float initialHealth = enemy.Health;
			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction,
				"Agent 0 warrior should attack agent 1 pawn");

			yield return WaitUntil(
				() => enemy == null || enemy.Health < initialHealth,
				timeoutSeconds: 10f,
				failMessage: "Agent 1 enemy health did not decrease from agent 0 warrior attack");

			if (enemy != null)
				Assert.Less(enemy.Health, initialHealth,
					"Cross-agent attack should deal damage");
		}

		/// <summary>
		/// Agent 1 warrior attacks agent 0 pawn successfully.
		/// </summary>
		[UnityTest]
		public IEnumerator Agent1Warrior_AttacksAgent0Pawn_HealthDecreases()
		{
			Unit friendlyPawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(11, 10, 0), ctx.Agent1Go);

			float initialHealth = friendlyPawn.Health;
			enemy.StartAttacking(new AttackEventArgs(enemy, friendlyPawn));

			Assert.AreEqual(UnitAction.ATTACK, enemy.CurrentAction,
				"Agent 1 warrior should attack agent 0 pawn");

			yield return WaitUntil(
				() => friendlyPawn == null || friendlyPawn.Health < initialHealth,
				timeoutSeconds: 10f,
				failMessage: "Agent 0 pawn health did not decrease from agent 1 attack");

			if (friendlyPawn != null)
				Assert.Less(friendlyPawn.Health, initialHealth,
					"Reverse cross-agent attack should deal damage");
		}

		#endregion

		#region Competing Resources

		/// <summary>
		/// Pawns from both agents can gather from the same mine independently.
		/// </summary>
		[UnityTest]
		public IEnumerator BothAgents_GatherSameMine_BothGainGold()
		{
			// base0 at (2,10) covers x=[2,7], y=[7,10]; base1 at (24,10) covers x=[24,29], y=[7,10]
			Unit base0 = PlaceUnit(UnitType.BASE, new Vector3Int(2, 10, 0));
			base0.IsBuilt = true;

			Unit base1 = PlaceUnit(UnitType.BASE, new Vector3Int(24, 10, 0), ctx.Agent1Go);
			base1.IsBuilt = true;

			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));

			// Pawns must be outside their respective BASE footprints
			Unit pawn0 = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 15, 0));
			Unit pawn1 = PlaceUnit(UnitType.PAWN, new Vector3Int(20, 15, 0), ctx.Agent1Go);

			Agent agent0 = GetAgent0();
			Agent agent1 = ctx.GetAgent(1);

			int gold0Before = agent0.Gold;
			int gold1Before = agent1.Gold;

			pawn0.StartGathering(new GatherEventArgs(pawn0, mine, base0));
			pawn1.StartGathering(new GatherEventArgs(pawn1, mine, base1));

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
