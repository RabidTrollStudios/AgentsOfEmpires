using System.Collections;
using System.Collections.Generic;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests verifying that UnitManager's agent-filtered queries
	/// correctly distinguish units belonging to different agents.
	/// </summary>
	[TestFixture]
	public class AgentUnitFilterTests : PlayModeTestBase
	{
		#region Agent 0 Units Excluded from Agent 1 Filter

		/// <summary>
		/// GetUnitNbrsOfType with agent 0's agent number returns only agent 0's workers,
		/// not agent 1's workers.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnitNbrsOfType_FilterByAgent0_ExcludesAgent1Units()
		{
			int agent0Nbr = GetAgent0().AgentNbr;
			int agent1Nbr = ctx.GetAgent(1).AgentNbr;

			Unit agent0Worker = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			Unit agent1Worker = PlaceUnit(UnitType.WORKER, new Vector3Int(15, 15, 0), ctx.Agent1Go);

			yield return WaitFrames(1);

			var agent0Workers = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WORKER, agent0Nbr);
			var agent1Workers = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WORKER, agent1Nbr);

			Assert.IsTrue(agent0Workers.Contains(agent0Worker.UnitNbr),
				"Agent 0's filter should include agent 0's worker");
			Assert.IsFalse(agent0Workers.Contains(agent1Worker.UnitNbr),
				"Agent 0's filter should NOT include agent 1's worker");

			Assert.IsTrue(agent1Workers.Contains(agent1Worker.UnitNbr),
				"Agent 1's filter should include agent 1's worker");
			Assert.IsFalse(agent1Workers.Contains(agent0Worker.UnitNbr),
				"Agent 1's filter should NOT include agent 0's worker");
		}

		#endregion

		#region Correct Counts per Agent

		/// <summary>
		/// After placing 3 workers for agent 0 and 2 for agent 1,
		/// GetUnitNbrsOfType returns the correct count for each agent.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnitNbrsOfType_CountsMatchPerAgent()
		{
			int agent0Nbr = GetAgent0().AgentNbr;
			int agent1Nbr = ctx.GetAgent(1).AgentNbr;

			// Place 3 workers for agent 0
			PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.WORKER, new Vector3Int(5, 7, 0));
			PlaceUnit(UnitType.WORKER, new Vector3Int(5, 9, 0));

			// Place 2 workers for agent 1
			PlaceUnit(UnitType.WORKER, new Vector3Int(15, 5, 0), ctx.Agent1Go);
			PlaceUnit(UnitType.WORKER, new Vector3Int(15, 7, 0), ctx.Agent1Go);

			yield return WaitFrames(1);

			var agent0Workers = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WORKER, agent0Nbr);
			var agent1Workers = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WORKER, agent1Nbr);

			Assert.AreEqual(3, agent0Workers.Count,
				"Should have 3 workers for agent 0");
			Assert.AreEqual(2, agent1Workers.Count,
				"Should have 2 workers for agent 1");
		}

		#endregion

		#region Mixed Unit Types per Agent

		/// <summary>
		/// Each agent's units are correctly segregated across different unit types.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnitNbrsOfType_MultipleTypes_FilteredCorrectly()
		{
			int agent0Nbr = GetAgent0().AgentNbr;

			PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 7, 0));
			PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 9, 0));

			// Agent 1 has soldiers too
			PlaceUnit(UnitType.SOLDIER, new Vector3Int(15, 5, 0), ctx.Agent1Go);

			yield return WaitFrames(1);

			var agent0Soldiers = ctx.UnitManager.GetUnitNbrsOfType(UnitType.SOLDIER, agent0Nbr);
			var agent0Archers = ctx.UnitManager.GetUnitNbrsOfType(UnitType.ARCHER, agent0Nbr);

			Assert.AreEqual(2, agent0Soldiers.Count,
				"Agent 0 should have exactly 2 soldiers");
			Assert.AreEqual(1, agent0Archers.Count,
				"Agent 0 should have exactly 1 archer");
		}

		#endregion

		#region Empty Filter Results

		/// <summary>
		/// Querying for a unit type that agent 0 has not trained returns an empty list.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnitNbrsOfType_NoUnitsOfType_ReturnsEmpty()
		{
			int agent0Nbr = GetAgent0().AgentNbr;

			// Agent 0 has no archers
			PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));

			yield return WaitFrames(1);

			var agent0Archers = ctx.UnitManager.GetUnitNbrsOfType(UnitType.ARCHER, agent0Nbr);

			Assert.AreEqual(0, agent0Archers.Count,
				"Query for unit type with no units should return an empty list");
		}

		#endregion

		#region Unit Removed After Destruction

		/// <summary>
		/// After a unit is destroyed, it no longer appears in agent-filtered queries.
		/// </summary>
		[UnityTest]
		public IEnumerator DestroyedUnit_NotInAgentFilter()
		{
			int agent0Nbr = GetAgent0().AgentNbr;

			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));
			int soldierNbr = soldier.UnitNbr;

			yield return WaitFrames(1);

			// Verify unit is present
			var soldiersBefore = ctx.UnitManager.GetUnitNbrsOfType(UnitType.SOLDIER, agent0Nbr);
			Assert.IsTrue(soldiersBefore.Contains(soldierNbr));

			// Destroy the unit
			soldier.Health = 0;

			yield return WaitUntil(
				() => ctx.UnitManager.GetUnit(soldierNbr) == null,
				timeoutSeconds: 10f,
				failMessage: "Soldier was not destroyed");

			var soldiersAfter = ctx.UnitManager.GetUnitNbrsOfType(UnitType.SOLDIER, agent0Nbr);
			Assert.IsFalse(soldiersAfter.Contains(soldierNbr),
				"Destroyed unit should not appear in agent-filtered query");
		}

		#endregion
	}
}
