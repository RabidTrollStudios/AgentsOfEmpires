using System.Collections.Generic;

namespace AgentSDK
{
    /// <summary>
    /// Abstraction over a game unit for the shared TickEngine.
    /// Both SimUnit (headless) and Unity's Unit (MonoBehaviour) implement this.
    /// </summary>
    public interface ITickUnit
    {
        // Identity (read-only)
        int UnitNbr { get; }
        UnitType UnitType { get; }
        int OwnerAgentNbr { get; }
        bool CanMove { get; }
        bool CanAttack { get; }
        bool CanBuild { get; }
        bool CanGather { get; }
        bool CanTrain { get; }
        bool CanHeal { get; }

        // Core state
        Position GridPosition { get; set; }
        Position CenterPosition { get; }
        float Health { get; set; }
        bool IsBuilt { get; set; }
        UnitAction CurrentAction { get; set; }
        float Mana { get; set; }

        // Movement
        List<Position> TickPath { get; set; }
        int PathIndex { get; set; }
        float PathProgress { get; set; }

        // Training
        float TrainTimer { get; set; }
        UnitType TrainTarget { get; set; }

        // Building
        float BuildTimer { get; set; }
        UnitType BuildTarget { get; set; }
        Position BuildSite { get; set; }
        int BuildTargetNbr { get; set; }

        /// <summary>
        /// Construction progress (seconds) accumulated on an unbuilt BUILDING unit.
        /// Lives on the building — not the pawn — so it survives the builder's death
        /// and lets any pawn resume construction. Ignored on non-building units.
        /// </summary>
        float BuildProgress { get; set; }

        // Gathering
        int GatherMineNbr { get; set; }
        int GatherBaseNbr { get; set; }
        GatherPhase GatherPhase { get; set; }
        float MiningTimer { get; set; }
        int GoldCarried { get; set; }

        // Combat
        int AttackTargetNbr { get; set; }

        /// <summary>
        /// Set when a pursuit pathfind was deferred this tick by <see cref="PathBudget"/>.
        /// The unit keeps its action and target but has no path yet; the per-tick Advance*
        /// handler retries the (gated) pathfind each tick until the budget admits it.
        /// Cleared as soon as a path is obtained or the action ends. Purely an engine-side
        /// rate-limit flag — the PlanningAgent neither sets nor sees it.
        /// </summary>
        bool RepathPending { get; set; }

        // Repair
        int RepairBuildingNbr { get; set; }

        // Heal
        int HealTargetNbr { get; set; }
    }
}
