using System.Collections.Generic;
using System.Reflection;
using AgentSDK;
using NUnit.Framework;
using Preloader;
using UnityEngine;
using UnityEngine.UI;

namespace GameManager.Tests
{
	/// <summary>
	/// EditMode tests for GameManager match result display methods:
	/// - DisplaySingleAgentResults (agent0 wins / agent1 wins)
	/// - DisplayMultiAgentResults (agent0 is ORC / agent0 is HUM)
	/// - UpdateCustomDebugUI foreach body (HUM agent, ORC agent, null-Agent skip)
	/// </summary>
	[TestFixture]
	public class GameManagerResultsDisplayTests
	{
		private GameManager gm;
		private List<GameObject> createdObjects;

		[SetUp]
		public void SetUp()
		{
			gm = GameManager.Instance;
			createdObjects = new List<GameObject>();
		}

		[TearDown]
		public void TearDown()
		{
			SetField("Prefabs", null);
			SetProp("Agents", null);
			SetProp("AgentWins", null);
			SetField("HumanCustomDebugText", null);
			SetField("OrcCustomDebugText", null);

			var stateField = typeof(GameManager).GetField("gameState",
				BindingFlags.NonPublic | BindingFlags.Instance);
			stateField.SetValue(gm, System.Enum.ToObject(stateField.FieldType, 0));
			gm.TotalNbrOfRounds = 3;

			foreach (var go in createdObjects)
				if (go != null) Object.DestroyImmediate(go);
			createdObjects.Clear();
		}

		// ── Reflection helpers ─────────────────────────────────────────────────

		private void SetProp(string name, object value) =>
			typeof(GameManager)
				.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)
				?.SetValue(gm, value);

		private void SetField(string name, object value) =>
			typeof(GameManager)
				.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
				?.SetValue(gm, value);

		private void InvokeVoid(string methodName, params object[] args) =>
			typeof(GameManager)
				.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(gm, args.Length == 0 ? null : args);

		// ── Factory helpers ────────────────────────────────────────────────────

		private GameObject MakeAgentGo(string agentName, int agentNbr)
		{
			var go = new GameObject("Agent_" + agentName);
			createdObjects.Add(go);
			var bridge = go.AddComponent<AgentBridge>();
			bridge.InitializeAgent(agentName, "TestDLL" + agentNbr, agentNbr, ".");
			var controller = go.AddComponent<AgentController>();
			typeof(AgentController)
				.GetField("Agent", BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(controller, bridge);
			return go;
		}

		private (GameObject root, Text bannerText) MakeGameOverUI()
		{
			var root = new GameObject("GameOverUI");
			createdObjects.Add(root);
			root.AddComponent<Canvas>();
			var textGo = new GameObject("BannerText");
			textGo.transform.SetParent(root.transform);
			return (root, textGo.AddComponent<Text>());
		}

		private Text MakeText(string name)
		{
			var go = new GameObject(name);
			createdObjects.Add(go);
			return go.AddComponent<Text>();
		}

		private PrefabLoader MakePrefabs()
		{
			var go = new GameObject("TestPrefabLoader");
			createdObjects.Add(go);
			return go.AddComponent<PrefabLoader>();
		}

		// ── DisplaySingleAgentResults ──────────────────────────────────────────

		[Test]
		public void DisplaySingleAgentResults_Agent0IsOverallWinner_BannerContainsAgent0Name()
		{
			var (gameOverUI, bannerText) = MakeGameOverUI();
			var prefabs = MakePrefabs();
			prefabs.GameOverUI = gameOverUI;
			SetField("Prefabs", prefabs);

			var humGo = MakeAgentGo(Constants.HUMAN_ABBR, 0);
			var orcGo = MakeAgentGo(Constants.ORC_ABBR,   1);
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, humGo }, { 1, orcGo } });
			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.HUMAN_ABBR, 2 },
				{ Constants.ORC_ABBR,   1 }
			});
			gm.TotalNbrOfRounds = 3;

			InvokeVoid("DisplaySingleAgentResults");

			// HUM wins(2) >= ORC wins(1) → winnerAbbr = HUMAN; Agents[0].AgentName == HUMAN → winner = Agents[0]
			StringAssert.Contains(Constants.HUMAN_ABBR, bannerText.text,
				"Banner should contain human agent name when human has more wins");
			StringAssert.Contains("2", bannerText.text,
				"Banner should include the win count");
		}

		[Test]
		public void DisplaySingleAgentResults_Agent1IsOverallWinner_BannerContainsAgent1Name()
		{
			var (gameOverUI, bannerText) = MakeGameOverUI();
			var prefabs = MakePrefabs();
			prefabs.GameOverUI = gameOverUI;
			SetField("Prefabs", prefabs);

			var humGo = MakeAgentGo(Constants.HUMAN_ABBR, 0);
			var orcGo = MakeAgentGo(Constants.ORC_ABBR,   1);
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, humGo }, { 1, orcGo } });
			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.HUMAN_ABBR, 1 },
				{ Constants.ORC_ABBR,   2 }
			});
			gm.TotalNbrOfRounds = 3;

			InvokeVoid("DisplaySingleAgentResults");

			// ORC wins(2) > HUM wins(1) → winnerAbbr = ORC; Agents[0].AgentName = HUM ≠ ORC → winner = Agents[1]
			StringAssert.Contains(Constants.ORC_ABBR, bannerText.text,
				"Banner should contain orc agent name when orc has more wins");
			StringAssert.Contains("2", bannerText.text,
				"Banner should include the win count");
		}

		// ── DisplayMultiAgentResults ───────────────────────────────────────────

		[Test]
		public void DisplayMultiAgentResults_Agent0IsOrc_UsesAgent1AsRepresentative()
		{
			var (gameOverUI, bannerText) = MakeGameOverUI();
			var prefabs = MakePrefabs();
			prefabs.GameOverUI = gameOverUI;
			SetField("Prefabs", prefabs);

			// Agents[0] is ORC → code picks singleAgent = Agents[1] (HUM)
			var orcGo = MakeAgentGo(Constants.ORC_ABBR,   0);
			var humGo = MakeAgentGo(Constants.HUMAN_ABBR, 1);
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, orcGo }, { 1, humGo } });
			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.HUMAN_ABBR, 2 },
				{ Constants.ORC_ABBR,   1 }
			});
			gm.TotalNbrOfRounds = 3;

			InvokeVoid("DisplayMultiAgentResults");

			// singleAgent = Agents[1] (HUM); nbrWins = AgentWins[HUMAN_ABBR] = 2
			StringAssert.Contains(Constants.HUMAN_ABBR, bannerText.text,
				"Banner should use Agents[1] (HUM) when Agents[0] is ORC");
			StringAssert.Contains("2", bannerText.text,
				"Banner should include the win count");
		}

		[Test]
		public void DisplayMultiAgentResults_Agent0IsHuman_UsesAgent0AsRepresentative()
		{
			var (gameOverUI, bannerText) = MakeGameOverUI();
			var prefabs = MakePrefabs();
			prefabs.GameOverUI = gameOverUI;
			SetField("Prefabs", prefabs);

			// Agents[0] is HUM → code picks singleAgent = Agents[0] (HUM)
			var humGo = MakeAgentGo(Constants.HUMAN_ABBR, 0);
			var orcGo = MakeAgentGo(Constants.ORC_ABBR,   1);
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, humGo }, { 1, orcGo } });
			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.HUMAN_ABBR, 3 },
				{ Constants.ORC_ABBR,   0 }
			});
			gm.TotalNbrOfRounds = 3;

			InvokeVoid("DisplayMultiAgentResults");

			// singleAgent = Agents[0] (HUM); nbrWins = AgentWins[HUMAN_ABBR] = 3
			StringAssert.Contains(Constants.HUMAN_ABBR, bannerText.text,
				"Banner should use Agents[0] (HUM) when Agents[0] is not ORC");
			StringAssert.Contains("3", bannerText.text,
				"Banner should include the win count");
		}

		// ── UpdateCustomDebugUI (foreach body) ────────────────────────────────

		[Test]
		public void UpdateCustomDebugUI_HumanAgent_WritesEmptyDisplayToHumanText()
		{
			var humanText = MakeText("HumanText");
			var orcText   = MakeText("OrcText");
			humanText.text = "stale";
			orcText.text   = "stale";
			SetField("HumanCustomDebugText", humanText);
			SetField("OrcCustomDebugText",   orcText);
			gm.OnAgentToggleChanged(true); // HasAgentDebugging = true

			var humGo = MakeAgentGo(Constants.HUMAN_ABBR, 0);
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, humGo } });

			InvokeVoid("UpdateCustomDebugUI");

			// PlanningAgentDebugText always returns "" in tests (no planning agent loaded)
			Assert.AreEqual("", humanText.text,
				"HumanCustomDebugText should be set to empty when PlanningAgentDebugText is empty");
		}

		[Test]
		public void UpdateCustomDebugUI_OrcAgent_WritesEmptyDisplayToOrcText()
		{
			var humanText = MakeText("HumanText");
			var orcText   = MakeText("OrcText");
			humanText.text = "stale";
			orcText.text   = "stale";
			SetField("HumanCustomDebugText", humanText);
			SetField("OrcCustomDebugText",   orcText);
			gm.OnAgentToggleChanged(true);

			var orcGo = MakeAgentGo(Constants.ORC_ABBR, 1);
			SetProp("Agents", new Dictionary<int, GameObject> { { 1, orcGo } });

			InvokeVoid("UpdateCustomDebugUI");

			Assert.AreEqual("", orcText.text,
				"OrcCustomDebugText should be set to empty when PlanningAgentDebugText is empty");
		}

		[Test]
		public void UpdateCustomDebugUI_AgentWithNullAgentField_SkipsWithoutChangingText()
		{
			var humanText = MakeText("HumanText");
			humanText.text = "unchanged";
			SetField("HumanCustomDebugText", humanText);
			SetField("OrcCustomDebugText",   null);
			gm.OnAgentToggleChanged(true);

			// Create a GO with AgentController but leave Agent field null (not set via reflection)
			var agentGo = new GameObject("AgentWithNullAgent");
			createdObjects.Add(agentGo);
			agentGo.AddComponent<AgentController>(); // Agent private field stays null
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, agentGo } });

			Assert.DoesNotThrow(() => InvokeVoid("UpdateCustomDebugUI"),
				"UpdateCustomDebugUI should not throw when controller.Agent is null");
			Assert.AreEqual("unchanged", humanText.text,
				"HumanCustomDebugText should not be modified when the agent has no Agent component");
		}
	}
}
