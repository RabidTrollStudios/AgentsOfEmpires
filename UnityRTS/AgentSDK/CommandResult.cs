namespace AgentSDK
{
    /// <summary>
    /// Result codes returned by IAgentActions command methods.
    /// Agents can inspect these to understand why a command failed
    /// and avoid re-issuing the same failing command every frame.
    /// </summary>
    public enum CommandResult
    {
        /// <summary>Command was accepted and dispatched to the unit.</summary>
        SUCCESS,

        /// <summary>The specified unit does not exist.</summary>
        UNIT_NOT_FOUND,

        /// <summary>The unit does not have the capability for this command
        /// (e.g., a warrior trying to build).</summary>
        UNIT_CANNOT_PERFORM_ACTION,

        /// <summary>The target position is outside the map boundaries.</summary>
        INVALID_POSITION,

        /// <summary>The target position is not walkable (blocked by terrain).</summary>
        POSITION_NOT_WALKABLE,

        /// <summary>The build area is occupied or otherwise not buildable.</summary>
        AREA_NOT_BUILDABLE,

        /// <summary>A required dependency building has not been built yet.</summary>
        MISSING_DEPENDENCY,

        /// <summary>Not enough gold to perform the action.</summary>
        INSUFFICIENT_GOLD,

        /// <summary>The target unit does not exist.</summary>
        TARGET_NOT_FOUND,

        /// <summary>The target unit type is invalid for this command
        /// (e.g., attacking a mine, gathering from a non-mine).</summary>
        INVALID_TARGET,

        /// <summary>Cannot attack or interact with your own units in this way.</summary>
        FRIENDLY_FIRE,

        /// <summary>The unit is busy with another action and cannot accept this command.</summary>
        UNIT_BUSY,

        /// <summary>The building has not finished construction yet.</summary>
        BUILDING_NOT_FINISHED,

        /// <summary>No path could be found to the target.</summary>
        NO_PATH_FOUND,

        /// <summary>The command was throttled because the same unit recently failed
        /// a similar command. Try again after a short delay.</summary>
        ON_COOLDOWN
    }
}
