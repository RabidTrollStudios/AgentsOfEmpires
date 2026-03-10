using System.Collections.Generic;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Tracks which phase of the gather cycle a unit is in.
    /// </summary>
    public enum GatherPhase
    {
        TO_MINE,
        MINING,
        TO_BASE
    }

    /// <summary>
    /// Mutable unit state in the simulation. Mirrors the real game's Unit class
    /// but without MonoBehaviour/Unity dependencies.
    /// </summary>
    public class SimUnit
    {
        public int UnitNbr { get; }
        public UnitType UnitType { get; }
        public int OwnerAgentNbr { get; }
        public Position GridPosition { get; set; }
        public float Health { get; set; }
        public bool IsBuilt { get; set; }
        public UnitAction CurrentAction { get; set; }

        // Movement state
        internal List<Position> Path;
        internal int PathIndex;

        // Training state
        internal float TrainTimer;
        internal UnitType TrainTarget;

        // Building state — pawn walks to site, then counts down build timer
        internal float BuildTimer;
        internal UnitType BuildTarget;
        internal Position BuildSite;
        internal bool BuildPlaced; // whether the building has been placed on the map

        // Gathering state
        internal int GatherMineNbr;
        internal int GatherBaseNbr;
        internal GatherPhase GatherPhase;
        internal float MiningTimer;

        // Movement speed accumulator (for fractional movement)
        internal float MoveAccumulator;

        // Attack state
        internal int AttackTargetNbr;

        public SimUnit(int unitNbr, UnitType unitType, int ownerAgentNbr, Position gridPosition, float health, bool isBuilt)
        {
            UnitNbr = unitNbr;
            UnitType = unitType;
            OwnerAgentNbr = ownerAgentNbr;
            GridPosition = gridPosition;
            Health = health;
            IsBuilt = isBuilt;
            CurrentAction = UnitAction.IDLE;
            AttackTargetNbr = -1;
            GatherMineNbr = -1;
            GatherBaseNbr = -1;
        }

        /// <summary>
        /// Center cell of this unit's footprint. Use for distance calculations.
        /// For 1x1 units equals GridPosition; for 3x3 structures returns GridPosition+(1,-1).
        /// </summary>
        public Position CenterPosition
        {
            get
            {
                var size = GameConstants.UNIT_SIZE[UnitType];
                return Position.Center(GridPosition, size);
            }
        }

        /// <summary>
        /// Create an immutable UnitInfo snapshot for IGameState queries.
        /// </summary>
        public UnitInfo ToUnitInfo()
        {
            return new UnitInfo(
                UnitNbr,
                UnitType,
                GridPosition,
                Health,
                IsBuilt,
                CurrentAction,
                GameConstants.CAN_MOVE[UnitType],
                GameConstants.CAN_BUILD[UnitType],
                GameConstants.CAN_TRAIN[UnitType],
                GameConstants.CAN_ATTACK[UnitType],
                GameConstants.CAN_GATHER[UnitType],
                OwnerAgentNbr
            );
        }
    }
}
