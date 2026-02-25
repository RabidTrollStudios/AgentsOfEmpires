using AgentSDK;
using UnityEngine;

namespace GameManager
{
    /// <summary>
    /// Implements IAgentActions by resolving unit IDs to Unit objects
    /// and delegating to the existing Agent command methods (which handle validation).
    /// </summary>
    public class AgentActionsAdapter : IAgentActions
    {
        private Agent agent;
        private UnitManager unitManager;

        public AgentActionsAdapter(Agent agent, UnitManager unitManager)
        {
            this.agent = agent;
            this.unitManager = unitManager;
        }

        public void Move(int unitNbr, Position target)
        {
            var unit = unitManager.GetUnit(unitNbr);
            if (unit == null) return;
            agent.Move(unit, new Vector3Int(target.X, target.Y, 0));
        }

        public void Build(int unitNbr, Position target, AgentSDK.UnitType unitType)
        {
            var unit = unitManager.GetUnit(unitNbr);
            if (unit == null) return;
            agent.Build(unit, new Vector3Int(target.X, target.Y, 0), unitType);
        }

        public void Gather(int workerNbr, int mineNbr, int baseNbr)
        {
            var worker = unitManager.GetUnit(workerNbr);
            var mine = unitManager.GetUnit(mineNbr);
            var baseUnit = unitManager.GetUnit(baseNbr);
            if (worker == null || mine == null || baseUnit == null) return;
            agent.Gather(worker, mine, baseUnit);
        }

        public void Train(int buildingNbr, AgentSDK.UnitType unitType)
        {
            var building = unitManager.GetUnit(buildingNbr);
            if (building == null) return;
            agent.Train(building, unitType);
        }

        public void Attack(int unitNbr, int targetNbr)
        {
            var unit = unitManager.GetUnit(unitNbr);
            var target = unitManager.GetUnit(targetNbr);
            if (unit == null || target == null) return;
            agent.Attack(unit, target);
        }

        public void Log(string message)
        {
            agent.Log(message);
        }
    }
}
