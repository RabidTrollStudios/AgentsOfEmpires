namespace AgentSDK
{
    /// <summary>
    /// All unit types in the game
    /// </summary>
    public enum UnitType
    {
        /// <summary>Gold mine resource</summary>
        MINE,
        /// <summary>Worker unit - gathers resources and builds structures</summary>
        WORKER,
        /// <summary>Melee combat unit</summary>
        SOLDIER,
        /// <summary>Ranged combat unit</summary>
        ARCHER,
        /// <summary>Main base structure - trains workers</summary>
        BASE,
        /// <summary>Military structure - trains soldiers and archers</summary>
        BARRACKS,
        /// <summary>Economic structure - boosts mining speed</summary>
        REFINERY,
    }
}
