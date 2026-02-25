namespace GameManager.EnumTypes
{
	/// <summary>
	/// GatherPhase - phases of the gathering action
	/// </summary>
	public enum GatherPhase
    {
        /// <summary>
        /// TO_MINE - moving to the resource
        /// </summary>
        TO_MINE,
        /// <summary>
        /// MINING - collecting the resource
        /// </summary>
        MINING,
        /// <summary>
        /// TO_BASE - returning to base
        /// </summary>
        TO_BASE
    }
}
