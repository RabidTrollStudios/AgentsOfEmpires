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
	/// EditMode tests for GameManager UI and match-support methods:
	/// - UpdateTimerUI
	/// - SetAllAgentsInactive
	/// - DeclareRoundWinner (HUMAN branch and ORC branch)
	/// - DisplaySingleAgentResults (agent0 wins / agent1 wins)
	/// - DisplayMultiAgentResults (agent0 is ORC / agent0 is HUM)
	/// - UpdateCustomDebugUI foreach body (HUM agent, ORC agent, null-Agent skip)
	/// - GetBuildableLocationNearCorner (open map / blocked origin)
	/// - GetRandomBuildableLocationExcludingCorners
	/// All require private state injection via reflection.
	/// </summary>
	[TestFixture]
	public class GameManagerMatchTests
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
			// Restore injected private state
			SetField("Prefabs", null);
			SetProp("Agents", null);
			SetProp("AgentWins", null);
			SetField("mapManager", null);
			SetField("HumanCustomDebugText", null);
			SetField("OrcCustomDebugText", null);

			// Restore game state to PLAYING (0) and timer
			var stateField = typeof(GameManager).GetField("gameState",
				BindingFlags.NonPublic | BindingFlags.Instance);
			stateField.SetValue(gm, System.Enum.ToObject(stateField.FieldType, 0));
			SetProp("TimeToDisplayBanner", 0f);
			gm.TotalNbrOfRounds = 3;
			gm.TotalGameTime = 0f;
			Constants.GAME_SPEED = 1;
			Constants.CalculateGameConstants();

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

		private T Invoke<T>(string methodName, params object[] args) =>
			(T)typeof(GameManager)
				.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(gm, args.Length == 0 ? null : args);

		private int GetGameStateInt()
		{
			var field = typeof(GameManager).GetField("gameState",
				BindingFlags.NonPublic | BindingFlags.Instance);
			return (int)field.GetValue(gm);
		}

		private float GetTimeToDisplayBanner() =>
			(float)typeof(GameManager)
				.GetProperty("TimeToDisplayBanner", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(gm);

		// ── Factory helpers ────────────────────────────────────────────────────

		private GameObject MakeAgentGo(string agentName, int agentNbr)
		{
			var go = new GameObject("Agent_" + agentName);
			createdObjects.Add(go);
			var bridge = go.AddComponent<AgentBridge>();
			// Use a unique DLL name per agent so their log-file paths don't collide when
			// OpenLogFile() is called (each agent appends to "PlanningAgent_<dllName>.csv").
			bridge.InitializeAgent(agentName, "TestDLL" + agentNbr, agentNbr, ".");
			var controller = go.AddComponent<AgentController>();
			typeof(AgentController)
				.GetField("Agent", BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(controller, bridge);
			return go;
		}

		/// <summary>
		/// Creates a minimal GameOverUI: a GameObject with a Canvas component and a Text child.
		/// GameManager reads the Canvas to enable/disable the overlay and GetComponentInChildren&lt;Text&gt;()
		/// to write the banner message.
		/// </summary>
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

		// ── UpdateTimerUI ──────────────────────────────────────────────────────

		[Test]
		public void UpdateTimerUI_SetsTimerTextAndSpeedText()
		{
			var timerText = MakeText("TimerText");
			var speedText = MakeText("SpeedText");
			var prefabs = MakePrefabs();
			prefabs.TimerText = timerText;
			prefabs.SpeedText = speedText;
			SetField("Prefabs", prefabs);

			gm.TotalGameTime = 5f;
			Constants.GAME_SPEED = 10;

			InvokeVoid("UpdateTimerUI");

			// UpdateTimerUI adds Time.deltaTime * GAME_SPEED to TotalGameTime before formatting.
			// Read the actual post-call value so the assertion is independent of deltaTime.
			Assert.AreEqual(gm.TotalGameTime.ToString("0.00000"), timerText.text,
				"TimerText should display the post-call TotalGameTime formatted to 5 decimal places");
			Assert.AreEqual("10", speedText.text,
				"SpeedText should display the current GAME_SPEED");
		}

		// ── SetAllAgentsInactive ───────────────────────────────────────────────

		[Test]
		public void SetAllAgentsInactive_DeactivatesAllAgentGameObjects()
		{
			var humGo = MakeAgentGo(Constants.HUMAN_ABBR, 0);
			var orcGo = MakeAgentGo(Constants.ORC_ABBR, 1);

			// SetAllAgentsInactive calls CloseLogFile() → LogFileStream.Close().
			// MakeAgentGo uses DLL names "TestDLL0" / "TestDLL1" so each agent writes to a
			// distinct file (PlanningAgent_TestDLL0[_N].csv vs PlanningAgent_TestDLL1[_N].csv).
			humGo.GetComponent<AgentBridge>().OpenLogFile();
			orcGo.GetComponent<AgentBridge>().OpenLogFile();

			SetProp("Agents", new Dictionary<int, GameObject> { { 0, humGo }, { 1, orcGo } });

			InvokeVoid("SetAllAgentsInactive");

			Assert.IsFalse(humGo.activeSelf,
				"Human agent GO should be inactive after SetAllAgentsInactive");
			Assert.IsFalse(orcGo.activeSelf,
				"Orc agent GO should be inactive after SetAllAgentsInactive");
		}

		// ── DeclareRoundWinner ─────────────────────────────────────────────────

		[Test]
		public void DeclareRoundWinner_HumanWins_IncrementsHumanScoreAndSetsShowingWinner()
		{
			var (gameOverUI, _) = MakeGameOverUI();
			var humanScoreText = MakeText("HumanScore");
			var orcScoreText   = MakeText("OrcScore");
			var prefabs = MakePrefabs();
			prefabs.GameOverUI      = gameOverUI;
			prefabs.HumanScoreText  = humanScoreText;
			prefabs.OrcScoreText    = orcScoreText;
			SetField("Prefabs", prefabs);

			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.HUMAN_ABBR, 0 },
				{ Constants.ORC_ABBR,   0 }
			});

			var humGo = MakeAgentGo(Constants.HUMAN_ABBR, 0);
			InvokeVoid("DeclareRoundWinner", humGo);

			Assert.IsTrue(gameOverUI.GetComponent<Canvas>().enabled,
				"GameOverUI Canvas should be enabled after declaring a winner");
			Assert.AreEqual("1", humanScoreText.text,
				"HumanScoreText should show 1 after the first human win");
			Assert.AreEqual(1, GetGameStateInt(),
				"gameState should be SHOWING_WINNER (1)");
			Assert.AreEqual(1.5f, GetTimeToDisplayBanner(), 0.001f,
				"TimeToDisplayBanner should be set to 1.5f");
		}

		[Test]
		public void DeclareRoundWinner_OrcWins_IncrementsOrcScoreAndSetsShowingWinner()
		{
			var (gameOverUI, _) = MakeGameOverUI();
			var humanScoreText = MakeText("HumanScore");
			var orcScoreText   = MakeText("OrcScore");
			var prefabs = MakePrefabs();
			prefabs.GameOverUI      = gameOverUI;
			prefabs.HumanScoreText  = humanScoreText;
			prefabs.OrcScoreText    = orcScoreText;
			SetField("Prefabs", prefabs);

			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.HUMAN_ABBR, 1 },
				{ Constants.ORC_ABBR,   0 }
			});

			var orcGo = MakeAgentGo(Constants.ORC_ABBR, 1);
			InvokeVoid("DeclareRoundWinner", orcGo);

			Assert.IsTrue(gameOverUI.GetComponent<Canvas>().enabled,
				"GameOverUI Canvas should be enabled after declaring a winner");
			Assert.AreEqual("1", orcScoreText.text,
				"OrcScoreText should show 1 after the first orc win");
			Assert.AreEqual(1, GetGameStateInt(),
				"gameState should be SHOWING_WINNER (1)");
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
			// → display = "" → HumanCustomDebugText.text = ""
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

		// ── GetBuildableLocationNearCorner ─────────────────────────────────────

		[Test]
		public void GetBuildableLocationNearCorner_OpenMap_ReturnsCornerPositionAtRadiusZero()
		{
			var (mapManager, tilemapGo) = MapManagerTestHelper.Build(30, 30);
			createdObjects.Add(tilemapGo);
			SetField("mapManager", mapManager);

			// On a fully open map at radius 0 the corner cell itself is buildable and valid.
			var result = Invoke<Vector3Int>("GetBuildableLocationNearCorner", 1, 1, UnitType.WORKER);

			Assert.AreEqual(new Vector3Int(1, 1, 0), result,
				"Should return the corner position directly when the map is fully open");
		}

		[Test]
		public void GetBuildableLocationNearCorner_OriginCellBlocked_SearchesOutwardAndReturnsValidCell()
		{
			// Block only (1,1) so radius 0 fails and the search expands to radius 1.
			var (mapManager, tilemapGo) = MapManagerTestHelper.Build(30, 30,
				new[] { (1, 1) });
			createdObjects.Add(tilemapGo);
			SetField("mapManager", mapManager);

			var result = Invoke<Vector3Int>("GetBuildableLocationNearCorner", 1, 1, UnitType.WORKER);

			Assert.AreNotEqual(new Vector3Int(1, 1, 0), result,
				"Should not return the blocked cell");
			bool inBounds = result.x >= 0 && result.x < 30 && result.y >= 0 && result.y < 30;
			Assert.IsTrue(inBounds, $"Result {result} must be within the 30x30 map bounds");
		}

		// ── GetRandomBuildableLocationExcludingCorners ─────────────────────────

		[Test]
		public void GetRandomBuildableLocationExcludingCorners_NeverReturnsCornerLocation()
		{
			var (mapManager, tilemapGo) = MapManagerTestHelper.Build(30, 30);
			createdObjects.Add(tilemapGo);
			SetField("mapManager", mapManager);

			for (int i = 0; i < 5; i++)
			{
				var result = Invoke<Vector3Int>(
					"GetRandomBuildableLocationExcludingCorners", UnitType.WORKER);

				bool inLowerLeftCorner  = result.x < 5 && result.y < 5;
				bool inUpperRightCorner = result.x >= 25 && result.y >= 25;
				Assert.IsFalse(inLowerLeftCorner || inUpperRightCorner,
					$"Iteration {i}: result {result} must not be inside a corner zone");
			}
		}
	}
}
