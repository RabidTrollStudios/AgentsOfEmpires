using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AgentSDK;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for GameManager win-condition logic:
	/// - DetermineRoundWinner: both/one/no agent has units, not-timed-out paths
	/// - CalcScorePerUnit (via the timeout path of DetermineRoundWinner):
	///     agent0 higher score, agent1 higher score,
	///     equal score + agent0 more gold, equal score + agent0 &lt;= agent1 gold
	/// </summary>
	[TestFixture]
	public class GameManagerWinConditionTests : PlayModeTestBase
	{
		// ── Reflection helpers ────────────────────────────────────────────────

		private static void SetProp(string name, object value)
		{
			typeof(GameManager)
				.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(GameManager.Instance, value);
		}

		private static GameObject InvokeDetermineRoundWinner()
		{
			return (GameObject)typeof(GameManager)
				.GetMethod("DetermineRoundWinner", BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(GameManager.Instance, null);
		}

		/// <summary>
		/// Inject {0: Agent0Go, 1: Agent1Go} into the private Agents dictionary.
		/// Required because the test environment does not call InitializeMatch.
		/// </summary>
		private void InjectAgents()
		{
			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, ctx.Agent0Go },
				{ 1, ctx.Agent1Go }
			});
		}

		// ── DetermineRoundWinner — not timed-out paths ────────────────────────

		[UnityTest]
		public IEnumerator DetermineRoundWinner_BothHaveUnits_ReturnsNull()
		{
			InjectAgents();
			GameManager.Instance.TotalGameTime = 0f;
			GameManager.Instance.MaxNbrOfSeconds = 300;

			PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.WORKER, new Vector3Int(20, 20, 0), ctx.Agent1Go);

			yield return null;

			var winner = InvokeDetermineRoundWinner();

			Assert.IsNull(winner, "Two live agents should yield no winner yet");
		}

		[UnityTest]
		public IEnumerator DetermineRoundWinner_Agent1HasNoUnits_ReturnsAgent0()
		{
			InjectAgents();
			GameManager.Instance.TotalGameTime = 0f;
			GameManager.Instance.MaxNbrOfSeconds = 300;

			// Only agent 0 has a unit
			PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));

			yield return null;

			var winner = InvokeDetermineRoundWinner();

			Assert.AreEqual(ctx.Agent0Go, winner,
				"Agent 0 should win when agent 1 has no units");
		}

		[UnityTest]
		public IEnumerator DetermineRoundWinner_Agent0HasNoUnits_ReturnsAgent1()
		{
			InjectAgents();
			GameManager.Instance.TotalGameTime = 0f;
			GameManager.Instance.MaxNbrOfSeconds = 300;

			// Only agent 1 has a unit
			PlaceUnit(UnitType.WORKER, new Vector3Int(20, 20, 0), ctx.Agent1Go);

			yield return null;

			var winner = InvokeDetermineRoundWinner();

			Assert.AreEqual(ctx.Agent1Go, winner,
				"Agent 1 should win when agent 0 has no units");
		}

		[UnityTest]
		public IEnumerator DetermineRoundWinner_NeitherHasUnits_ReturnsNull()
		{
			InjectAgents();
			GameManager.Instance.TotalGameTime = 0f;
			GameManager.Instance.MaxNbrOfSeconds = 300;

			// No units placed for either agent
			yield return null;

			var winner = InvokeDetermineRoundWinner();

			Assert.IsNull(winner, "No units on either side should yield no winner");
		}

		// ── DetermineRoundWinner — timeout paths (CalcScorePerUnit) ───────────

		[UnityTest]
		public IEnumerator DetermineRoundWinner_Timeout_Agent0HigherScore_ReturnsAgent0()
		{
			InjectAgents();
			// SOLDIER value=4 vs WORKER value=1
			PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.WORKER,  new Vector3Int(20, 20, 0), ctx.Agent1Go);

			// Trigger timeout path
			GameManager.Instance.TotalGameTime  = 999f;
			GameManager.Instance.MaxNbrOfSeconds = 300;

			yield return null;

			var winner = InvokeDetermineRoundWinner();

			Assert.AreEqual(ctx.Agent0Go, winner,
				"SOLDIER (4 pts) beats WORKER (1 pt) — agent 0 should win on score");
		}

		[UnityTest]
		public IEnumerator DetermineRoundWinner_Timeout_Agent1HigherScore_ReturnsAgent1()
		{
			InjectAgents();
			// WORKER value=1 vs SOLDIER value=4
			PlaceUnit(UnitType.WORKER,  new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.SOLDIER, new Vector3Int(20, 20, 0), ctx.Agent1Go);

			GameManager.Instance.TotalGameTime  = 999f;
			GameManager.Instance.MaxNbrOfSeconds = 300;

			yield return null;

			var winner = InvokeDetermineRoundWinner();

			Assert.AreEqual(ctx.Agent1Go, winner,
				"SOLDIER (4 pts) beats WORKER (1 pt) — agent 1 should win on score");
		}

		[UnityTest]
		public IEnumerator DetermineRoundWinner_Timeout_EqualScore_Agent0MoreGold_ReturnsAgent0()
		{
			InjectAgents();
			// Equal units — one WORKER each (value=1)
			PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.WORKER, new Vector3Int(20, 20, 0), ctx.Agent1Go);

			// Agent 0 has more gold than agent 1
			ctx.GetAgent(0).Gold = 2000;
			ctx.GetAgent(1).Gold = 1000;

			GameManager.Instance.TotalGameTime  = 999f;
			GameManager.Instance.MaxNbrOfSeconds = 300;

			yield return null;

			var winner = InvokeDetermineRoundWinner();

			Assert.AreEqual(ctx.Agent0Go, winner,
				"Equal unit score; agent 0 has more gold so should win");
		}

		[UnityTest]
		public IEnumerator DetermineRoundWinner_Timeout_EqualScore_Agent1MoreGold_ReturnsAgent1()
		{
			InjectAgents();
			// Equal units — one WORKER each (value=1)
			PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.WORKER, new Vector3Int(20, 20, 0), ctx.Agent1Go);

			// Agent 1 has more gold than agent 0
			ctx.GetAgent(0).Gold = 500;
			ctx.GetAgent(1).Gold = 1000;

			GameManager.Instance.TotalGameTime  = 999f;
			GameManager.Instance.MaxNbrOfSeconds = 300;

			yield return null;

			var winner = InvokeDetermineRoundWinner();

			Assert.AreEqual(ctx.Agent1Go, winner,
				"Equal unit score; agent 1 has more gold so should win (else branch)");
		}
	}
}
