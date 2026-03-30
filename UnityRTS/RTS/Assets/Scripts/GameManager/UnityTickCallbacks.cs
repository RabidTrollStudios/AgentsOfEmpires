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
                // Sync legacy GridCells
                var map = GameManager.Instance.Map;
                var fromV = new Vector3Int(from.X, from.Y, 0);
                var toV = new Vector3Int(to.X, to.Y, 0);
                if (!map.GridCells[fromV.x, fromV.y].IsPassage())
                    map.GridCells[fromV.x, fromV.y].SetBuildable(true);
                map.GridCells[toV.x, toV.y].SetBuildable(false);

                // Enqueue visual waypoint
                u.EnqueueVisualWaypoint(toV);

                // Update velocity for animation facing
                u.SetVelocityFromMovement(fromV, toV);
            }
        }

        public void OnDamageDealt(ITickUnit attacker, ITickUnit target, float damage) { }
        public void OnUnitKilled(ITickUnit unit) { }

        public void OnTrainingComplete(ITickUnit building, ITickUnit spawnedUnit) { }

        public void OnBuildProgress(ITickUnit pawn, ITickUnit building, float progress, float total)
        {
            if (building is Unit buildingUnit)
            {
                buildingUnit.BuildProgress = progress;
                // Lerp opacity
                var sr = buildingUnit.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    float pct = Mathf.Clamp01(progress / total);
                    var c = sr.color;
                    c.a = 0.3f + pct * 0.4f;
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
        public void OnHealApplied(ITickUnit healer, ITickUnit target, float amount) { }
        public void OnRepairTick(ITickUnit pawn, ITickUnit building, float amount) { }
        public void OnUnitRepath(ITickUnit unit, List<Position> newPath) { }
    }
}
