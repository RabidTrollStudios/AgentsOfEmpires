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
	/// - DisplayMultiAgentResults (agent0 is RED / agent0 is BLU)
	/// - UpdateCustomDebugUI foreach body (BLU agent, RED agent, null-Agent skip)
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
			SetField("BlueCustomDebugText", null);
			SetField("RedCustomDebugText", null);

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

			var blueGo = MakeAgentGo(Constants.BLUE_ABBR, 0);
			var redGo = MakeAgentGo(Constants.RED_ABBR,   1);
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, blueGo }, { 1, redGo } });
			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.BLUE_ABBR, 2 },
				{ Constants.RED_ABBR,   1 }
			});
			gm.TotalNbrOfRounds = 3;

			InvokeVoid("DisplaySingleAgentResults");

			// BLU wins(2) >= RED wins(1) → winnerAbbr = BLUE; Agents[0].AgentName == BLUE → winner = Agents[0]
			StringAssert.Contains(Constants.BLUE_ABBR, bannerText.text,
				"Banner should contain blue agent name when blue has more wins");
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

			var blueGo = MakeAgentGo(Constants.BLUE_ABBR, 0);
			var redGo = MakeAgentGo(Constants.RED_ABBR,   1);
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, blueGo }, { 1, redGo } });
			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.BLUE_ABBR, 1 },
				{ Constants.RED_ABBR,   2 }
			});
			gm.TotalNbrOfRounds = 3;

			InvokeVoid("DisplaySingleAgentResults");

			// RED wins(2) > BLU wins(1) → winnerAbbr = RED; Agents[0].AgentName = BLU ≠ RED → winner = Agents[1]
			StringAssert.Contains(Constants.RED_ABBR, bannerText.text,
				"Banner should contain red agent name when red has more wins");
			StringAssert.Contains("2", bannerText.text,
				"Banner should include the win count");
		}

		// ── DisplayMultiAgentResults ───────────────────────────────────────────

		[Test]
		public void DisplayMultiAgentResults_Agent0IsRed_UsesAgent1AsRepresentative()
		{
			var (gameOverUI, bannerText) = MakeGameOverUI();
			var prefabs = MakePrefabs();
			prefabs.GameOverUI = gameOverUI;
			SetField("Prefabs", prefabs);

			// Agents[0] is RED → code picks singleAgent = Agents[1] (BLU)
			var redGo = MakeAgentGo(Constants.RED_ABBR,   0);
			var blueGo = MakeAgentGo(Constants.BLUE_ABBR, 1);
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, redGo }, { 1, blueGo } });
			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.BLUE_ABBR, 2 },
				{ Constants.RED_ABBR,   1 }
			});
			gm.TotalNbrOfRounds = 3;

			InvokeVoid("DisplayMultiAgentResults");

			// singleAgent = Agents[1] (BLU); nbrWins = AgentWins[BLUE_ABBR] = 2
			StringAssert.Contains(Constants.BLUE_ABBR, bannerText.text,
				"Banner should use Agents[1] (BLU) when Agents[0] is RED");
			StringAssert.Contains("2", bannerText.text,
				"Banner should include the win count");
		}

		[Test]
		public void DisplayMultiAgentResults_Agent0IsBlue_UsesAgent0AsRepresentative()
		{
			var (gameOverUI, bannerText) = MakeGameOverUI();
			var prefabs = MakePrefabs();
			prefabs.GameOverUI = gameOverUI;
			SetField("Prefabs", prefabs);

			// Agents[0] is BLU → code picks singleAgent = Agents[0] (BLU)
			var blueGo = MakeAgentGo(Constants.BLUE_ABBR, 0);
			var redGo = MakeAgentGo(Constants.RED_ABBR,   1);
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, blueGo }, { 1, redGo } });
			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.BLUE_ABBR, 3 },
				{ Constants.RED_ABBR,   0 }
			});
			gm.TotalNbrOfRounds = 3;

			InvokeVoid("DisplayMultiAgentResults");

			// singleAgent = Agents[0] (BLU); nbrWins = AgentWins[BLUE_ABBR] = 3
			StringAssert.Contains(Constants.BLUE_ABBR, bannerText.text,
				"Banner should use Agents[0] (BLU) when Agents[0] is not RED");
			StringAssert.Contains("3", bannerText.text,
				"Banner should include the win count");
		}

		// ── UpdateCustomDebugUI (foreach body) ────────────────────────────────

		[Test]
		public void UpdateCustomDebugUI_BlueAgent_WritesEmptyDisplayToBlueText()
		{
			var blueText = MakeText("BlueText");
			var redText   = MakeText("RedText");
			blueText.text = "stale";
			redText.text   = "stale";
			SetField("BlueCustomDebugText", blueText);
			SetField("RedCustomDebugText",   redText);
			gm.OnAgentToggleChanged(true); // HasAgentDebugging = true

			var blueGo = MakeAgentGo(Constants.BLUE_ABBR, 0);
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, blueGo } });

			InvokeVoid("UpdateCustomDebugUI");

			// PlanningAgentDebugText always returns "" in tests (no planning agent loaded)
			Assert.AreEqual("", blueText.text,
				"BlueCustomDebugText should be set to empty when PlanningAgentDebugText is empty");
		}

		[Test]
		public void UpdateCustomDebugUI_RedAgent_WritesEmptyDisplayToRedText()
		{
			var blueText = MakeText("BlueText");
			var redText   = MakeText("RedText");
			blueText.text = "stale";
			redText.text   = "stale";
			SetField("BlueCustomDebugText", blueText);
			SetField("RedCustomDebugText",   redText);
			gm.OnAgentToggleChanged(true);

			var redGo = MakeAgentGo(Constants.RED_ABBR, 1);
			SetProp("Agents", new Dictionary<int, GameObject> { { 1, redGo } });

			InvokeVoid("UpdateCustomDebugUI");

			Assert.AreEqual("", redText.text,
				"RedCustomDebugText should be set to empty when PlanningAgentDebugText is empty");
		}

		[Test]
		public void UpdateCustomDebugUI_AgentWithNullAgentField_SkipsWithoutChangingText()
		{
			var blueText = MakeText("BlueText");
			blueText.text = "unchanged";
			SetField("BlueCustomDebugText", blueText);
			SetField("RedCustomDebugText",   null);
			gm.OnAgentToggleChanged(true);

			// Create a GO with AgentController but leave Agent field null (not set via reflection)
			var agentGo = new GameObject("AgentWithNullAgent");
			createdObjects.Add(agentGo);
			agentGo.AddComponent<AgentController>(); // Agent private field stays null
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, agentGo } });

			Assert.DoesNotThrow(() => InvokeVoid("UpdateCustomDebugUI"),
				"UpdateCustomDebugUI should not throw when controller.Agent is null");
			Assert.AreEqual("unchanged", blueText.text,
				"BlueCustomDebugText should not be modified when the agent has no Agent component");
		}
	}
}
