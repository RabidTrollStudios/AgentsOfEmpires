using System.Collections.Generic;

namespace AgentSDK
{
    /// <summary>
    /// Callback interface for engine-specific side effects during step processing.
    /// Unity implements this for visual effects, sounds, analytics.
    /// SimGame uses <see cref="NullSimCallbacks"/> (no-op).
    /// </summary>
    public interface ISimCallbacks
    {
        void OnUnitMoved(ISimUnit unit, Position from, Position to);
        void OnDamageDealt(ISimUnit attacker, ISimUnit target, float damage);
        void OnUnitKilled(ISimUnit unit);
        void OnTrainingComplete(ISimUnit building, ISimUnit spawnedUnit);
        void OnBuildProgress(ISimUnit pawn, ISimUnit building, float progress, float total);
        void OnBuildComplete(ISimUnit pawn, ISimUnit building);
        void OnMiningTick(ISimUnit pawn, ISimUnit mine, int goldMined);
        void OnGoldDeposited(ISimUnit pawn, int amount);
        void OnGatherPhaseChanged(ISimUnit pawn, GatherPhase oldPhase, GatherPhase newPhase);
        void OnHealApplied(ISimUnit healer, ISimUnit target, float amount);
        void OnRepairTick(ISimUnit pawn, ISimUnit building, float amount);
        void OnUnitRepath(ISimUnit unit, List<Position> newPath);
    }

    /// <summary>
    /// No-op implementation of <see cref="ISimCallbacks"/> for headless simulation.
    /// </summary>
    public class NullSimCallbacks : ISimCallbacks
    {
        public static readonly NullSimCallbacks Instance = new NullSimCallbacks();

        public void OnUnitMoved(ISimUnit unit, Position from, Position to) { }
        public void OnDamageDealt(ISimUnit attacker, ISimUnit target, float damage) { }
        public void OnUnitKilled(ISimUnit unit) { }
        public void OnTrainingComplete(ISimUnit building, ISimUnit spawnedUnit) { }
        public void OnBuildProgress(ISimUnit pawn, ISimUnit building, float progress, float total) { }
        public void OnBuildComplete(ISimUnit pawn, ISimUnit building) { }
        public void OnMiningTick(ISimUnit pawn, ISimUnit mine, int goldMined) { }
        public void OnGoldDeposited(ISimUnit pawn, int amount) { }
        public void OnGatherPhaseChanged(ISimUnit pawn, GatherPhase oldPhase, GatherPhase newPhase) { }
        public void OnHealApplied(ISimUnit healer, ISimUnit target, float amount) { }
        public void OnRepairTick(ISimUnit pawn, ISimUnit building, float amount) { }
        public void OnUnitRepath(ISimUnit unit, List<Position> newPath) { }
    }
}
