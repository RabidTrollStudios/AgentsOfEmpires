using System.Collections.Generic;
using AgentSDK;
using GameManager.EnumTypes;
using GameManager.Graph;
using UnityEngine;

namespace GameManager.GameElements
{
	/// <summary>
	/// Represents a single unit (troop or building) in the game
	/// </summary>
	public partial class Unit : MonoBehaviour, IColorable
	{
		#region Static Variables

		/// <summary>
		/// Does this unit have debugging information visible
		/// </summary>
		public static bool HasDebugging { get; set; }

		#endregion

		#region Properties

		/// <summary>
		/// Unique number for this unit
		/// </summary>
		public int UnitNbr { get; internal set; }

		/// <summary>
		/// Type of this unit
		/// </summary>
		public UnitType UnitType { get; internal set; }

		/// <summary>
		/// Is building of this unit complete?
		/// </summary>
		public bool IsBuilt { get; internal set; }

		/// <summary>
		/// Accumulated build time in seconds. Only meaningful when IsBuilt=false.
		/// Allows construction to pause and resume independently of which pawn is building.
		/// </summary>
		internal float BuildProgress { get; set; }

		/// <summary>
		/// Set of UnitNbrs of pawns currently constructing or repairing this building.
		/// Empty when unattended. Multiple pawns can build/repair simultaneously.
		/// </summary>
		internal HashSet<int> ActiveBuilders { get; set; } = new HashSet<int>();

		/// <summary>
		/// Color of this agent
		/// </summary>
		public Color Color { get; internal set; }

		/// <summary>
		/// Change this unit's color
		/// internal by interface
		/// </summary>
		/// <param name="color">new color</param>
		public void ChangeColor(Color color)
		{
			Color = color;
		}

		/// <summary>
		/// World position of this agent
		/// </summary>
		public Vector3 WorldPosition
		{
			get => transform.position;
			internal set => transform.position = value;
		}

		/// <summary>
		/// Position on the grid of this agent (top-left corner of footprint)
		/// </summary>
		public Vector3Int GridPosition { get; internal set; }

		/// <summary>
		/// Center cell of this unit's footprint. Use for distance calculations.
		/// For 1x1 units equals GridPosition; for 3x3 structures returns GridPosition+(1,-1).
		/// </summary>
		internal Vector3Int CenterGridPosition
		{
			get
			{
				var size = Constants.UNIT_SIZE[UnitType];
				return new Vector3Int(GridPosition.x + (size.x - 1) / 2,
				                      GridPosition.y - (size.y - 1) / 2, 0);
			}
		}

		/// <summary>
		/// Current hit points of this unit
		/// </summary>
		public float Health { get; internal set; }

		/// <summary>
		/// Agent that owns this unit
		/// </summary>
		public GameObject Agent { get; internal set; }

		/// <summary>
		/// Grid position that this unit is targetting
		/// </summary>
		public Vector3Int TargetGridPos { get; internal set; }

		/// <summary>
		/// Unit type that this unit is targetting
		/// </summary>
		public UnitType TargetUnitType { get; internal set; }

		#endregion

		#region Data Members

		// State Variables
		private float taskTime;
		private Animator animator;

		// Mining Variables
		private int totalGold = 0;
		private float minedGold = 0.0f;
		private GatherPhase gatherPhase = GatherPhase.TO_MINE;
	private bool goldNuggetSpawnedThisCycle = false;

		// Training Variables
		// Building Variables
		private UnitType taskUnitType;
		private BuildPhase buildPhase;
		private GameObject currentBuilding;

		// Attacking Variables
		private int attackUnitNbr = -1;
		private float damage = 0.0f;
		private float totalDamage = 0.0f;
		private bool arrowFiredThisCycle = false;
		private const float ARROW_SPEED = 7.5f;

		// Path variables
		private int baseUnit = -1;
		private int mineUnit = -1;
		private List<Vector3Int> path;
		private Vector3 velocity;
		private int pathUpdateCounter = 0;
		private int pathFailCount = 0;
		private int pathBackoffMultiplier = 1;
		private int localAvoidWaitFrames = 0;

		// Path visualization
		private LineRenderer pathLineRenderer;
		// Red line from attacker to its target
		private LineRenderer targetLineRenderer;
		private GameObject targetArrowhead;

		// State indicator squares under unit
		private SpriteRenderer unitSprite;
		private SpriteRenderer attackIndicator;
		private SpriteRenderer moveIndicator;
		private SpriteRenderer gatherIndicator;
		private SpriteRenderer buildIndicator;
		private Color moveColor;
		private Color actionColor;
		private Color buildColor;
		// Health bar
		private Transform healthBarFill;
		private Transform healthBarBg;
		private float maxHealth;
		private int healthBarColorTier = -1; // 0=red, 1=yellow, 2=green; -1=unset
		private int tierChangeFrames;       // countdown for tier-change effects
		private const int TIER_FLASH_FRAMES = 24; // total frames for flash/pulse/shake
		private int buildPulseFrames;             // countdown for build-complete pulse
		private const int BUILD_PULSE_TOTAL = 24; // 3 pulses over 24 frames (8 frames each)
		// Building fire: spawn a fire every 2% health lost
		private int lastFireThreshold = 50; // starts at 100% = 50 (100/2)
		// Small bar (mobile units): SmallBar_Base visible = 94x19 px, inner channel = 82x9 px
		// PPU = 64.  Channel spans image y=[27,35], center at y=31 (sprite center y=32).
		private const float SM_BAR_SCALE = 64f / 94f;            // uniform scale → visible = 1 cell
		private const float SM_BAR_FILL_SCALE_X = 0.629089713f;  // fill X scale (tuned to frame)
		private const float SM_BAR_FILL_SCALE_Y = 0.339356124f;  // fill Y scale (tuned to frame)
		private const float SM_BAR_FILL_X_OFFSET = 0.00120000006f;
		private const float SM_BAR_FILL_Y_OFFSET = 1.00549996f;
		private const float SM_BAR_Y_OFFSET = 1.0f;

		// Big bar (buildings/mines): tuned in editor
		private const float BIG_BAR_SCALE_X = 0.928591847f;
		private const float BIG_BAR_SCALE_Y = 0.528470576f;
		private const float BIG_BAR_Y_OFFSET = 2.67000008f;
		private const float BIG_BAR_FILL_SCALE_X = 0.933809578f;
		private const float BIG_BAR_FILL_SCALE_Y = 0.580541074f;
		private const float BIG_BAR_FILL_X_OFFSET = 0.00149977207f;
		private const float BIG_BAR_FILL_Y_OFFSET = 2.66759968f;

		// Tracks which bar type this unit uses
		private bool usesBigBar;

		// Training bar (buildings only) — positioned below health bar
		private Transform trainingBarFrame;
		private Transform trainingBarFill;
		private const float TRAIN_BAR_Y_GAP = 0.35f; // gap below health bar

		private static Sprite _squareSprite;
		private static Sprite _healthFillSprite;
		private static Sprite _bigHealthFillSprite;

		/// <summary>
		/// Lazily create a shared filled-square sprite for state indicators.
		/// </summary>
		private static Sprite GetSquareSprite()
		{
			if (_squareSprite != null) return _squareSprite;
			int size = 4;
			var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
			for (int y = 0; y < size; y++)
				for (int x = 0; x < size; x++)
					tex.SetPixel(x, y, Color.white);
			tex.Apply();
			tex.filterMode = FilterMode.Point;
			_squareSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
			return _squareSprite;
		}

		/// <summary>
		/// Lazily create a shared white sprite for health bar fill.
		/// White tints cleanly with SpriteRenderer.color (unlike the red SmallBar_Fill.png).
		/// Sized to match the SmallBar_Base inner channel: 82 x 9 pixels at PPU 64.
		/// </summary>
		private static Sprite GetHealthFillSprite()
		{
			if (_healthFillSprite != null) return _healthFillSprite;
			int w = 82, h = 9;
			var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			for (int y = 0; y < h; y++)
				for (int x = 0; x < w; x++)
					tex.SetPixel(x, y, Color.white);
			tex.Apply();
			tex.filterMode = FilterMode.Point;
			_healthFillSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64);
			return _healthFillSprite;
		}

		/// <summary>
		/// Lazily create a shared white sprite for big health bar fill.
		/// Sized to match the BigBar_Base inner channel: 90 x 19 pixels at PPU 64.
		/// </summary>
		private static Sprite GetBigHealthFillSprite()
		{
			if (_bigHealthFillSprite != null) return _bigHealthFillSprite;
			int w = 90, h = 19;
			var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			for (int y = 0; y < h; y++)
				for (int x = 0; x < w; x++)
					tex.SetPixel(x, y, Color.white);
			tex.Apply();
			tex.filterMode = FilterMode.Point;
			_bigHealthFillSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64);
			return _bigHealthFillSprite;
		}

		#endregion

		#region Constant Properties

		/// <summary>
		/// Movement speed of the unit
		/// </summary>
		public float Speed => Constants.MOVING_SPEED[UnitType];

		/// <summary>
		/// Mining speed of the unit
		/// </summary>
		public float MiningSpeed => Constants.MINING_SPEED[UnitType];

		/// <summary>
		/// Carrying capacity of a miner
		/// </summary>
		public float MiningCapacity => Constants.MINING_CAPACITY[UnitType];

		/// <summary>
		/// Cost to train or build the unit
		/// </summary>
		public float Cost => Constants.COST[UnitType];

		/// <summary>
		/// Time to train or build the unit
		/// </summary>
		public float CreationTime => Constants.CREATION_TIME[UnitType];

		/// <summary>
		/// Unit dependencies that must be satisfied before
		/// building or training this unit
		/// </summary>
		public List<UnitType> Dependencies => Constants.DEPENDENCY[UnitType];

		/// <summary>
		/// Can this unit move
		/// </summary>
		public bool CanMove => Constants.CAN_MOVE[UnitType];

		/// <summary>
		/// Can this unit build others
		/// </summary>
		public bool CanBuild => Constants.CAN_BUILD[UnitType];

		/// <summary>
		/// Can this unit train others
		/// </summary>
		public bool CanTrain => Constants.CAN_TRAIN[UnitType];

		/// <summary>
		/// Can this unit attack others
		/// </summary>
		public bool CanAttack => Constants.CAN_ATTACK[UnitType];

		/// <summary>
		/// Can this unit gather
		/// </summary>
		public bool CanGather => Constants.CAN_GATHER[UnitType];

		/// <summary>
		/// Which Units does this unit Train
		/// </summary>
		public List<UnitType> Trains => Constants.TRAINS[UnitType];

		/// <summary>
		/// Which Units does this unit Train
		/// </summary>
		public List<UnitType> Builds => Constants.BUILDS[UnitType];

		#endregion

		#region Properties

		/// <summary>
		/// Velocity of this unit
		/// </summary>
		public Vector3 Velocity { get; internal set; }

		/// <summary>
		/// Current action of this unit
		/// </summary>
		public UnitAction CurrentAction { get; internal set; }

		/// <summary>
		/// Current main base of this unit
		/// </summary>
		public Unit BaseUnit
		{
			get
			{
				Unit unit = GameManager.Instance.Units.GetUnit(baseUnit);
				if (baseUnit != -1 && unit != null)
					return unit;
				else
					return null;
			}
			internal set
			{
				if (value == null)
					baseUnit = -1;
				else
					baseUnit = value.GetComponent<Unit>().UnitNbr;
			}
		}

		/// <summary>
		/// Current main mine of this unit
		/// null otherwise
		/// </summary>
		public Unit MineUnit
		{
			get
			{
				Unit unit = GameManager.Instance.Units.GetUnit(mineUnit);
				if (mineUnit != -1 && unit != null)
					return unit;
				else
					return null;
			}
			internal set
			{
				if (value == null)
					mineUnit = -1;
				else
					mineUnit = value.GetComponent<Unit>().UnitNbr;
			}
		}

		/// <summary>
		/// Unit that this unit is attacking
		/// null otherwise
		/// </summary>
		public Unit AttackUnit
		{
			get
			{
				Unit unit = GameManager.Instance.Units.GetUnit(attackUnitNbr);
				if (attackUnitNbr != -1 && unit != null)
					return unit;
				else
					return null;
			}
			internal set
			{
				if (value == null)
					attackUnitNbr = -1;
				else
					attackUnitNbr = value.GetComponent<Unit>().UnitNbr;
			}
		}

		/// <summary>
		/// CanTrainUnit asks if the current unit
		/// can train the type of unit provided by the parameter
		/// </summary>
		/// <param name="UnitType">type of unit to train</param>
		/// <returns>true if trainable and false otherwise</returns>
		public bool CanTrainUnit(UnitType UnitType)
		{
			return Trains.Contains(UnitType);
		}

		/// <summary>
		/// CanBuildUnit asks if the current unit
		/// can build the type of unit provided by the parameter
		/// </summary>
		/// <param name="UnitType">type of unit to train</param>
		/// <returns>true if buildable, false otherwise</returns>
		public bool CanBuildUnit(UnitType UnitType)
		{
			return Builds.Contains(UnitType);
		}

		#endregion

		#region Initializers

		/// <summary>
		/// InitializeRound this unit
		/// </summary>
		/// <param name="agent">agent that owns this unit</param>
		/// <param name="gridPosition">initial position of this unit</param>
		/// <param name="unitType">type of this unit</param>
		/// <param name="unitNbr">the unique number for this unit</param>
		internal void Initialize(GameObject agent, Vector3Int gridPosition, UnitType unitType, int unitNbr)
		{
			unitSprite = GetComponent<SpriteRenderer>();
			HasDebugging = GameManager.Instance.HasUnitDebugging;
			Agent = agent;
			UnitNbr = unitNbr;
			this.velocity = Vector3.zero;
			this.CurrentAction = UnitAction.IDLE;
			path = new List<Vector3Int>();
			pathFailCount = 0;
			pathBackoffMultiplier = 1;
			UnitType = unitType;
			if (Constants.BUILDS[UnitType.PAWN].Contains(UnitType))
			{
				IsBuilt = false;
				if (unitSprite != null)
				{
					var c = unitSprite.color;
					c.a = 0.3f;
					unitSprite.color = c;
				}
			}
			else
			{
				IsBuilt = true;
			}

			GridPosition = gridPosition;
			Health = Constants.HEALTH[UnitType];
			animator = gameObject.GetComponent<Animator>();

			if (animator != null && animator.runtimeAnimatorController != null)
			{
				if (UnitType == UnitType.MINE)
				{
					// Offset mine animations so gold stones shimmer at different times
					animator.Play(0, 0, UnityEngine.Random.value);
				}
				else if (UnitType == UnitType.LANCER)
				{
					InitLancerStateHashes();
					animator.Play(lancerStateHashes[0], 0, 0f); // Start with Idle
				}
				else if (CanMove)
				{
					// Force idle animation from the first frame (mobile units only)
					animator.SetInteger("State", 0);
					animator.Play(0, 0, 0f);
				}
			}

			pathLineRenderer = gameObject.AddComponent<LineRenderer>();
			pathLineRenderer.startWidth = 0.05f;
			pathLineRenderer.endWidth = 0.05f;
			pathLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
			pathLineRenderer.startColor = Color.cyan;
			pathLineRenderer.endColor = Color.cyan;
			pathLineRenderer.useWorldSpace = true;
			pathLineRenderer.sortingLayerName = "UnitUI";
			pathLineRenderer.sortingOrder = 10;
			pathLineRenderer.positionCount = 0;

			var targetLineObj = new GameObject("TargetLine");
			targetLineObj.transform.SetParent(transform);
			targetLineObj.transform.localPosition = Vector3.zero;
			targetLineRenderer = targetLineObj.AddComponent<LineRenderer>();
			targetLineRenderer.startWidth = 0.05f;
			targetLineRenderer.endWidth = 0.05f;
			targetLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
			targetLineRenderer.startColor = Color.red;
			targetLineRenderer.endColor = Color.red;
			targetLineRenderer.useWorldSpace = true;
			targetLineRenderer.sortingLayerName = "UnitUI";
			targetLineRenderer.sortingOrder = 1;
			targetLineRenderer.positionCount = 0;
			targetArrowhead = CreateArrowhead("TargetArrowhead", Color.red, "UnitUI", 11);

			// Determine indicator colors by agent faction (semi-transparent overlays)
			bool isRed = agent.GetComponent<AgentController>()?.Agent?.AgentName == Constants.RED_ABBR;
			moveColor   = isRed ? new Color(0f, 1f, 1f, 0.5f)      : new Color(0f, 0f, 1f, 0.5f);      // cyan / blue
			actionColor = isRed ? new Color(1f, 0f, 1f, 0.5f)      : new Color(1f, 0f, 0f, 0.5f);      // magenta / red
			buildColor  = isRed ? new Color(1f, 0.65f, 0f, 0.5f) : new Color(0.8f, 0.25f, 0f, 0.5f);  // light orange / dark orange

			// Square indicator overlays rendered ABOVE the unit sprite (sortingOrder 1 > unit's 0)
			var attackObj = new GameObject("AttackIndicator") { layer = LayerMask.NameToLayer("Units") };
			attackObj.transform.SetParent(transform);
			attackObj.transform.localPosition = Vector3.zero;
			attackObj.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
			attackIndicator = attackObj.AddComponent<SpriteRenderer>();
			attackIndicator.sprite = GetSquareSprite();
			attackIndicator.color = actionColor;
			attackIndicator.sortingLayerName = "Agents";
			attackIndicator.sortingOrder = 1;
			attackIndicator.enabled = false;

			var moveObj = new GameObject("MoveIndicator") { layer = LayerMask.NameToLayer("Units") };
			moveObj.transform.SetParent(transform);
			moveObj.transform.localPosition = Vector3.zero;
			moveObj.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
			moveIndicator = moveObj.AddComponent<SpriteRenderer>();
			moveIndicator.sprite = GetSquareSprite();
			moveIndicator.color = moveColor;
			moveIndicator.sortingLayerName = "Agents";
			moveIndicator.sortingOrder = 1;
			moveIndicator.enabled = false;

			var gatherObj = new GameObject("GatherIndicator") { layer = LayerMask.NameToLayer("Units") };
			gatherObj.transform.SetParent(transform);
			gatherObj.transform.localPosition = Vector3.zero;
			gatherObj.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
			gatherIndicator = gatherObj.AddComponent<SpriteRenderer>();
			gatherIndicator.sprite = GetSquareSprite();
			gatherIndicator.color = actionColor;
			gatherIndicator.sortingLayerName = "Agents";
			gatherIndicator.sortingOrder = 1;
			gatherIndicator.enabled = false;

			var buildObj = new GameObject("BuildIndicator") { layer = LayerMask.NameToLayer("Units") };
			buildObj.transform.SetParent(transform);
			buildObj.transform.localPosition = Vector3.zero;
			buildObj.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
			buildIndicator = buildObj.AddComponent<SpriteRenderer>();
			buildIndicator.sprite = GetSquareSprite();
			buildIndicator.color = buildColor;
			buildIndicator.sortingLayerName = "Agents";
			buildIndicator.sortingOrder = 1;
			buildIndicator.enabled = false;

			// Health bar — small bar for mobile units, big bar for buildings/mines
			maxHealth = Health;
			if (CanMove)
			{
				usesBigBar = false;

				var bgObj = new GameObject("HealthBarFrame");
				bgObj.transform.SetParent(transform);
				bgObj.transform.localPosition = new Vector3(0f, SM_BAR_Y_OFFSET, 0f);
				bgObj.transform.localScale = new Vector3(SM_BAR_SCALE, SM_BAR_SCALE, 1f);
				var bgSr = bgObj.AddComponent<SpriteRenderer>();
				bgSr.sprite = GameManager.Instance.SmallBarBase;
				bgSr.sortingLayerName = "AgentUI";
				bgSr.sortingOrder = 30;
				healthBarBg = bgObj.transform;

				var fillObj = new GameObject("HealthBarFill");
				fillObj.transform.SetParent(transform);
				fillObj.transform.localPosition = new Vector3(SM_BAR_FILL_X_OFFSET, SM_BAR_FILL_Y_OFFSET, 0f);
				fillObj.transform.localScale = new Vector3(SM_BAR_FILL_SCALE_X, SM_BAR_FILL_SCALE_Y, 1f);
				var fillSr = fillObj.AddComponent<SpriteRenderer>();
				fillSr.sprite = GetHealthFillSprite();
				fillSr.color = Color.green;
				fillSr.sortingLayerName = "AgentUI";
				fillSr.sortingOrder = 31;
				healthBarFill = fillObj.transform;
			}
			else
			{
				usesBigBar = true;

				var bgObj = new GameObject("HealthBarFrame");
				bgObj.transform.SetParent(transform);
				bgObj.transform.localPosition = new Vector3(0f, BIG_BAR_Y_OFFSET, 0f);
				bgObj.transform.localScale = new Vector3(BIG_BAR_SCALE_X, BIG_BAR_SCALE_Y, 1f);
				var bgSr = bgObj.AddComponent<SpriteRenderer>();
				bgSr.sprite = GameManager.Instance.BigBarBase;
				bgSr.sortingLayerName = "UnitUI";
				bgSr.sortingOrder = 30;
				healthBarBg = bgObj.transform;

				var fillObj = new GameObject("HealthBarFill");
				fillObj.transform.SetParent(transform);
				fillObj.transform.localPosition = new Vector3(BIG_BAR_FILL_X_OFFSET, BIG_BAR_FILL_Y_OFFSET, 0f);
				fillObj.transform.localScale = new Vector3(BIG_BAR_FILL_SCALE_X, BIG_BAR_FILL_SCALE_Y, 1f);
				var fillSr = fillObj.AddComponent<SpriteRenderer>();
				fillSr.sprite = GetBigHealthFillSprite();
				fillSr.color = Color.green;
				fillSr.sortingLayerName = "UnitUI";
				fillSr.sortingOrder = 31;
				healthBarFill = fillObj.transform;

				// Training bar (only for buildings that can train)
				if (CanTrain)
				{
					float trainBarY = BIG_BAR_Y_OFFSET - TRAIN_BAR_Y_GAP;
					float trainFillY = BIG_BAR_FILL_Y_OFFSET - TRAIN_BAR_Y_GAP;

					var trainBgObj = new GameObject("TrainingBarFrame");
					trainBgObj.transform.SetParent(transform);
					trainBgObj.transform.localPosition = new Vector3(0f, trainBarY, 0f);
					trainBgObj.transform.localScale = new Vector3(BIG_BAR_SCALE_X, BIG_BAR_SCALE_Y, 1f);
					var trainBgSr = trainBgObj.AddComponent<SpriteRenderer>();
					trainBgSr.sprite = GameManager.Instance.BigBarBase;
					trainBgSr.sortingLayerName = "UnitUI";
					trainBgSr.sortingOrder = 30;
					trainBgObj.SetActive(false);
					trainingBarFrame = trainBgObj.transform;

					var trainFillObj = new GameObject("TrainingBarFill");
					trainFillObj.transform.SetParent(transform);
					trainFillObj.transform.localPosition = new Vector3(BIG_BAR_FILL_X_OFFSET, trainFillY, 0f);
					trainFillObj.transform.localScale = new Vector3(BIG_BAR_FILL_SCALE_X, BIG_BAR_FILL_SCALE_Y, 1f);
					var trainFillSr = trainFillObj.AddComponent<SpriteRenderer>();
					trainFillSr.sprite = GetBigHealthFillSprite();
					trainFillSr.color = new Color(0.2f, 0.4f, 1f); // blue
					trainFillSr.sortingLayerName = "UnitUI";
					trainFillSr.sortingOrder = 31;
					trainFillObj.SetActive(false);
					trainingBarFill = trainFillObj.transform;
				}
			}
		}

		#endregion
	}
}
