namespace AgentSDK
{
    /// <summary>
    /// A command that passed queue-time validation but failed during Phase 1 processing.
    /// Returned by <see cref="IGameState.GetFailedCommands"/>.
    /// </summary>
    public struct FailedCommand
    {
        /// <summary>The unit that was issued the command.</summary>
        public int UnitNbr;

        /// <summary>The type of command that failed.</summary>
        public CommandType Type;

        /// <summary>The reason the command failed.</summary>
        public CommandResult Reason;

        public FailedCommand(int unitNbr, CommandType type, CommandResult reason)
        {
            UnitNbr = unitNbr;
            Type = type;
            Reason = reason;
        }
    }
}
