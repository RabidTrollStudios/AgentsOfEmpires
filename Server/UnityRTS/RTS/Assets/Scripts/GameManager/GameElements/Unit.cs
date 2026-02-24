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
		private bool isInsideMine = false;
		private Vector3Int mineEntryGridPos;

		// Training Variables
		// Building Variables
		private UnitType taskUnitType;
		private BuildPhase buildPhase;
		private GameObject currentBuilding;

		// Attacking Variables
		private int attackUnitNbr = -1;
		private float damage = 0.0f;
		private float totalDamage = 0.0f;

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

		// State indicator squares under unit
		private SpriteRenderer attackIndicator;
		private SpriteRenderer moveIndicator;
		private SpriteRenderer gatherIndicator;
		private static Sprite _squareSprite;

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
			HasDebugging = GameManager.Instance.HasUnitDebugging;
			Agent = agent;
			UnitNbr = unitNbr;
			this.velocity = Vector3.zero;
			this.CurrentAction = UnitAction.IDLE;
			path = new List<Vector3Int>();
			pathFailCount = 0;
			pathBackoffMultiplier = 1;
			UnitType = unitType;
			if (Constants.BUILDS[UnitType.WORKER].Contains(UnitType))
			{
				IsBuilt = false;
			}
			else
			{
				IsBuilt = true;
			}

			GridPosition = gridPosition;
			Health = Constants.HEALTH[UnitType];
			animator = gameObject.GetComponent<Animator>();

			pathLineRenderer = gameObject.AddComponent<LineRenderer>();
			pathLineRenderer.startWidth = 0.05f;
			pathLineRenderer.endWidth = 0.05f;
			pathLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
			pathLineRenderer.startColor = Color.cyan;
			pathLineRenderer.endColor = Color.cyan;
			pathLineRenderer.useWorldSpace = true;
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
			targetLineRenderer.sortingOrder = 11;
			targetLineRenderer.positionCount = 0;

			// Determine indicator colors by agent faction
			bool isOrc = agent.GetComponent<AgentController>()?.Agent?.AgentName == Constants.ORC_ABBR;
			Color moveColor = isOrc ? Color.cyan : Color.blue;
			Color actionColor = isOrc ? Color.magenta : Color.red;

			// Square indicators shown under units by state
			var attackObj = new GameObject("AttackIndicator") { layer = LayerMask.NameToLayer("Units") };
			attackObj.transform.SetParent(transform);
			attackObj.transform.localPosition = Vector3.zero;
			attackObj.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
			attackIndicator = attackObj.AddComponent<SpriteRenderer>();
			attackIndicator.sprite = GetSquareSprite();
			attackIndicator.color = actionColor;
			attackIndicator.sortingLayerName = "Agents";
			attackIndicator.sortingOrder = 0;
			attackIndicator.enabled = false;

			var moveObj = new GameObject("MoveIndicator") { layer = LayerMask.NameToLayer("Units") };
			moveObj.transform.SetParent(transform);
			moveObj.transform.localPosition = Vector3.zero;
			moveObj.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
			moveIndicator = moveObj.AddComponent<SpriteRenderer>();
			moveIndicator.sprite = GetSquareSprite();
			moveIndicator.color = moveColor;
			moveIndicator.sortingLayerName = "Agents";
			moveIndicator.sortingOrder = 0;
			moveIndicator.enabled = false;

			var gatherObj = new GameObject("GatherIndicator") { layer = LayerMask.NameToLayer("Units") };
			gatherObj.transform.SetParent(transform);
			gatherObj.transform.localPosition = Vector3.zero;
			gatherObj.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
			gatherIndicator = gatherObj.AddComponent<SpriteRenderer>();
			gatherIndicator.sprite = GetSquareSprite();
			gatherIndicator.color = actionColor;
			gatherIndicator.sortingLayerName = "Agents";
			gatherIndicator.sortingOrder = 0;
			gatherIndicator.enabled = false;
		}

		#endregion
	}
}
