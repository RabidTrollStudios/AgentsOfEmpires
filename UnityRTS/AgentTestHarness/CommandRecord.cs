using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// A single recorded agent command with the frame it was issued on.
    /// Uses a flat structure — unused fields per command type cost nothing meaningful.
    /// </summary>
    public class CommandRecord
    {
        public int Frame;
        public int AgentNbr;
        public CommandType Type;

        // Move / Build / Gather (pawn) / Attack / Repair (pawn)
        public int UnitNbr;

        // Move target / Build site
        public Position Target;

        // Build
        public UnitType BuildingType;

        // Gather
        public int MineNbr;
        public int BaseNbr;

        // Train
        public int BuildingNbr;
        public UnitType TrainType;

        // Attack
        public int TargetUnitNbr;

        // Repair
        public int RepairBuildingNbr;

        // Log
        public string LogMessage;
    }
}
