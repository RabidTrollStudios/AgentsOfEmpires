using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Task advancement: processes in-progress unit actions each tick
    /// (move, train, build, gather, attack).
    /// </summary>
    public partial class SimGame
    {
        private void AdvanceAllUnits()
        {
            // Process each unit's task logic + movement together before
            // moving to the next unit. This matches Unity's TickFixedUpdate
            // which runs GameLogicTick then movement for each unit in the
            // deterministic sorted loop.
            var unitKeys = Units.Keys.ToList();
            unitKeys.Sort(); // deterministic order matching Unity

            foreach (int key in unitKeys)
            {
                if (!Units.TryGetValue(key, out var unit)) continue;

                // Task logic (matches Unity's GameLogicTick)
                switch (unit.CurrentAction)
                {
                    case UnitAction.MOVE:
                        AdvanceMove(unit);
                        break;
                    case UnitAction.TRAIN:
                        AdvanceTrain(unit);
                        break;
                    case UnitAction.BUILD:
                        AdvanceBuild(unit);
                        break;
                    case UnitAction.GATHER:
                        AdvanceGather(unit);
                        break;
                    case UnitAction.ATTACK:
                        AdvanceAttack(unit);
                        break;
                    case UnitAction.REPAIR:
                        AdvanceRepair(unit);
                        break;
                    case UnitAction.HEAL:
                        AdvanceHeal(unit);
                        break;
                }

                // Movement (matches Unity's movement loop within TickFixedUpdate)
                if (unit.Path != null && unit.PathIndex < unit.Path.Count)
                    MoveUnitOneStep(unit);
            }
        }

        #region Movement

        private void AdvanceMove(SimUnit unit)
        {
            // Path exhausted → go IDLE. Movement handled by Pass 2.
            if (unit.Path == null || unit.PathIndex >= unit.Path.Count)
            {
                unit.CurrentAction = UnitAction.IDLE;
                unit.Path = null;
            }
        }

        /// <summary>
        /// Move a unit one step along its path with 3-phase collision avoidance
        /// matching the Unity engine's FixedUpdate logic:
        ///   Phase 1: Cell is buildable (empty) — move forward normally.
        ///   Phase 2: Cell is walkable but not buildable (mobile unit blocking) —
        ///            wait, then detour, then full re-path with avoidUnits.
        ///   Phase 3: Cell is not walkable (terrain/building) — re-path immediately.
        /// </summary>
        private void MoveUnitOneStep(SimUnit unit)
        {
            if (unit.Path == null || unit.PathIndex >= unit.Path.Count) return;

            // Movement matches Unity's FixedUpdate model:
            //   Unity Speed = GAME_SPEED * 0.05 * speedMultiplier
            //   Per tick: accumulate Speed, consume cells costing their Euclidean distance
            //   (1.0 for cardinal, sqrt(2) for diagonal — matching Unity's distance check)
            float speed = movingSpeed[unit.UnitType];
            unit.MoveAccumulator += speed;

            while (unit.Path != null && unit.PathIndex < unit.Path.Count)
            {
                // Check if we have enough accumulated movement to reach the next cell
                float dist = Position.Distance(unit.GridPosition, unit.Path[unit.PathIndex]);
                if (dist < 0.01f) dist = 1.0f; // safety: same-cell fallback
                if (unit.MoveAccumulator < dist) break;

                if (!MoveToNextCell(unit, dist)) break;
            }
        }

        /// <summary>
        /// Attempt to move the unit to the next cell in its path.
        /// Uses shared TaskEngine.TryMoveToCell for the 3-phase check, then
        /// applies the result to SimUnit state.
        /// Returns true if the move succeeded, false if blocked.
        /// </summary>
        private bool MoveToNextCell(SimUnit unit, float dist)
        {
            Position nextPos = unit.Path[unit.PathIndex];

            var result = TaskEngine.TryMoveToCell(
                unit.GridPosition, nextPos, unit.MoveAccumulator, Map.Grid, out float distCost);

            switch (result)
            {
                case TaskEngine.MoveResult.Moved:
                    unit.MoveAccumulator -= distCost;
                    unit.LocalAvoidWaitTicks = 0;

                    if (GameConstants.CAN_MOVE[unit.UnitType])
                    {
                        // Claim new cell BEFORE releasing old — prevents
                        // a window where both cells appear OPEN to other units.
                        Map.Grid.SetCellOccupied(nextPos, true);            // claim new cell
                        Map.Grid.SetCellOccupied(unit.GridPosition, false); // leave old cell
                    }

                    unit.GridPosition = nextPos;
                    unit.PathIndex++;

                    if (unit.PathIndex >= unit.Path.Count)
                    {
                        if (unit.CurrentAction == UnitAction.MOVE)
                        {
                            unit.CurrentAction = UnitAction.IDLE;
                            unit.Path = null;
                        }
                    }
                    return true;

                case TaskEngine.MoveResult.BlockedByUnit:
                    // Final cell occupied — don't overlap
                    if (unit.PathIndex == unit.Path.Count - 1)
                    {
                        // MOVE: stop here (close enough)
                        // Other actions (GATHER, BUILD, ATTACK, etc.): consume path
                        // so the task system can check adjacency and re-path if needed
                        if (unit.CurrentAction == UnitAction.MOVE)
                        {
                            unit.CurrentAction = UnitAction.IDLE;
                        }
                        unit.Path = null;
                        return false;
                    }
                    // Mid-path: pass through
                    goto case TaskEngine.MoveResult.Moved;

                case TaskEngine.MoveResult.BlockedByTerrain:
                    unit.MoveAccumulator -= distCost;
                    unit.LocalAvoidWaitTicks = 0;
                    Position dest = unit.Path[unit.Path.Count - 1];
                    var repath = Map.FindPath(unit.GridPosition, dest);
                    if (repath.Count > 0)
                    {
                        unit.Path = repath;
                        unit.PathIndex = 0;
                    }
                    else
                    {
                        unit.Path = null;
                        if (unit.CurrentAction == UnitAction.MOVE)
                            unit.CurrentAction = UnitAction.IDLE;
                    }
                    return false;

                default: // InsufficientMovement
                    return false;
            }
        }

        #endregion

        #region Training

        private void AdvanceTrain(SimUnit building)
        {
            // Count up (matches Unity's taskTime += fixedDeltaTime)
            building.TrainTimer += Config.TickDuration;
            if (building.TrainTimer < creationTime[building.TrainTarget])
                return;

            // Training complete — find a buildable cell to spawn the unit
            var spawnPositions = Map.GetBuildablePositionsNearUnit(building.UnitType, building.GridPosition);
            if (spawnPositions.Count == 0)
            {
                // Can't spawn — back off timer slightly so we retry next tick
                building.TrainTimer = creationTime[building.TrainTarget] - Config.TickDuration;
                return;
            }

            Position spawnPos = spawnPositions[0];
            float health = GameConstants.HEALTH[building.TrainTarget];
            PlaceUnit(building.OwnerAgentNbr, building.TrainTarget, spawnPos, health, true);
            building.CurrentAction = UnitAction.IDLE;
        }

        #endregion

        #region Building

        private void AdvanceBuild(SimUnit pawn)
        {
            // Phase 1: walk to build site (movement handled by Pass 2)
            if (pawn.Path != null && pawn.PathIndex < pawn.Path.Count)
                return;

            // Phase 2: count up build timer (matches Unity's BuildProgress += fixedDeltaTime)
            pawn.BuildTimer += Config.TickDuration;
            if (pawn.BuildTimer < creationTime[pawn.BuildTarget])
                return;

            // Build complete — mark building as built
            foreach (var u in Units.Values)
            {
                if (u.UnitType == pawn.BuildTarget
                    && u.GridPosition == pawn.BuildSite
                    && u.OwnerAgentNbr == pawn.OwnerAgentNbr
                    && !u.IsBuilt)
                {
                    u.IsBuilt = true;
                    break;
                }
            }
            pawn.CurrentAction = UnitAction.IDLE;
            pawn.Path = null;
        }

        #endregion

        #region Gathering

        private void AdvanceGather(SimUnit pawn)
        {
            switch (pawn.GatherPhase)
            {
                case GatherPhase.TO_MINE:
                    AdvanceGatherToMine(pawn);
                    break;
                case GatherPhase.MINING:
                    AdvanceGatherMining(pawn);
                    break;
                case GatherPhase.TO_BASE:
                    AdvanceGatherToBase(pawn);
                    break;
            }
        }

        private void AdvanceGatherToMine(SimUnit pawn)
        {
            // If mine was destroyed, go idle
            if (!Units.TryGetValue(pawn.GatherMineNbr, out var mine))
            {
                pawn.CurrentAction = UnitAction.IDLE;
                pawn.Path = null;
                return;
            }

            // Walk toward the mine
            if (pawn.Path != null && pawn.PathIndex < pawn.Path.Count)
                return;

            // Check adjacency to mine (matches Unity's IsNeighborOfUnit check)
            if (Map.Grid.IsNeighborOfUnit(pawn.GridPosition, UnitType.MINE, mine.GridPosition))
            {
                // Arrived adjacent to mine — start mining
                pawn.GatherPhase = GatherPhase.MINING;
                pawn.MiningTimer = 0f;
                pawn.GoldCarried = 0;
            }
            else
            {
                // Not adjacent — re-path to a different mine neighbor
                var repath = Map.FindPathToUnit(pawn.GridPosition, UnitType.MINE, mine.GridPosition);
                if (repath.Count > 0)
                {
                    pawn.Path = repath;
                    pawn.PathIndex = 0;
                }
                else
                {
                    pawn.CurrentAction = UnitAction.IDLE;
                }
            }
        }

        private void AdvanceGatherMining(SimUnit pawn)
        {
            if (!Units.TryGetValue(pawn.GatherMineNbr, out var mine))
            {
                pawn.CurrentAction = UnitAction.IDLE;
                return;
            }

            if (mine.Health <= 0)
            {
                // Mine depleted — head to base with whatever we have, or go idle
                if (!Units.TryGetValue(pawn.GatherBaseNbr, out var baseForEmpty))
                {
                    pawn.CurrentAction = UnitAction.IDLE;
                    return;
                }
                var emptyPath = Map.FindPathToUnit(pawn.GridPosition, UnitType.BASE, baseForEmpty.GridPosition);
                pawn.GatherPhase = GatherPhase.TO_BASE;
                pawn.Path = emptyPath;
                pawn.PathIndex = 0;
                return;
            }

            // Accumulate gold each tick (matches Unity's continuous mining)
            pawn.MiningTimer += Config.TickDuration * miningSpeed;

            // Deduct from mine 1 gold at a time (matches Unity's per-integer deduction)
            if (pawn.MiningTimer >= 1f)
            {
                int goldChunk = (int)pawn.MiningTimer;
                mine.Health -= goldChunk;
                pawn.GoldCarried += goldChunk;
                pawn.MiningTimer -= goldChunk;
            }

            // Reached capacity — head to base
            if (pawn.GoldCarried >= (int)miningCapacity)
            {
                if (!Units.TryGetValue(pawn.GatherBaseNbr, out var baseUnit))
                {
                    pawn.CurrentAction = UnitAction.IDLE;
                    return;
                }

                var path = Map.FindPathToUnit(pawn.GridPosition, UnitType.BASE, baseUnit.GridPosition);
                pawn.GatherPhase = GatherPhase.TO_BASE;
                pawn.Path = path;
                pawn.PathIndex = 0;
            }
        }

        private void AdvanceGatherToBase(SimUnit pawn)
        {
            // If base was destroyed, go idle
            if (!Units.TryGetValue(pawn.GatherBaseNbr, out var baseUnit))
            {
                pawn.CurrentAction = UnitAction.IDLE;
                pawn.Path = null;
                return;
            }

            // Walk toward the base (movement handled by Pass 2)
            if (pawn.Path != null && pawn.PathIndex < pawn.Path.Count)
                return;

            // Check adjacency to base (matches Unity's IsNeighborOfUnit check)
            if (Map.Grid.IsNeighborOfUnit(pawn.GridPosition, UnitType.BASE, baseUnit.GridPosition))
            {
                // Arrived at base — deposit gold
                Gold[pawn.OwnerAgentNbr] += pawn.GoldCarried;
                pawn.GoldCarried = 0;

                // Cycle back to mine
                if (!Units.TryGetValue(pawn.GatherMineNbr, out var mine) || mine.Health <= 0)
                {
                    pawn.CurrentAction = UnitAction.IDLE;
                    return;
                }

                var path = Map.FindPathToUnit(pawn.GridPosition, UnitType.MINE, mine.GridPosition);
                pawn.GatherPhase = GatherPhase.TO_MINE;
                pawn.Path = path;
                pawn.PathIndex = 0;
            }
            else
            {
                // Not adjacent yet — re-path to a different base neighbor
                var path = Map.FindPathToUnit(pawn.GridPosition, UnitType.BASE, baseUnit.GridPosition);
                if (path.Count > 0)
                {
                    pawn.Path = path;
                    pawn.PathIndex = 0;
                }
                else
                {
                    pawn.CurrentAction = UnitAction.IDLE;
                }
            }
        }

        #endregion

        #region Repair

        private void AdvanceRepair(SimUnit pawn)
        {
            // If building was destroyed, go idle
            if (!Units.TryGetValue(pawn.RepairBuildingNbr, out var building) || building.Health <= 0)
            {
                pawn.CurrentAction = UnitAction.IDLE;
                pawn.Path = null;
                return;
            }

            // Phase 1: walk to building (movement handled by Pass 2)
            if (pawn.Path != null && pawn.PathIndex < pawn.Path.Count)
                return;

            // Phase 2: heal at 110% of the build rate (shared formula)
            float maxHp = GameConstants.HEALTH[building.UnitType];

            if (building.Health >= maxHp)
            {
                pawn.CurrentAction = UnitAction.IDLE;
                pawn.Path = null;
                return;
            }

            float repairAmount = TaskEngine.ComputeRepairPerTick(
                building.UnitType, creationTime[building.UnitType], Config.TickDuration);
            building.Health = Math.Min(building.Health + repairAmount, maxHp);

            // Done — building is at full health
            if (building.Health >= maxHp)
            {
                pawn.CurrentAction = UnitAction.IDLE;
                pawn.Path = null;
            }
        }

        #endregion

        #region Combat

        private void AdvanceAttack(SimUnit attacker)
        {
            if (!Units.TryGetValue(attacker.AttackTargetNbr, out var target) || target.Health <= 0)
            {
                // Target dead or removed
                attacker.CurrentAction = UnitAction.IDLE;
                attacker.Path = null;
                return;
            }

            if (TaskEngine.IsInAttackRange(attacker.UnitType, attacker.CenterPosition,
                    target.UnitType, target.CenterPosition))
            {
                // In range — deal damage via shared formula
                float dmg = TaskEngine.ComputeDamagePerTick(
                    attacker.UnitType, target.UnitType,
                    damage[attacker.UnitType], Config.TickDuration);
                target.Health -= dmg;

                // Target killed — return to idle immediately
                if (target.Health <= 0)
                {
                    attacker.CurrentAction = UnitAction.IDLE;
                    attacker.Path = null;
                    return;
                }
            }
            else
            {
                // Retarget interrupt: if any enemy is within attack range, switch to them
                int? closerNbr = FindClosestEnemyInRange(attacker);
                if (closerNbr.HasValue && closerNbr.Value != attacker.AttackTargetNbr)
                {
                    attacker.AttackTargetNbr = closerNbr.Value;
                    attacker.Path = null;
                    attacker.PathIndex = 0;
                    return;
                }

                // Out of range — movement handled by Pass 2
                if (attacker.Path != null && attacker.PathIndex < attacker.Path.Count)
                {
                    // Movement will happen in Pass 2
                }
                else
                {
                    // Repath toward target (use FindPathToUnit for multi-cell buildings)
                    var path = Map.FindPathToUnit(attacker.GridPosition, target.UnitType, target.GridPosition);
                    if (path.Count > 0)
                    {
                        attacker.Path = path;
                        attacker.PathIndex = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Find the closest enemy unit within effective attack range of the attacker.
        /// Returns null if no enemy is in range.
        /// </summary>
        private int? FindClosestEnemyInRange(SimUnit attacker)
        {
            float closestDist = float.MaxValue;
            int? closest = null;

            foreach (var kvp in Units)
            {
                var enemy = kvp.Value;
                if (enemy.OwnerAgentNbr == attacker.OwnerAgentNbr) continue;
                if (enemy.UnitType == UnitType.MINE) continue;
                if (enemy.Health <= 0) continue;

                float effectiveRange = GameConstants.EffectiveAttackRange(attacker.UnitType, enemy.UnitType);
                float dist = Position.Distance(attacker.CenterPosition, enemy.CenterPosition);
                if (dist <= effectiveRange + 0.1f && dist < closestDist)
                {
                    closestDist = dist;
                    closest = enemy.UnitNbr;
                }
            }
            return closest;
        }

        #endregion

        #region Healing

        private void AdvanceHeal(SimUnit monk)
        {
            if (!Units.TryGetValue(monk.HealTargetNbr, out var target) || target.Health <= 0)
            {
                monk.CurrentAction = UnitAction.IDLE;
                monk.HealTargetNbr = -1;
                monk.Path = null;
                return;
            }

            if (TaskEngine.IsInHealRange(monk.UnitType, monk.CenterPosition, target.CenterPosition))
            {
                // In range — check mana and threshold via shared logic
                if (!TaskEngine.CanHeal(monk.Mana, target.Health, target.UnitType))
                {
                    monk.CurrentAction = UnitAction.IDLE;
                    monk.HealTargetNbr = -1;
                    monk.Path = null;
                    return;
                }

                // Apply heal via shared formula
                float healAmount = TaskEngine.ComputeHealAmount(target.UnitType);
                float targetMaxHealth = GameConstants.HEALTH[target.UnitType];
                target.Health = Math.Min(target.Health + healAmount, targetMaxHealth);
                monk.Mana -= GameConstants.MANA_COST;

                monk.CurrentAction = UnitAction.IDLE;
                monk.HealTargetNbr = -1;
                monk.Path = null;
            }
            else
            {
                // Out of range — movement handled by Pass 2
                if (monk.Path != null && monk.PathIndex < monk.Path.Count)
                {
                    // Movement will happen in Pass 2
                }
                else
                {
                    var path = Map.FindPathToUnit(monk.GridPosition, target.UnitType, target.GridPosition);
                    if (path.Count > 0)
                    {
                        monk.Path = path;
                        monk.PathIndex = 0;
                    }
                }
            }
        }

        #endregion
    }
}
