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
	/// EditMode tests for GameManager round lifecycle methods:
	/// - UpdateTimerUI
	/// - SetAllAgentsInactive
	/// - DeclareRoundWinner (BLUE branch and RED branch)
	/// </summary>
	[TestFixture]
	public class GameManagerRoundLifecycleTests
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
				.SetValue(gm, value);

		private void SetField(string name, object value) =>
			typeof(GameManager)
				.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(gm, value);

		private void InvokeVoid(string methodName, params object[] args) =>
			typeof(GameManager)
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
			var blueGo = MakeAgentGo(Constants.BLUE_ABBR, 0);
			var redGo = MakeAgentGo(Constants.RED_ABBR, 1);

			// SetAllAgentsInactive calls CloseLogFile() → LogFileStream.Close().
			// Use unique DLL names so each agent writes to a distinct file.
			blueGo.GetComponent<AgentBridge>().OpenLogFile();
			redGo.GetComponent<AgentBridge>().OpenLogFile();

			SetProp("Agents", new Dictionary<int, GameObject> { { 0, blueGo }, { 1, redGo } });

			InvokeVoid("SetAllAgentsInactive");

			Assert.IsFalse(blueGo.activeSelf,
				"Blue agent GO should be inactive after SetAllAgentsInactive");
			Assert.IsFalse(redGo.activeSelf,
				"Red agent GO should be inactive after SetAllAgentsInactive");
		}

		// ── DeclareRoundWinner ─────────────────────────────────────────────────

		[Test]
		public void DeclareRoundWinner_BlueWins_IncrementsBlueScoreAndSetsShowingWinner()
		{
			var (gameOverUI, _) = MakeGameOverUI();
			var blueScoreText = MakeText("BlueScore");
			var redScoreText   = MakeText("RedScore");
			var prefabs = MakePrefabs();
			prefabs.GameOverUI      = gameOverUI;
			prefabs.BlueScoreText  = blueScoreText;
			prefabs.RedScoreText    = redScoreText;
			SetField("Prefabs", prefabs);

			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.BLUE_ABBR, 0 },
				{ Constants.RED_ABBR,   0 }
			});

			var blueGo = MakeAgentGo(Constants.BLUE_ABBR, 0);
			InvokeVoid("DeclareRoundWinner", blueGo);

			Assert.IsTrue(gameOverUI.GetComponent<Canvas>().enabled,
				"GameOverUI Canvas should be enabled after declaring a winner");
			Assert.AreEqual("1", blueScoreText.text,
				"BlueScoreText should show 1 after the first blue win");
			Assert.AreEqual(2, GetGameStateInt(),
				"gameState should be SHOWING_WINNER (2)");
			Assert.AreEqual(1.5f, GetTimeToDisplayBanner(), 0.001f,
				"TimeToDisplayBanner should be set to 1.5f");
		}

		[Test]
		public void DeclareRoundWinner_RedWins_IncrementsRedScoreAndSetsShowingWinner()
		{
			var (gameOverUI, _) = MakeGameOverUI();
			var blueScoreText = MakeText("BlueScore");
			var redScoreText   = MakeText("RedScore");
			var prefabs = MakePrefabs();
			prefabs.GameOverUI      = gameOverUI;
			prefabs.BlueScoreText  = blueScoreText;
			prefabs.RedScoreText    = redScoreText;
			SetField("Prefabs", prefabs);

			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.BLUE_ABBR, 1 },
				{ Constants.RED_ABBR,   0 }
			});

			var redGo = MakeAgentGo(Constants.RED_ABBR, 1);
			InvokeVoid("DeclareRoundWinner", redGo);

			Assert.IsTrue(gameOverUI.GetComponent<Canvas>().enabled,
				"GameOverUI Canvas should be enabled after declaring a winner");
			Assert.AreEqual("1", redScoreText.text,
				"RedScoreText should show 1 after the first red win");
			Assert.AreEqual(2, GetGameStateInt(),
				"gameState should be SHOWING_WINNER (2)");
		}
	}
}
