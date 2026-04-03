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
	/// <summary>Whether to use a hand-made tilemap or a procedurally generated map.</summary>
	public enum MapMode { HandMade, Procedural }

	/// <summary>Procedural map layout template.</summary>
	public enum MapTemplate { OpenField, Maze, Forest }

	/// <summary>Symmetry enforcement for procedural maps.</summary>
	public enum MapSymmetryMode { None, Mirror, Rotational }

	/// <summary>
	/// Orchestrates the game: manages match/round lifecycle, agents, and delegates
	/// to specialized managers for map, units, events, and DLL loading.
	/// </summary>
	[DefaultExecutionOrder(-100)] // Run FixedUpdate before Unit components
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
		/// Duration in seconds for each banner display (intro title, versus, round winner).
		/// Set low (e.g. 0.1) for fast testing iteration.
		/// </summary>
		[Header("Banner Timing")]
		[SerializeField] public float BannerDuration = 3f;

		/// <summary>
		/// Whether to use a hand-made tilemap or procedural generation.
		/// </summary>
		[Header("Map Configuration")]
		[SerializeField] private MapMode mapMode = MapMode.HandMade;

		/// <summary>
		/// Index into MapPrefabs for hand-made mode. 0 = scene Grid (default).
		/// </summary>
		[SerializeField] private int selectedMapIndex;

		/// <summary>
		/// Grid prefabs available for hand-made map selection.
		/// </summary>
		[SerializeField] private GameObject[] mapPrefabs = new GameObject[0];

		/// <summary>
		/// Width of the procedural map in cells.
		/// </summary>
		[SerializeField] private int mapWidth = 30;

		/// <summary>
		/// Height of the procedural map in cells.
		/// </summary>
		[SerializeField] private int mapHeight = 30;

		/// <summary>
		/// Procedural map layout template.
		/// </summary>
		[SerializeField] private MapTemplate mapTemplate = MapTemplate.OpenField;

		/// <summary>
		/// Fraction of the map covered by tree obstacles.
		/// Max depends on template: OpenField=0.20, Forest=0.35, Maze=0.35.
		/// </summary>
		[SerializeField] private float treeDensity = 0.15f;

		/// <summary>
		/// Random seed for deterministic procedural generation.
		/// </summary>
		[SerializeField] private int mapSeed = 42;

		/// <summary>
		/// Symmetry enforcement for procedural maps.
		/// </summary>
		[SerializeField] private MapSymmetryMode mapSymmetry = MapSymmetryMode.Mirror;

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

		// Runtime-instantiated debug panels (created in InitializeMatch)
		private GameObject blueDebuggerPanel;
		private GameObject redDebuggerPanel;
		private Text blueCustomDebugText;
		private Text redCustomDebugText;

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
		/// Heal effect animator controller for monk healing
		/// </summary>
		public RuntimeAnimatorController HealEffectAnimatorController => Prefabs.HealEffectAnimatorController;

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

		public Dictionary<string, Sprite> GetDebugPanelIcons(string agentName) => Prefabs.GetIconsForAgent(agentName);

		/// <summary>Expose map configuration for parity export.</summary>
		internal MapMode MapConfigMode => mapMode;
		internal MapTemplate MapConfigTemplate => mapTemplate;
		internal int MapConfigWidth => mapWidth;
		internal int MapConfigHeight => mapHeight;
		internal float MapConfigDensity => treeDensity;
		internal int MapConfigSeed => mapSeed;
		internal MapSymmetryMode MapConfigSymmetry => mapSymmetry;

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
		internal Dictionary<int, GameObject> Agents { get; set; }

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

		// Procedural map state (set during InitializeMatch if mapMode == Procedural)
		private ProceduralMapResult proceduralMapResult;
		private GameObject runtimeGrid; // The Grid created/instantiated at runtime (if any)

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

	
			// Fixed timestep matching SimGame's TickDuration for exact parity.
			// All game logic runs in FixedUpdate at this rate (20 Hz).
			Time.fixedDeltaTime = 0.05f;

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
				{ UnitType.MONASTERY, Prefabs.RedMonasteryPrefab },
				{ UnitType.MONK, Prefabs.RedMonkPrefab },
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
				{ UnitType.MONASTERY, Prefabs.BlueMonasteryPrefab },
				{ UnitType.MONK, Prefabs.BlueMonkPrefab },
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
