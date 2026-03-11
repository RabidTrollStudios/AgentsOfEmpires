using System.Collections.Generic;
using UnityEngine;
using AgentSDK;
using GameManager.EnumTypes;

namespace GameManager
{
    /// <summary>
    /// Constants - set of game-defining constants.
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
        public Dictionary<UnitType, float> Damage => DAMAGE;

        /// <summary>
        /// Moving speed associated with each unit
        /// </summary>
        public Dictionary<UnitType, float> MovingSpeed => MOVING_SPEED;

        /// <summary>
        /// Mining speed associated with each unit
        /// </summary>
        public Dictionary<UnitType, float> MiningSpeed => MINING_SPEED;

        /// <summary>
        /// Mining time associated with each unit
        /// </summary>
        public Dictionary<UnitType, float> MiningCapacity => MINING_CAPACITY;

        /// <summary>
        /// Cost associated with each unit
        /// </summary>
        public Dictionary<UnitType, float> Cost => COST;

        /// <summary>
        /// Creation time associated with each unit
        /// </summary>
        public Dictionary<UnitType, float> CreationTime => CREATION_TIME;

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
        public Dictionary<UnitType, bool> CanMove => CAN_MOVE;

        /// <summary>
        /// Can unit build
        /// </summary>
        public Dictionary<UnitType, bool> CanBuild => CAN_BUILD;

        /// <summary>
        /// Can unit train
        /// </summary>
        public Dictionary<UnitType, bool> CanTrain => CAN_TRAIN;

        /// <summary>
        /// Can unit attack
        /// </summary>
        public Dictionary<UnitType, bool> CanAttack => CAN_ATTACK;

        /// <summary>
        /// Can unit gather
        /// </summary>
        public Dictionary<UnitType, bool> CanGather => CAN_GATHER;

        /// <summary>
        /// Unit Attack range
        /// </summary>
        public Dictionary<UnitType, float> AttackRange => ATTACK_RANGE;

        /// <summary>
        /// Unit size
        /// </summary>
        public Dictionary<UnitType, Vector3Int> UnitSize => UNIT_SIZE;

        #endregion

		#region Static Arrays

		/// <summary>
		/// Initial damage associated with each unit
		/// </summary>
		public static Dictionary<UnitType, float> DAMAGE;

        /// <summary>
        /// Initial health associated with each unit
        /// </summary>
        public static readonly Dictionary<UnitType, float> HEALTH = new Dictionary<UnitType, float>(GameConstants.HEALTH);


        /// <summary>
        /// Time to mine a resource in seconds
        /// </summary>
        public static Dictionary<UnitType, float> MINING_CAPACITY;

        /// <summary>
        /// Cost to build each unit
        /// </summary>
        public static Dictionary<UnitType, float> COST;

		/// <summary>
		/// Dependencies of each unit in order to build/train them (delegated from SDK)
		/// </summary>
		public static readonly Dictionary<UnitType, List<UnitType>> DEPENDENCY = ToMutableListDict(GameConstants.DEPENDENCY);

		/// <summary>
		/// Set of Units built by each unit (delegated from SDK)
		/// </summary>
		public static readonly Dictionary<UnitType, List<UnitType>> BUILDS = ToMutableListDict(GameConstants.BUILDS);

		/// <summary>
		/// Set of Units trained by each unit (delegated from SDK)
		/// </summary>
		public static readonly Dictionary<UnitType, List<UnitType>> TRAINS = ToMutableListDict(GameConstants.TRAINS);

		/// <summary>
		/// Which Units can move (delegated from SDK)
		/// </summary>
		public static readonly Dictionary<UnitType, bool> CAN_MOVE = new Dictionary<UnitType, bool>(GameConstants.CAN_MOVE);

		/// <summary>
		/// Which Units can build (delegated from SDK)
		/// </summary>
		public static readonly Dictionary<UnitType, bool> CAN_BUILD = new Dictionary<UnitType, bool>(GameConstants.CAN_BUILD);

		/// <summary>
		/// Which Units can train (delegated from SDK)
		/// </summary>
		public static readonly Dictionary<UnitType, bool> CAN_TRAIN = new Dictionary<UnitType, bool>(GameConstants.CAN_TRAIN);

		/// <summary>
		/// Which Units can attack (delegated from SDK)
		/// </summary>
		public static readonly Dictionary<UnitType, bool> CAN_ATTACK = new Dictionary<UnitType, bool>(GameConstants.CAN_ATTACK);

		/// <summary>
		/// Which Units can gather (delegated from SDK)
		/// </summary>
		public static readonly Dictionary<UnitType, bool> CAN_GATHER = new Dictionary<UnitType, bool>(GameConstants.CAN_GATHER);

		/// <summary>
		/// Attack range for each unit (delegated from SDK)
		/// </summary>
		public static readonly Dictionary<UnitType, float> ATTACK_RANGE = new Dictionary<UnitType, float>(GameConstants.ATTACK_RANGE);

		/// <summary>
		/// Raw unit sizes as Vector3Int (converted from SDK Position)
		/// </summary>
		public static readonly Dictionary<UnitType, Vector3Int> UNIT_SIZE = ToVector3IntDict(GameConstants.UNIT_SIZE);


		/// <summary>
		/// Primary damage value
		/// </summary>
		internal static float SCALAR_DAMAGE;

		/// <summary>
		/// Base move speed factor applied to GAME_SPEED to get pawn speed.
		/// Pawn speed = GAME_SPEED * BASE_MOVE_SPEED.
		/// </summary>
		private const float BASE_MOVE_SPEED = 0.05f;

		/// <summary>Warrior moves at 2.1x pawn speed (slowest combat unit, heavy armored).</summary>
		private const float WARRIOR_SPEED_MULTIPLIER = 2.1f;

		/// <summary>Archer moves at 3.0x pawn speed (light, fast).</summary>
		private const float ARCHER_SPEED_MULTIPLIER = 3.0f;

		/// <summary>Lancer moves at 3.45x pawn speed (fastest combat unit, cavalry).</summary>
		private const float LANCER_SPEED_MULTIPLIER = 3.45f;

		/// <summary>
        /// Base moving speed (= pawn speed). Multiply by unit speed multipliers for other units.
        /// </summary>
        internal static float SCALAR_MOVING_SPEED;

        /// <summary>
        /// Speed at which each unit moves
        /// </summary>
        internal static Dictionary<UnitType, float> MOVING_SPEED;

        /// <summary>
        /// Mining speed constants (per second)
        /// </summary>		
        internal static float SCALAR_MINING_SPEED;

        /// <summary>
        /// Speed at which each unit mines resources
        /// </summary>
        internal static Dictionary<UnitType, float> MINING_SPEED;

        /// <summary>
        /// Time constants (delegated from SDK)
        /// </summary>
        internal static float SCALAR_MINING_CAPACITY = GameConstants.SCALAR_MINING_CAPACITY;

        /// <summary>
        /// Cost constants (delegated from SDK)
        /// </summary>
        internal static float SCALAR_COST = GameConstants.SCALAR_COST;

        /// <summary>
        /// Creation time constants
        /// </summary>
        internal static float SCALAR_CREATION_TIME;

        /// <summary>
        /// Time to create each unit in seconds
        /// </summary>
        internal static Dictionary<UnitType, float> CREATION_TIME;

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

		/// <summary>
		/// Stores the values for units to compute the "winner" if no one destroys the other agent
		/// </summary>
		internal static readonly Dictionary<UnitType, int> UNIT_VALUE = new Dictionary<UnitType, int>()
		{
			{ UnitType.MINE,        0 },
			{ UnitType.PAWN,      1 },
			{ UnitType.WARRIOR,     4 },
			{ UnitType.ARCHER,      5 },
			{ UnitType.BASE,        2 },
			{ UnitType.BARRACKS,    3 },
			{ UnitType.ARCHERY,     3 },
			{ UnitType.LANCER,      5 },
			{ UnitType.TOWER,       3 },
		};

        /// <summary>
		/// InitializeRound the game constants
		/// </summary>
		internal static void CalculateGameConstants()
        {
	        // Update mine health from GameManager (can't do this in the static initializer
	        // because the singleton may not exist yet when the type initializer runs)
	        HEALTH[UnitType.MINE] = GameManager.Instance.StartingMineGold;

	        SCALAR_MOVING_SPEED = GAME_SPEED * BASE_MOVE_SPEED;
	        MOVING_SPEED = new Dictionary<UnitType, float>()
	        {
		        { UnitType.MINE,        0.0f },
		        { UnitType.PAWN,      SCALAR_MOVING_SPEED },
		        { UnitType.WARRIOR,     SCALAR_MOVING_SPEED * WARRIOR_SPEED_MULTIPLIER },
		        { UnitType.ARCHER,      SCALAR_MOVING_SPEED * ARCHER_SPEED_MULTIPLIER },
		        { UnitType.BASE,        0.0f },
		        { UnitType.BARRACKS,    0.0f },
		        { UnitType.ARCHERY,     0.0f },
		        { UnitType.LANCER,      SCALAR_MOVING_SPEED * LANCER_SPEED_MULTIPLIER },
		        { UnitType.TOWER,       0.0f },
	        };

	        SCALAR_MINING_SPEED = GAME_SPEED;
	        MINING_SPEED = new Dictionary<UnitType, float>()
	        {
		        { UnitType.MINE,        0.0f},
		        { UnitType.PAWN,      SCALAR_MINING_SPEED * 20.0f},
		        { UnitType.WARRIOR,     0.0f },
		        { UnitType.ARCHER,      0.0f },
		        { UnitType.BASE,        0.0f },
		        { UnitType.BARRACKS,    0.0f },
		        { UnitType.ARCHERY,     0.0f },
		        { UnitType.LANCER,      0.0f },
		        { UnitType.TOWER,       0.0f },
	        };

	        COST = new Dictionary<UnitType, float>(GameConstants.COST);

			MINING_CAPACITY = new Dictionary<UnitType, float>(GameConstants.MINING_CAPACITY);

			SCALAR_CREATION_TIME = GAME_SPEED > 0 ? 1f / GAME_SPEED : float.PositiveInfinity;
	        CREATION_TIME = new Dictionary<UnitType, float>();
	        foreach (var kvp in GameConstants.CREATION_TIME_MULTIPLIER)
		        CREATION_TIME[kvp.Key] = kvp.Value * SCALAR_CREATION_TIME;

	        SCALAR_DAMAGE = GAME_SPEED;
	        DAMAGE = new Dictionary<UnitType, float>();
	        foreach (var kvp in GameConstants.BASE_DAMAGE)
		        DAMAGE[kvp.Key] = kvp.Value * SCALAR_DAMAGE;

        }

        /// <summary>
        /// Converts SDK ReadOnlyDictionary with IReadOnlyList values to mutable Dictionary with List values
        /// </summary>
        private static Dictionary<UnitType, List<UnitType>> ToMutableListDict(IReadOnlyDictionary<UnitType, IReadOnlyList<UnitType>> source)
        {
            var result = new Dictionary<UnitType, List<UnitType>>();
            foreach (var kvp in source)
                result[kvp.Key] = new List<UnitType>(kvp.Value);
            return result;
        }

        /// <summary>
        /// Converts SDK Position-based unit sizes to Unity Vector3Int
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
