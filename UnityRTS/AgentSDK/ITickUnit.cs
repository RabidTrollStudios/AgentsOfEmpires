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
        float MoveAccumulator { get; set; }

        // Training
        float TrainTimer { get; set; }
        UnitType TrainTarget { get; set; }

        // Building
        float BuildTimer { get; set; }
        UnitType BuildTarget { get; set; }
        Position BuildSite { get; set; }
        int BuildTargetNbr { get; set; }

        // Gathering
        int GatherMineNbr { get; set; }
        int GatherBaseNbr { get; set; }
        GatherPhase GatherPhase { get; set; }
        float MiningTimer { get; set; }
        int GoldCarried { get; set; }

        // Combat
        int AttackTargetNbr { get; set; }

        // Repair
        int RepairBuildingNbr { get; set; }

        // Heal
        int HealTargetNbr { get; set; }
    }
}
