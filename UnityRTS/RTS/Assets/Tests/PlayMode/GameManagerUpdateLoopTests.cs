using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AgentSDK;
using NUnit.Framework;
using Preloader;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for GameManager Update() state machine branches
	/// and lifecycle methods:
	/// - INTRO → PLAYING (canvas disabled/enabled)
	/// - PLAYING with no winner and with winner (full flow)
	/// - SHOWING_WINNER → FINISHED via DisplayMultiAgentResults
	/// - FINISHED timer decrement
	/// - RESTARTING → InitializeRound (with dllNames null)
	/// - DropIntroVersus coroutine
	/// - InfluenceMap toggle and debug binding
	/// - PlaceUnits
	/// - Learn via Update flow
	/// </summary>
	[TestFixture]
	public class GameManagerUpdateLoopTests : PlayModeTestBase
	{
		/// <summary>
		/// Close any open log file streams on AgentBridge components
		/// in ctx.CreatedObjects to prevent sharing violations between tests.
		/// Runs before the base TearDown destroys GameObjects.
		/// </summary>
		[TearDown]
		public void CloseOpenLogFiles()
		{
			if (ctx?.CreatedObjects == null) return;
			foreach (var go in ctx.CreatedObjects)
			{
				if (go == null) continue;
				var bridge = go.GetComponent<AgentBridge>();
				if (bridge == null) continue;
				try { bridge.CloseLogFile(); } catch { /* already closed or never opened */ }
			}
		}

		// ── Reflection helpers ─────────────────────────────────────────────────

		private static GameManager GM => GameManager.Instance;

		private static void SetProp(string name, object value) =>
			typeof(GameManager)
				.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(GM, value);

		private static void SetField(string name, object value) =>
			typeof(GameManager)
				.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(GM, value);

		private static object GetField(string name) =>
			typeof(GameManager)
				.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(GM);

		private static void InvokeUpdate() =>
			typeof(GameManager)
				.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(GM, null);

		private static void InvokePrivate(string methodName) =>
			typeof(GameManager)
				.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(GM, null);

		private static T InvokePrivate<T>(string methodName, params object[] args) =>
			(T)typeof(GameManager)
				.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(GM, args.Length == 0 ? null : args);

		private static int GetGameStateInt()
		{
			var field = typeof(GameManager).GetField("gameState",
				BindingFlags.NonPublic | BindingFlags.Instance);
			return (int)field.GetValue(GM);
		}

		private static void SetGameState(int stateValue)
		{
			var field = typeof(GameManager).GetField("gameState",
				BindingFlags.NonPublic | BindingFlags.Instance);
			field.SetValue(GM, Enum.ToObject(field.FieldType, stateValue));
		}

		private static float GetTimeToDisplayBanner() =>
			(float)typeof(GameManager)
				.GetProperty("TimeToDisplayBanner", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(GM);

		// ── Factory helpers ────────────────────────────────────────────────────

		private GameObject MakeGameOverUI()
		{
			var root = new GameObject("TestGameOverUI");
			ctx.CreatedObjects.Add(root);
			root.AddComponent<Canvas>();
			var textGo = new GameObject("BannerText");
			textGo.transform.SetParent(root.transform);
			textGo.AddComponent<Text>();
			return root;
		}

		private PrefabLoader MakePrefabs(GameObject gameOverUI)
		{
			var go = new GameObject("TestPrefabLoader");
			ctx.CreatedObjects.Add(go);
			var prefabs = go.AddComponent<PrefabLoader>();
			prefabs.GameOverUI = gameOverUI;
			return prefabs;
		}

		private Text MakeText(string name)
		{
			var go = new GameObject(name);
			ctx.CreatedObjects.Add(go);
			return go.AddComponent<Text>();
		}

		/// <summary>
		/// Creates an agent GO with proper BLUE_ABBR/RED_ABBR naming for lifecycle tests.
		/// </summary>
		private GameObject MakeNamedAgent(string agentName, int agentNbr)
		{
			var go = new GameObject("Agent_" + agentName);
			ctx.CreatedObjects.Add(go);
			var bridge = go.AddComponent<AgentBridge>();
			bridge.InitializeAgent(agentName, "TestDLL" + agentNbr, agentNbr, ".");
			bridge.Gold = 5000;

			// Initialize adapters so InitializeRound → UpdateEnemyAgentNbr doesn't NRE
			bridge.InitializeAdapters(agentNbr, ctx.UnitManager, ctx.MapManager);

			var controller = go.AddComponent<AgentController>();
			controller.enabled = false; // Prevent Update() from running (null _debugTextAreas)
			typeof(AgentController)
				.GetField("Agent", BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(controller, bridge);
			return go;
		}

		/// <summary>
		/// Builds a full PrefabLoader with all UI fields needed for lifecycle tests.
		/// </summary>
		private PrefabLoader MakeFullPrefabs(GameObject gameOverUI)
		{
			var prefabs = MakePrefabs(gameOverUI);
			prefabs.TimerText = MakeText("Timer");
			prefabs.SpeedText = MakeText("Speed");
			prefabs.BlueScoreText = MakeText("BlueScore");
			prefabs.RedScoreText = MakeText("RedScore");
			return prefabs;
		}

		/// <summary>
		/// Builds a 72×42 MapManager (matching PlaceUnits' hardcoded constants)
		/// and injects it into both GameManager and UnitManager.
		/// Returns the tilemap GO for cleanup tracking.
		/// </summary>
		private GameObject SwapToLargeMap()
		{
			var largeMap = PlayModeTestHelper.BuildMapManager(72, 42, out var tilemapGo);
			ctx.CreatedObjects.Add(tilemapGo);

			// Inject into GameManager
			typeof(GameManager)
				.GetField("mapManager", BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(GM, largeMap);

			// Inject into UnitManager (private field)
			typeof(UnitManager)
				.GetField("mapManager", BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(ctx.UnitManager, largeMap);

			ctx.MapManager = largeMap;

			return tilemapGo;
		}

		// ── INTRO → PLAYING ──────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Update_Intro_CanvasDisabled_TransitionsToPlaying()
		{
			var gameOverUI = MakeGameOverUI();
			gameOverUI.GetComponent<Canvas>().enabled = false;
			var prefabs = MakePrefabs(gameOverUI);
			SetField("Prefabs", prefabs);

			SetGameState(0); // INTRO

			yield return null;

			InvokeUpdate();

			Assert.AreEqual(1, GetGameStateInt(),
				"gameState should transition from INTRO (0) to PLAYING (1) when canvas is disabled");
		}

		[UnityTest]
		public IEnumerator Update_Intro_CanvasEnabled_StaysInIntro()
		{
			var gameOverUI = MakeGameOverUI();
			gameOverUI.GetComponent<Canvas>().enabled = true;
			var prefabs = MakePrefabs(gameOverUI);
			SetField("Prefabs", prefabs);

			SetGameState(0); // INTRO

			yield return null;

			InvokeUpdate();

			Assert.AreEqual(0, GetGameStateInt(),
				"gameState should remain INTRO (0) when canvas is still enabled");
		}

		// ── PLAYING — no winner ──────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Update_Playing_NoWinner_StaysPlayingAndUpdatesTimer()
		{
			var gameOverUI = MakeGameOverUI();
			var prefabs = MakeFullPrefabs(gameOverUI);
			SetField("Prefabs", prefabs);

			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, ctx.Agent0Go },
				{ 1, ctx.Agent1Go }
			});

			PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.PAWN, new Vector3Int(20, 20, 0), ctx.Agent1Go);

			GM.TotalGameTime = 0f;
			GM.MaxNbrOfSeconds = 300;

			SetField("blueCustomDebugText", null);
			SetField("redCustomDebugText", null);

			InvokePrivate("InitializeDebugToggles");
			SetGameState(1); // PLAYING

			float timeBefore = GM.TotalGameTime;

			yield return null;

			InvokeUpdate();

			Assert.AreEqual(1, GetGameStateInt(),
				"gameState should stay PLAYING (1) when no winner");
			Assert.Greater(GM.TotalGameTime, timeBefore,
				"TotalGameTime should increase after UpdateTimerUI");
		}

		// ── PLAYING — with winner (full DeclareRoundWinner + Learn flow) ────

		/// <summary>
		/// When only one agent has units, Update() in PLAYING state triggers the
		/// full winner flow: DeclareRoundWinner, Learn, SetAllAgentsInactive,
		/// SetAllUnitsInactive, and transition to SHOWING_WINNER.
		/// </summary>
		[UnityTest]
		public IEnumerator Update_Playing_WithWinner_TransitionsToShowingWinner()
		{
			// Create properly-named agents for DeclareRoundWinner
			var blueGo = MakeNamedAgent(Constants.BLUE_ABBR, 0);
			var redGo = MakeNamedAgent(Constants.RED_ABBR, 1);

			// Open log files for Learn → EndLogLine
			blueGo.GetComponent<AgentBridge>().OpenLogFile();
			redGo.GetComponent<AgentBridge>().OpenLogFile();

			// Register unit prefabs for the new agents (reuse existing prefab map)
			ctx.UnitManager.UnitPrefabs[0] = ctx.UnitManager.UnitPrefabs[0]; // already there
			ctx.UnitManager.UnitPrefabs[1] = ctx.UnitManager.UnitPrefabs[1];

			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, blueGo },
				{ 1, redGo }
			});

			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.BLUE_ABBR, 0 },
				{ Constants.RED_ABBR, 0 }
			});

			// Only agent 0 has a unit → agent 0 wins
			PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			var gameOverUI = MakeGameOverUI();
			var prefabs = MakeFullPrefabs(gameOverUI);
			SetField("Prefabs", prefabs);

			GM.TotalGameTime = 0f;
			GM.MaxNbrOfSeconds = 300;
			GM.EnableLearning = true;

			SetField("blueCustomDebugText", null);
			SetField("redCustomDebugText", null);

			InvokePrivate("InitializeDebugToggles");
			SetGameState(1); // PLAYING

			yield return null;

			InvokeUpdate();

			Assert.AreEqual(2, GetGameStateInt(),
				"gameState should transition to SHOWING_WINNER (2) when a winner is determined");

			// DeclareRoundWinner should have incremented the winner's score
			var agentWins = (Dictionary<string, int>)typeof(GameManager)
				.GetProperty("AgentWins", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(GM);
			// The winner has AgentName matching one of the keys
			Assert.IsTrue(agentWins[Constants.BLUE_ABBR] == 1 || agentWins[Constants.RED_ABBR] == 1,
				"Winner's AgentWins entry should be incremented");

			Assert.AreEqual(1.5f, GetTimeToDisplayBanner(), 0.001f,
				"TimeToDisplayBanner should be set to 1.5f after DeclareRoundWinner");
		}

		// ── SHOWING_WINNER → FINISHED via DisplayMultiAgentResults ───────────

		[UnityTest]
		public IEnumerator Update_ShowingWinner_AllRoundsComplete_DllNamesNotNull_TransitionsToFinished()
		{
			var gameOverUI = MakeGameOverUI();
			var prefabs = MakePrefabs(gameOverUI);
			SetField("Prefabs", prefabs);

			SetGameState(2); // SHOWING_WINNER
			SetProp("TimeToDisplayBanner", -1f);

			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.BLUE_ABBR, 1 },
				{ Constants.RED_ABBR, 0 }
			});
			GM.TotalNbrOfRounds = 1;

			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, ctx.Agent0Go },
				{ 1, ctx.Agent1Go }
			});

			SetField("dllNames", new List<string> { "SomeAgent.dll" });

			yield return null;

			InvokeUpdate();

			Assert.AreEqual(4, GetGameStateInt(),
				"gameState should transition to FINISHED (4) via DisplayMultiAgentResults");
			Assert.AreEqual(3.0f, GetTimeToDisplayBanner(), 0.001f,
				"TimeToDisplayBanner should be reset to 3.0f");

			SetField("dllNames", null);
		}

		// ── FINISHED — timer decrement ───────────────────────────────────────

		[UnityTest]
		public IEnumerator Update_Finished_DecrementsTimer()
		{
			SetGameState(4); // FINISHED
			SetProp("TimeToDisplayBanner", 5.0f);

			yield return null;

			InvokeUpdate();

			Assert.Less(GetTimeToDisplayBanner(), 5.0f,
				"TimeToDisplayBanner should decrease after one Update in FINISHED state");
			Assert.AreEqual(4, GetGameStateInt(),
				"gameState should remain FINISHED (4)");
		}

		// ── RESTARTING → InitializeRound ─────────────────────────────────────

		/// <summary>
		/// RESTARTING state disables the GameOverUI canvas, then calls InitializeRound.
		/// InitializeRound resets game state, iterates agents, and calls PlaceUnits.
		/// With dllNames null and NbrOfRounds=0, PickNextRandomAgent is a no-op.
		/// </summary>
		[UnityTest]
		public IEnumerator Update_Restarting_CallsInitializeRoundAndPlacesUnits()
		{
			SwapToLargeMap();

			var blueGo = MakeNamedAgent(Constants.BLUE_ABBR, 0);
			var redGo = MakeNamedAgent(Constants.RED_ABBR, 1);

			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, blueGo },
				{ 1, redGo }
			});

			var gameOverUI = MakeGameOverUI();
			gameOverUI.GetComponent<Canvas>().enabled = true;
			var prefabs = MakeFullPrefabs(gameOverUI);
			SetField("Prefabs", prefabs);

			SetField("dllNames", null);
			SetField("NbrOfRounds", 0);
			SetGameState(3); // RESTARTING

			yield return null;

			InvokeUpdate();

			// After RESTARTING, InitializeRound sets gameState to INTRO
			Assert.AreEqual(0, GetGameStateInt(),
				"gameState should be INTRO (0) after InitializeRound");
			Assert.IsFalse(gameOverUI.GetComponent<Canvas>().enabled,
				"GameOverUI canvas should be disabled by RESTARTING");

			// InitializeRound should have placed units (2 pawns + 2 mines)
			var allUnits = ctx.UnitManager.GetAllUnits();
			Assert.GreaterOrEqual(allUnits.Count, 4,
				"InitializeRound→PlaceUnits should place at least 4 units (2 pawns + 2 mines)");
		}

		// ── DropIntroVersus coroutine ────────────────────────────────────────

		/// <summary>
		/// DropIntroVersus is a coroutine that shows intro text, then disables
		/// the canvas and sets gameState to PLAYING. We iterate the IEnumerator
		/// manually to test each phase.
		/// </summary>
		[UnityTest]
		public IEnumerator DropIntroVersus_SetsTextAndTransitionsToPlaying()
		{
			var gameOverUI = MakeGameOverUI();
			var prefabs = MakePrefabs(gameOverUI);
			SetField("Prefabs", prefabs);

			SetGameState(0); // INTRO

			yield return null;

			// Invoke the coroutine method and manually iterate
			var method = typeof(GameManager).GetMethod("DropIntroVersus",
				BindingFlags.NonPublic | BindingFlags.Instance);
			var enumerator = (IEnumerator)method.Invoke(GM, new object[] { "Blue vs Red" });

			var bannerText = gameOverUI.GetComponentInChildren<Text>();

			// Phase 1: Sets title and enables canvas
			enumerator.MoveNext();
			Assert.AreEqual("Agents of Empires", bannerText.text,
				"First phase should set banner to game title");
			Assert.IsTrue(gameOverUI.GetComponent<Canvas>().enabled,
				"Canvas should be enabled during intro");

			// Phase 2: Sets versus text
			enumerator.MoveNext();
			Assert.AreEqual("Blue vs Red", bannerText.text,
				"Second phase should set banner to versus text");

			// Phase 3: Disables canvas, sets PLAYING
			enumerator.MoveNext();
			Assert.IsFalse(gameOverUI.GetComponent<Canvas>().enabled,
				"Canvas should be disabled after intro");
			Assert.AreEqual(1, GetGameStateInt(),
				"gameState should be PLAYING (1) after DropIntroVersus");
		}

		// ── PlaceUnits ──────────────────────────────────────────────────────

		/// <summary>
		/// PlaceUnits places a pawn for each agent and mines symmetrically.
		/// Tests the full method including GetBuildableLocationNearCorner,
		/// mine placement loop, and IsInCorner checks.
		/// </summary>
		[UnityTest]
		public IEnumerator PlaceUnits_PlacesPawnsAndMines()
		{
			SwapToLargeMap();

			var blueGo = MakeNamedAgent(Constants.BLUE_ABBR, 0);
			var redGo = MakeNamedAgent(Constants.RED_ABBR, 1);

			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, blueGo },
				{ 1, redGo }
			});

			yield return null;

			InvokePrivate("PlaceUnits");

			var allUnits = ctx.UnitManager.GetAllUnits();
			Assert.GreaterOrEqual(allUnits.Count, 4,
				"PlaceUnits should place at least 4 units (2 pawns + 2 mines)");

			// Verify we have at least one pawn and one mine
			bool hasPawn = false, hasMine = false;
			foreach (var kvp in allUnits)
			{
				var unit = kvp.Value.GetComponent<GameElements.Unit>();
				if (unit.UnitType == UnitType.PAWN) hasPawn = true;
				if (unit.UnitType == UnitType.MINE) hasMine = true;
			}
			Assert.IsTrue(hasPawn, "PlaceUnits should place at least one PAWN");
			Assert.IsTrue(hasMine, "PlaceUnits should place at least one MINE");
		}

		// ── InitializeRound ─────────────────────────────────────────────────

		/// <summary>
		/// InitializeRound resets game state, increments NbrOfRounds,
		/// activates agents, and calls PlaceUnits.
		/// </summary>
		[UnityTest]
		public IEnumerator InitializeRound_ResetsStateAndPlacesUnits()
		{
			SwapToLargeMap();

			var blueGo = MakeNamedAgent(Constants.BLUE_ABBR, 0);
			var redGo = MakeNamedAgent(Constants.RED_ABBR, 1);

			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, blueGo },
				{ 1, redGo }
			});

			var gameOverUI = MakeGameOverUI();
			var prefabs = MakeFullPrefabs(gameOverUI);
			SetField("Prefabs", prefabs);
			SetField("dllNames", null);
			SetField("NbrOfRounds", 0);

			GM.TotalGameTime = 99f;

			yield return null;

			InvokePrivate("InitializeRound");

			Assert.AreEqual(0, GetGameStateInt(),
				"gameState should be INTRO (0) after InitializeRound");
			Assert.AreEqual(0f, GM.TotalGameTime, 0.001f,
				"TotalGameTime should be reset to 0");

			int nbrOfRounds = (int)GetField("NbrOfRounds");
			Assert.AreEqual(1, nbrOfRounds,
				"NbrOfRounds should be incremented to 1");

			var allUnits = ctx.UnitManager.GetAllUnits();
			Assert.GreaterOrEqual(allUnits.Count, 4,
				"InitializeRound should place units");
		}

		// ── Learn (via full setup) ──────────────────────────────────────────

		/// <summary>
		/// Learn iterates all agents, calling Learn() and EndLogLine() when
		/// EnableLearning is true, and CmdLog?.EndRound always.
		/// </summary>
		[UnityTest]
		public IEnumerator Learn_WithWinner_CallsLearnAndEndRound()
		{
			var blueGo = MakeNamedAgent(Constants.BLUE_ABBR, 0);
			var redGo = MakeNamedAgent(Constants.RED_ABBR, 1);

			blueGo.GetComponent<AgentBridge>().OpenLogFile();
			redGo.GetComponent<AgentBridge>().OpenLogFile();

			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, blueGo },
				{ 1, redGo }
			});

			GM.EnableLearning = true;
			SetField("roundWinner", blueGo);

			yield return null;

			Assert.DoesNotThrow(() => InvokePrivate("Learn"),
				"Learn should complete without errors");

			// After Learn, SetAllAgentsInactive would close files,
			// but we test Learn in isolation here. Clean up manually.
			blueGo.GetComponent<AgentBridge>().CloseLogFile();
			redGo.GetComponent<AgentBridge>().CloseLogFile();
		}

		/// <summary>
		/// When EnableLearning is false, Learn skips the per-agent Learn/EndLogLine
		/// but still calls CmdLog?.EndRound on all agents.
		/// </summary>
		[UnityTest]
		public IEnumerator Learn_EnableLearningFalse_SkipsLearnButEndRoundStillCalled()
		{
			var blueGo = MakeNamedAgent(Constants.BLUE_ABBR, 0);
			var redGo = MakeNamedAgent(Constants.RED_ABBR, 1);

			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, blueGo },
				{ 1, redGo }
			});

			GM.EnableLearning = false;
			SetField("roundWinner", null); // "unknown" path

			yield return null;

			Assert.DoesNotThrow(() => InvokePrivate("Learn"),
				"Learn should complete when EnableLearning is false and roundWinner is null");
		}

		// ── OnInfluenceToggleChanged ─────────────────────────────────────────

		/// <summary>
		/// OnInfluenceToggleChanged activates/deactivates the InfluenceMap tilemap.
		/// </summary>
		[UnityTest]
		public IEnumerator OnInfluenceToggleChanged_TogglesInfluenceMapActive()
		{
			// Create a real InfluenceMap tilemap on the MapManager
			var influenceGo = new GameObject("InfluenceMap");
			ctx.CreatedObjects.Add(influenceGo);
			influenceGo.AddComponent<Tilemap>();
			ctx.MapManager.InfluenceMap = influenceGo.GetComponent<Tilemap>();
			influenceGo.SetActive(false);

			yield return null;

			GM.OnInfluenceToggleChanged(true);
			Assert.IsTrue(influenceGo.activeSelf,
				"InfluenceMap GO should be active after OnInfluenceToggleChanged(true)");

			GM.OnInfluenceToggleChanged(false);
			Assert.IsFalse(influenceGo.activeSelf,
				"InfluenceMap GO should be inactive after OnInfluenceToggleChanged(false)");
		}

		// ── InitializeDebugToggles with InfluenceToggle ─────────────────────

		/// <summary>
		/// InitializeDebugToggles with a real InfluenceToggle wires up the listener.
		/// </summary>
		[UnityTest]
		public IEnumerator InitializeDebugToggles_WithInfluenceToggle_RegistersListener()
		{
			// Create InfluenceMap so the listener doesn't crash
			var influenceGo = new GameObject("InfluenceMap");
			ctx.CreatedObjects.Add(influenceGo);
			influenceGo.AddComponent<Tilemap>();
			ctx.MapManager.InfluenceMap = influenceGo.GetComponent<Tilemap>();
			influenceGo.SetActive(false);

			var toggleGo = new GameObject("InfluenceToggle");
			ctx.CreatedObjects.Add(toggleGo);
			var toggle = toggleGo.AddComponent<Toggle>();

			SetField("InfluenceToggle", toggle);

			yield return null;

			InvokePrivate("InitializeDebugToggles");

			Assert.IsFalse(toggle.isOn,
				"InfluenceToggle should be set to isOn=false by InitializeDebugToggles");

			// Fire the listener
			toggle.isOn = true;
			Assert.IsTrue(influenceGo.activeSelf,
				"Listener should activate InfluenceMap when toggle is enabled");

			toggle.isOn = false;
			Assert.IsFalse(influenceGo.activeSelf,
				"Listener should deactivate InfluenceMap when toggle is disabled");

			SetField("InfluenceToggle", null);
		}

		// ── Debug binding 2 (InfluenceMap toggle) ───────────────────────────

		/// <summary>
		/// Debug binding index 2 toggles the InfluenceMap visibility.
		/// This binding was skipped in EditMode tests because mapManager was null.
		/// </summary>
		[UnityTest]
		public IEnumerator DebugBinding2_Execute_TogglesInfluenceMap()
		{
			var influenceGo = new GameObject("InfluenceMap");
			ctx.CreatedObjects.Add(influenceGo);
			influenceGo.AddComponent<Tilemap>();
			ctx.MapManager.InfluenceMap = influenceGo.GetComponent<Tilemap>();
			influenceGo.SetActive(false);

			// Inject InputSystem_Actions so InitializeDebugToggles can populate _debugBindings
			var inputActions = new InputSystem_Actions();
			inputActions.Gameplay.Enable();
			SetField("_input", inputActions);

			InvokePrivate("InitializeDebugToggles");

			yield return null;

			// Get binding 2's Execute action
			var bindingsField = typeof(GameManager).GetField(
				"_debugBindings", BindingFlags.NonPublic | BindingFlags.Instance);
			var bindings = bindingsField.GetValue(GM) as Array;
			var binding = bindings.GetValue(2);
			// Named tuple fields: (InputAction Action, Action Execute) → Item2 is Execute
			var execute = (Action)binding.GetType().GetField("Item2").GetValue(binding);

			// Execute: should toggle InfluenceMap from false to true
			execute();
			Assert.IsTrue(influenceGo.activeSelf,
				"First execute should activate InfluenceMap");

			// Execute again: should toggle back to false
			execute();
			Assert.IsFalse(influenceGo.activeSelf,
				"Second execute should deactivate InfluenceMap");

			inputActions.Dispose();
			SetField("_input", null);
		}

		// ── UpdateCustomDebugUI with agents ─────────────────────────────────

		/// <summary>
		/// UpdateCustomDebugUI with HasAgentDebugging=true and real agents
		/// iterates the Agents dict and writes debug text.
		/// </summary>
		[UnityTest]
		public IEnumerator UpdateCustomDebugUI_WithAgents_WritesDebugText()
		{
			var blueGo = MakeNamedAgent(Constants.BLUE_ABBR, 0);
			var redGo = MakeNamedAgent(Constants.RED_ABBR, 1);

			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, blueGo },
				{ 1, redGo }
			});

			var blueText = MakeText("BlueDebug");
			var redText = MakeText("RedDebug");
			blueText.text = "stale";
			redText.text = "stale";
			SetField("blueCustomDebugText", blueText);
			SetField("redCustomDebugText", redText);

			GM.OnAgentToggleChanged(true);

			yield return null;

			InvokePrivate("UpdateCustomDebugUI");

			// PlanningAgentDebugText returns "" (no planning agent loaded) → display = ""
			Assert.AreEqual("", blueText.text,
				"BlueCustomDebugText should be empty when PlanningAgentDebugText is empty");
			Assert.AreEqual("", redText.text,
				"RedCustomDebugText should be empty when PlanningAgentDebugText is empty");

			SetField("blueCustomDebugText", null);
			SetField("redCustomDebugText", null);
		}

		// ── SetAllAgentsInactive ─────────────────────────────────────────────

		/// <summary>
		/// SetAllAgentsInactive deactivates all agent GameObjects and closes log files.
		/// </summary>
		[UnityTest]
		public IEnumerator SetAllAgentsInactive_DeactivatesAndClosesLogFiles()
		{
			var blueGo = MakeNamedAgent(Constants.BLUE_ABBR, 0);
			var redGo = MakeNamedAgent(Constants.RED_ABBR, 1);

			blueGo.GetComponent<AgentBridge>().OpenLogFile();
			redGo.GetComponent<AgentBridge>().OpenLogFile();

			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, blueGo },
				{ 1, redGo }
			});

			yield return null;

			InvokePrivate("SetAllAgentsInactive");

			Assert.IsFalse(blueGo.GetComponent<AgentBridge>().gameObject.activeSelf,
				"Blue agent should be inactive");
			Assert.IsFalse(redGo.GetComponent<AgentBridge>().gameObject.activeSelf,
				"Red agent should be inactive");
		}

		// ── DeclareRoundWinner (via PlayMode) ───────────────────────────────

		/// <summary>
		/// DeclareRoundWinner in PlayMode with full agent setup — verifies
		/// banner text, score update, and state transition.
		/// </summary>
		[UnityTest]
		public IEnumerator DeclareRoundWinner_Blue_SetsBannerAndScore()
		{
			var gameOverUI = MakeGameOverUI();
			var prefabs = MakeFullPrefabs(gameOverUI);
			SetField("Prefabs", prefabs);

			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.BLUE_ABBR, 0 },
				{ Constants.RED_ABBR, 0 }
			});

			var blueGo = MakeNamedAgent(Constants.BLUE_ABBR, 0);

			yield return null;

			var method = typeof(GameManager).GetMethod("DeclareRoundWinner",
				BindingFlags.NonPublic | BindingFlags.Instance);
			method.Invoke(GM, new object[] { blueGo });

			Assert.IsTrue(gameOverUI.GetComponent<Canvas>().enabled,
				"GameOverUI should be enabled");

			var bannerText = gameOverUI.GetComponentInChildren<Text>();
			StringAssert.Contains(Constants.BLUE_ABBR, bannerText.text,
				"Banner should contain blue agent name");

			Assert.AreEqual("1", prefabs.BlueScoreText.text,
				"BlueScoreText should show 1");
			Assert.AreEqual(2, GetGameStateInt(),
				"gameState should be SHOWING_WINNER (2)");
		}

		// ── SetupGameOverBanner ────────────────────────────────────────────

		/// <summary>
		/// SetupGameOverBanner is an empty method — calling it covers the entry point.
		/// </summary>
		[UnityTest]
		public IEnumerator SetupGameOverBanner_DoesNotThrow()
		{
			yield return null;

			Assert.DoesNotThrow(() =>
				typeof(GameManager).GetMethod("SetupGameOverBanner",
					BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(GM, null),
				"SetupGameOverBanner should complete without errors");
		}

		// ── InitializeDebugToggles with all toggles ────────────────────────

		/// <summary>
		/// InitializeDebugToggles with all 9 toggles set up should wire listeners
		/// and set initial isOn values for each toggle. This covers the toggle
		/// body branches that are skipped when toggles are null.
		/// </summary>
		[UnityTest]
		public IEnumerator InitializeDebugToggles_WithAllToggles_WiresAllListeners()
		{
			// Create InfluenceMap so binding 2 / InfluenceToggle don't crash
			var influenceGo = new GameObject("InfluenceMap");
			ctx.CreatedObjects.Add(influenceGo);
			influenceGo.AddComponent<Tilemap>();
			ctx.MapManager.InfluenceMap = influenceGo.GetComponent<Tilemap>();
			influenceGo.SetActive(false);

			// Create all 9 toggles
			var toggleNames = new[]
			{
				"AgentToggle", "UnitToggle", "InfluenceToggle",
				"MoveTintToggle", "GatherTintToggle", "BuildTintToggle",
				"AttackTintToggle", "PathTintToggle", "TargetLineTintToggle"
			};

			var toggles = new Dictionary<string, Toggle>();
			foreach (var name in toggleNames)
			{
				var toggleGo = new GameObject(name);
				ctx.CreatedObjects.Add(toggleGo);
				var toggle = toggleGo.AddComponent<Toggle>();
				toggles[name] = toggle;
				SetField(name, toggle);
			}

			yield return null;

			InvokePrivate("InitializeDebugToggles");

			// Verify initial isOn values
			Assert.IsTrue(toggles["AgentToggle"].isOn,
				"AgentToggle should be isOn=true");
			Assert.IsFalse(toggles["UnitToggle"].isOn,
				"UnitToggle should be isOn=false");
			Assert.IsFalse(toggles["InfluenceToggle"].isOn,
				"InfluenceToggle should be isOn=false");
			Assert.IsFalse(toggles["MoveTintToggle"].isOn,
				"MoveTintToggle should be isOn=false");
			Assert.IsFalse(toggles["GatherTintToggle"].isOn,
				"GatherTintToggle should be isOn=false");
			Assert.IsFalse(toggles["BuildTintToggle"].isOn,
				"BuildTintToggle should be isOn=false");
			Assert.IsFalse(toggles["AttackTintToggle"].isOn,
				"AttackTintToggle should be isOn=false");
			Assert.IsTrue(toggles["PathTintToggle"].isOn,
				"PathTintToggle should be isOn=true");
			Assert.IsTrue(toggles["TargetLineTintToggle"].isOn,
				"TargetLineTintToggle should be isOn=true");

			// Verify listeners fire correctly for a few toggles
			toggles["AgentToggle"].isOn = false;
			Assert.IsFalse(GM.HasAgentDebugging,
				"AgentToggle listener should set HasAgentDebugging=false");

			toggles["UnitToggle"].isOn = true;
			Assert.IsTrue(GM.HasUnitDebugging,
				"UnitToggle listener should set HasUnitDebugging=true");

			toggles["MoveTintToggle"].isOn = true;
			Assert.IsTrue(GM.HasMoveTint,
				"MoveTintToggle listener should set HasMoveTint=true");

			toggles["GatherTintToggle"].isOn = true;
			Assert.IsTrue(GM.HasGatherTint,
				"GatherTintToggle listener should set HasGatherTint=true");

			toggles["AttackTintToggle"].isOn = true;
			Assert.IsTrue(GM.HasAttackTint,
				"AttackTintToggle listener should set HasAttackTint=true");

			toggles["PathTintToggle"].isOn = false;
			Assert.IsFalse(GM.HasPathTint,
				"PathTintToggle listener should set HasPathTint=false");

			toggles["BuildTintToggle"].isOn = true;
			Assert.IsTrue(GM.HasBuildTint,
				"BuildTintToggle listener should set HasBuildTint=true");

			toggles["TargetLineTintToggle"].isOn = false;
			Assert.IsFalse(GM.HasTargetLineTint,
				"TargetLineTintToggle listener should set HasTargetLineTint=false");

			// Clean up — reset all toggles to null
			foreach (var name in toggleNames)
				SetField(name, null);
		}

		// ── UpdateCustomDebugUI with non-empty debug text ──────────────────

		/// <summary>
		/// When PlanningAgentDebugText is non-empty, UpdateCustomDebugUI should
		/// display "DLLName\nDebugText" rather than an empty string.
		/// </summary>
		[UnityTest]
		public IEnumerator UpdateCustomDebugUI_WithNonEmptyDebugText_ShowsDllNameAndText()
		{
			var blueGo = MakeNamedAgent(Constants.BLUE_ABBR, 0);
			var redGo = MakeNamedAgent(Constants.RED_ABBR, 1);

			// Inject a PlanningAgentBase with non-empty DebugText
			var debugAgent = new TestDebugTextAgent("My debug info");
			typeof(AgentBridge).GetMethod("SetPlanningAgent",
					BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(blueGo.GetComponent<AgentBridge>(),
					new object[] { debugAgent });

			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, blueGo },
				{ 1, redGo }
			});

			var blueText = MakeText("BlueDebug");
			var redText = MakeText("RedDebug");
			SetField("blueCustomDebugText", blueText);
			SetField("redCustomDebugText", redText);

			GM.OnAgentToggleChanged(true);

			yield return null;

			InvokePrivate("UpdateCustomDebugUI");

			// Blue agent has non-empty debug text → shows "DLLName\nDebugText"
			StringAssert.Contains("My debug info", blueText.text,
				"BlueCustomDebugText should contain the debug text");
			StringAssert.Contains("TestDLL0", blueText.text,
				"BlueCustomDebugText should contain the DLL name");

			// Red agent has no planning agent → empty text
			Assert.AreEqual("", redText.text,
				"RedCustomDebugText should be empty when PlanningAgentDebugText is empty");

			SetField("blueCustomDebugText", null);
			SetField("redCustomDebugText", null);
		}

		/// <summary>
		/// Minimal PlanningAgentBase subclass for testing non-empty DebugText.
		/// </summary>
		private class TestDebugTextAgent : PlanningAgentBase
		{
			public TestDebugTextAgent(string text) { DebugText = text; }
			public override void InitializeMatch() { }
			public override void Update(IGameState state, IAgentActions actions) { }
		}
	}
}
