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
        /// Pre-tick update: death check, IDLE cleanup, mana regen.
        /// Called before TickEngine.AdvanceAllUnits.
        /// </summary>
        internal void PreTickUpdate()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

            MineUnit = GameManager.Instance.Units.GetUnit(mineUnit);
            BaseUnit = GameManager.Instance.Units.GetUnit(baseUnit);
            pathUpdateCounter++;

            // Death check
            if (Health <= 0)
            {
                SpawnDeathDust();
                if (currentBuilding != null)
                    currentBuilding.GetComponent<Unit>().ActiveBuilders.Remove(UnitNbr);
                currentBuilding = null;
                GameManager.Instance.Units.DestroyUnit(gameObject);
                return;
            }

            // IDLE cleanup
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
                TargetGridPos = GridPosition;
                TargetUnitType = UnitType.PAWN;
                AttackUnit = null;
                MineUnit = null;
                BaseUnit = null;
                arrowFiredThisCycle = false;
            }

            // Mana regen
            Mana = AgentSDK.TaskEngine.RegenMana(Mana, MaxMana, Constants.MANA_REGEN, Time.fixedDeltaTime);
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

        // Identity — delegate to existing properties
        int ITickUnit.UnitNbr => UnitNbr;
        UnitType ITickUnit.UnitType => UnitType;
        int ITickUnit.OwnerAgentNbr => OwnerAgentNbr;

        // GridPosition — convert between Vector3Int and Position
        Position ITickUnit.GridPosition
        {
            get => new Position(GridPosition.x, GridPosition.y);
            set => GridPosition = new Vector3Int(value.X, value.Y, 0);
        }

        Position ITickUnit.CenterPosition => new Position(
            (int)CenterGridPosition.x, (int)CenterGridPosition.y);

        // Health, IsBuilt, CurrentAction, Mana — direct
        float ITickUnit.Health { get => Health; set => Health = value; }
        bool ITickUnit.IsBuilt { get => IsBuilt; set => IsBuilt = value; }
        UnitAction ITickUnit.CurrentAction { get => CurrentAction; set => CurrentAction = value; }
        float ITickUnit.Mana { get => Mana; set => Mana = value; }

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
        AgentSDK.GatherPhase ITickUnit.GatherPhase
        {
            get => (AgentSDK.GatherPhase)(int)gatherPhase;
            set => gatherPhase = (EnumTypes.GatherPhase)(int)value;
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
