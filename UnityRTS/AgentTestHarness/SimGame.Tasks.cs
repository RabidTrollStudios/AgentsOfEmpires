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
            // Snapshot unit keys so we can modify the collection
            var unitKeys = Units.Keys.ToList();
            foreach (int key in unitKeys)
            {
                if (!Units.TryGetValue(key, out var unit)) continue;
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
                }
            }
        }

        #region Movement

        private void AdvanceMove(SimUnit unit)
        {
            if (unit.Path == null || unit.PathIndex >= unit.Path.Count)
            {
                unit.CurrentAction = UnitAction.IDLE;
                unit.Path = null;
                return;
            }

            MoveUnitOneStep(unit);
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

            // Fractional movement: accumulate speed each tick, move when >= 1.0
            float speed = GameConstants.MOVEMENT_SPEED[unit.UnitType];
            unit.MoveAccumulator += speed;
            if (unit.MoveAccumulator < 1.0f) return;

            Position nextPos = unit.Path[unit.PathIndex];

            // Phase 1: Next cell is buildable (truly empty) — move forward normally
            if (Map.IsPositionBuildable(nextPos))
            {
                unit.MoveAccumulator -= 1.0f;
                unit.LocalAvoidWaitTicks = 0;

                if (GameConstants.CAN_MOVE[unit.UnitType])
                    Map.SetAreaBuildability(unit.UnitType, unit.GridPosition, true);

                unit.GridPosition = nextPos;
                unit.PathIndex++;

                if (GameConstants.CAN_MOVE[unit.UnitType])
                    Map.SetAreaBuildability(unit.UnitType, unit.GridPosition, false);

                if (unit.PathIndex >= unit.Path.Count)
                {
                    if (unit.CurrentAction == UnitAction.MOVE)
                    {
                        unit.CurrentAction = UnitAction.IDLE;
                        unit.Path = null;
                    }
                }
            }
            // Phase 2: Walkable but not buildable — blocked by a mobile unit (temporary)
            else if (Map.IsPositionWalkable(nextPos))
            {
                unit.MoveAccumulator -= 1.0f; // consume to prevent catch-up

                // For MOVE: if close to destination or only 1 step left, stop here
                if (unit.CurrentAction == UnitAction.MOVE)
                {
                    int stepsRemaining = unit.Path.Count - unit.PathIndex;
                    Position target = unit.Path[unit.Path.Count - 1];
                    float distToTarget = Position.Distance(unit.GridPosition, target);
                    if (stepsRemaining == 1 || distToTarget <= 3.0f)
                    {
                        unit.Path = null;
                        unit.CurrentAction = UnitAction.IDLE;
                        unit.LocalAvoidWaitTicks = 0;
                        return;
                    }
                }

                unit.LocalAvoidWaitTicks++;

                // Wait 3 ticks for the blocker to move (MOVE actions skip the wait)
                if (unit.CurrentAction != UnitAction.MOVE && unit.LocalAvoidWaitTicks <= 3)
                    return;

                // Try to find a detour around the blocker
                var detour = FindDetourAroundBlocker(unit);
                if (detour != null)
                {
                    unit.Path = detour;
                    unit.PathIndex = 0;
                    unit.LocalAvoidWaitTicks = 0;
                }
                else if (unit.LocalAvoidWaitTicks > 10)
                {
                    // Fallback: full re-path to original target avoiding units
                    Position target = unit.Path[unit.Path.Count - 1];
                    var newPath = Map.FindPath(unit.GridPosition, target, avoidUnits: true);
                    if (newPath.Count > 0)
                    {
                        unit.Path = newPath;
                        unit.PathIndex = 0;
                    }
                    // If re-path fails, keep old path (blocker may clear)
                    unit.LocalAvoidWaitTicks = 0;
                }
            }
            // Phase 3: Not walkable — terrain or building, re-path immediately
            else
            {
                unit.MoveAccumulator -= 1.0f;
                unit.LocalAvoidWaitTicks = 0;
                Position target = unit.Path[unit.Path.Count - 1];
                var newPath = Map.FindPath(unit.GridPosition, target);
                if (newPath.Count > 0)
                {
                    unit.Path = newPath;
                    unit.PathIndex = 0;
                }
                else
                {
                    unit.Path = null;
                    if (unit.CurrentAction == UnitAction.MOVE)
                        unit.CurrentAction = UnitAction.IDLE;
                }
            }
        }

        /// <summary>
        /// Scan ahead in the current path to find the next buildable (unoccupied) cell,
        /// then re-path from the unit's current position to that cell avoiding units.
        /// Returns the spliced detour + remainder path, or null if no detour found.
        /// Mirrors Unity's FindDetourAroundBlocker().
        /// </summary>
        private List<Position> FindDetourAroundBlocker(SimUnit unit)
        {
            if (unit.Path == null) return null;

            // Find first buildable cell further along the path
            int resumeIndex = -1;
            for (int i = unit.PathIndex + 1; i < unit.Path.Count; i++)
            {
                if (Map.IsPositionBuildable(unit.Path[i]))
                {
                    resumeIndex = i;
                    break;
                }
            }

            if (resumeIndex < 0) return null;

            Position waypoint = unit.Path[resumeIndex];
            var detour = Map.FindPath(unit.GridPosition, waypoint, avoidUnits: true);
            if (detour.Count == 0) return null;

            // Splice: detour to the waypoint + remainder of original path after it
            for (int i = resumeIndex + 1; i < unit.Path.Count; i++)
            {
                detour.Add(unit.Path[i]);
            }
            return detour;
        }

        #endregion

        #region Training

        private void AdvanceTrain(SimUnit building)
        {
            building.TrainTimer -= Config.TickDuration;

            if (building.TrainTimer <= 0f)
            {
                // Find a buildable cell adjacent to the building to spawn the unit
                var spawnPositions = Map.GetBuildablePositionsNearUnit(building.UnitType, building.GridPosition);
                if (spawnPositions.Count == 0)
                {
                    // No room — stay in TRAIN state, retry next tick
                    building.TrainTimer = 0.001f;
                    return;
                }

                Position spawnPos = spawnPositions[0];
                float health = GameConstants.HEALTH[building.TrainTarget];
                PlaceUnit(building.OwnerAgentNbr, building.TrainTarget, spawnPos, health, true);

                building.CurrentAction = UnitAction.IDLE;
            }
        }

        #endregion

        #region Building

        private void AdvanceBuild(SimUnit pawn)
        {
            // Phase 1: walk to build site
            if (pawn.Path != null && pawn.PathIndex < pawn.Path.Count)
            {
                MoveUnitOneStep(pawn);
                // Keep action as BUILD even after step
                pawn.CurrentAction = UnitAction.BUILD;
                return;
            }

            // Phase 2: count down build timer
            pawn.BuildTimer -= Config.TickDuration;

            if (pawn.BuildTimer <= 0f)
            {
                // Mark building as built
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
            {
                MoveUnitOneStep(pawn);
                pawn.CurrentAction = UnitAction.GATHER;
                return;
            }

            // Arrived adjacent to mine — start mining
            pawn.GatherPhase = GatherPhase.MINING;
            float miningTime = miningSpeed > 0 ? miningCapacity / miningSpeed : 1f;
            pawn.MiningTimer = miningTime;
        }

        private void AdvanceGatherMining(SimUnit pawn)
        {
            if (!Units.TryGetValue(pawn.GatherMineNbr, out var mine))
            {
                pawn.CurrentAction = UnitAction.IDLE;
                return;
            }

            pawn.MiningTimer -= Config.TickDuration;

            if (pawn.MiningTimer <= 0f)
            {
                // Deduct gold from mine
                float goldMined = Math.Min(miningCapacity, mine.Health);
                mine.Health -= goldMined;

                // Path to base
                if (!Units.TryGetValue(pawn.GatherBaseNbr, out var baseUnit))
                {
                    pawn.CurrentAction = UnitAction.IDLE;
                    return;
                }

                var path = Map.FindPathToUnit(pawn.GridPosition, UnitType.BASE, baseUnit.GridPosition);
                pawn.GatherPhase = GatherPhase.TO_BASE;
                pawn.Path = path;
                pawn.PathIndex = 0;

                // Store gold carried as a small temporary value on the mining timer
                pawn.MiningTimer = goldMined;
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

            // Walk toward the base
            if (pawn.Path != null && pawn.PathIndex < pawn.Path.Count)
            {
                MoveUnitOneStep(pawn);
                pawn.CurrentAction = UnitAction.GATHER;
                return;
            }

            // Arrived at base — deposit gold
            float goldCarried = pawn.MiningTimer;
            Gold[pawn.OwnerAgentNbr] += (int)goldCarried;

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

            // Phase 1: walk to building
            if (pawn.Path != null && pawn.PathIndex < pawn.Path.Count)
            {
                MoveUnitOneStep(pawn);
                pawn.CurrentAction = UnitAction.REPAIR;
                return;
            }

            // Phase 2: heal at 2x the build rate
            float maxHp = GameConstants.HEALTH[building.UnitType];

            // Already at full health
            if (building.Health >= maxHp)
            {
                pawn.CurrentAction = UnitAction.IDLE;
                pawn.Path = null;
                return;
            }

            float repairRate = 2f * maxHp / creationTime[building.UnitType];
            building.Health += repairRate * Config.TickDuration;
            if (building.Health > maxHp)
                building.Health = maxHp;

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

            float range = GameConstants.EffectiveAttackRange(attacker.UnitType, target.UnitType);
            float dist = Position.Distance(attacker.CenterPosition, target.CenterPosition);

            if (dist <= range + 0.1f)
            {
                // In range — deal damage (apply armor/damage-type multiplier)
                float dmg = damage[attacker.UnitType] * Config.TickDuration
                    * GameConstants.DamageMultiplier(attacker.UnitType, target.UnitType);
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

                // Out of range — move closer
                if (attacker.Path != null && attacker.PathIndex < attacker.Path.Count)
                {
                    MoveUnitOneStep(attacker);
                    attacker.CurrentAction = UnitAction.ATTACK;
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
    }
}
