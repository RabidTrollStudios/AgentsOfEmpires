using System.Collections.Generic;
using System.Reflection;
using AgentSDK;
using GameManager.GameElements;
using GameManager.Graph;
using Preloader;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Holds all GameObjects created during test setup so they can be torn down.
	/// </summary>
	public class PlayModeTestContext
	{
		public GameObject GameManagerGo;
		public GameObject TilemapGo;
		public GameObject Agent0Go;
		public GameObject Agent1Go;
		public MapManager MapManager;
		public UnitManager UnitManager;
		public List<GameObject> CreatedObjects = new List<GameObject>();

		public Agent GetAgent(int agentNbr)
		{
			var go = agentNbr == 0 ? Agent0Go : Agent1Go;
			return go.GetComponent<AgentController>().Agent;
		}
	}

	/// <summary>
	/// Builds a synthetic game environment for Play Mode tests.
	/// Avoids loading the heavy main.unity scene by constructing
	/// all required objects programmatically.
	/// </summary>
	internal static class PlayModeTestHelper
	{
		private const int MAP_WIDTH = 30;
		private const int MAP_HEIGHT = 30;
		private const int TEST_GAME_SPEED = 20;

		internal static PlayModeTestContext BuildTestEnvironment()
		{
			var ctx = new PlayModeTestContext();

			// 1. Create GameManager singleton on an INACTIVE GameObject.
			// Awake() never runs on inactive GOs — it would crash creating sub-managers
			// with null Prefabs. We inject our own sub-managers via reflection instead.
			ctx.GameManagerGo = new GameObject("TestGameManager");
			ctx.GameManagerGo.SetActive(false);
			var gm = ctx.GameManagerGo.AddComponent<GameManager>();

			// Force-set the singleton. The constructor should do this, but may not
			// run reliably on an inactive GO in all Unity versions.
			typeof(GameManager)
				.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)
				.SetValue(null, gm);

			// Set serialized fields
			gm.StartingPlayerGold = 5000;
			gm.StartingMineGold = 10000;
			Constants.GAME_SPEED = TEST_GAME_SPEED;
			Constants.CalculateGameConstants();

			// Disable unit debugging via reflection (HasUnitDebugging has private set)
			typeof(GameManager)
				.GetProperty("HasUnitDebugging", BindingFlags.Public | BindingFlags.Instance)
				.SetValue(gm, false);
			typeof(GameManager)
				.GetProperty("HasAgentDebugging", BindingFlags.Public | BindingFlags.Instance)
				.SetValue(gm, false);

			// 2. Build MapManager with synthetic grid (same pattern as EditMode MapManagerTestHelper)
			ctx.MapManager = BuildMapManager(out ctx.TilemapGo);

			// 3. Build PrefabLoader with minimal prefabs
			var prefabLoaderGo = new GameObject("TestPrefabLoader");
			ctx.CreatedObjects.Add(prefabLoaderGo);
			var prefabs = prefabLoaderGo.AddComponent<PrefabLoader>();
			prefabs.UnitDebuggerPrefab = CreateUnitDebuggerPrefab(ctx);

			// Create minimal unit prefabs for all types
			var unitPrefabMap = new Dictionary<UnitType, GameObject>();
			foreach (UnitType ut in System.Enum.GetValues(typeof(UnitType)))
			{
				var prefab = CreateMinimalUnitPrefab(ut.ToString(), ctx);
				unitPrefabMap[ut] = prefab;
			}

			// 4. Build UnitManager
			ctx.UnitManager = new UnitManager(ctx.MapManager, prefabs);

			// 5. Build EventDispatcher
			var eventDispatcher = new EventDispatcher(ctx.UnitManager, ctx.MapManager);

			// 6. Inject sub-managers into GameManager via reflection
			SetPrivateField(gm, "mapManager", ctx.MapManager);
			SetPrivateField(gm, "unitManager", ctx.UnitManager);
			SetPrivateField(gm, "eventDispatcher", eventDispatcher);

			// 7. Create test agents
			ctx.Agent0Go = CreateTestAgent(ctx, 0, "TestPlayer0", unitPrefabMap);
			ctx.Agent1Go = CreateTestAgent(ctx, 1, "TestPlayer1", unitPrefabMap);

			return ctx;
		}

		internal static void TearDown(PlayModeTestContext ctx)
		{
			if (ctx == null) return;

			// Destroy all units first (uses Object.Destroy, deferred)
			if (ctx.UnitManager != null)
			{
				ctx.UnitManager.DestroyAllUnits();
			}

			// Destroy all tracked objects
			foreach (var go in ctx.CreatedObjects)
			{
				if (go != null) Object.Destroy(go);
			}

			if (ctx.TilemapGo != null) Object.Destroy(ctx.TilemapGo);
			if (ctx.Agent0Go != null) Object.Destroy(ctx.Agent0Go);
			if (ctx.Agent1Go != null) Object.Destroy(ctx.Agent1Go);
			if (ctx.GameManagerGo != null) Object.Destroy(ctx.GameManagerGo);

			// Clear the singleton so the next test gets a fresh instance
			typeof(GameManager)
				.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)
				.SetValue(null, null);
		}

		/// <summary>
		/// Place a unit via UnitManager and return the Unit component.
		/// </summary>
		internal static Unit PlaceUnit(PlayModeTestContext ctx, GameObject agentGo,
			UnitType unitType, Vector3Int gridPosition)
		{
			var go = ctx.UnitManager.PlaceUnit(agentGo, gridPosition, unitType, Color.white);
			return go.GetComponent<Unit>();
		}

		#region Private Helpers

		private static MapManager BuildMapManager(out GameObject tilemapGo)
		{
			var manager = new MapManager();
			var mapSize = new Vector3Int(MAP_WIDTH, MAP_HEIGHT, 0);

			typeof(MapManager)
				.GetProperty("MapSize", BindingFlags.Public | BindingFlags.Instance)
				.SetValue(manager, mapSize);

			tilemapGo = new GameObject("TestTilemap");
			tilemapGo.AddComponent<Grid>();
			var tilemap = tilemapGo.AddComponent<Tilemap>();

			var cells = new GridCell[MAP_WIDTH, MAP_HEIGHT];
			for (int x = 0; x < MAP_WIDTH; x++)
				for (int y = 0; y < MAP_HEIGHT; y++)
					cells[x, y] = new GridCell(tilemap, new Vector3Int(x, y, 0));

			typeof(MapManager)
				.GetProperty("GridCells", BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(manager, cells);

			// Build graph with edges
			var graph = new Graph<GridCell>();
			for (int x = 0; x < MAP_WIDTH; x++)
				for (int y = 0; y < MAP_HEIGHT; y++)
					graph.AddNode(Utility.GridToInt(new Vector3Int(x, y, 0), mapSize), cells[x, y]);

			for (int x = 0; x < MAP_WIDTH; x++)
			{
				for (int y = 0; y < MAP_HEIGHT; y++)
				{
					for (int dx = -1; dx <= 1; dx++)
					{
						for (int dy = -1; dy <= 1; dy++)
						{
							if (dx == 0 && dy == 0) continue;
							int nx = x + dx, ny = y + dy;
							if (nx >= 0 && ny >= 0 && nx < MAP_WIDTH && ny < MAP_HEIGHT)
							{
								graph.AddEdge(
									Utility.GridToInt(new Vector3Int(x, y, 0), mapSize),
									Utility.GridToInt(new Vector3Int(nx, ny, 0), mapSize),
									Vector3.Distance(cells[x, y].GetPosition(),
													 cells[nx, ny].GetPosition()));
							}
						}
					}
				}
			}

			typeof(MapManager)
				.GetProperty("Graph", BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(manager, graph);

			return manager;
		}

		private static GameObject CreateUnitDebuggerPrefab(PlayModeTestContext ctx)
		{
			var go = new GameObject("UnitDebuggerPrefab");
			ctx.CreatedObjects.Add(go);

			var canvas = go.AddComponent<Canvas>();
			canvas.enabled = false;

			// Unit.UpdateDebuggingInfo expects these named Text children
			string[] textNames = { "Unit Number", "State Label", "State Variable", "Health Value" };
			foreach (string name in textNames)
			{
				var textGo = new GameObject(name);
				textGo.transform.SetParent(go.transform);
				textGo.AddComponent<Text>();
			}

			return go;
		}

		private static GameObject CreateMinimalUnitPrefab(string name, PlayModeTestContext ctx)
		{
			var go = new GameObject("Prefab_" + name);
			ctx.CreatedObjects.Add(go);
			go.AddComponent<Animator>();
			go.AddComponent<SpriteRenderer>();
			return go;
		}

		private static GameObject CreateTestAgent(PlayModeTestContext ctx, int agentNbr,
			string agentName, Dictionary<UnitType, GameObject> unitPrefabMap)
		{
			var agentGo = new GameObject(agentName);

			// Add AgentBridge (concrete subclass of abstract Agent) and initialize
			var agent = agentGo.AddComponent<AgentBridge>();
			agent.InitializeAgent(agentName, "TestDLL", agentNbr, ".");
			agent.Gold = 5000;

			// Add AgentController and wire up the Agent field.
			// Disable it so its Update() doesn't run (it references debuggerCanvas which is null in tests).
			var controller = agentGo.AddComponent<AgentController>();
			controller.enabled = false;
			typeof(AgentController)
				.GetField("Agent", BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(controller, agent);

			// Register unit prefabs for this agent
			ctx.UnitManager.UnitPrefabs[agentNbr] = unitPrefabMap;

			return agentGo;
		}

		private static void SetPrivateField(object target, string fieldName, object value)
		{
			var field = target.GetType().GetField(fieldName,
				BindingFlags.NonPublic | BindingFlags.Instance);
			field.SetValue(target, value);
		}

		#endregion
	}
}
