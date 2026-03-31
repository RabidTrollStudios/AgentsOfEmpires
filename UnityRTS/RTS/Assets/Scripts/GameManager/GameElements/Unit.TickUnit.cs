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
        /// Post-tick update: mana regen and death removal.
        /// Called AFTER TickEngine.AdvanceAllUnits, matching SimGame's Phase 3+4.
        /// </summary>
        internal void PostTickUpdate()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

            // Update cached references
            MineUnit = GameManager.Instance.Units.GetUnit(mineUnit);
            BaseUnit = GameManager.Instance.Units.GetUnit(baseUnit);

            // IDLE visual cleanup (Unity-specific, not in TickEngine)
            if (CurrentAction == UnitAction.IDLE)
            {
                if (currentBuilding != null)
                    currentBuilding.GetComponent<Unit>().ActiveBuilders.Remove(UnitNbr);
                currentBuilding = null;
                path.Clear();
                pathIndex = 0;
                MoveAccumulator = 0f;
                visualWaypoints.Clear();
                visualSegmentT = 1.0f;
            }

            // Phase 3: Mana regen
            Mana = AgentSDK.TaskEngine.RegenMana(Mana, MaxMana, Constants.MANA_REGEN, Time.fixedDeltaTime);

            // Phase 4: Remove dead units
            if (Health <= 0)
            {
                SpawnDeathDust();
                if (currentBuilding != null)
                    currentBuilding.GetComponent<Unit>().ActiveBuilders.Remove(UnitNbr);
                currentBuilding = null;
                GameManager.Instance.Units.DestroyUnit(gameObject);
            }
        }

        /// <summary>Enqueue a visual waypoint for smooth movement interpolation.</summary>
        internal void EnqueueVisualWaypoint(Vector3Int pos)
        {
            visualWaypoints.Enqueue(pos);
            visualSpeed = Speed / Time.fixedDeltaTime;
        }

        /// <summary>Set velocity for animation facing from a movement step.</summary>
        internal void SetVelocityFromMovement(Vector3Int from, Vector3Int to)
        {
            velocity = (Vector3)(to - from);
            velocity = Utility.SafeNormalize(velocity);
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

        Position ITickUnit.CenterPosition => new Position(
            (int)CenterGridPosition.x, (int)CenterGridPosition.y);

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
                visualWaypoints.Clear();
                visualSegmentT = 1.0f;
                WorldPosition = (Vector3)GridPosition + new Vector3(0.5f, 0f, 0);
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
