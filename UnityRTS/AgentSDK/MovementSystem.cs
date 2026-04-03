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
        public static void Advance(ITickUnit unit, float dt, ITickWorld world, ITickCallbacks callbacks)
        {
            // Cache TickPath in a local to avoid repeated interface dispatch.
            // The ITickUnit.TickPath property goes through explicit interface
            // implementation on Unity's MonoBehaviour, and repeated virtual calls
            // can return null if the setter fires between reads.
            var path = unit.TickPath;
            if (path == null || unit.PathIndex >= path.Count) return;
            if (!unit.CanMove) return;
            if (dt <= 0f) return;

            var constants = world.Constants;
            if (constants == null)
                throw new NullReferenceException($"[MovementSystem] world.Constants is null for unit {unit.UnitNbr}");
            var grid = world.Grid;
            if (grid == null)
                throw new NullReferenceException($"[MovementSystem] world.Grid is null for unit {unit.UnitNbr}");

            float speed = constants.MovingSpeed[unit.UnitType] / world.TickDuration;
            if (speed <= 0f) return;
            float remainingDist = speed * dt;

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
                    remainingDist -= progressDist;

                    // Collision check
                    var cellState = CheckCell(nextCell, grid);

                    if (cellState == TaskEngine.MoveResult.BLOCKED_BY_TERRAIN)
                    {
                        // Repath around obstacle
                        unit.PathProgress = 0f;
                        Position dest = path[path.Count - 1];
                        var repath = world.FindPath(unit.GridPosition, dest);
                        if (repath.Count > 0)
                        {
                            unit.TickPath = repath;
                            path = repath;
                            unit.PathIndex = 0;
                            callbacks.OnUnitRepath(unit, repath);
                        }
                        else
                        {
                            if (unit.CurrentAction == UnitAction.MOVE)
                                TickEngine.SetIdle(unit);
                            else
                            {
                                unit.TickPath = null;
                                unit.PathIndex = 0;
                            }
                        }
                        return;
                    }

                    if (cellState == TaskEngine.MoveResult.BLOCKED_BY_UNIT)
                    {
                        if (unit.PathIndex == path.Count - 1)
                        {
                            // Final cell blocked — let task logic handle repath
                            unit.PathProgress = 0f;
                            if (unit.CurrentAction == UnitAction.MOVE)
                                TickEngine.SetIdle(unit);
                            else
                            {
                                unit.TickPath = null;
                                unit.PathIndex = 0;
                            }
                            return;
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
                    path = unit.TickPath;
                    if (path == null) return;

                    // Path complete?
                    if (unit.PathIndex >= path.Count)
                    {
                        if (unit.CurrentAction == UnitAction.MOVE)
                            TickEngine.SetIdle(unit);
                        return;
                    }
                }
                else
                {
                    // Partial progress within this segment
                    unit.PathProgress += remainingDist / segmentDist;
                    remainingDist = 0f;
                }
            }
        }

        /// <summary>Check if a cell is passable, without accumulator logic.</summary>
        private static TaskEngine.MoveResult CheckCell(Position pos, GameGrid grid)
        {
            var state = grid.GetCell(pos);

            if (state == CellState.OPEN)
                return TaskEngine.MoveResult.MOVED;

            if (state == CellState.WALKABLE)
                return grid.IsPassageCell(pos)
                    ? TaskEngine.MoveResult.MOVED
                    : TaskEngine.MoveResult.BLOCKED_BY_UNIT;

            return TaskEngine.MoveResult.BLOCKED_BY_TERRAIN;
        }
    }
}
