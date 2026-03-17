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
using UnityEngine.Serialization;
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
		/// Name of the DLL to use for the Blue agent
		/// </summary>
		[Header("Player Settings")]

		[FormerlySerializedAs("HumanDllName")]
		[SerializeField] public string BlueDllName;

		/// <summary>
		/// Name of the DLL to use for the Red agent
		/// </summary>
		[FormerlySerializedAs("OrcDllName")]
		[SerializeField] public string RedDllName;

		/// <summary>
		/// Should matches be played against random agents?
		/// </summary>
		[FormerlySerializedAs("RandomizeAgentsAsOrc")]
		[SerializeField] public bool RandomizeAgentsAsRed;

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

		[Header("Debug Toggles")]
		[SerializeField] private Toggle AgentToggle;
		[SerializeField] private Toggle UnitToggle;
		[SerializeField] private Toggle InfluenceToggle;
		[SerializeField] private Toggle MoveTintToggle;
		[SerializeField] private Toggle GatherTintToggle;
		[SerializeField] private Toggle BuildTintToggle;
		[SerializeField] private Toggle AttackTintToggle;
		[SerializeField] private Toggle PathTintToggle;
		[SerializeField] private Toggle TargetLineTintToggle;

		[Header("Debug Info")]
		/// <summary>
		/// Blue Debugger Panel
		/// </summary>
		[FormerlySerializedAs("HumanDebuggerPanel")]
		[SerializeField] private GameObject BlueDebuggerPanel;

		/// <summary>
		/// Red Debugger Panel
		/// </summary>
		[FormerlySerializedAs("OrcDebuggerPanel")]
		[SerializeField] private GameObject RedDebuggerPanel;

		[FormerlySerializedAs("HumanCustomDebugText")]
		[SerializeField] private Text BlueCustomDebugText;
		[FormerlySerializedAs("OrcCustomDebugText")]
		[SerializeField] private Text RedCustomDebugText;

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
		/// Tints GATHER-state pawns when enabled
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

		/// <summary>
		/// Tints BUILD-state pawns orange when enabled
		/// </summary>
		public bool HasBuildTint { get; private set; }

		/// <summary>
		/// Shows the red attacker-to-target line when enabled
		/// </summary>
		public bool HasTargetLineTint { get; private set; }

		/// <summary>
		/// True when the game is actively playing (not paused for intro, showing winner, etc.)
		/// </summary>
		public bool IsPlaying => gameState == GameState.PLAYING;

		/// <summary>
		/// Arrow sprite for archer projectiles
		/// </summary>
		public Sprite ArrowSprite => Prefabs.ArrowSprite;

		/// <summary>
		/// Fire animator controller for flaming arrows
		/// </summary>
		public RuntimeAnimatorController FireAnimatorController => Prefabs.FireAnimatorController;

		/// <summary>
		/// Explosion animator controller for arrow impacts
		/// </summary>
		public RuntimeAnimatorController ExplosionAnimatorController => Prefabs.ExplosionAnimatorController;

		/// <summary>
		/// Fire animator controllers for building impact fires
		/// </summary>
		public RuntimeAnimatorController[] BuildingFireControllers => new[]
		{
			Prefabs.Fire1AnimatorController,
			Prefabs.Fire2AnimatorController,
			Prefabs.Fire3AnimatorController
		};

		/// <summary>
		/// Dust 2 animator controller for unit death effect
		/// </summary>
		public RuntimeAnimatorController Dust2AnimatorController => Prefabs.Dust2AnimatorController;

		/// <summary>
		/// Gold resource sprite for mining nugget effect
		/// </summary>
		public Sprite GoldResourceSprite => Prefabs.GoldResourceSprite;

		public Sprite SmallBarBase => Prefabs.SmallBarBase;
		public Sprite SmallBarFill => Prefabs.SmallBarFill;
		public Sprite BigBarBase => Prefabs.BigBarBase;

		#endregion

		#region Private Fields

		/// <summary>
		/// Singleton instance
		/// </summary>
		private static GameManager instance;

		private enum GameState { INTRO, PLAYING, SHOWING_WINNER, RESTARTING, FINISHED };
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

		private bool isBlueUsingDllNames = false;

		private GameObject roundWinner = null;

		// Sub-managers
		private MapManager mapManager;
		private UnitManager unitManager;
		private EventDispatcher eventDispatcher;
		private AgentLoader agentLoader;

		// Input
		private InputSystem_Actions _input;

		#endregion

		#region Initialization

		/// <summary>
		/// Constructor for GameManager - Singleton
		/// </summary>
		private GameManager()
		{
			if (instance == null)
			{
				instance = this;
			}
		}

		private void OnDestroy()
		{
			_input?.Gameplay.Disable();
			_input?.Dispose();
			_input = null;
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

			_input = new InputSystem_Actions();
			_input.asset.bindingMask = null;
			_input.Gameplay.Enable();

			InitializeDebugToggles();
			SetupGameOverBanner();

			unitManager.RedUnitPrefabs = new Dictionary<UnitType, GameObject>()
			{
				{ UnitType.MINE, Prefabs.MinePrefab },
				{ UnitType.PAWN, Prefabs.RedPawnPrefab },
				{ UnitType.WARRIOR, Prefabs.RedWarriorPrefab },
				{ UnitType.ARCHER, Prefabs.RedArcherPrefab },
				{ UnitType.BASE, Prefabs.RedBasePrefab },
				{ UnitType.BARRACKS, Prefabs.RedBarracksPrefab },
				{ UnitType.ARCHERY, Prefabs.RedArcheryPrefab },
				{ UnitType.LANCER, Prefabs.RedLancerPrefab },
				{ UnitType.TOWER, Prefabs.RedTowerPrefab },
			};

			unitManager.BlueUnitPrefabs = new Dictionary<UnitType, GameObject>()
			{
				{ UnitType.MINE, Prefabs.MinePrefab },
				{ UnitType.PAWN, Prefabs.BluePawnPrefab },
				{ UnitType.WARRIOR, Prefabs.BlueWarriorPrefab },
				{ UnitType.ARCHER, Prefabs.BlueArcherPrefab },
				{ UnitType.BASE, Prefabs.BlueBasePrefab },
				{ UnitType.BARRACKS, Prefabs.BlueBarracksPrefab },
				{ UnitType.ARCHERY, Prefabs.BlueArcheryPrefab },
				{ UnitType.LANCER, Prefabs.BlueLancerPrefab },
				{ UnitType.TOWER, Prefabs.BlueTowerPrefab },
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
