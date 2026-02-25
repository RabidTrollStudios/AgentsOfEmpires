namespace AgentSDK
{
    /// <summary>
    /// Actions a unit can be performing
    /// </summary>
    public enum UnitAction
    {
        /// <summary>Unit has nothing to do</summary>
        IDLE,
        /// <summary>Moving to a point</summary>
        MOVE,
        /// <summary>Training a new unit</summary>
        TRAIN,
        /// <summary>Building a structure</summary>
        BUILD,
        /// <summary>Gathering resources</summary>
        GATHER,
        /// <summary>Attacking a target</summary>
        ATTACK
    }
}
