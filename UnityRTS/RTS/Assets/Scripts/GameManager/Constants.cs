using System.Collections.Generic;
using UnityEngine;
using AgentSDK;
using GameManager.EnumTypes;

namespace GameManager
{
    /// <summary>
    /// Constants - thin pass-through to AgentSDK.GameConstants and DerivedGameConstants.
    /// All gameplay values are owned by AgentSDK. This file provides Unity-compatible
    /// accessors and Unity-only values (colors, directions, game speed control).
    /// See ADR-0001: Dual-Engine Parity.
    /// </summary>
    public class Constants
    {
        #region Game Configuration

        /// <summary>
        /// String that represents the name of the Blue agent
        /// </summary>
        public const string BLUE_ABBR = "(BLU)";

        /// <summary>
        /// String that represents the name of the Red agent
        /// </summary>
        public const string RED_ABBR = "(RED)";

        /// <summary>
        /// Health associated with each unit (Mine health is amount of gold)
        /// </summary>
        public Dictionary<UnitType, float> Health => HEALTH;

        /// <summary>
        /// Damage associated with each unit
        /// </summary>
        public IReadOnlyDictionary<UnitType, float> Damage => DAMAGE;

        /// <summary>
        /// Moving speed associated with each unit
        /// </summary>
        public IReadOnlyDictionary<UnitType, float> MovingSpeed => MOVING_SPEED;

        /// <summary>
        /// Mining speed associated with each unit
        /// </summary>
        public IReadOnlyDictionary<UnitType, float> MiningSpeed => MINING_SPEED;

        /// <summary>
        /// Mining capacity associated with each unit
        /// </summary>
        public IReadOnlyDictionary<UnitType, float> MiningCapacity => MINING_CAPACITY;

        /// <summary>
        /// Cost associated with each unit
        /// </summary>
        public IReadOnlyDictionary<UnitType, float> Cost => COST;

        /// <summary>
        /// Creation time associated with each unit
        /// </summary>
        public IReadOnlyDictionary<UnitType, float> CreationTime => CREATION_TIME;

        /// <summary>
        /// Dependencies associated with each unit
        /// </summary>
        public Dictionary<UnitType, List<UnitType>> Dependency => DEPENDENCY;

        /// <summary>
        /// Builds associated with each unit
        /// </summary>
        public Dictionary<UnitType, List<UnitType>> Builds => BUILDS;

        /// <summary>
        /// Trains associated with each unit
        /// </summary>
        public Dictionary<UnitType, List<UnitType>> Trains => TRAINS;

        /// <summary>
        /// Can unit move
        /// </summary>
        public IReadOnlyDictionary<UnitType, bool> CanMove => CAN_MOVE;

        /// <summary>
        /// Can unit build
        /// </summary>
        public IReadOnlyDictionary<UnitType, bool> CanBuild => CAN_BUILD;

        /// <summary>
        /// Can unit train
        /// </summary>
        public IReadOnlyDictionary<UnitType, bool> CanTrain => CAN_TRAIN;

        /// <summary>
        /// Can unit attack
        /// </summary>
        public IReadOnlyDictionary<UnitType, bool> CanAttack => CAN_ATTACK;

        /// <summary>
        /// Can unit gather
        /// </summary>
        public IReadOnlyDictionary<UnitType, bool> CanGather => CAN_GATHER;

        /// <summary>
        /// Unit Attack range
        /// </summary>
        public IReadOnlyDictionary<UnitType, float> AttackRange => ATTACK_RANGE;

        /// <summary>
        /// Unit size
        /// </summary>
        public Dictionary<UnitType, Vector3Int> UnitSize => UNIT_SIZE;

        #endregion

		#region Static Values — Pass-throughs to AgentSDK

        // =====================================================================
        // Static dictionaries: direct references to GameConstants (single source
        // of truth). No copies — these ARE the SDK dictionaries.
        // =====================================================================

        /// <summary>
        /// Initial health associated with each unit.
        /// Note: HEALTH[MINE] is overwritten at runtime with StartingMineGold.
        /// We must keep a mutable copy for this one mutation.
        /// </summary>
        public static readonly Dictionary<UnitType, float> HEALTH = new Dictionary<UnitType, float>(GameConstants.HEALTH);

        /// <summary>
        /// Which Units can move (pass-through to SDK)
        /// </summary>
        public static readonly IReadOnlyDictionary<UnitType, bool> CAN_MOVE = GameConstants.CAN_MOVE;

        /// <summary>
        /// Which Units can build (pass-through to SDK)
        /// </summary>
        public static readonly IReadOnlyDictionary<UnitType, bool> CAN_BUILD = GameConstants.CAN_BUILD;

        /// <summary>
        /// Which Units can train (pass-through to SDK)
        /// </summary>
        public static readonly IReadOnlyDictionary<UnitType, bool> CAN_TRAIN = GameConstants.CAN_TRAIN;

        /// <summary>
        /// Which Units can attack (pass-through to SDK)
        /// </summary>
        public static readonly IReadOnlyDictionary<UnitType, bool> CAN_ATTACK = GameConstants.CAN_ATTACK;

        /// <summary>
        /// Which Units can gather (pass-through to SDK)
        /// </summary>
        public static readonly IReadOnlyDictionary<UnitType, bool> CAN_GATHER = GameConstants.CAN_GATHER;

        /// <summary>
        /// Attack range for each unit (pass-through to SDK)
        /// </summary>
        public static readonly IReadOnlyDictionary<UnitType, float> ATTACK_RANGE = GameConstants.ATTACK_RANGE;

        /// <summary>
        /// Which Units can heal (pass-through to SDK)
        /// </summary>
        public static readonly IReadOnlyDictionary<UnitType, bool> CAN_HEAL = GameConstants.CAN_HEAL;

        /// <summary>
        /// Heal range for each unit (pass-through to SDK)
        /// </summary>
        public static readonly IReadOnlyDictionary<UnitType, float> HEAL_RANGE = GameConstants.HEAL_RANGE;

        /// <summary>
        /// Maximum mana for each unit (pass-through to SDK)
        /// </summary>
        public static readonly IReadOnlyDictionary<UnitType, float> MAX_MANA = GameConstants.MAX_MANA;

        /// <summary>
        /// Dependencies of each unit in order to build/train them.
        /// Mutable copy needed because Unity code may modify the lists.
        /// TODO: Make Unity code use IReadOnlyList and remove this copy.
        /// </summary>
        public static readonly Dictionary<UnitType, List<UnitType>> DEPENDENCY = ToMutableListDict(GameConstants.DEPENDENCY);

        /// <summary>
        /// Set of Units built by each unit.
        /// Mutable copy — see DEPENDENCY note above.
        /// </summary>
        public static readonly Dictionary<UnitType, List<UnitType>> BUILDS = ToMutableListDict(GameConstants.BUILDS);

        /// <summary>
        /// Set of Units trained by each unit.
        /// Mutable copy — see DEPENDENCY note above.
        /// </summary>
        public static readonly Dictionary<UnitType, List<UnitType>> TRAINS = ToMutableListDict(GameConstants.TRAINS);

        /// <summary>
        /// Raw unit sizes as Vector3Int (converted from SDK Position — Unity-specific type)
        /// </summary>
        public static readonly Dictionary<UnitType, Vector3Int> UNIT_SIZE = ToVector3IntDict(GameConstants.UNIT_SIZE);

        // =====================================================================
        // Runtime-computed values: stored from DerivedGameConstants instance.
        // Recomputed when game speed changes via CalculateGameConstants().
        // =====================================================================

        /// <summary>
        /// Cached DerivedGameConstants instance for current game speed
        /// </summary>
        internal static DerivedGameConstants Derived { get; private set; }

        /// <summary>
        /// Damage per unit (speed-scaled)
        /// </summary>
        public static IReadOnlyDictionary<UnitType, float> DAMAGE => Derived?.Damage;

        /// <summary>
        /// Moving speed per unit (speed-scaled)
        /// </summary>
        internal static IReadOnlyDictionary<UnitType, float> MOVING_SPEED => Derived?.MovingSpeed;

        /// <summary>
        /// Mining speed per unit (speed-scaled)
        /// </summary>
        internal static IReadOnlyDictionary<UnitType, float> MINING_SPEED => Derived?.MiningSpeedPerUnit;

        /// <summary>
        /// Mining capacity per unit
        /// </summary>
        public static IReadOnlyDictionary<UnitType, float> MINING_CAPACITY => Derived?.MiningCapacityPerUnit;

        /// <summary>
        /// Cost per unit
        /// </summary>
        public static IReadOnlyDictionary<UnitType, float> COST => Derived?.Cost;

        /// <summary>
        /// Creation time per unit (speed-scaled)
        /// </summary>
        internal static IReadOnlyDictionary<UnitType, float> CREATION_TIME => Derived?.CreationTime;

        /// <summary>
        /// Scalar damage value
        /// </summary>
        internal static float SCALAR_DAMAGE => Derived?.ScalarDamage ?? 0f;

        /// <summary>
        /// Base moving speed (= pawn speed)
        /// </summary>
        internal static float SCALAR_MOVING_SPEED => Derived?.ScalarMovingSpeed ?? 0f;

        /// <summary>
        /// Mining speed scalar
        /// </summary>
        internal static float SCALAR_MINING_SPEED => Derived?.ScalarMiningSpeed ?? 0f;

        /// <summary>
        /// Creation time scalar
        /// </summary>
        internal static float SCALAR_CREATION_TIME => Derived?.ScalarCreationTime ?? 0f;

        /// <summary>
        /// Mana regeneration rate per second (speed-scaled)
        /// </summary>
        internal static float MANA_REGEN => Derived?.ManaRegen ?? 0f;

        /// <summary>
        /// Cost scalar (pass-through to SDK)
        /// </summary>
        internal static float SCALAR_COST = GameConstants.SCALAR_COST;

        /// <summary>
        /// Mining capacity scalar (pass-through to SDK)
        /// </summary>
        internal static float SCALAR_MINING_CAPACITY = GameConstants.SCALAR_MINING_CAPACITY;

        // =====================================================================
        // Unity-only values (not in AgentSDK)
        // =====================================================================

        /// <summary>
        /// Stores the values for units to compute the "winner" on timeout.
        /// Pass-through to shared DerivedGameConstants.UNIT_VALUE.
        /// </summary>
        internal static readonly IReadOnlyDictionary<UnitType, int> UNIT_VALUE =
			AgentSDK.DerivedGameConstants.UNIT_VALUE;

        /// <summary>
        /// GAME_SPEED - increase this value to make the game go faster
        /// </summary>
        internal static int GAME_SPEED = 1;

        /// <summary>
        /// Maximum game speed
        /// </summary>
        internal static readonly int MAX_GAME_SPEED = 30;

        /// <summary>
        /// Directions used to control unit animations
        /// </summary>
        internal static readonly Dictionary<Direction, Vector3Int> directions = new Dictionary<Direction, Vector3Int>()
        {
	        { Direction.S,  (Vector3Int.down) },
	        { Direction.SE, (new Vector3Int(1, -1, 0)) },
	        { Direction.E,  (Vector3Int.right) },
	        { Direction.NE, (new Vector3Int(1, 1, 0)) },
	        { Direction.N,  (Vector3Int.up) },
	        { Direction.NW, (new Vector3Int(-1, 1, 0)) },
	        { Direction.W,  (Vector3Int.left) },
	        { Direction.SW, (new Vector3Int(-1, -1, 0)) }
        };

        // =====================================================================
        // Initialization
        // =====================================================================

        /// <summary>
		/// Recompute speed-scaled constants from AgentSDK.
		/// Called when game speed changes.
		/// </summary>
		internal static void CalculateGameConstants()
        {
	        // Update mine health from GameManager config
	        HEALTH[UnitType.MINE] = GameManager.Instance.StartingMineGold;

	        // Compute all speed-scaled values from AgentSDK (single source of truth)
	        Derived = AgentSDK.DerivedGameConstants.Compute(GAME_SPEED);
        }

        /// <summary>
        /// Converts SDK ReadOnlyDictionary with IReadOnlyList values to mutable Dictionary with List values.
        /// TODO: Remove when Unity code is updated to use IReadOnlyList.
        /// </summary>
        private static Dictionary<UnitType, List<UnitType>> ToMutableListDict(IReadOnlyDictionary<UnitType, IReadOnlyList<UnitType>> source)
        {
            var result = new Dictionary<UnitType, List<UnitType>>();
            foreach (var kvp in source)
                result[kvp.Key] = new List<UnitType>(kvp.Value);
            return result;
        }

        /// <summary>
        /// Converts SDK Position-based unit sizes to Unity Vector3Int.
        /// Required because Unity uses Vector3Int, SDK uses Position.
        /// </summary>
        private static Dictionary<UnitType, Vector3Int> ToVector3IntDict(IReadOnlyDictionary<UnitType, Position> source)
        {
            var result = new Dictionary<UnitType, Vector3Int>();
            foreach (var kvp in source)
                result[kvp.Key] = new Vector3Int(kvp.Value.X, kvp.Value.Y, 0);
            return result;
        }
		#endregion
	}

}
