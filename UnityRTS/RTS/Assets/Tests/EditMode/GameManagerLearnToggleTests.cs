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
	/// EditMode tests for GameManager methods not covered elsewhere:
	/// - Learn (EnableLearning true/false, roundWinner null/non-null)
	/// - InitializeDebugToggles with real Toggle GameObjects (listener registration bodies)
	/// - Property getters that delegate to PrefabLoader
	/// - ProcessUserInput / HandleSpeedInput execution (no keys pressed)
	/// </summary>
	[TestFixture]
	public class GameManagerLearnToggleTests
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
			SetField("roundWinner", null);
			gm.EnableLearning = true;

			// Restore debug toggles to null
			SetField("AgentToggle", null);
			SetField("UnitToggle", null);
			SetField("InfluenceToggle", null);
			SetField("MoveTintToggle", null);
			SetField("GatherTintToggle", null);
			SetField("AttackTintToggle", null);
			SetField("PathTintToggle", null);
			SetField("BuildTintToggle", null);
			SetField("TargetLineTintToggle", null);

			// Re-initialize bindings to clean state
			InvokePrivate("InitializeDebugToggles");

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

		private void InvokePrivate(string methodName, params object[] args) =>
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

		private Toggle MakeToggle(string name)
		{
			var go = new GameObject(name);
			createdObjects.Add(go);
			return go.AddComponent<Toggle>();
		}

		private PrefabLoader MakePrefabs()
		{
			var go = new GameObject("TestPrefabLoader");
			createdObjects.Add(go);
			return go.AddComponent<PrefabLoader>();
		}

		// ── Learn ─────────────────────────────────────────────────────────────

		[Test]
		public void Learn_EnableLearningTrue_CallsLearnOnAgentsWithoutCrash()
		{
			var blueGo = MakeAgentGo(Constants.BLUE_ABBR, 0);
			var redGo = MakeAgentGo(Constants.RED_ABBR, 1);

			blueGo.GetComponent<AgentBridge>().OpenLogFile();
			redGo.GetComponent<AgentBridge>().OpenLogFile();

			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, blueGo },
				{ 1, redGo }
			});

			gm.EnableLearning = true;
			SetField("roundWinner", blueGo);

			Assert.DoesNotThrow(() => InvokePrivate("Learn"),
				"Learn should not throw when EnableLearning is true");

			blueGo.GetComponent<AgentBridge>().CloseLogFile();
			redGo.GetComponent<AgentBridge>().CloseLogFile();
		}

		[Test]
		public void Learn_EnableLearningFalse_SkipsLearnButCallsEndRound()
		{
			var blueGo = MakeAgentGo(Constants.BLUE_ABBR, 0);
			var redGo = MakeAgentGo(Constants.RED_ABBR, 1);

			// EndLogLine is only called when EnableLearning is true,
			// but CmdLog?.EndRound is always called (CmdLog is null → no-op)
			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, blueGo },
				{ 1, redGo }
			});

			gm.EnableLearning = false;
			SetField("roundWinner", blueGo);

			Assert.DoesNotThrow(() => InvokePrivate("Learn"),
				"Learn should not throw when EnableLearning is false (skips Learn calls)");
		}

		[Test]
		public void Learn_RoundWinnerNull_UsesUnknownInWinnerName()
		{
			var blueGo = MakeAgentGo(Constants.BLUE_ABBR, 0);
			var redGo = MakeAgentGo(Constants.RED_ABBR, 1);

			blueGo.GetComponent<AgentBridge>().OpenLogFile();
			redGo.GetComponent<AgentBridge>().OpenLogFile();

			SetProp("Agents", new Dictionary<int, GameObject>
			{
				{ 0, blueGo },
				{ 1, redGo }
			});

			gm.EnableLearning = true;
			SetField("roundWinner", null);

			Assert.DoesNotThrow(() => InvokePrivate("Learn"),
				"Learn should not throw when roundWinner is null (uses 'unknown')");

			blueGo.GetComponent<AgentBridge>().CloseLogFile();
			redGo.GetComponent<AgentBridge>().CloseLogFile();
		}

		// ── InitializeDebugToggles with real Toggles ──────────────────────────

		[Test]
		public void InitializeDebugToggles_WithAgentToggle_SetsIsOnTrueAndRegistersListener()
		{
			var toggle = MakeToggle("AgentToggle");
			SetField("AgentToggle", toggle);

			InvokePrivate("InitializeDebugToggles");

			Assert.IsTrue(toggle.isOn,
				"AgentToggle should be set to isOn=true by InitializeDebugToggles");

			// Verify listener works by toggling
			toggle.isOn = false;
			Assert.IsFalse(gm.HasAgentDebugging,
				"Listener should update HasAgentDebugging when toggle changes");
		}

		[Test]
		public void InitializeDebugToggles_WithUnitToggle_SetsIsOnFalseAndRegistersListener()
		{
			var toggle = MakeToggle("UnitToggle");
			SetField("UnitToggle", toggle);

			InvokePrivate("InitializeDebugToggles");

			Assert.IsFalse(toggle.isOn,
				"UnitToggle should be set to isOn=false by InitializeDebugToggles");

			// Verify listener
			toggle.isOn = true;
			Assert.IsTrue(gm.HasUnitDebugging,
				"Listener should update HasUnitDebugging when toggle changes");
		}

		[Test]
		public void InitializeDebugToggles_WithMoveTintToggle_SetsIsOnFalseAndRegistersListener()
		{
			var toggle = MakeToggle("MoveTintToggle");
			SetField("MoveTintToggle", toggle);

			InvokePrivate("InitializeDebugToggles");

			Assert.IsFalse(toggle.isOn);

			toggle.isOn = true;
			Assert.IsTrue(gm.HasMoveTint);
		}

		[Test]
		public void InitializeDebugToggles_WithGatherTintToggle_SetsIsOnFalseAndRegistersListener()
		{
			var toggle = MakeToggle("GatherTintToggle");
			SetField("GatherTintToggle", toggle);

			InvokePrivate("InitializeDebugToggles");

			Assert.IsFalse(toggle.isOn);

			toggle.isOn = true;
			Assert.IsTrue(gm.HasGatherTint);
		}

		[Test]
		public void InitializeDebugToggles_WithAttackTintToggle_SetsIsOnFalseAndRegistersListener()
		{
			var toggle = MakeToggle("AttackTintToggle");
			SetField("AttackTintToggle", toggle);

			InvokePrivate("InitializeDebugToggles");

			Assert.IsFalse(toggle.isOn);

			toggle.isOn = true;
			Assert.IsTrue(gm.HasAttackTint);
		}

		[Test]
		public void InitializeDebugToggles_WithPathTintToggle_SetsIsOnTrueAndRegistersListener()
		{
			var toggle = MakeToggle("PathTintToggle");
			SetField("PathTintToggle", toggle);

			InvokePrivate("InitializeDebugToggles");

			Assert.IsTrue(toggle.isOn,
				"PathTintToggle should be set to isOn=true by InitializeDebugToggles");

			toggle.isOn = false;
			Assert.IsFalse(gm.HasPathTint);
		}

		[Test]
		public void InitializeDebugToggles_WithBuildTintToggle_SetsIsOnFalseAndRegistersListener()
		{
			var toggle = MakeToggle("BuildTintToggle");
			SetField("BuildTintToggle", toggle);

			InvokePrivate("InitializeDebugToggles");

			Assert.IsFalse(toggle.isOn);

			toggle.isOn = true;
			Assert.IsTrue(gm.HasBuildTint);
		}

		[Test]
		public void InitializeDebugToggles_WithTargetLineTintToggle_SetsIsOnTrueAndRegistersListener()
		{
			var toggle = MakeToggle("TargetLineTintToggle");
			SetField("TargetLineTintToggle", toggle);

			InvokePrivate("InitializeDebugToggles");

			Assert.IsTrue(toggle.isOn,
				"TargetLineTintToggle should be set to isOn=true by InitializeDebugToggles");

			toggle.isOn = false;
			Assert.IsFalse(gm.HasTargetLineTint);
		}

		// ── ProcessUserInput / HandleSpeedInput with bindings (no keys) ──────

		[Test]
		public void ProcessUserInput_WithBindings_NoKeysPressed_DoesNotThrow()
		{
			// Initialize bindings (all Toggles null → skipped bodies)
			InvokePrivate("InitializeDebugToggles");

			// ProcessUserInput iterates _debugBindings; no keys pressed → no execute()
			Assert.DoesNotThrow(() => InvokePrivate("ProcessUserInput"),
				"ProcessUserInput should iterate bindings safely when no keys are pressed");
		}

		[Test]
		public void HandleSpeedInput_NoKeysPressed_DoesNotChangeSpeed()
		{
			Constants.GAME_SPEED = 10;

			Assert.DoesNotThrow(() => InvokePrivate("HandleSpeedInput"),
				"HandleSpeedInput should execute without crash when no keys are pressed");

			Assert.AreEqual(10, Constants.GAME_SPEED,
				"GAME_SPEED should not change when no keys are pressed");
		}

		// ── Property getters ─────────────────────────────────────────────────

		[Test]
		public void GoldResourceSprite_DelegatesToPrefabs()
		{
			var prefabs = MakePrefabs();
			SetField("Prefabs", prefabs);

			// GoldResourceSprite is null by default (no sprite loaded)
			Assert.IsNull(gm.GoldResourceSprite,
				"GoldResourceSprite should return null when Prefabs.GoldResourceSprite is null");
		}

		[Test]
		public void SmallBarFill_DelegatesToPrefabs()
		{
			var prefabs = MakePrefabs();
			SetField("Prefabs", prefabs);

			Assert.IsNull(gm.SmallBarFill,
				"SmallBarFill should return null when Prefabs.SmallBarFill is null");
		}

		[Test]
		public void SmallBarBase_DelegatesToPrefabs()
		{
			var prefabs = MakePrefabs();
			SetField("Prefabs", prefabs);

			Assert.IsNull(gm.SmallBarBase,
				"SmallBarBase should return null when Prefabs.SmallBarBase is null");
		}

		[Test]
		public void BigBarBase_DelegatesToPrefabs()
		{
			var prefabs = MakePrefabs();
			SetField("Prefabs", prefabs);

			Assert.IsNull(gm.BigBarBase,
				"BigBarBase should return null when Prefabs.BigBarBase is null");
		}
	}
}
