namespace AgentSDK
{
    /// <summary>
    /// Phases of the gathering action cycle.
    /// </summary>
    public enum GatherPhase
    {
        /// <summary>Walking to the mine.</summary>
        TO_MINE,
        /// <summary>Mining gold at the mine.</summary>
        MINING,
        /// <summary>Returning gold to the base.</summary>
        TO_BASE
    }
}
