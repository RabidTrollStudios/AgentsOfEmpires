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
        /// Called by OnUnitMoved when the unit crosses into a new cell.
        /// Notifies the VSM to snapshot action/phase for animation selection.
        /// Visual position is now derived directly from GridPosition + PathProgress.
        /// </summary>
        internal void NotifyVisualCellCrossed()
        {
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
                // Reset build phase when a new non-empty path is assigned for a build command
                if (value != null && value.Count > 0
                    && (CurrentAction == UnitAction.BUILD || CurrentAction == UnitAction.REPAIR))
                    buildPhase = BuildPhase.TO_POSITION;
                // Snapshot action/phase for VSM animation when path starts
                if (value != null && value.Count > 0)
                    _vsm?.NotifyMoveStarted(CurrentAction, gatherPhase, buildPhase);
            }
        }
        int ITickUnit.PathIndex { get => pathIndex; set => pathIndex = value; }
        float ITickUnit.PathProgress { get => PathProgress; set => PathProgress = value; }

        // Training — map to taskTime/taskUnitType
        float ITickUnit.TrainTimer { get => taskTime; set => taskTime = value; }
        UnitType ITickUnit.TrainTarget { get => taskUnitType; set => taskUnitType = value; }

        // Building — BuildTimer stored on pawn as taskTime (same as training).
        // BuildProgress lives on the BUILDING unit (survives pawn death, enables resume).
        float ITickUnit.BuildTimer { get => taskTime; set => taskTime = value; }
        float ITickUnit.BuildProgress { get => BuildProgress; set => BuildProgress = value; }
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
        // Guards on BUILD for the same reason RepairBuildingNbr guards on REPAIR: the two
        // targets share the currentBuilding field, so each must only report during its own
        // action to match SimUnit's separate fields and avoid a false parity divergence.
        int ITickUnit.BuildTargetNbr
        {
            get => (CurrentAction == UnitAction.BUILD && currentBuilding != null)
                ? currentBuilding.GetComponent<Unit>().UnitNbr : -1;
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

        // Transient engine-side flag: a pursuit pathfind was deferred by PathBudget this
        // tick. Not serialized and not visual — lives only for the tick loop's retry.
        private bool repathPending;
        bool ITickUnit.RepathPending { get => repathPending; set => repathPending = value; }

        // Repair. NOTE: currentBuilding is SHARED with BuildTargetNbr (a pawn's build
        // site and its repair target are the same Unity field). So the getter MUST guard
        // on the action — during BUILD, RepairBuildingNbr is -1 (there is no repair target),
        // matching SimUnit which keeps the two as separate fields. Without this guard a
        // building pawn reported its build target as its repair target, a false parity
        // divergence surfaced by the full-field snapshot.
        int ITickUnit.RepairBuildingNbr
        {
            get => (CurrentAction == UnitAction.REPAIR && currentBuilding != null)
                ? currentBuilding.GetComponent<Unit>().UnitNbr : -1;
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
