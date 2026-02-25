namespace AgentSDK
{
    /// <summary>
    /// Read-only snapshot of a unit's current state.
    /// You cannot modify game state through this struct.
    /// </summary>
    public readonly struct UnitInfo
    {
        /// <summary>Unique identifier for this unit</summary>
        public int UnitNbr { get; }

        /// <summary>Type of this unit</summary>
        public UnitType UnitType { get; }

        /// <summary>Current grid position</summary>
        public Position GridPosition { get; }

        /// <summary>Current health (for mines, this is remaining gold)</summary>
        public float Health { get; }

        /// <summary>Whether this unit has finished being built/trained</summary>
        public bool IsBuilt { get; }

        /// <summary>What the unit is currently doing</summary>
        public UnitAction CurrentAction { get; }

        /// <summary>Whether this unit type can move</summary>
        public bool CanMove { get; }

        /// <summary>Whether this unit type can build structures</summary>
        public bool CanBuild { get; }

        /// <summary>Whether this unit type can train units</summary>
        public bool CanTrain { get; }

        /// <summary>Whether this unit type can attack</summary>
        public bool CanAttack { get; }

        /// <summary>Whether this unit type can gather resources</summary>
        public bool CanGather { get; }

        /// <summary>Agent number of the unit's owner</summary>
        public int OwnerAgentNbr { get; }

        /// <summary>
        /// Center cell of this unit's footprint.
        /// Use this for distance calculations instead of GridPosition, which is the top-left corner.
        /// For 1x1 units (soldiers, archers, workers) this equals GridPosition.
        /// For 3x3 structures (bases, barracks, mines, refineries) this is GridPosition+(1,-1).
        /// </summary>
        public Position CenterPosition
        {
            get
            {
                var size = GameConstants.UNIT_SIZE[UnitType];
                return Position.Center(GridPosition, size);
            }
        }

        public UnitInfo(int unitNbr, UnitType unitType, Position gridPosition,
            float health, bool isBuilt, UnitAction currentAction,
            bool canMove, bool canBuild, bool canTrain, bool canAttack,
            bool canGather, int ownerAgentNbr)
        {
            UnitNbr = unitNbr;
            UnitType = unitType;
            GridPosition = gridPosition;
            Health = health;
            IsBuilt = isBuilt;
            CurrentAction = currentAction;
            CanMove = canMove;
            CanBuild = canBuild;
            CanTrain = canTrain;
            CanAttack = canAttack;
            CanGather = canGather;
            OwnerAgentNbr = ownerAgentNbr;
        }
    }
}
