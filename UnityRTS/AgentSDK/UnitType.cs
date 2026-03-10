namespace AgentSDK
{
    /// <summary>
    /// All unit types in the game
    /// </summary>
    public enum UnitType
    {
        /// <summary>Gold mine resource</summary>
        MINE,
        /// <summary>Pawn unit - gathers resources and builds structures</summary>
        PAWN,
        /// <summary>Melee combat unit</summary>
        WARRIOR,
        /// <summary>Ranged combat unit</summary>
        ARCHER,
        /// <summary>Main base structure - trains pawns</summary>
        BASE,
        /// <summary>Military structure - trains warriors</summary>
        BARRACKS,
        /// <summary>Ranged military structure - trains archers</summary>
        ARCHERY,
    }
}
