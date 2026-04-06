using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AgentSDK
{
    /// <summary>
    /// Game balance constants. These are the authoritative values used by the game engine.
    /// </summary>
    public static class GameConstants
    {
        /// <summary>Base cost scalar (all costs are multiples of this)</summary>
        public static readonly float SCALAR_COST = 50f;

        /// <summary>Base mining capacity scalar</summary>
        public static readonly float SCALAR_MINING_CAPACITY = 10f;

        /// <summary>Scoring divisor: UNIT_VALUE = ceil(COST / SCORING_SCALAR)</summary>
        public static readonly float SCORING_SCALAR = 20f;

        /// <summary>
        /// Cost to build or train each unit type.
        /// Check your Gold against these before issuing Build/Train commands.
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, float> COST =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE,        0.0f },
                { UnitType.PAWN,      75f },
                { UnitType.WARRIOR,     85f },
                { UnitType.ARCHER,      80f },
                { UnitType.BASE,        SCALAR_COST * 10 },
                { UnitType.BARRACKS,    SCALAR_COST * 8 },
                { UnitType.ARCHERY,     SCALAR_COST * 7 },
                { UnitType.LANCER,      90f },
                { UnitType.TOWER,       SCALAR_COST * 8 },
                { UnitType.MONASTERY,   SCALAR_COST * 7 },
                { UnitType.MONK,        90f },
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
                { UnitType.PAWN,      200.0f },
                { UnitType.WARRIOR,     2000.0f },
                { UnitType.ARCHER,      600.0f },
                { UnitType.BASE,        8000.0f },
                { UnitType.BARRACKS,    4000.0f },
                { UnitType.ARCHERY,     4000.0f },
                { UnitType.LANCER,      900.0f },
                { UnitType.TOWER,       3000.0f },
                { UnitType.MONASTERY,   3500.0f },
                { UnitType.MONK,        400.0f },
            });

        /// <summary>
        /// How much gold a pawn can carry per mining trip
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, float> MINING_CAPACITY =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE,        0.0f },
                { UnitType.PAWN,      25f },
                { UnitType.WARRIOR,     0.0f },
                { UnitType.ARCHER,      0.0f },
                { UnitType.BASE,        0.0f },
                { UnitType.BARRACKS,    0.0f },
                { UnitType.ARCHERY,     0.0f },
                { UnitType.LANCER,      0.0f },
                { UnitType.TOWER,       0.0f },
                { UnitType.MONASTERY,   0.0f },
                { UnitType.MONK,        0.0f },
            });

        /// <summary>
        /// Base damage per game-speed unit (multiply by game speed to get actual DPS).
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, float> BASE_DAMAGE =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE,        0f },
                { UnitType.PAWN,      0f },
                { UnitType.WARRIOR,     50f },
                { UnitType.ARCHER,      28f },
                { UnitType.BASE,        0f },
                { UnitType.BARRACKS,    0f },
                { UnitType.ARCHERY,     0f },
                { UnitType.LANCER,      35f },
                { UnitType.TOWER,       0f },
                { UnitType.MONASTERY,   0f },
                { UnitType.MONK,        0f },
            });

        /// <summary>
        /// Creation time multipliers (divide by game speed to get duration in seconds).
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, float> CREATION_TIME_MULTIPLIER =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE,        0f },
                { UnitType.PAWN,      2f },
                { UnitType.WARRIOR,     4f },
                { UnitType.ARCHER,      4f },
                { UnitType.BASE,        10f },
                { UnitType.BARRACKS,    20f },
                { UnitType.ARCHERY,     18f },
                { UnitType.LANCER,      4f },
                { UnitType.TOWER,       15f },
                { UnitType.MONASTERY,   18f },
                { UnitType.MONK,        5f },
            });

        /// <summary>
        /// Base attack cooldown in seconds (at game speed 1). Time between discrete attacks.
        /// Matches animation cycle length: 6 frames at 12 FPS = 0.5s.
        /// Actual cooldown = BASE_ATTACK_COOLDOWN / gameSpeed.
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, float> BASE_ATTACK_COOLDOWN =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE,        0f },
                { UnitType.PAWN,        0f },
                { UnitType.WARRIOR,     0.5f },
                { UnitType.ARCHER,      0.5f },
                { UnitType.BASE,        0f },
                { UnitType.BARRACKS,    0f },
                { UnitType.ARCHERY,     0f },
                { UnitType.LANCER,      0.5f },
                { UnitType.TOWER,       0f },
                { UnitType.MONASTERY,   0f },
                { UnitType.MONK,        0f },
            });

        /// <summary>
        /// Attack range for each unit type (in grid units)
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, float> ATTACK_RANGE =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE,        0.0f },
                { UnitType.PAWN,      0.0f },
                { UnitType.WARRIOR,     1.0f },
                { UnitType.ARCHER,      8.0f },
                { UnitType.BASE,        0.0f },
                { UnitType.BARRACKS,    0.0f },
                { UnitType.ARCHERY,     0.0f },
                { UnitType.LANCER,      2.0f },
                { UnitType.TOWER,       0.0f },
                { UnitType.MONASTERY,   0.0f },
                { UnitType.MONK,        0.0f },
            });


        /// <summary>
        /// Size of each unit on the grid (width, height)
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, Position> UNIT_SIZE =
            new ReadOnlyDictionary<UnitType, Position>(new Dictionary<UnitType, Position>()
            {
                { UnitType.MINE,        new Position(2, 2) },
                { UnitType.PAWN,      new Position(1, 1) },
                { UnitType.WARRIOR,     new Position(1, 1) },
                { UnitType.ARCHER,      new Position(1, 1) },
                { UnitType.BASE,        new Position(6, 4) },
                { UnitType.BARRACKS,    new Position(3, 3) },
                { UnitType.ARCHERY,     new Position(3, 3) },
                { UnitType.LANCER,      new Position(1, 1) },
                { UnitType.TOWER,       new Position(2, 2) },
                { UnitType.MONASTERY,   new Position(3, 3) },
                { UnitType.MONK,        new Position(1, 1) },
            });

        /// <summary>
        /// Prerequisites required before building/training each unit type
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, IReadOnlyList<UnitType>> DEPENDENCY =
            new ReadOnlyDictionary<UnitType, IReadOnlyList<UnitType>>(new Dictionary<UnitType, IReadOnlyList<UnitType>>()
            {
                { UnitType.MINE,        new List<UnitType>() },
                { UnitType.PAWN,      new List<UnitType>() { UnitType.BASE } },
                { UnitType.WARRIOR,     new List<UnitType>() { UnitType.BARRACKS } },
                { UnitType.ARCHER,      new List<UnitType>() { UnitType.ARCHERY } },
                { UnitType.BASE,        new List<UnitType>() },
                { UnitType.BARRACKS,    new List<UnitType>() { UnitType.BASE } },
                { UnitType.ARCHERY,     new List<UnitType>() { UnitType.BASE } },
                { UnitType.LANCER,      new List<UnitType>() { UnitType.TOWER } },
                { UnitType.TOWER,       new List<UnitType>() { UnitType.BASE } },
                { UnitType.MONASTERY,   new List<UnitType>() { UnitType.BASE } },
                { UnitType.MONK,        new List<UnitType>() { UnitType.MONASTERY } },
            });

        /// <summary>
        /// What structures each unit type can build
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, IReadOnlyList<UnitType>> BUILDS =
            new ReadOnlyDictionary<UnitType, IReadOnlyList<UnitType>>(new Dictionary<UnitType, IReadOnlyList<UnitType>>()
            {
                { UnitType.MINE,        new List<UnitType>() },
                { UnitType.PAWN,      new List<UnitType>() { UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER, UnitType.MONASTERY } },
                { UnitType.WARRIOR,     new List<UnitType>() },
                { UnitType.ARCHER,      new List<UnitType>() },
                { UnitType.BASE,        new List<UnitType>() },
                { UnitType.BARRACKS,    new List<UnitType>() },
                { UnitType.ARCHERY,     new List<UnitType>() },
                { UnitType.LANCER,      new List<UnitType>() },
                { UnitType.TOWER,       new List<UnitType>() },
                { UnitType.MONASTERY,   new List<UnitType>() },
                { UnitType.MONK,        new List<UnitType>() },
            });

        /// <summary>
        /// What unit types each structure can train
        /// </summary>
        public static readonly ReadOnlyDictionary<UnitType, IReadOnlyList<UnitType>> TRAINS =
            new ReadOnlyDictionary<UnitType, IReadOnlyList<UnitType>>(new Dictionary<UnitType, IReadOnlyList<UnitType>>()
            {
                { UnitType.MINE,        new List<UnitType>() },
                { UnitType.PAWN,      new List<UnitType>() },
                { UnitType.WARRIOR,     new List<UnitType>() },
                { UnitType.ARCHER,      new List<UnitType>() },
                { UnitType.BASE,        new List<UnitType>() { UnitType.PAWN } },
                { UnitType.BARRACKS,    new List<UnitType>() { UnitType.WARRIOR } },
                { UnitType.ARCHERY,     new List<UnitType>() { UnitType.ARCHER } },
                { UnitType.LANCER,      new List<UnitType>() },
                { UnitType.TOWER,       new List<UnitType>() { UnitType.LANCER } },
                { UnitType.MONASTERY,   new List<UnitType>() { UnitType.MONK } },
                { UnitType.MONK,        new List<UnitType>() },
            });

        /// <summary>Which unit types can move</summary>
        public static readonly ReadOnlyDictionary<UnitType, bool> CAN_MOVE =
            new ReadOnlyDictionary<UnitType, bool>(new Dictionary<UnitType, bool>()
            {
                { UnitType.MINE, false }, { UnitType.PAWN, true }, { UnitType.WARRIOR, true },
                { UnitType.ARCHER, true }, { UnitType.BASE, false }, { UnitType.BARRACKS, false },
                { UnitType.ARCHERY, false }, { UnitType.LANCER, true }, { UnitType.TOWER, false },
                { UnitType.MONASTERY, false }, { UnitType.MONK, true },
            });

        /// <summary>Which unit types can build structures</summary>
        public static readonly ReadOnlyDictionary<UnitType, bool> CAN_BUILD =
            new ReadOnlyDictionary<UnitType, bool>(new Dictionary<UnitType, bool>()
            {
                { UnitType.MINE, false }, { UnitType.PAWN, true }, { UnitType.WARRIOR, false },
                { UnitType.ARCHER, false }, { UnitType.BASE, false }, { UnitType.BARRACKS, false },
                { UnitType.ARCHERY, false }, { UnitType.LANCER, false }, { UnitType.TOWER, false },
                { UnitType.MONASTERY, false }, { UnitType.MONK, false },
            });

        /// <summary>Which unit types can train units</summary>
        public static readonly ReadOnlyDictionary<UnitType, bool> CAN_TRAIN =
            new ReadOnlyDictionary<UnitType, bool>(new Dictionary<UnitType, bool>()
            {
                { UnitType.MINE, false }, { UnitType.PAWN, false }, { UnitType.WARRIOR, false },
                { UnitType.ARCHER, false }, { UnitType.BASE, true }, { UnitType.BARRACKS, true },
                { UnitType.ARCHERY, true }, { UnitType.LANCER, false }, { UnitType.TOWER, true },
                { UnitType.MONASTERY, true }, { UnitType.MONK, false },
            });

        /// <summary>Which unit types can attack</summary>
        public static readonly ReadOnlyDictionary<UnitType, bool> CAN_ATTACK =
            new ReadOnlyDictionary<UnitType, bool>(new Dictionary<UnitType, bool>()
            {
                { UnitType.MINE, false }, { UnitType.PAWN, false }, { UnitType.WARRIOR, true },
                { UnitType.ARCHER, true }, { UnitType.BASE, false }, { UnitType.BARRACKS, false },
                { UnitType.ARCHERY, false }, { UnitType.LANCER, true }, { UnitType.TOWER, false },
                { UnitType.MONASTERY, false }, { UnitType.MONK, false },
            });

        /// <summary>Which unit types can gather resources</summary>
        public static readonly ReadOnlyDictionary<UnitType, bool> CAN_GATHER =
            new ReadOnlyDictionary<UnitType, bool>(new Dictionary<UnitType, bool>()
            {
                { UnitType.MINE, false }, { UnitType.PAWN, true }, { UnitType.WARRIOR, false },
                { UnitType.ARCHER, false }, { UnitType.BASE, false }, { UnitType.BARRACKS, false },
                { UnitType.ARCHERY, false }, { UnitType.LANCER, false }, { UnitType.TOWER, false },
                { UnitType.MONASTERY, false }, { UnitType.MONK, false },
            });

        /// <summary>Which unit types can heal allied units</summary>
        public static readonly ReadOnlyDictionary<UnitType, bool> CAN_HEAL =
            new ReadOnlyDictionary<UnitType, bool>(new Dictionary<UnitType, bool>()
            {
                { UnitType.MINE, false }, { UnitType.PAWN, false }, { UnitType.WARRIOR, false },
                { UnitType.ARCHER, false }, { UnitType.BASE, false }, { UnitType.BARRACKS, false },
                { UnitType.ARCHERY, false }, { UnitType.LANCER, false }, { UnitType.TOWER, false },
                { UnitType.MONASTERY, false }, { UnitType.MONK, true },
            });

        /// <summary>Heal range for each unit type (in grid units)</summary>
        public static readonly ReadOnlyDictionary<UnitType, float> HEAL_RANGE =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE, 0f }, { UnitType.PAWN, 0f }, { UnitType.WARRIOR, 0f },
                { UnitType.ARCHER, 0f }, { UnitType.BASE, 0f }, { UnitType.BARRACKS, 0f },
                { UnitType.ARCHERY, 0f }, { UnitType.LANCER, 0f }, { UnitType.TOWER, 0f },
                { UnitType.MONASTERY, 0f }, { UnitType.MONK, 4.0f },
            });

        /// <summary>Maximum mana pool for each unit type</summary>
        public static readonly ReadOnlyDictionary<UnitType, float> MAX_MANA =
            new ReadOnlyDictionary<UnitType, float>(new Dictionary<UnitType, float>()
            {
                { UnitType.MINE, 0f }, { UnitType.PAWN, 0f }, { UnitType.WARRIOR, 0f },
                { UnitType.ARCHER, 0f }, { UnitType.BASE, 0f }, { UnitType.BARRACKS, 0f },
                { UnitType.ARCHERY, 0f }, { UnitType.LANCER, 0f }, { UnitType.TOWER, 0f },
                { UnitType.MONASTERY, 0f }, { UnitType.MONK, 100f },
            });

        /// <summary>Mana cost per heal action (25% of max mana)</summary>
        public static readonly float MANA_COST = 25f;

        /// <summary>Base mana regeneration rate (scaled by game speed)</summary>
        /// <summary>Base mana regeneration rate (scaled by game speed). Full pool (100) in ~3s at speed 20.</summary>
        public static readonly float BASE_MANA_REGEN = 100f / 60f;

        /// <summary>Flat HP restored per heal action</summary>
        public static readonly float HEAL_AMOUNT = 100f;

        // --- Unit Abilities ---

        /// <summary>Warrior Charge: speed multiplier when charging toward an enemy.</summary>
        public static readonly float CHARGE_SPEED_MULTIPLIER = 3.0f;
        /// <summary>Warrior Charge: triggers when an attack target is within this range.</summary>
        public static readonly float CHARGE_RANGE = 8.0f;
        /// <summary>Warrior Charge: duration of the speed boost (seconds at game speed 1).</summary>
        public static readonly float CHARGE_DURATION = 2.0f;
        /// <summary>Warrior Charge: cooldown between charges (seconds at game speed 1).</summary>
        public static readonly float CHARGE_COOLDOWN = 3.0f;

        /// <summary>Archer Volley: damage multiplier on the first hit against a new target.</summary>
        public static readonly float VOLLEY_BONUS_MULTIPLIER = 1.75f;
        /// <summary>Archer Volley: seconds before the same target can receive volley bonus again.</summary>
        public static readonly float VOLLEY_COOLDOWN = 3.0f;

        /// <summary>Lancer Joust: minimum distance traveled since last attack to trigger bonus.</summary>
        public static readonly float JOUST_MIN_DISTANCE = 3.0f;
        /// <summary>Lancer Joust: damage multiplier on first hit after moving minimum distance.</summary>
        public static readonly float JOUST_BONUS_MULTIPLIER = 1.75f;

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
        /// Rock-paper-scissors triangle: Warrior→Archer→Lancer→Warrior.
        /// Strong matchup = 1.5x damage, weak matchup = 0.67x damage.
        /// </summary>
        public static float DamageMultiplier(UnitType attacker, UnitType defender)
        {
            // Warrior beats Archer (close combat overwhelms light ranged)
            if (attacker == UnitType.WARRIOR && defender == UnitType.ARCHER)
                return 1.5f;
            if (attacker == UnitType.ARCHER && defender == UnitType.WARRIOR)
                return 0.67f;
            // Archer beats Lancer (ranged fire picks off cavalry)
            if (attacker == UnitType.ARCHER && defender == UnitType.LANCER)
                return 1.5f;
            if (attacker == UnitType.LANCER && defender == UnitType.ARCHER)
                return 0.67f;
            // Lancer beats Warrior (lance reach defeats heavy melee)
            if (attacker == UnitType.LANCER && defender == UnitType.WARRIOR)
                return 1.5f;
            if (attacker == UnitType.WARRIOR && defender == UnitType.LANCER)
                return 0.67f;
            return 1.0f;
        }

        /// <summary>
        /// Validate that every UnitType-keyed dictionary covers all enum values.
        /// Returns a list of error messages (empty if all dictionaries are complete).
        /// Call at game startup or in tests to catch missing entries early.
        /// </summary>
        public static List<string> ValidateDictionaries()
        {
            var errors = new List<string>();
            var allTypes = (UnitType[])Enum.GetValues(typeof(UnitType));

            void Check<T>(IReadOnlyDictionary<UnitType, T> dict, string name)
            {
                foreach (var ut in allTypes)
                {
                    if (!dict.ContainsKey(ut))
                        errors.Add($"{name} missing entry for {ut}");
                }
            }

            Check(COST, nameof(COST));
            Check(HEALTH, nameof(HEALTH));
            Check(MINING_CAPACITY, nameof(MINING_CAPACITY));
            Check(BASE_DAMAGE, nameof(BASE_DAMAGE));
            Check(CREATION_TIME_MULTIPLIER, nameof(CREATION_TIME_MULTIPLIER));
            Check(ATTACK_RANGE, nameof(ATTACK_RANGE));
            Check(UNIT_SIZE, nameof(UNIT_SIZE));
            Check(DEPENDENCY, nameof(DEPENDENCY));
            Check(BUILDS, nameof(BUILDS));
            Check(TRAINS, nameof(TRAINS));
            Check(CAN_MOVE, nameof(CAN_MOVE));
            Check(CAN_BUILD, nameof(CAN_BUILD));
            Check(CAN_TRAIN, nameof(CAN_TRAIN));
            Check(CAN_ATTACK, nameof(CAN_ATTACK));
            Check(CAN_GATHER, nameof(CAN_GATHER));
            Check(CAN_HEAL, nameof(CAN_HEAL));
            Check(HEAL_RANGE, nameof(HEAL_RANGE));
            Check(MAX_MANA, nameof(MAX_MANA));
            Check(DerivedGameConstants.SPEED_MULTIPLIER, "SPEED_MULTIPLIER");
            Check(DerivedGameConstants.UNIT_VALUE, "UNIT_VALUE");

            return errors;
        }
    }
}
