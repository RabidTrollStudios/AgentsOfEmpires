using System.Collections.Generic;
using AgentSDK;

namespace AgentTestHarness
{
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
        internal float MiningTimer;  // fractional gold accumulator during MINING phase
        internal int GoldCarried;    // total gold mined this trip

        // Movement speed accumulator (for fractional movement)
        internal float MoveAccumulator;

        // Attack state
        internal int AttackTargetNbr;

        // Repair state
        internal int RepairBuildingNbr;

        // Local avoidance state — how many ticks the unit has waited for a blocker to clear
        internal int LocalAvoidWaitTicks;

        // Heal state
        internal int HealTargetNbr;
        public float Mana { get; set; }

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
            RepairBuildingNbr = -1;
            GatherMineNbr = -1;
            GatherBaseNbr = -1;
            HealTargetNbr = -1;
            Mana = GameConstants.MAX_MANA[unitType];
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
                if (!GameConstants.CAN_MOVE[UnitType] && size.Y > 1)
                {
                    // Building: center on non-walkable rows (skip walkable top row)
                    var nwAnchor = new Position(GridPosition.X, GridPosition.Y - 1);
                    var nwSize = new Position(size.X, size.Y - 1);
                    return Position.Center(nwAnchor, nwSize);
                }
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
                GameConstants.CAN_HEAL[UnitType],
                Mana,
                OwnerAgentNbr
            );
        }
    }
}
