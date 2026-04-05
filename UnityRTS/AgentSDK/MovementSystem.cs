using System;
using System.Collections.Generic;

namespace AgentSDK
{
    /// <summary>
    /// Continuous euclidean movement system. Units advance along their path
    /// by speed * dt each call, crossing cell boundaries as distance is consumed.
    /// Both Unity (per-frame with Time.deltaTime) and SimGame (per-tick with tickDuration)
    /// call this with their respective time deltas for identical results.
    /// </summary>
    public static class MovementSystem
    {
        /// <summary>
        /// Advance a unit along its path by speed * dt.
        /// Crosses cell boundaries as needed, firing OnUnitMoved for each cell entered.
        /// Handles collision: BlockedByTerrain triggers repath, BlockedByUnit at final
        /// cell clears path for task logic to handle, mid-path BlockedByUnit passes through.
        /// </summary>
        public static void Advance(ISimUnit unit, float dt, ISimWorld world, ISimCallbacks callbacks)
        {
            // Cache SimPath in a local to avoid repeated interface dispatch.
            // The ISimUnit.SimPath property goes through explicit interface
            // implementation on Unity's MonoBehaviour, and repeated virtual calls
            // can return null if the setter fires between reads.
            var path = unit.SimPath;
            if (path == null || unit.PathIndex >= path.Count) return;
            if (!unit.CanMove) return;
            if (dt <= 0f) return;

            var constants = world.Constants;
            if (constants == null)
                throw new NullReferenceException($"[MovementSystem] world.Constants is null for unit {unit.UnitNbr}");
            var grid = world.Grid;
            if (grid == null)
                throw new NullReferenceException($"[MovementSystem] world.Grid is null for unit {unit.UnitNbr}");

            float speed = constants.MovingSpeed[unit.UnitType] / world.StepDuration;
            if (speed <= 0f) return;

            // Warrior Charge: speed boost when moving toward attack target and cooldown ready
            if (unit.UnitType == UnitType.WARRIOR
                && unit.CurrentAction == UnitAction.ATTACK
                && unit.ChargeCooldown <= 0f
                && unit.AttackTargetNbr >= 0)
            {
                var target = world.GetUnit(unit.AttackTargetNbr);
                if (target != null && target.Health > 0)
                {
                    float distToTarget = Position.Distance(unit.CenterPosition, target.CenterPosition);
                    if (distToTarget <= GameConstants.CHARGE_RANGE)
                    {
                        speed *= GameConstants.CHARGE_SPEED_MULTIPLIER;
                    }
                }
            }

            float remainingDist = speed * dt;
            float distThisFrame = 0f; // track for lancer joust

            while (remainingDist > 0f && path != null && unit.PathIndex < path.Count)
            {
                Position nextCell = path[unit.PathIndex];

                // Distance for the full segment (current grid cell to next cell)
                float segmentDist = Position.Distance(unit.GridPosition, nextCell);
                if (segmentDist < 0.01f) segmentDist = 1.0f; // safety: same-cell

                // How much distance remains in this segment
                float progressDist = segmentDist * (1.0f - unit.PathProgress);

                if (remainingDist >= progressDist)
                {
                    // Enough distance to cross into the next cell
                    distThisFrame += progressDist;
                    remainingDist -= progressDist;

                    // Collision check
                    var cellState = CheckCell(nextCell, grid);

                    if (cellState == TaskEngine.MoveResult.BlockedByTerrain)
                    {
                        // Repath around obstacle
                        unit.PathProgress = 0f;
                        Position dest = path[path.Count - 1];
                        var repath = world.FindPath(unit.GridPosition, dest);
                        if (repath.Count > 0)
                        {
                            unit.SimPath = repath;
                            path = repath;
                            unit.PathIndex = 0;
                            callbacks.OnUnitRepath(unit, repath);
                        }
                        else
                        {
                            if (unit.CurrentAction == UnitAction.MOVE)
                                StepEngine.SetIdle(unit);
                            else
                            {
                                unit.SimPath = null;
                                unit.PathIndex = 0;
                            }
                        }
                        return;
                    }

                    if (cellState == TaskEngine.MoveResult.BlockedByUnit)
                    {
                        if (unit.PathIndex == path.Count - 1)
                        {
                            // Final cell blocked — find a nearby OPEN cell
                            unit.PathProgress = 0f;
                            bool redirected = false;

                            // For attack/heal, use the target's current position as reference
                            // so the redirect cell is adjacent to where the target IS, not WAS
                            Position searchCenter = nextCell;
                            if (unit.CurrentAction == UnitAction.ATTACK && unit.AttackTargetNbr >= 0)
                            {
                                var target = world.GetUnit(unit.AttackTargetNbr);
                                if (target != null && target.CanMove)
                                    searchCenter = target.GridPosition;
                            }
                            else if (unit.CurrentAction == UnitAction.HEAL && unit.HealTargetNbr >= 0)
                            {
                                var target = world.GetUnit(unit.HealTargetNbr);
                                if (target != null && target.CanMove)
                                    searchCenter = target.GridPosition;
                            }

                            // Search expanding rings around the target for an OPEN cell
                            var bestCell = grid.FindNearestOpenCell(searchCenter);

                            if (bestCell.HasValue)
                            {
                                var redirect = grid.FindPath(unit.GridPosition, bestCell.Value);
                                if (redirect.Count > 0)
                                {
                                    unit.SimPath = redirect;
                                    path = redirect;
                                    unit.PathIndex = 0;
                                    redirected = true;
                                }
                            }

                            if (!redirected)
                            {
                                if (unit.CurrentAction == UnitAction.MOVE)
                                    StepEngine.SetIdle(unit);
                                else
                                {
                                    unit.SimPath = null;
                                    unit.PathIndex = 0;
                                }
                                return;
                            }
                            continue; // restart movement loop with new path
                        }
                        // Mid-path: pass through (units can walk through each other)
                    }

                    // Cross into the cell
                    Position oldPos = unit.GridPosition;
                    grid.SetCellOccupied(nextCell, true);
                    grid.SetCellOccupied(oldPos, false);

                    unit.GridPosition = nextCell;
                    unit.PathIndex++;
                    unit.PathProgress = 0f;

                    callbacks.OnUnitMoved(unit, oldPos, nextCell);

                    // Re-read path — callback side effects or setter may have changed it
                    path = unit.SimPath;
                    if (path == null) return;

                    // Path complete?
                    if (unit.PathIndex >= path.Count)
                    {
                        if (unit.CurrentAction == UnitAction.MOVE)
                            StepEngine.SetIdle(unit);
                        return;
                    }
                }
                else
                {
                    // Partial progress within this segment
                    distThisFrame += remainingDist;
                    unit.PathProgress += remainingDist / segmentDist;
                    remainingDist = 0f;
                }
            }

            // Lancer Joust: accumulate distance traveled for joust bonus
            if (unit.UnitType == UnitType.LANCER)
                unit.JoustDistance += distThisFrame;
        }

        /// <summary>Check if a cell is passable, without accumulator logic.</summary>
        private static TaskEngine.MoveResult CheckCell(Position pos, GameGrid grid)
        {
            var state = grid.GetCell(pos);

            if (state == CellState.OPEN)
                return TaskEngine.MoveResult.Moved;

            if (state == CellState.WALKABLE)
                return grid.IsPassageCell(pos)
                    ? TaskEngine.MoveResult.Moved
                    : TaskEngine.MoveResult.BlockedByUnit;

            return TaskEngine.MoveResult.BlockedByTerrain;
        }
    }
}
