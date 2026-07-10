namespace AgentSDK
{
    /// <summary>
    /// Engine-specific implementations of a single game tick's phases. Both the
    /// headless SimGame and the Unity GameManager implement this so that
    /// <see cref="TickSequence.RunOneTick"/> can drive them through an identical,
    /// canonical phase order — guaranteeing per-tick parity.
    ///
    /// The phases, in canonical order, are:
    ///   0. RecordSnapshot   — export/observe the PRE-processing state for this tick
    ///   1. ProcessCommands  — apply commands queued during the previous tick's agent Update
    ///   2. AdvanceUnits     — advance tasks/movement/combat/mana/death (shared TickEngine)
    ///   3. RunAgentUpdates  — invoke each agent's IPlanningAgent.Update; agents observe the
    ///                         post-advance state and queue commands for the NEXT tick
    ///
    /// Snapshotting FIRST means the recorded state at tick N is the state before tick N's
    /// command processing — identical in both engines. Agent Update runs LAST so a command
    /// issued during tick N is processed at the start of tick N+1 in both engines.
    /// </summary>
    public interface ITickParticipant
    {
        /// <summary>Phase 0: record/observe the pre-processing state for the given tick.</summary>
        void RecordSnapshot(int tick);

        /// <summary>Phase 1: process commands queued during the previous tick's agent Update.</summary>
        void ProcessQueuedCommands();

        /// <summary>Phase 2: advance all units by one tick (tasks, movement, combat, mana, death).</summary>
        void AdvanceUnits();

        /// <summary>Phase 3: run each agent's Update, queueing commands for the next tick.</summary>
        void RunAgentUpdates();
    }

    /// <summary>
    /// Canonical single-tick driver shared by all engines. Defines the one true phase
    /// order so Unity and the headless SimGame can never drift out of tick alignment.
    /// </summary>
    public static class TickSequence
    {
        /// <summary>
        /// Run exactly one tick against <paramref name="engine"/> using the canonical
        /// phase order. <paramref name="currentTick"/> is the tick number whose
        /// pre-processing state is recorded in Phase 0. The caller is responsible for
        /// advancing its own tick counter after this returns.
        /// </summary>
        public static void RunOneTick(ITickParticipant engine, int currentTick)
        {
            engine.RecordSnapshot(currentTick);   // Phase 0: pre-processing snapshot
            engine.ProcessQueuedCommands();       // Phase 1
            engine.AdvanceUnits();                // Phase 2
            engine.RunAgentUpdates();             // Phase 3: queue commands for next tick
        }
    }
}
