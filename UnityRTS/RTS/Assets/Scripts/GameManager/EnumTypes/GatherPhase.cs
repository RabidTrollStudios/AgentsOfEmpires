namespace GameManager.EnumTypes
{
	/// <summary>
	/// GatherPhase - phases of the gathering action.
	/// Values must match AgentSDK.GatherPhase for parity casting.
	/// </summary>
	public enum GatherPhase
    {
        TO_MINE,
        MINING,
        TO_BASE
    }
}
