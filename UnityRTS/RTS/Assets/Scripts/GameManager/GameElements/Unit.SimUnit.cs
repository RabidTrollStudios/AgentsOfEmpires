using System.Collections.Generic;
using AgentSDK;
using UnityEngine;

namespace GameManager.GameElements
{
    /// <summary>
    /// ISimUnit implementation for Unity's Unit.
    /// Bridges between Unity types (Vector3Int) and AgentSDK types (Position).
    /// </summary>
    public partial class Unit : ISimUnit
    {
        /// <summary>
        /// Post-step visual update. Mana regen and death are now in shared StepEngine.
        /// Only Unity-specific reference caching remains.
        /// </summary>
        internal void PostStepUpdate()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

            // Update cached Unity references (not in shared StepEngine)
            MineUnit = GameManager.Instance.Units.GetUnit(mineUnit);
            BaseUnit = GameManager.Instance.Units.GetUnit(baseUnit);
        }

        /// <summary>
        /// Called by OnUnitMoved when the unit crosses into a new cell.
        /// Notifies the VSM to snapshot action/phase for animation selection.
        /// Visual position is now derived directly from GridPosition + PathProgress.
        /// </summary>
        internal void NotifyVisualCellCrossed()
        {
            _vsm?.NotifyMoveStarted(CurrentAction, gatherPhase, buildPhase);
        }

        // Explicit implementations for properties with non-public setters
        float ISimUnit.Health { get => Health; set => Health = value; }
        bool ISimUnit.IsBuilt { get => IsBuilt; set => IsBuilt = value; }
        UnitAction ISimUnit.CurrentAction { get => CurrentAction; set => CurrentAction = value; }
        float ISimUnit.Mana { get => Mana; set => Mana = value; }

        // GridPosition — convert between Vector3Int and Position
        Position ISimUnit.GridPosition
        {
            get => new Position(GridPosition.x, GridPosition.y);
            set => GridPosition = new Vector3Int(value.X, value.Y, 0);
        }

        Position ISimUnit.CenterPosition =>
            AgentSDK.TaskEngine.ComputeCenterPosition(UnitType, new Position(GridPosition.x, GridPosition.y));

        // Movement — SimPath stores a separate List<Position> that StepEngine uses directly.
        // The old List<Vector3Int> path is kept in sync for visual code.
        private List<Position> _simPath;
        List<Position> ISimUnit.SimPath
        {
            get => _simPath;
            set
            {
                _simPath = value;
                // Sync legacy path for visual code
                path.Clear();
                if (value != null)
                {
                    foreach (var p in value)
                        path.Add(new Vector3Int(p.X, p.Y, 0));
                }
                pathIndex = 0;
                // Reset build phase when a new non-empty path is assigned for a build command
                if (value != null && value.Count > 0
                    && (CurrentAction == UnitAction.BUILD || CurrentAction == UnitAction.REPAIR))
                    buildPhase = BuildPhase.TO_POSITION;
                // Snapshot action/phase for VSM animation when path starts
                if (value != null && value.Count > 0)
                    _vsm?.NotifyMoveStarted(CurrentAction, gatherPhase, buildPhase);
            }
        }
        int ISimUnit.PathIndex { get => pathIndex; set => pathIndex = value; }
        float ISimUnit.PathProgress { get => PathProgress; set => PathProgress = value; }

        // Training — map to taskTime/taskUnitType
        float ISimUnit.TrainTimer { get => taskTime; set => taskTime = value; }
        UnitType ISimUnit.TrainTarget { get => taskUnitType; set => taskUnitType = value; }

        // Building — BuildTimer stored on pawn as taskTime (same as training)
        float ISimUnit.BuildTimer { get => taskTime; set => taskTime = value; }
        UnitType ISimUnit.BuildTarget { get => taskUnitType; set => taskUnitType = value; }
        private Position _buildSite;
        Position ISimUnit.BuildSite
        {
            get => currentBuilding != null
                ? new Position(currentBuilding.GetComponent<Unit>().GridPosition.x,
                              currentBuilding.GetComponent<Unit>().GridPosition.y)
                : _buildSite;
            set => _buildSite = value;
        }
        int ISimUnit.BuildTargetNbr
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
        int ISimUnit.GatherMineNbr { get => mineUnit; set => mineUnit = value; }
        int ISimUnit.GatherBaseNbr { get => baseUnit; set => baseUnit = value; }
        GatherPhase ISimUnit.GatherPhase
        {
            get => gatherPhase;
            set => gatherPhase = value;
        }
        float ISimUnit.MiningTimer { get => minedGold; set => minedGold = value; }
        int ISimUnit.GoldCarried { get => totalGold; set => totalGold = value; }

        // Combat
        int ISimUnit.AttackTargetNbr { get => attackUnitNbr; set => attackUnitNbr = value; }

        // Combat timing
        private float _attackCooldown;
        float ISimUnit.AttackCooldown { get => _attackCooldown; set => _attackCooldown = value; }

        // Abilities
        private float _chargeCooldown;
        private int _volleyTargetNbr = -1;
        private float _volleyTimer;
        private float _joustDistance;
        float ISimUnit.ChargeCooldown { get => _chargeCooldown; set => _chargeCooldown = value; }
        int ISimUnit.VolleyTargetNbr { get => _volleyTargetNbr; set => _volleyTargetNbr = value; }
        float ISimUnit.VolleyTimer { get => _volleyTimer; set => _volleyTimer = value; }
        float ISimUnit.JoustDistance { get => _joustDistance; set => _joustDistance = value; }

        // Repair
        int ISimUnit.RepairBuildingNbr
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
        int ISimUnit.HealTargetNbr { get => healTargetNbr; set => healTargetNbr = value; }
    }
}
