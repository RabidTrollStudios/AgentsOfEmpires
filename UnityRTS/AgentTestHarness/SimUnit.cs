using System.Collections.Generic;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Mutable unit state in the simulation. Mirrors the real game's Unit class
    /// but without MonoBehaviour/Unity dependencies.
    /// Implements <see cref="ITickUnit"/> for the shared TickEngine.
    /// </summary>
    public class SimUnit : ITickUnit
    {
        // Identity
        public int UnitNbr { get; }
        public UnitType UnitType { get; }
        public int OwnerAgentNbr { get; }
        public bool CanMove => GameConstants.CAN_MOVE[UnitType];
        public bool CanAttack => GameConstants.CAN_ATTACK[UnitType];
        public bool CanBuild => GameConstants.CAN_BUILD[UnitType];
        public bool CanGather => GameConstants.CAN_GATHER[UnitType];
        public bool CanTrain => GameConstants.CAN_TRAIN[UnitType];
        public bool CanHeal => GameConstants.CAN_HEAL[UnitType];

        // Core state
        public Position GridPosition { get; set; }
        public float Health { get; set; }
        public bool IsBuilt { get; set; }
        public UnitAction CurrentAction { get; set; }
        public float Mana { get; set; }

        // Movement
        public List<Position> TickPath { get; set; }
        public int PathIndex { get; set; }
        public float PathProgress { get; set; }

        // Training
        public float TrainTimer { get; set; }
        public UnitType TrainTarget { get; set; }

        // Building
        public float BuildTimer { get; set; }
        public UnitType BuildTarget { get; set; }
        public Position BuildSite { get; set; }
        public int BuildTargetNbr { get; set; }

        /// <summary>Construction progress (seconds) on an unbuilt building — see ITickUnit.BuildProgress.</summary>
        public float BuildProgress { get; set; }

        // Gathering
        public int GatherMineNbr { get; set; }
        public int GatherBaseNbr { get; set; }
        public GatherPhase GatherPhase { get; set; }
        public float MiningTimer { get; set; }
        public int GoldCarried { get; set; }

        // Combat
        public int AttackTargetNbr { get; set; }

        // Repair
        public int RepairBuildingNbr { get; set; }

        // Heal
        public int HealTargetNbr { get; set; }

        // Local avoidance (not in ITickUnit but kept for backward compat)
        internal int LocalAvoidWaitTicks;

        // Legacy alias for code that still uses "Path"
        internal List<Position> Path
        {
            get => TickPath;
            set => TickPath = value;
        }

        // Legacy alias for BuildPlaced
        internal bool BuildPlaced { get; set; }

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
            BuildTargetNbr = -1;
            GatherMineNbr = -1;
            GatherBaseNbr = -1;
            HealTargetNbr = -1;
            Mana = GameConstants.MAX_MANA[unitType];
        }

        /// <summary>
        /// Center cell of this unit's footprint. Use for distance calculations.
        /// </summary>
        public Position CenterPosition => TaskEngine.ComputeCenterPosition(UnitType, GridPosition);

        /// <summary>
        /// Create an immutable UnitInfo snapshot for IGameState queries.
        /// </summary>
        public UnitInfo ToUnitInfo()
        {
            return new UnitInfo(
                UnitNbr, UnitType, GridPosition, Health, IsBuilt,
                CurrentAction, CanMove, CanBuild, CanTrain, CanAttack,
                CanGather, CanHeal, Mana, OwnerAgentNbr);
        }
    }
}
