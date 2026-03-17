using System.Collections;
using System.Collections.Generic;
using System.IO;
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
	/// Tests for GameManager.Awake() and InitializeMatch() — the full startup path.
	/// These tests create a complete synthetic environment (Grid, PrefabLoader, DLLs)
	/// and invoke Awake via reflection to cover the initialization code.
	/// </summary>
	[TestFixture]
	public class GameManagerAwakeTests
	{
		private List<GameObject> createdObjects = new List<GameObject>();
		private GameManager prevInstance;

		[SetUp]
		public void SetUp()
		{
			// Save existing singleton (may be null)
			prevInstance = GameManager.Instance;
		}

		[UnityTearDown]
		public IEnumerator TearDown()
		{
			// Clean up agents and units created by Awake/InitializeMatch
			if (GameManager.Instance != null && GameManager.Instance != prevInstance)
			{
				// Set state to FINISHED so Update early-returns
				var gsField = typeof(GameManager).GetField("gameState",
					BindingFlags.NonPublic | BindingFlags.Instance);
				gsField.SetValue(GameManager.Instance,
					System.Enum.ToObject(gsField.FieldType, 4)); // FINISHED

				// Destroy units
				try { GameManager.Instance.Units?.DestroyAllUnits(); } catch { }

				// Destroy agent GOs (Instantiated clones + LoadDLL GOs)
				var agents = typeof(GameManager)
					.GetProperty("Agents", BindingFlags.NonPublic | BindingFlags.Instance)
					?.GetValue(GameManager.Instance) as Dictionary<int, GameObject>;
				if (agents != null)
				{
					var agentField = typeof(AgentController)
						.GetField("Agent", BindingFlags.NonPublic | BindingFlags.Instance);

					foreach (var agentGo in agents.Values)
					{
						if (agentGo == null) continue;
						// Destroy the inner Agent GO (created by LoadDLL)
						var controller = agentGo.GetComponent<AgentController>();
						if (controller != null && agentField != null)
						{
							var agent = agentField.GetValue(controller) as Agent;
							if (agent != null && agent.gameObject != agentGo)
								Object.DestroyImmediate(agent.gameObject);
						}
						// Destroy the AgentController GO (Instantiated clone)
						Object.DestroyImmediate(agentGo);
					}
				}
			}

			// Destroy all tracked objects
			foreach (var go in createdObjects)
				if (go != null) Object.Destroy(go);
			createdObjects.Clear();

			yield return null;

			// Restore previous singleton
			typeof(GameManager)
				.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)
				.SetValue(null, prevInstance);
		}

		private GameObject Track(GameObject go)
		{
			createdObjects.Add(go);
			return go;
		}

		private Text MakeText(string name)
		{
			var go = Track(new GameObject(name));
			return go.AddComponent<Text>();
		}

		/// <summary>
		/// Creates a Grid GO with a "Ground 1" tilemap (72×42 with tiles) and
		/// an InfluenceMap tilemap tagged "InfluenceMap".
		/// This is what MapManager.GenerateGraph expects.
		/// </summary>
		private GameObject MakeGrid(int width, int height)
		{
			var gridGo = Track(new GameObject("Grid"));
			gridGo.AddComponent<Grid>();

			// Ground tilemap — GenerateGraph looks for a child named "Ground 1"
			var groundGo = new GameObject("Ground 1");
			groundGo.transform.SetParent(gridGo.transform);
			var groundTilemap = groundGo.AddComponent<Tilemap>();
			groundGo.AddComponent<TilemapRenderer>();

			// Fill every cell with a tile so the whole map is walkable/buildable
			var tile = ScriptableObject.CreateInstance<Tile>();
			for (int x = 0; x < width; x++)
				for (int y = 0; y < height; y++)
					groundTilemap.SetTile(new Vector3Int(x, y, 0), tile);

			// InfluenceMap tilemap — GenerateGraph looks for tag "InfluenceMap"
			var influenceGo = new GameObject("InfluenceMap");
			influenceGo.transform.SetParent(gridGo.transform);
			influenceGo.AddComponent<Tilemap>();
			influenceGo.tag = "InfluenceMap";

			return gridGo;
		}

		/// <summary>
		/// Creates a player prefab with an AgentController component
		/// (CreateAgent calls Instantiate on this and expects AgentController).
		/// </summary>
		private GameObject MakePlayerPrefab(string name)
		{
			var go = Track(new GameObject(name));
			go.AddComponent<AgentController>();
			return go;
		}

		/// <summary>
		/// Creates a minimal unit prefab (SpriteRenderer + Animator).
		/// </summary>
		private GameObject MakeUnitPrefab(string name)
		{
			var go = Track(new GameObject("Prefab_" + name));
			go.AddComponent<SpriteRenderer>();
			go.AddComponent<Animator>();
			return go;
		}

		/// <summary>
		/// Creates a fully-populated PrefabLoader with all fields needed by Awake/InitializeMatch.
		/// </summary>
		private PrefabLoader MakeFullPrefabLoader(GameObject grid)
		{
			var go = Track(new GameObject("PrefabLoader"));
			var prefabs = go.AddComponent<PrefabLoader>();

			// GameOverUI
			var gameOverUI = Track(new GameObject("GameOverUI"));
			gameOverUI.AddComponent<Canvas>();
			var textChild = new GameObject("BannerText");
			textChild.transform.SetParent(gameOverUI.transform);
			textChild.AddComponent<Text>();
			prefabs.GameOverUI = gameOverUI;

			// Grid
			prefabs.Grid = grid;

			// Player prefabs
			prefabs.BluePlayerPrefab = MakePlayerPrefab("BluePlayer");
			prefabs.RedPlayerPrefab = MakePlayerPrefab("RedPlayer");

			// Unit prefabs — all types for both colors
			prefabs.MinePrefab = MakeUnitPrefab("Mine");
			prefabs.BluePawnPrefab = MakeUnitPrefab("BluePawn");
			prefabs.BlueWarriorPrefab = MakeUnitPrefab("BlueWarrior");
			prefabs.BlueArcherPrefab = MakeUnitPrefab("BlueArcher");
			prefabs.BlueBasePrefab = MakeUnitPrefab("BlueBase");
			prefabs.BlueBarracksPrefab = MakeUnitPrefab("BlueBarracks");
			prefabs.BlueArcheryPrefab = MakeUnitPrefab("BlueArchery");
			prefabs.BlueLancerPrefab = MakeUnitPrefab("BlueLancer");
			prefabs.BlueTowerPrefab = MakeUnitPrefab("BlueTower");
			prefabs.RedPawnPrefab = MakeUnitPrefab("RedPawn");
			prefabs.RedWarriorPrefab = MakeUnitPrefab("RedWarrior");
			prefabs.RedArcherPrefab = MakeUnitPrefab("RedArcher");
			prefabs.RedBasePrefab = MakeUnitPrefab("RedBase");
			prefabs.RedBarracksPrefab = MakeUnitPrefab("RedBarracks");
			prefabs.RedArcheryPrefab = MakeUnitPrefab("RedArchery");
			prefabs.RedLancerPrefab = MakeUnitPrefab("RedLancer");
			prefabs.RedTowerPrefab = MakeUnitPrefab("RedTower");

			// UnitDebuggerPrefab — Unit.InitializeDebuggingUI expects Canvas + named Text children
			var debugPrefab = Track(new GameObject("UnitDebuggerPrefab"));
			var canvas = debugPrefab.AddComponent<Canvas>();
			canvas.enabled = false;
			foreach (var n in new[] { "Unit Number", "State Label", "State Variable", "Health Value" })
			{
				var child = new GameObject(n);
				child.transform.SetParent(debugPrefab.transform);
				child.AddComponent<Text>();
			}
			prefabs.UnitDebuggerPrefab = debugPrefab;

			// Text fields
			prefabs.TimerText = MakeText("Timer");
			prefabs.SpeedText = MakeText("Speed");
			prefabs.BlueScoreText = MakeText("BlueScore");
			prefabs.RedScoreText = MakeText("RedScore");
			prefabs.BlueLabelText = MakeText("BlueLabel");
			prefabs.RedLabelText = MakeText("RedLabel");

			return prefabs;
		}

		private static void SetField(object target, string name, object value)
		{
			typeof(GameManager)
				.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(target, value);
		}

		/// <summary>
		/// Close agent log files to prevent sharing violations between tests.
		/// Uses reflection because AgentController.Agent is internal to GameManager assembly.
		/// </summary>
		private static void CloseAgentLogs(GameManager gm)
		{
			var agents = typeof(GameManager)
				.GetProperty("Agents", BindingFlags.NonPublic | BindingFlags.Instance)
				?.GetValue(gm) as Dictionary<int, GameObject>;
			if (agents == null) return;

			var agentField = typeof(AgentController)
				.GetField("Agent", BindingFlags.NonPublic | BindingFlags.Instance);

			foreach (var agentGo in agents.Values)
			{
				if (agentGo == null) continue;
				var controller = agentGo.GetComponent<AgentController>();
				if (controller == null) continue;
				var bridge = agentField?.GetValue(controller) as AgentBridge;
				if (bridge == null) continue;
				try { bridge.CloseLogFile(); } catch { }
				try { bridge.CloseCommandLog(); } catch { }
			}
		}

		private const string TEST_DLL = "Dummy";

		/// <summary>
		/// Checks whether the Dummy DLL exists and skips the test if not.
		/// </summary>
		private static void SkipIfNoDll()
		{
			string dllDir = Application.dataPath
				+ Path.AltDirectorySeparatorChar + ".."
				+ Path.AltDirectorySeparatorChar + ".."
				+ Path.AltDirectorySeparatorChar + "EnemyAgents";
			string dll = dllDir + Path.AltDirectorySeparatorChar + "PlanningAgent_" + TEST_DLL + ".dll";
			if (!File.Exists(dll))
				Assert.Ignore($"PlanningAgent_{TEST_DLL}.dll not found — skipping Awake integration test");
		}

		/// <summary>
		/// Creates an inactive GameManager GO with all infrastructure and returns it.
		/// Call gmGo.SetActive(true) to trigger Awake().
		/// </summary>
		private (GameObject gmGo, GameManager gm, PrefabLoader prefabs) BuildAwakeTestGM(
			string goName, int totalRounds = 3, bool randomizeRed = false)
		{
			var gmGo = Track(new GameObject(goName));
			gmGo.SetActive(false);
			var gm = gmGo.AddComponent<GameManager>();

			typeof(GameManager)
				.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)
				.SetValue(null, gm);

			gm.BlueDllName = TEST_DLL;
			gm.RedDllName = randomizeRed ? "" : TEST_DLL;
			gm.TotalNbrOfRounds = totalRounds;
			gm.StartingGameSpeed = 1;
			gm.StartingPlayerGold = 1000;
			gm.StartingMineGold = 10000;
			gm.RandomizeAgentsAsRed = randomizeRed;

			var grid = MakeGrid(72, 42);
			var prefabs = MakeFullPrefabLoader(grid);
			SetField(gm, "Prefabs", prefabs);
			SetField(gm, "BlueCustomDebugText", MakeText("BlueDebug_" + goName));
			SetField(gm, "RedCustomDebugText", MakeText("RedDebug_" + goName));

			return (gmGo, gm, prefabs);
		}

		// ── Tests ─────────────────────────────────────────────────────────────

		/// <summary>
		/// Awake() on a fully-configured GameManager should initialize sub-managers,
		/// build unit prefab dictionaries, run InitializeMatch (loading DLLs, creating
		/// agents, placing units), and transition to INTRO state.
		/// </summary>
		[UnityTest]
		public IEnumerator Awake_WithFullSetup_InitializesGameAndLoadsAgents()
		{
			SkipIfNoDll();

			var (gmGo, gm, prefabs) = BuildAwakeTestGM("AwakeTestGM");

			gmGo.SetActive(true);
			yield return null;

			// Singleton and sub-managers
			Assert.IsNotNull(GameManager.Instance, "Singleton should be set");
			Assert.AreEqual(3, gm.TotalNbrOfRounds,
				"Odd TotalNbrOfRounds should remain unchanged");
			Assert.IsNotNull(gm.Map, "MapManager should be initialized");
			Assert.IsNotNull(gm.Units, "UnitManager should be initialized");
			Assert.IsNotNull(gm.Events, "EventDispatcher should be initialized");

			// Map generated correctly
			Assert.AreEqual(72, gm.Map.MapSize.x, "Map width should be 72");
			Assert.AreEqual(42, gm.Map.MapSize.y, "Map height should be 42");

			// Agents created
			var agents = (Dictionary<int, GameObject>)typeof(GameManager)
				.GetProperty("Agents", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(gm);
			Assert.AreEqual(2, agents.Count, "Two agents should be created");

			// AgentWins initialized
			var agentWins = (Dictionary<string, int>)typeof(GameManager)
				.GetProperty("AgentWins", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(gm);
			Assert.IsTrue(agentWins.ContainsKey(Constants.BLUE_ABBR), "AgentWins should have BLUE entry");
			Assert.IsTrue(agentWins.ContainsKey(Constants.RED_ABBR), "AgentWins should have RED entry");

			// Units placed by InitializeRound → PlaceUnits
			Assert.GreaterOrEqual(gm.Units.GetAllUnits().Count, 4,
				"PlaceUnits should place at least 4 units (2 pawns + 2 mines)");

			// InfluenceMap disabled
			Assert.IsFalse(gm.Map.InfluenceMap.gameObject.activeSelf,
				"InfluenceMap should be deactivated");

			// Unit prefab dicts populated with all 9 types
			Assert.AreEqual(9, gm.Units.BlueUnitPrefabs.Count,
				"BlueUnitPrefabs should have 9 entries");
			Assert.AreEqual(9, gm.Units.RedUnitPrefabs.Count,
				"RedUnitPrefabs should have 9 entries");

			// Label texts show DLL name
			Assert.AreEqual(TEST_DLL, prefabs.BlueLabelText.text);
			Assert.AreEqual(TEST_DLL, prefabs.RedLabelText.text);

			CloseAgentLogs(gm);
		}

		/// <summary>
		/// When TotalNbrOfRounds is even, Awake should increment it to make it odd.
		/// </summary>
		[UnityTest]
		public IEnumerator Awake_EvenRounds_IncrementsToOdd()
		{
			SkipIfNoDll();

			var (gmGo, gm, _) = BuildAwakeTestGM("AwakeTestGM_Even", totalRounds: 4);

			gmGo.SetActive(true);
			yield return null;

			Assert.AreEqual(5, gm.TotalNbrOfRounds,
				"Even TotalNbrOfRounds (4) should be incremented to 5");

			CloseAgentLogs(gm);
		}

		/// <summary>
		/// Awake with RandomizeAgentsAsRed=true should load DLL names from the
		/// EnemyAgents directory and pick one at random for the red agent.
		/// </summary>
		[UnityTest]
		public IEnumerator Awake_RandomizeAgentsAsRed_LoadsDllNamesAndPicksRed()
		{
			SkipIfNoDll();

			var (gmGo, gm, _) = BuildAwakeTestGM("AwakeTestGM_Random",
				totalRounds: 1, randomizeRed: true);

			gmGo.SetActive(true);
			yield return null;

			// RedDllName should have been set to a random DLL from the directory
			Assert.IsNotEmpty(gm.RedDllName,
				"RedDllName should be set to a randomly selected DLL name");

			// dllNames should have been populated
			var dllNames = (List<string>)typeof(GameManager)
				.GetField("dllNames", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(gm);
			Assert.IsNotNull(dllNames, "dllNames should be populated");
			Assert.Greater(dllNames.Count, 0, "dllNames should contain at least one DLL");

			// isBlueUsingDllNames should be false
			var isBlue = (bool)typeof(GameManager)
				.GetField("isBlueUsingDllNames", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(gm);
			Assert.IsFalse(isBlue,
				"isBlueUsingDllNames should be false when RandomizeAgentsAsRed is true");

			CloseAgentLogs(gm);
		}
	}
}
