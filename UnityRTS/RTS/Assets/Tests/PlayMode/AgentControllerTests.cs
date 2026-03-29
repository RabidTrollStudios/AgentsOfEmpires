using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for AgentController: initialization, delegation, and Update behaviour.
	/// Internal methods are called directly (InternalsVisibleTo is declared in Agent.cs for
	/// this assembly). Private fields (_debugUpdaters, _debugTextAreas) are accessed via reflection.
	/// </summary>
	[TestFixture]
	public class AgentControllerTests : PlayModeTestBase
	{
		// ── StubAgent ─────────────────────────────────────────────────────────────

		/// <summary>
		/// Concrete Agent whose lifecycle callbacks record whether they were called.
		/// </summary>
		private class StubAgent : Agent
		{
			public bool InitializeMatchCalled;
			public bool InitializeRoundCalled;
			public bool LearnCalled;

			public override void InitializeMatch()  => InitializeMatchCalled  = true;
			public override void InitializeRound()  => InitializeRoundCalled  = true;
			public override void Learn()             => LearnCalled            = true;
		}

		// ── Reflection Helpers ─────────────────────────────────────────────────────

		private static T GetPrivateField<T>(AgentController controller, string fieldName) =>
			(T)typeof(AgentController)
				.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(controller);

		private static void SetHasAgentDebugging(bool value) =>
			typeof(GameManager)
				.GetProperty("HasAgentDebugging", BindingFlags.Public | BindingFlags.Instance)
				.SetValue(GameManager.Instance, value);

		// ── Factory Helper ─────────────────────────────────────────────────────────

		/// <summary>
		/// Creates a fresh agentGo+StubAgent pair and a controllerGo+AgentController pair,
		/// both tracked in ctx.CreatedObjects for automatic teardown.
		/// </summary>
		private (GameObject agentGo, StubAgent stub, AgentController controller)
			CreateFreshController()
		{
			var agentGo = new GameObject("StubAgentGO");
			ctx.CreatedObjects.Add(agentGo);
			var stub = agentGo.AddComponent<StubAgent>();

			var controllerGo = new GameObject("AgentControllerGO");
			ctx.CreatedObjects.Add(controllerGo);
			var controller = controllerGo.AddComponent<AgentController>();
			controller.enabled = false;   // prevent automatic Update() calls during the test

			return (agentGo, stub, controller);
		}

		// ── InitializeAgent ────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator InitializeAgent_NullPanel_TextAreasIsEmpty()
		{
			var (agentGo, _, controller) = CreateFreshController();

			controller.InitializeAgent(agentGo, "Blue", "TestDLL", 0, null, ".");

			var textAreas = GetPrivateField<Text[]>(controller, "_debugTextAreas");
			Assert.AreEqual(0, textAreas.Length,
				"Null debugger panel should yield an empty Text array");

			yield return null;
		}

		[UnityTest]
		public IEnumerator InitializeAgent_PopulatesDebugUpdaters_WithTenKeys()
		{
			var (agentGo, _, controller) = CreateFreshController();

			controller.InitializeAgent(agentGo, "Blue", "TestDLL", 0, null, ".");

			var updaters = GetPrivateField<Dictionary<string, Func<string>>>(
				controller, "_debugUpdaters");
			Assert.AreEqual(14, updaters.Count,
				"InitializeAgent should populate exactly 14 debug updater entries");

			yield return null;
		}

		[UnityTest]
		public IEnumerator InitializeAgent_WithPanel_TextAreasMatchesPanelChildren()
		{
			var (agentGo, _, controller) = CreateFreshController();

			var panelGo = new GameObject("DebugPanel");
			ctx.CreatedObjects.Add(panelGo);
			foreach (var name in new[] { "Agent Name", "Gold Value" })
			{
				var child = new GameObject(name);
				child.transform.SetParent(panelGo.transform);
				child.AddComponent<Text>();
			}

			controller.InitializeAgent(agentGo, "Blue", "TestDLL", 0, panelGo, ".");

			var textAreas = GetPrivateField<Text[]>(controller, "_debugTextAreas");
			Assert.AreEqual(2, textAreas.Length,
				"_debugTextAreas should contain all Text children of the panel");

			yield return null;
		}

		// ── InitializeMatch ────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator InitializeMatch_DelegatesToAgent()
		{
			var (_, stub, controller) = CreateFreshController();
			controller.Agent = stub;

			controller.InitializeMatch();

			Assert.IsTrue(stub.InitializeMatchCalled,
				"InitializeMatch should delegate to Agent.InitializeMatch");

			yield return null;
		}

		// ── InitializeRound ────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator InitializeRound_SetsAgentGoldToStartingGold()
		{
			var (_, stub, controller) = CreateFreshController();
			controller.Agent = stub;
			stub.Gold = 0;

			controller.InitializeRound();

			Assert.AreEqual(GameManager.Instance.StartingPlayerGold, stub.Gold,
				"InitializeRound should set Agent.Gold to GameManager.Instance.StartingPlayerGold");

			yield return null;
		}

		[UnityTest]
		public IEnumerator InitializeRound_DelegatesToAgent()
		{
			var (_, stub, controller) = CreateFreshController();
			controller.Agent = stub;

			controller.InitializeRound();

			Assert.IsTrue(stub.InitializeRoundCalled,
				"InitializeRound should delegate to Agent.InitializeRound");

			yield return null;
		}

		// ── Learn ─────────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Learn_DelegatesToAgent()
		{
			var (_, stub, controller) = CreateFreshController();
			controller.Agent = stub;

			controller.Learn();

			Assert.IsTrue(stub.LearnCalled,
				"Learn should delegate to Agent.Learn");

			yield return null;
		}

		// ── Update ────────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Update_AgentPresent_DebuggingOff_DoesNotThrow()
		{
			var (_, stub, controller) = CreateFreshController();
			controller.Agent = stub;
			// HasAgentDebugging is false by default in PlayModeTestHelper

			Assert.DoesNotThrow(() => controller.Update(),
				"Update with Agent set and debugging off should not throw");

			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_DebuggingOn_UpdatesMatchingTextArea()
		{
			var (agentGo, _, controller) = CreateFreshController();

			// Build a panel with a single Text child named "Agent Name"
			var panelGo = new GameObject("DebugPanel");
			ctx.CreatedObjects.Add(panelGo);
			var textGo = new GameObject("Agent Name");
			textGo.transform.SetParent(panelGo.transform);
			var textArea = textGo.AddComponent<Text>();

			controller.InitializeAgent(agentGo, "Blue", "TestDLL", 0, panelGo, ".");
			SetHasAgentDebugging(true);
			controller.enabled = true;

			controller.Update();

			Assert.AreEqual("Blue TestDLL", textArea.text,
				"Update with debugging on should write AgentName + AgentDLLName to the matching Text");

			yield return null;
		}
	}
}
