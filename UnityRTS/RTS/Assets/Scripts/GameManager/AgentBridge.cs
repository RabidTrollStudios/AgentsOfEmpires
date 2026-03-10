using AgentSDK;
using System.Collections.Generic;
using System.Linq;

namespace GameManager
{
    /// <summary>
    /// MonoBehaviour wrapper that hosts an IPlanningAgent instance.
    /// Extends Agent so it fits into the existing AgentController/GameManager infrastructure.
    /// </summary>
    public class AgentBridge : Agent
    {
        private IPlanningAgent planningAgent;
        private GameStateAdapter gameState;
        private AgentActionsAdapter actions;

        /// <summary>
        /// Debug text from the planning agent, if it extends PlanningAgentBase.
        /// </summary>
        public string PlanningAgentDebugText =>
            (planningAgent as PlanningAgentBase)?.DebugText ?? "";

        internal void SetPlanningAgent(IPlanningAgent agent)
        {
            this.planningAgent = agent;
        }

        internal void InitializeAdapters(int agentNbr, UnitManager unitManager,
            MapManager mapManager, EventDispatcher events)
        {
            gameState = new GameStateAdapter(agentNbr, unitManager, mapManager);
            actions = new AgentActionsAdapter(this, unitManager);
        }

        internal void UpdateEnemyAgentNbr()
        {
            List<int> enemies = GameManager.Instance.GetEnemyAgentNbrs(AgentNbr);
            if (enemies.Any())
            {
                gameState.UpdateEnemyAgentNbr(enemies[0]);
            }
        }

        public override void InitializeMatch()
        {
            planningAgent?.InitializeMatch();
        }

        public override void InitializeRound()
        {
            UpdateEnemyAgentNbr();
            planningAgent?.InitializeRound(gameState);
        }

        public override void Update()
        {
            if (planningAgent == null || !GameManager.Instance.IsPlaying) return;
            planningAgent.Update(gameState, actions);
        }

        public override void Learn()
        {
            planningAgent?.Learn(gameState);
        }
    }
}
