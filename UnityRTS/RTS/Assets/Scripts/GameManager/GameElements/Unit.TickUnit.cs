using System.Collections.Generic;
using AgentSDK;
using UnityEngine;

namespace GameManager.GameElements
{
    /// <summary>
    /// ITickUnit implementation for Unity's Unit.
    /// Bridges between Unity types (Vector3Int) and AgentSDK types (Position).
    /// </summary>
    public partial class Unit : ITickUnit
    {
        /// <summary>
        /// Post-tick visual update. Mana regen and death are now in shared TickEngine.
        /// Only Unity-specific reference caching remains.
        /// </summary>
        internal void PostTickUpdate()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

            // Update cached Unity references (not in shared TickEngine)
            MineUnit = GameManager.Instance.Units.GetUnit(mineUnit);
            BaseUnit = GameManager.Instance.Units.GetUnit(baseUnit);
        }

        /// <summary>
        /// Start a visual interpolation segment to the given grid cell.
        /// Called by OnUnitMoved when the TickEngine moves this unit to a new cell.
        /// If a previous segment is still active, snaps to its target first so
        /// multi-cell moves within a single tick chain correctly.
        /// </summary>
        internal void StartVisualMove(Vector3Int toCell)
        {
            Vector3 target = (Vector3)toCell + new Vector3(0.5f, 0f, 0);
            visualSpeed = Speed / Time.fixedDeltaTime;

            if (visualT >= 1.0f && visualQueue.Count == 0)
            {
                // Not currently interpolating — start a new segment immediately
                visualFrom = WorldPosition;
                visualTo = target;
                visualT = 0f;
                velocity = Utility.SafeNormalize(visualTo - visualFrom);
            }
            else
            {
                // Already interpolating — queue this waypoint for smooth chaining
                visualQueue.Enqueue(target);
            }

            // Notify state machine so it can snapshot action/phase for run-variant selection
            _vsm?.NotifyMoveStarted(CurrentAction, gatherPhase, buildPhase);
        }

        // Explicit implementations for properties with non-public setters
        float ITickUnit.Health { get => Health; set => Health = value; }
        bool ITickUnit.IsBuilt { get => IsBuilt; set => IsBuilt = value; }
        UnitAction ITickUnit.CurrentAction { get => CurrentAction; set => CurrentAction = value; }
        float ITickUnit.Mana { get => Mana; set => Mana = value; }

        // GridPosition — convert between Vector3Int and Position
        Position ITickUnit.GridPosition
        {
            get => new Position(GridPosition.x, GridPosition.y);
            set => GridPosition = new Vector3Int(value.X, value.Y, 0);
        }

        Position ITickUnit.CenterPosition =>
            AgentSDK.TaskEngine.ComputeCenterPosition(UnitType, new Position(GridPosition.x, GridPosition.y));

        // Movement — TickPath stores a separate List<Position> that TickEngine uses directly.
        // The old List<Vector3Int> path is kept in sync for visual code.
        private List<Position> _tickPath;
        List<Position> ITickUnit.TickPath
        {
            get => _tickPath;
            set
            {
                _tickPath = value;
                // Sync legacy path for visual code
                path.Clear();
                if (value != null)
                {
                    foreach (var p in value)
                        path.Add(new Vector3Int(p.X, p.Y, 0));
                }
                pathIndex = 0;
                // Clear visual queue when path changes — old waypoints are invalid
                if (CanMove)
                    visualQueue.Clear();
                // Reset build phase when a new non-empty path is assigned for a build command
                if (value != null && value.Count > 0
                    && (CurrentAction == UnitAction.BUILD || CurrentAction == UnitAction.REPAIR))
                    buildPhase = BuildPhase.TO_POSITION;
            }
        }
        int ITickUnit.PathIndex { get => pathIndex; set => pathIndex = value; }
        float ITickUnit.MoveAccumulator { get => MoveAccumulator; set => MoveAccumulator = value; }

        // Training — map to taskTime/taskUnitType
        float ITickUnit.TrainTimer { get => taskTime; set => taskTime = value; }
        UnitType ITickUnit.TrainTarget { get => taskUnitType; set => taskUnitType = value; }

        // Building — BuildTimer stored on pawn as taskTime (same as training)
        float ITickUnit.BuildTimer { get => taskTime; set => taskTime = value; }
        UnitType ITickUnit.BuildTarget { get => taskUnitType; set => taskUnitType = value; }
        private Position _buildSite;
        Position ITickUnit.BuildSite
        {
            get => currentBuilding != null
                ? new Position(currentBuilding.GetComponent<Unit>().GridPosition.x,
                              currentBuilding.GetComponent<Unit>().GridPosition.y)
                : _buildSite;
            set => _buildSite = value;
        }
        int ITickUnit.BuildTargetNbr
        {
            get => currentBuilding != null ? currentBuilding.GetComponent<Unit>().UnitNbr : -1;
            set
            {
                if (value >= 0)
                {
                    var u = GameManager.Instance.Units.GetUnit(value);
                    currentBuilding = u != null ? u.gameObject : null;
                }
                else
                {
                    currentBuilding = null;
                }
            }
        }

        // Gathering
        int ITickUnit.GatherMineNbr { get => mineUnit; set => mineUnit = value; }
        int ITickUnit.GatherBaseNbr { get => baseUnit; set => baseUnit = value; }
        GatherPhase ITickUnit.GatherPhase
        {
            get => gatherPhase;
            set => gatherPhase = value;
        }
        float ITickUnit.MiningTimer { get => minedGold; set => minedGold = value; }
        int ITickUnit.GoldCarried { get => totalGold; set => totalGold = value; }

        // Combat
        int ITickUnit.AttackTargetNbr { get => attackUnitNbr; set => attackUnitNbr = value; }

        // Repair
        int ITickUnit.RepairBuildingNbr
        {
            get => currentBuilding != null ? currentBuilding.GetComponent<Unit>().UnitNbr : -1;
            set
            {
                if (value >= 0)
                {
                    var u = GameManager.Instance.Units.GetUnit(value);
                    currentBuilding = u != null ? u.gameObject : null;
                }
                else
                {
                    currentBuilding = null;
                }
            }
        }

        // Heal
        int ITickUnit.HealTargetNbr { get => healTargetNbr; set => healTargetNbr = value; }
    }
}
