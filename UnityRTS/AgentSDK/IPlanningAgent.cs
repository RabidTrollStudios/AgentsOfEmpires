namespace AgentSDK
{
    /// <summary>
    /// The interface your AI agent must implement.
    /// The game engine will call these methods at the appropriate times.
    /// </summary>
    public interface IPlanningAgent
    {
        /// <summary>
        /// Called once at the beginning of a match (before any rounds).
        /// Use this for one-time setup that persists across rounds.
        /// </summary>
        void InitializeMatch();

        /// <summary>
        /// Called at the beginning of each round.
        /// Use this to reset per-round state. The game state is available
        /// for initial queries (e.g., finding build positions).
        /// </summary>
        /// <param name="state">The initial game state for this round</param>
        void InitializeRound(IGameState state);

        /// <summary>
        /// Called every frame during gameplay.
        /// This is your main decision-making method - query the game state
        /// and issue commands to your units.
        /// </summary>
        /// <param name="state">Current game state (read-only)</param>
        /// <param name="actions">Interface for issuing commands</param>
        void Update(IGameState state, IAgentActions actions);

        /// <summary>
        /// Called at the end of each round before units are destroyed.
        /// Use this to observe the win/loss state and learn from it.
        /// </summary>
        /// <param name="state">The final game state for this round</param>
        void Learn(IGameState state);
    }
}
