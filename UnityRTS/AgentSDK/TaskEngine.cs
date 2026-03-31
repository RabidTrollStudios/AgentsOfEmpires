using System;
using System.Collections.Generic;

namespace AgentSDK
{
    /// <summary>
    /// Shared game-rule logic for unit tasks. Both the Unity engine and SimGame
    /// call these methods so task behavior is guaranteed identical.
    ///
    /// Methods are pure functions of their inputs — no Unity dependencies.
    /// </summary>
    public static class TaskEngine
    {
        #region Movement

        /// <summary>
        /// Result of attempting to move a unit one cell along its path.
        /// </summary>
        public enum MoveResult
        {
            /// <summary>Successfully moved to the next cell.</summary>
            Moved,
            /// <summary>Blocked by a mobile unit (walkable but not buildable).</summary>
            BlockedByUnit,
            /// <summary>Blocked by terrain/building (not walkable). Needs re-path.</summary>
            BlockedByTerrain,
            /// <summary>Not enough accumulated movement to reach next cell.</summary>
            InsufficientMovement,
        }

        /// <summary>
        /// Attempt to move to the next cell in a path. Checks grid state and deducts
        /// movement from the accumulator. Does NOT modify position or grid state —
        /// the caller is responsible for applying the result.
        /// </summary>
        /// <param name="currentPos">Unit's current grid position</param>
        /// <param name="nextPos">Next cell in the path</param>
        /// <param name="moveAccumulator">Current accumulated movement budget</param>
        /// <param name="grid">The shared game grid</param>
        /// <param name="distCost">Euclidean distance cost to move (output)</param>
        /// <returns>The move result indicating what happened.</returns>
        public static MoveResult TryMoveToCell(
            Position currentPos, Position nextPos, float moveAccumulator,
            GameGrid grid, out float distCost)
        {
            distCost = Position.Distance(currentPos, nextPos);
            if (distCost < 0.01f) distCost = 1.0f; // safety: same-cell fallback

            if (moveAccumulator < distCost)
                return MoveResult.InsufficientMovement;

            var state = grid.GetCell(nextPos);




            // OPEN = free cell, can move and stand
            if (state == CellState.OPEN)
                return MoveResult.Moved;

            // WALKABLE: either a building passage or a mobile-unit-occupied cell.
            // Both are walkable, but only unit-occupied cells allow pass-through.
            // Passage cells (building top rows) are normal movement — return Moved.
            if (state == CellState.WALKABLE)
                return grid.IsPassageCell(nextPos) ? MoveResult.Moved : MoveResult.BlockedByUnit;

            // BLOCKED = terrain or building body
            return MoveResult.BlockedByTerrain;
        }

        /// <summary>
        /// Scan ahead in a path to find the first buildable/passage cell past the blocker,
        /// then re-path from current position to that cell avoiding units.
        /// Returns the spliced detour + remainder, or null if no detour found.
        /// </summary>
        public static List<Position> FindDetourAroundBlocker(
            Position currentPos, List<Position> path, int pathIndex, GameGrid grid)
        {
            if (path == null) return null;

            int resumeIndex = -1;
            for (int i = pathIndex + 1; i < path.Count; i++)
            {
                if (grid.IsPositionBuildable(path[i]) || grid.IsPositionPassage(path[i]))
                {
                    resumeIndex = i;
                    break;
                }
            }

            if (resumeIndex < 0) return null;

            Position waypoint = path[resumeIndex];
            var detour = grid.FindPath(currentPos, waypoint, avoidUnits: true);
            if (detour.Count == 0) return null;

            // Splice: detour to the waypoint + remainder of original path after it
            for (int i = resumeIndex + 1; i < path.Count; i++)
                detour.Add(path[i]);

            return detour;
        }

        #endregion

        #region Combat

        /// <summary>
        /// Compute damage dealt per tick by an attacker to a target.
        /// </summary>
        public static float ComputeDamagePerTick(
            UnitType attackerType, UnitType targetType,
            float baseDamage, float tickDuration)
        {
            return baseDamage * tickDuration
                * GameConstants.DamageMultiplier(attackerType, targetType);
        }

        /// <summary>
        /// Check if an attacker is within effective attack range of a target.
        /// </summary>
        public static bool IsInAttackRange(
            UnitType attackerType, Position attackerCenter,
            UnitType targetType, Position targetCenter)
        {
            float range = GameConstants.EffectiveAttackRange(attackerType, targetType);
            float dist = Position.Distance(attackerCenter, targetCenter);
            return dist < range + 0.5f;
        }

        /// <summary>
        /// Compute the center position of a unit for range calculations.
        /// For 1x1 units, returns the grid position.
        /// For multi-cell buildings, returns center of the non-walkable footprint
        /// (excluding the top passage row).
        /// </summary>
        public static Position ComputeCenterPosition(UnitType unitType, Position gridPosition)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            if (size.X <= 1 && size.Y <= 1)
                return gridPosition;

            // For buildings: center of the non-walkable body.
            // Top row (j=0) is passage for multi-row buildings, so body is rows 1..sizeY-1.
            // CenterX = anchor.X + sizeX/2.0 - 0.5
            // CenterY = anchor.Y - sizeY/2.0 + 0.5 (with passage row adjustment)
            bool hasPassage = size.Y > 1;
            int bodyHeight = hasPassage ? size.Y - 1 : size.Y;
            float cx = gridPosition.X + (size.X - 1) / 2.0f;
            float cy = gridPosition.Y - (hasPassage ? 1 : 0) - (bodyHeight - 1) / 2.0f;
            return new Position((int)Math.Round(cx), (int)Math.Round(cy));
        }

        #endregion

        #region Training

        /// <summary>
        /// Small epsilon for timer comparisons. Floating-point accumulation error
        /// (e.g. 0.2f - 4×0.05f leaves ~8e-9 residual) can prevent timers from
        /// reaching exactly zero. 1e-4 is safely above worst-case drift (~1e-6)
        /// but well below the smallest tick duration (0.05).
        /// </summary>
        public const float TimerEpsilon = 1e-4f;

        /// <summary>
        /// Advance training timer. Returns true when training is complete.
        /// </summary>
        public static bool AdvanceTrainTimer(ref float trainTimer, float tickDuration)
        {
            trainTimer -= tickDuration;
            return trainTimer <= TimerEpsilon;
        }

        #endregion

        #region Building

        /// <summary>
        /// Advance build timer. Returns true when construction is complete.
        /// </summary>
        public static bool AdvanceBuildTimer(ref float buildTimer, float tickDuration)
        {
            buildTimer -= tickDuration;
            return buildTimer <= TimerEpsilon;
        }

        #endregion

        #region Gathering

        /// <summary>
        /// Compute mining time for a full mining capacity trip.
        /// </summary>
        public static float ComputeMiningTime(float miningCapacity, float miningSpeed)
        {
            return miningSpeed > 0 ? miningCapacity / miningSpeed : 1f;
        }

        /// <summary>
        /// Compute gold mined from a mine, capped at mine health and mining capacity.
        /// </summary>
        public static float ComputeGoldMined(float miningCapacity, float mineHealth)
        {
            return Math.Min(miningCapacity, mineHealth);
        }

        #endregion

        #region Healing

        /// <summary>
        /// Check if a heal action can be performed.
        /// Returns true if the monk has enough mana and the target is missing at least HEAL_AMOUNT HP.
        /// </summary>
        public static bool CanHeal(float monkMana, float targetHealth, UnitType targetType)
        {
            if (monkMana < GameConstants.MANA_COST)
                return false;

            float targetMaxHealth = GameConstants.HEALTH[targetType];
            if (targetMaxHealth <= 0) return false;

            return targetHealth <= targetMaxHealth - GameConstants.HEAL_AMOUNT;
        }

        /// <summary>
        /// Compute the heal amount for a target unit type.
        /// Returns the flat HEAL_AMOUNT, capped at missing HP.
        /// </summary>
        public static float ComputeHealAmount(UnitType targetType)
        {
            return GameConstants.HEAL_AMOUNT;
        }

        /// <summary>
        /// Check if a healer is within heal range of a target.
        /// </summary>
        public static bool IsInHealRange(
            UnitType healerType, Position healerCenter,
            Position targetCenter)
        {
            float range = GameConstants.HEAL_RANGE[healerType];
            float dist = Position.Distance(healerCenter, targetCenter);
            return dist < range + 0.5f;
        }

        #endregion

        #region Repair

        /// <summary>
        /// Compute repair amount per tick. Repair rate is 110% of build rate.
        /// </summary>
        public static float ComputeRepairPerTick(
            UnitType buildingType, float creationTime, float tickDuration)
        {
            float maxHp = GameConstants.HEALTH[buildingType];
            float repairRate = 1.1f * maxHp / creationTime;
            return repairRate * tickDuration;
        }

        #endregion

        #region Mana

        /// <summary>
        /// Regenerate mana for a unit per tick.
        /// </summary>
        public static float RegenMana(float currentMana, float maxMana, float manaRegenPerSecond, float tickDuration)
        {
            if (maxMana <= 0 || currentMana >= maxMana) return currentMana;
            return Math.Min(currentMana + manaRegenPerSecond * tickDuration, maxMana);
        }

        #endregion
    }
}
