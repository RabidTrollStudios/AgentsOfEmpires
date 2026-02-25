using AgentSDK;
using GameManager.GameElements;
using GameManager.Graph;
using Preloader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace GameManager
{
	/// <summary>
	/// Orchestrates the game: manages match/round lifecycle, agents, and delegates
	/// to specialized managers for map, units, events, and DLL loading.
	/// </summary>
	public partial class GameManager : MonoBehaviour
	{
		#region Public GameObjects

		/// <summary>
		/// Name of the DLL to use for the Humans
		/// </summary>
		[Header("Player Settings")]

		[SerializeField] public string HumanDllName;

		/// <summary>
		/// Name of the DLL to use for the Orcs
		/// </summary>
		[SerializeField] public string OrcDllName;

		/// <summary>
		/// Should matches be played against random agents?
		/// </summary>
		[SerializeField] public bool RandomizeAgentsAsOrc;

		/// <summary>
		/// Starting gold for each player
		/// </summary>
		[Header("Game Settings")]

		[SerializeField] public int StartingPlayerGold = 1000;

		/// <summary>
		/// Amount of starting gold in each mine
		/// </summary>
		[SerializeField] public int StartingMineGold = 10000;

		/// <summary>
		/// Number of mines at the start of the game
		/// </summary>
		[SerializeField] public int NumberOfMines = 2;

		/// <summary>
		/// Starting Game Speed
		/// </summary>
		[SerializeField] public int StartingGameSpeed = 1;

		/// <summary>
		/// Number of competition rounds
		/// </summary>
		[SerializeField] public int TotalNbrOfRounds = 3;

		/// <summary>
		/// Maximum number of seconds a game may run
		/// </summary>
		[SerializeField] public int MaxNbrOfSeconds = 300;

		/// <summary>
		/// Color for the GM's log statements
		/// </summary>
		[SerializeField] private string GameManagerLogColor = "cyan";

		/// <summary>
		/// Time that has passed in the game, corrected for game-speed.
		/// </summary>
		[SerializeField] public float TotalGameTime = 0;

		/// <summary>
		/// Enable Learning for the Agents
		/// </summary>
		[SerializeField] public bool EnableLearning = true;

		/// <summary>
		/// Loader for all the game prefabs
		/// </summary>
		[Header("Prefabs")]
		[SerializeField] private PrefabLoader Prefabs;

		/// <summary>
		/// Human Debugger Canvas
		/// </summary>
		[SerializeField] private Canvas HumanDebuggerCanvas;

		/// <summary>
		/// Orc Debugger Canvas
		/// </summary>
		[SerializeField] private Canvas OrcDebuggerCanvas;

		[Header("Custom Debug")]
		[SerializeField] private Text HumanCustomDebugText;
		[SerializeField] private Text OrcCustomDebugText;

		[Header("Debug Toggles")]
		[SerializeField] private Toggle AgentToggle;
		[SerializeField] private Toggle UnitToggle;
		[SerializeField] private Toggle InfluenceToggle;
		[SerializeField] private Toggle MoveTintToggle;
		[SerializeField] private Toggle GatherTintToggle;
		[SerializeField] private Toggle AttackTintToggle;
		[SerializeField] private Toggle PathTintToggle;

		#endregion

		#region Public Properties

		/// <summary>
		/// Instance of the game manager
		/// </summary>
		public static GameManager Instance => instance;

		/// <summary>
		/// Map manager - grid, pathfinding, buildability
		/// </summary>
		public MapManager Map => mapManager;

		/// <summary>
		/// Unit manager - unit creation, destruction, queries
		/// </summary>
		public UnitManager Units => unitManager;

		/// <summary>
		/// Event dispatcher - command validation and dispatch
		/// </summary>
		public EventDispatcher Events => eventDispatcher;

		/// <summary>
		/// Turns the unit-specific debugging UIs on and off
		/// </summary>
		public bool HasUnitDebugging { get; private set; }

		/// <summary>
		/// Turns the agent debugging UIs on and off
		/// </summary>
		public bool HasAgentDebugging { get; private set; }

		/// <summary>
		/// Tints MOVE-state units blue when enabled
		/// </summary>
		public bool HasMoveTint { get; private set; }

		/// <summary>
		/// Tints GATHER-state workers when enabled
		/// </summary>
		public bool HasGatherTint { get; private set; }

		/// <summary>
		/// Tints ATTACK-state units red when enabled
		/// </summary>
		public bool HasAttackTint { get; private set; }

		/// <summary>
		/// Shows unit path lines when enabled
		/// </summary>
		public bool HasPathTint { get; private set; }

		#endregion

		#region Private Fields

		/// <summary>
		/// Singleton instance
		/// </summary>
		private static GameManager instance;

		private enum GameState { PLAYING, SHOWING_WINNER, RESTARTING, FINISHED };
		private GameState gameState;

		/// <summary>
		/// Collection of Agents in the game
		/// </summary>
		private Dictionary<int, GameObject> Agents { get; set; }

		/// <summary>
		/// Number of wins per agent
		/// </summary>
		private Dictionary<string, int> AgentWins { get; set; }

		/// <summary>
		/// Number of agents created
		/// </summary>
		private int NbrOfAgents { get; set; }

		/// <summary>
		/// Time until we restart the game
		/// </summary>
		private float TimeToDisplayBanner { get; set; }

		/// <summary>
		/// Number of rounds run so far
		/// </summary>
		private int NbrOfRounds;

		/// <summary>
		/// List of dllNames to pull from for the competition
		/// </summary>
		private List<string> dllNames = null;

		private bool isHumanUsingDllNames = false;

		private GameObject roundWinner = null;

		// Sub-managers
		private MapManager mapManager;
		private UnitManager unitManager;
		private EventDispatcher eventDispatcher;
		private AgentLoader agentLoader;

		#endregion

		#region Initialization

		/// <summary>
		/// Constructor for GameManager - Singleton
		/// </summary>
		private GameManager()
		{
			if (instance != null && instance != this)
			{
				Destroy(gameObject);
			}
			else
			{
				instance = this;
			}
		}

		/// <summary>
		/// Initializes the Game Manager when it is instantiated
		/// </summary>
		private void Awake()
		{
			if (TotalNbrOfRounds % 2 == 0) TotalNbrOfRounds++;
			Constants.GAME_SPEED = StartingGameSpeed;
			Constants.CalculateGameConstants();

			string pathToDLLs = Application.dataPath + Path.AltDirectorySeparatorChar + ".."
				+ Path.AltDirectorySeparatorChar + ".." + Path.AltDirectorySeparatorChar
				+ "EnemyAgents";

			// Initialize sub-managers
			mapManager = new MapManager();
			unitManager = new UnitManager(mapManager, Prefabs);
			eventDispatcher = new EventDispatcher(unitManager, mapManager);
			agentLoader = new AgentLoader(pathToDLLs);

			InitializeDebugToggles();

			unitManager.OrcUnitPrefabs = new Dictionary<UnitType, GameObject>()
			{
				{ UnitType.MINE, Prefabs.MinePrefab },
				{ UnitType.WORKER, Prefabs.OrcPeonPrefab },
				{ UnitType.SOLDIER, Prefabs.OrcGruntPrefab },
				{ UnitType.ARCHER, Prefabs.OrcAxethrowerPrefab },
				{ UnitType.BASE, Prefabs.OrcBasePrefab },
				{ UnitType.BARRACKS, Prefabs.OrcBarracksPrefab },
				{ UnitType.REFINERY, Prefabs.OrcRefineryPrefab },
			};

			unitManager.HumanUnitPrefabs = new Dictionary<UnitType, GameObject>()
			{
				{ UnitType.MINE, Prefabs.MinePrefab },
				{ UnitType.WORKER, Prefabs.HumanPeasantPrefab },
				{ UnitType.SOLDIER, Prefabs.HumanFootmanPrefab },
				{ UnitType.ARCHER, Prefabs.HumanArcherPrefab },
				{ UnitType.BASE, Prefabs.HumanBasePrefab },
				{ UnitType.BARRACKS, Prefabs.HumanBarracksPrefab },
				{ UnitType.REFINERY, Prefabs.HumanRefineryPrefab },
			};

			InitializeMatch();
		}

		#endregion

		#region Public API

		/// <summary>
		/// Log message that colorizes all debug statements from this package
		/// </summary>
		#line hidden
		internal void Log(string message, GameObject context)
		{
			Debug.Log($"<color={GameManagerLogColor}>{message}</color>", context);
		}
		#line default

		/// <summary>
		/// Get the agent by their agent number
		/// </summary>
		public Agent GetAgent(int agentNbr)
		{
			return Agents[agentNbr].GetComponent<AgentController>().Agent;
		}

		/// <summary>
		/// Gets my enemy agent numbers
		/// </summary>
		public List<int> GetEnemyAgentNbrs(int agentNbr)
		{
			return Agents.Keys.Where(key => key != agentNbr).ToList();
		}

		#endregion

	}
}
