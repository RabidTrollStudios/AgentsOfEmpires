using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AgentSDK
{
    /// <summary>
    /// Game balance constants. These are the authoritative values used by the game engine.
    /// </summary>
    public static class GameConstants
    {
        /// <summary>Gold mining boost multiplier when a refinery is built</summary>
        public static readonly float MINING_BOOST = 2.0f;

        /// <summary>Base cost scalar (all costs are multiples of this)</summary>
        public static readonly float SCALAR_COST = 50f;

        /// <summary>Base mining capacity scalar</summary>
        public static readonly float SCALAR_MINING_CAPACITY = 10f;

        /// <summary>
        /// Cost to build or train each unit type.
        /// Check your Gold against these before issuing Build/Train commands.
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, float> COST =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE,        0.0f },
                { UnitType.WORKER,      SCALAR_COST },
                { UnitType.SOLDIER,     SCALAR_COST * 2 },
                { UnitType.ARCHER,      80f },
                { UnitType.BASE,        SCALAR_COST * 10 },
                { UnitType.BARRACKS,    SCALAR_COST * 8 },
                { UnitType.REFINERY,    SCALAR_COST * 6 },
            });

        /// <summary>
        /// Maximum health for each unit type.
        /// Note: Mine health (starting gold) varies per game configuration;
        /// read UnitInfo.Health at runtime for the actual value.
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, float> HEALTH =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE,        0.0f },
                { UnitType.WORKER,      200.0f },
                { UnitType.SOLDIER,     1200.0f },
                { UnitType.ARCHER,      800.0f },
                { UnitType.BASE,        4000.0f },
                { UnitType.BARRACKS,    2000.0f },
                { UnitType.REFINERY,    2000.0f },
            });

        /// <summary>
        /// How much gold a worker can carry per mining trip
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, float> MINING_CAPACITY =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE,        0.0f },
                { UnitType.WORKER,      SCALAR_MINING_CAPACITY * 5.0f },
                { UnitType.SOLDIER,     0.0f },
                { UnitType.ARCHER,      0.0f },
                { UnitType.BASE,        0.0f },
                { UnitType.BARRACKS,    0.0f },
                { UnitType.REFINERY,    0.0f },
            });

        /// <summary>
        /// Base damage per game-speed unit (multiply by game speed to get actual DPS).
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, float> BASE_DAMAGE =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE,        0f },
                { UnitType.WORKER,      0f },
                { UnitType.SOLDIER,     48f },
                { UnitType.ARCHER,      40f },
                { UnitType.BASE,        0f },
                { UnitType.BARRACKS,    0f },
                { UnitType.REFINERY,    0f },
            });

        /// <summary>
        /// Creation time multipliers (divide by game speed to get duration in seconds).
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, float> CREATION_TIME_MULTIPLIER =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE,        0f },
                { UnitType.WORKER,      2f },
                { UnitType.SOLDIER,     6f },
                { UnitType.ARCHER,      4f },
                { UnitType.BASE,        10f },
                { UnitType.BARRACKS,    20f },
                { UnitType.REFINERY,    15f },
            });

        /// <summary>
        /// Attack range for each unit type (in grid units)
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, float> ATTACK_RANGE =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE,        0.0f },
                { UnitType.WORKER,      0.0f },
                { UnitType.SOLDIER,     1.0f },
                { UnitType.ARCHER,      9.0f },
                { UnitType.BASE,        0.0f },
                { UnitType.BARRACKS,    0.0f },
                { UnitType.REFINERY,    0.0f },
            });

        /// <summary>
        /// Movement speed multiplier for each unit type (1.0 = baseline).
        /// Soldiers are slower (armored/heavy), archers are faster (light).
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, float> MOVEMENT_SPEED =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE,        0.0f },
                { UnitType.WORKER,      1.0f },
                { UnitType.SOLDIER,     0.75f },
                { UnitType.ARCHER,      1.0f },
                { UnitType.BASE,        0.0f },
                { UnitType.BARRACKS,    0.0f },
                { UnitType.REFINERY,    0.0f },
            });

        /// <summary>
        /// Size of each unit on the grid (width, height)
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, Position> UNIT_SIZE =
            new ReadOnlyDictionary<UnitType, Position>(new Dictionary<UnitType, Position>()
            {
                { UnitType.MINE,        new Position(3, 3) },
                { UnitType.WORKER,      new Position(1, 1) },
                { UnitType.SOLDIER,     new Position(1, 1) },
                { UnitType.ARCHER,      new Position(1, 1) },
                { UnitType.BASE,        new Position(4, 4) },
                { UnitType.BARRACKS,    new Position(3, 3) },
                { UnitType.REFINERY,    new Position(3, 3) },
            });

        /// <summary>
        /// Prerequisites required before building/training each unit type
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, IReadOnlyList<UnitType>> DEPENDENCY =
            new ReadOnlyDictionary<UnitType, IReadOnlyList<UnitType>>(new Dictionary<UnitType, IReadOnlyList<UnitType>>()
            {
                { UnitType.MINE,        new List<UnitType>() },
                { UnitType.WORKER,      new List<UnitType>() { UnitType.BASE } },
                { UnitType.SOLDIER,     new List<UnitType>() { UnitType.BARRACKS } },
                { UnitType.ARCHER,      new List<UnitType>() { UnitType.BARRACKS } },
                { UnitType.BASE,        new List<UnitType>() },
                { UnitType.BARRACKS,    new List<UnitType>() { UnitType.BASE } },
                { UnitType.REFINERY,    new List<UnitType>() { UnitType.BASE, UnitType.BARRACKS } },
            });

        /// <summary>
        /// What structures each unit type can build
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, IReadOnlyList<UnitType>> BUILDS =
            new ReadOnlyDictionary<UnitType, IReadOnlyList<UnitType>>(new Dictionary<UnitType, IReadOnlyList<UnitType>>()
            {
                { UnitType.MINE,        new List<UnitType>() },
                { UnitType.WORKER,      new List<UnitType>() { UnitType.BASE, UnitType.BARRACKS, UnitType.REFINERY } },
                { UnitType.SOLDIER,     new List<UnitType>() },
                { UnitType.ARCHER,      new List<UnitType>() },
                { UnitType.BASE,        new List<UnitType>() },
                { UnitType.BARRACKS,    new List<UnitType>() },
                { UnitType.REFINERY,    new List<UnitType>() },
            });

        /// <summary>
        /// What unit types each structure can train
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, IReadOnlyList<UnitType>> TRAINS =
            new ReadOnlyDictionary<UnitType, IReadOnlyList<UnitType>>(new Dictionary<UnitType, IReadOnlyList<UnitType>>()
            {
                { UnitType.MINE,        new List<UnitType>() },
                { UnitType.WORKER,      new List<UnitType>() },
                { UnitType.SOLDIER,     new List<UnitType>() },
                { UnitType.ARCHER,      new List<UnitType>() },
                { UnitType.BASE,        new List<UnitType>() { UnitType.WORKER } },
                { UnitType.BARRACKS,    new List<UnitType>() { UnitType.SOLDIER, UnitType.ARCHER } },
                { UnitType.REFINERY,    new List<UnitType>() },
            });

        /// <summary>Which unit types can move</summary>
        public static readonly ReadOnlyDictionary<UnitType, bool> CAN_MOVE =
            new ReadOnlyDictionary<UnitType, bool>(new Dictionary<UnitType, bool>()
            {
                { UnitType.MINE, false }, { UnitType.WORKER, true }, { UnitType.SOLDIER, true },
                { UnitType.ARCHER, true }, { UnitType.BASE, false }, { UnitType.BARRACKS, false },
                { UnitType.REFINERY, false },
            });

        /// <summary>Which unit types can build structures</summary>
        public static readonly ReadOnlyDictionary<UnitType, bool> CAN_BUILD =
            new ReadOnlyDictionary<UnitType, bool>(new Dictionary<UnitType, bool>()
            {
                { UnitType.MINE, false }, { UnitType.WORKER, true }, { UnitType.SOLDIER, false },
                { UnitType.ARCHER, false }, { UnitType.BASE, false }, { UnitType.BARRACKS, false },
                { UnitType.REFINERY, false },
            });

        /// <summary>Which unit types can train units</summary>
        public static readonly ReadOnlyDictionary<UnitType, bool> CAN_TRAIN =
            new ReadOnlyDictionary<UnitType, bool>(new Dictionary<UnitType, bool>()
            {
                { UnitType.MINE, false }, { UnitType.WORKER, false }, { UnitType.SOLDIER, false },
                { UnitType.ARCHER, false }, { UnitType.BASE, true }, { UnitType.BARRACKS, true },
                { UnitType.REFINERY, false },
            });

        /// <summary>Which unit types can attack</summary>
        public static readonly ReadOnlyDictionary<UnitType, bool> CAN_ATTACK =
            new ReadOnlyDictionary<UnitType, bool>(new Dictionary<UnitType, bool>()
            {
                { UnitType.MINE, false }, { UnitType.WORKER, false }, { UnitType.SOLDIER, true },
                { UnitType.ARCHER, true }, { UnitType.BASE, false }, { UnitType.BARRACKS, false },
                { UnitType.REFINERY, false },
            });

        /// <summary>Which unit types can gather resources</summary>
        public static readonly ReadOnlyDictionary<UnitType, bool> CAN_GATHER =
            new ReadOnlyDictionary<UnitType, bool>(new Dictionary<UnitType, bool>()
            {
                { UnitType.MINE, false }, { UnitType.WORKER, true }, { UnitType.SOLDIER, false },
                { UnitType.ARCHER, false }, { UnitType.BASE, false }, { UnitType.BARRACKS, false },
                { UnitType.REFINERY, false },
            });

        /// <summary>
        /// Compute effective attack range against a target, accounting for target unit size.
        /// Effective range = base attack range + max(target width, target height) / 2.
        /// </summary>
        public static float EffectiveAttackRange(UnitType attacker, UnitType target)
        {
            float baseRange = ATTACK_RANGE[attacker];
            var size = UNIT_SIZE[target];
            float halfSize = System.Math.Max(size.X, size.Y) / 2.0f;
            return baseRange + halfSize;
        }

        /// <summary>
        /// Damage multiplier based on attacker/defender type interaction.
        /// Soldiers (melee, armored) take 75% damage from ranged attacks.
        /// Archers (ranged, unarmored) take 125% damage from melee attacks.
        /// All other matchups are 1.0x.
        /// </summary>
        public static float DamageMultiplier(UnitType attacker, UnitType defender)
        {
            // Melee vs unarmored: soldiers deal 1.15x to archers
            if (attacker == UnitType.SOLDIER && defender == UnitType.ARCHER)
                return 1.15f;
            // Ranged vs armored: archers deal 0.85x to soldiers
            if (attacker == UnitType.ARCHER && defender == UnitType.SOLDIER)
                return 0.85f;
            return 1.0f;
        }
    }
}
