using System.Collections.Generic;

namespace AgentSDK
{
    /// <summary>
    /// Callback interface for engine-specific side effects during tick processing.
    /// Unity implements this for visual effects, sounds, analytics.
    /// SimGame uses <see cref="NullTickCallbacks"/> (no-op).
    /// </summary>
    public interface ITickCallbacks
    {
        void OnUnitMoved(ITickUnit unit, Position from, Position to);
        void OnDamageDealt(ITickUnit attacker, ITickUnit target, float damage);
        void OnUnitKilled(ITickUnit unit);
        void OnTrainingComplete(ITickUnit building, ITickUnit spawnedUnit);
        void OnBuildProgress(ITickUnit pawn, ITickUnit building, float progress, float total);
        void OnBuildComplete(ITickUnit pawn, ITickUnit building);
        void OnMiningTick(ITickUnit pawn, ITickUnit mine, int goldMined);
        void OnGoldDeposited(ITickUnit pawn, int amount);
        void OnGatherPhaseChanged(ITickUnit pawn, GatherPhase oldPhase, GatherPhase newPhase);
        void OnHealApplied(ITickUnit healer, ITickUnit target, float amount);
        void OnRepairTick(ITickUnit pawn, ITickUnit building, float amount);
        void OnUnitRepath(ITickUnit unit, List<Position> newPath);
    }

    /// <summary>
    /// No-op implementation of <see cref="ITickCallbacks"/> for headless simulation.
    /// </summary>
    public class NullTickCallbacks : ITickCallbacks
    {
        public static readonly NullTickCallbacks Instance = new NullTickCallbacks();

        public void OnUnitMoved(ITickUnit unit, Position from, Position to) { }
        public void OnDamageDealt(ITickUnit attacker, ITickUnit target, float damage) { }
        public void OnUnitKilled(ITickUnit unit) { }
        public void OnTrainingComplete(ITickUnit building, ITickUnit spawnedUnit) { }
        public void OnBuildProgress(ITickUnit pawn, ITickUnit building, float progress, float total) { }
        public void OnBuildComplete(ITickUnit pawn, ITickUnit building) { }
        public void OnMiningTick(ITickUnit pawn, ITickUnit mine, int goldMined) { }
        public void OnGoldDeposited(ITickUnit pawn, int amount) { }
        public void OnGatherPhaseChanged(ITickUnit pawn, GatherPhase oldPhase, GatherPhase newPhase) { }
        public void OnHealApplied(ITickUnit healer, ITickUnit target, float amount) { }
        public void OnRepairTick(ITickUnit pawn, ITickUnit building, float amount) { }
        public void OnUnitRepath(ITickUnit unit, List<Position> newPath) { }
    }
}
