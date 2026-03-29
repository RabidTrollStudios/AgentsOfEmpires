using System.Collections.Generic;

namespace AgentSDK
{
    /// <summary>
    /// Game-speed-dependent constants computed from <see cref="GameConstants"/> base values.
    /// Both the Unity engine and the SimGame test harness use this single implementation
    /// so derived values are guaranteed identical at any game speed.
    /// </summary>
    public class DerivedGameConstants
    {
        /// <summary>Base move speed factor: pawn speed = gameSpeed * BASE_MOVE_SPEED.</summary>
        public const float BASE_MOVE_SPEED = 0.05f;

        /// <summary>Speed multipliers relative to pawn (1.0). Matches both Unity and SimGame.</summary>
        public static readonly IReadOnlyDictionary<UnitType, float> SPEED_MULTIPLIER =
            new Dictionary<UnitType, float>
            {
                { UnitType.MINE, 0f }, { UnitType.BASE, 0f }, { UnitType.BARRACKS, 0f },
                { UnitType.ARCHERY, 0f }, { UnitType.TOWER, 0f }, { UnitType.MONASTERY, 0f },
                { UnitType.PAWN, 1.0f },
                { UnitType.WARRIOR, 2.1f },
                { UnitType.ARCHER, 3.0f },
                { UnitType.LANCER, 3.45f },
                { UnitType.MONK, 0.85f },
            };

        /// <summary>Unit scoring for timeout win condition: ceil(COST / SCORING_SCALAR).</summary>
        public static readonly IReadOnlyDictionary<UnitType, int> UNIT_VALUE = ComputeUnitValues();

        private static Dictionary<UnitType, int> ComputeUnitValues()
        {
            var values = new Dictionary<UnitType, int>();
            foreach (var kvp in GameConstants.COST)
            {
                values[kvp.Key] = (int)System.Math.Ceiling(kvp.Value / GameConstants.SCORING_SCALAR);
            }
            return values;
        }

        // ---- Computed fields ----

        public int GameSpeed { get; }

        /// <summary>Creation time scalar: 1 / gameSpeed.</summary>
        public float ScalarCreationTime { get; }

        /// <summary>Damage scalar: gameSpeed.</summary>
        public float ScalarDamage { get; }

        /// <summary>Base moving speed (pawn): gameSpeed * BASE_MOVE_SPEED.</summary>
        public float ScalarMovingSpeed { get; }

        /// <summary>Mining speed scalar: gameSpeed.</summary>
        public float ScalarMiningSpeed { get; }

        /// <summary>Mana regen per second (scaled by game speed).</summary>
        public float ManaRegen { get; }

        /// <summary>Gold per second for PAWN mining.</summary>
        public float MiningSpeed { get; }

        /// <summary>Gold per trip for PAWN.</summary>
        public float MiningCapacity { get; }

        /// <summary>Time to create each unit in seconds at current game speed.</summary>
        public Dictionary<UnitType, float> CreationTime { get; }

        /// <summary>Damage per second for each unit at current game speed.</summary>
        public Dictionary<UnitType, float> Damage { get; }

        /// <summary>Movement speed (cells/tick) for each unit at current game speed.</summary>
        public Dictionary<UnitType, float> MovingSpeed { get; }

        /// <summary>Mining speed for each unit type.</summary>
        public Dictionary<UnitType, float> MiningSpeedPerUnit { get; }

        /// <summary>Mining capacity for each unit type.</summary>
        public Dictionary<UnitType, float> MiningCapacityPerUnit { get; }

        /// <summary>Cost for each unit type (direct copy from GameConstants).</summary>
        public Dictionary<UnitType, float> Cost { get; }

        private DerivedGameConstants(int gameSpeed)
        {
            GameSpeed = gameSpeed;

            ScalarCreationTime = gameSpeed > 0 ? 1f / gameSpeed : float.PositiveInfinity;
            ScalarDamage = gameSpeed;
            ScalarMovingSpeed = gameSpeed * BASE_MOVE_SPEED;
            ScalarMiningSpeed = gameSpeed;
            ManaRegen = GameConstants.BASE_MANA_REGEN * gameSpeed;
            MiningSpeed = gameSpeed * 20f;
            MiningCapacity = GameConstants.MINING_CAPACITY[UnitType.PAWN];

            CreationTime = new Dictionary<UnitType, float>();
            foreach (var kvp in GameConstants.CREATION_TIME_MULTIPLIER)
                CreationTime[kvp.Key] = kvp.Value * ScalarCreationTime;

            Damage = new Dictionary<UnitType, float>();
            foreach (var kvp in GameConstants.BASE_DAMAGE)
                Damage[kvp.Key] = kvp.Value * ScalarDamage;

            MovingSpeed = new Dictionary<UnitType, float>();
            foreach (var kvp in SPEED_MULTIPLIER)
                MovingSpeed[kvp.Key] = ScalarMovingSpeed * kvp.Value;

            MiningSpeedPerUnit = new Dictionary<UnitType, float>();
            foreach (var kvp in SPEED_MULTIPLIER)
                MiningSpeedPerUnit[kvp.Key] = kvp.Key == UnitType.PAWN ? ScalarMiningSpeed * 20f : 0f;

            MiningCapacityPerUnit = new Dictionary<UnitType, float>(GameConstants.MINING_CAPACITY);

            Cost = new Dictionary<UnitType, float>(GameConstants.COST);
        }

        /// <summary>
        /// Compute all game-speed-dependent constants for the given game speed.
        /// </summary>
        public static DerivedGameConstants Compute(int gameSpeed)
        {
            return new DerivedGameConstants(gameSpeed);
        }
    }
}
