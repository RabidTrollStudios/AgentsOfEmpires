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

        // Movement — TickPath converts between List<Position> and List<Vector3Int>
        List<Position> ITickUnit.TickPath
        {
            get
            {
                if (path == null || path.Count == 0) return null;
                var result = new List<Position>(path.Count);
                foreach (var p in path)
                    result.Add(new Position(p.x, p.y));
                return result;
            }
            set
            {
                if (value == null || value.Count == 0)
                {
                    path.Clear();
                    pathIndex = 0;
                }
                else
                {
                    path.Clear();
                    foreach (var p in value)
                        path.Add(new Vector3Int(p.X, p.Y, 0));
                    pathIndex = 0;
                }
                // Clear visual waypoints when path changes
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

        // Building — map to taskTime/taskUnitType/currentBuilding
        float ITickUnit.BuildTimer
        {
            get => currentBuilding != null ? currentBuilding.GetComponent<Unit>().BuildProgress : 0f;
            set { if (currentBuilding != null) currentBuilding.GetComponent<Unit>().BuildProgress = value; }
        }
        UnitType ITickUnit.BuildTarget { get => taskUnitType; set => taskUnitType = value; }
        Position ITickUnit.BuildSite
        {
            get => currentBuilding != null
                ? new Position(currentBuilding.GetComponent<Unit>().GridPosition.x,
                              currentBuilding.GetComponent<Unit>().GridPosition.y)
                : new Position(0, 0);
            set { } // Build site is determined by the building object
        }
        int ITickUnit.BuildTargetNbr
        {
            get => currentBuilding != null ? currentBuilding.GetComponent<Unit>().UnitNbr : -1;
            set { } // Set via currentBuilding reference, not by number
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
            set { } // Set via currentBuilding reference
        }

        // Heal
        int ITickUnit.HealTargetNbr { get => healTargetNbr; set => healTargetNbr = value; }
    }
}
