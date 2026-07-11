using System.Collections.Generic;
using AgentSDK;
using GameManager.GameElements;
using UnityEngine;

namespace GameManager
{
    /// <summary>
    /// Unity-side callbacks for TickEngine events.
    /// Handles visual effects, grid cell sync, and analytics.
    /// </summary>
    internal class UnityTickCallbacks : ITickCallbacks
    {
        public void OnUnitMoved(ITickUnit unit, Position from, Position to)
        {
            if (unit is Unit u)
            {
                // Sync legacy GridCells (for visual/debug code that still uses them)
                var map = GameManager.Instance?.Map;
                if (map != null && map.GridCells != null && map.Grid != null)
                {
                    var fromV = new Vector3Int(from.X, from.Y, 0);
                    var toV = new Vector3Int(to.X, to.Y, 0);
                    if (fromV.x >= 0 && fromV.x < map.MapSize.x && fromV.y >= 0 && fromV.y < map.MapSize.y)
                    {
                        if (!map.Grid.IsPassageCell(from))
                            map.GridCells[fromV.x, fromV.y].SetBuildable(true);
                    }
                    if (toV.x >= 0 && toV.x < map.MapSize.x && toV.y >= 0 && toV.y < map.MapSize.y)
                    {
                        map.GridCells[toV.x, toV.y].SetBuildable(false);
                    }
                }

                // Notify VSM of cell crossing for animation snapshot
                u.NotifyVisualCellCrossed();
            }
        }

        public void OnDamageDealt(ITickUnit attacker, ITickUnit target, float damage) { }
        public void OnUnitKilled(ITickUnit unit)
        {
            if (unit is Unit u)
                u.SpawnDeathDust();
        }

        public void OnTrainingComplete(ITickUnit building, ITickUnit spawnedUnit)
        {
            UnityEngine.Debug.Log($"[TRAINDBG] OnTrainingComplete: {building.UnitType} -> spawned {spawnedUnit?.UnitType} #{spawnedUnit?.UnitNbr}");
        }

        public void OnBuildProgress(ITickUnit pawn, ITickUnit building, float progress, float total)
        {
            // Transition pawn to BUILDING animation phase (path consumed, now hammering)
            if (pawn is Unit pawnUnit)
                pawnUnit.buildPhase = BuildPhase.BUILDING;

            if (building is Unit buildingUnit)
            {
                buildingUnit.BuildProgress = progress;
                float newRatio = total > 0f ? progress / total : 0f;
                // Only allow ratio to increase — prevents bar dropping when game speed changes
                if (newRatio > buildingUnit.buildRatio)
                    buildingUnit.buildRatio = newRatio;
                // Lerp opacity
                var sr = buildingUnit.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    var c = sr.color;
                    c.a = 0.3f + Mathf.Clamp01(buildingUnit.buildRatio) * 0.4f;
                    sr.color = c;
                }
            }
        }

        public void OnBuildComplete(ITickUnit pawn, ITickUnit building)
        {
            if (building is Unit buildingUnit)
            {
                buildingUnit.buildPulseFrames = 24; // BUILD_PULSE_TOTAL
                var sr = buildingUnit.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    var c = sr.color;
                    c.a = 1.0f;
                    sr.color = c;
                }
            }
        }

        public void OnMiningTick(ITickUnit pawn, ITickUnit mine, int goldMined) { }
        public void OnGoldDeposited(ITickUnit pawn, int amount) { }
        public void OnGatherPhaseChanged(ITickUnit pawn, GatherPhase oldPhase, GatherPhase newPhase) { }
        public void OnHealApplied(ITickUnit healer, ITickUnit target, float amount)
        {
            if (target is Unit targetUnit && healer is Unit healerUnit)
            {
                healerUnit.SpawnHealEffect(targetUnit);
                healerUnit.lastHealTargetNbr = targetUnit.UnitNbr;
                healerUnit.healLineTimer = Unit.HEAL_LINE_DURATION;
            }
        }
        public void OnRepairTick(ITickUnit pawn, ITickUnit building, float amount) { }
        public void OnUnitRepath(ITickUnit unit, List<Position> newPath) { }
    }
}
