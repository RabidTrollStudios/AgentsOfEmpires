using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AgentSDK;
using UnityEngine;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("GameManager")]

namespace GameManager
{
	class AgentController : MonoBehaviour
	{
		private GameObject debuggerPanel;
		private Dictionary<string, Func<string>> _debugUpdaters;
		private Text[] _debugTextAreas;

		internal Agent Agent;

		/// <summary>
		/// Link the Agent to the UI controller by giving it the Agent
		/// </summary>
		/// <param name="agent"></param>
		/// <param name="agentName"></param>
		/// <param name="agentNbr"></param>
		/// <param name="agentDLLName"></param>
		/// <param name="debuggerPanel"></param>
		/// <param name="dllPath"></param>
		internal void InitializeAgent(GameObject agent, string agentName, string agentDLLName, int agentNbr, GameObject debuggerPanel, string dllPath)
		{
			Agent = agent.GetComponent<Agent>();
            Agent.InitializeAgent(agentName, agentDLLName, agentNbr, dllPath);
            this.debuggerPanel = debuggerPanel;

            // Initialize adapters for SDK-based agents
            if (Agent is AgentBridge bridge)
            {
                bridge.InitializeAdapters(agentNbr,
                    GameManager.Instance.Units,
                    GameManager.Instance.Map,
                    GameManager.Instance.Events);
            }

            _debugUpdaters = new Dictionary<string, Func<string>>
            {
                ["Agent Name"]     = () => $"{Agent.AgentName} {Agent.AgentDLLName}",
                ["Agent Nbr"]      = () => Agent.AgentNbr.ToString(),
                ["Gold Value"]     = () => Agent.Gold.ToString(),
                ["Workers Count"]  = () => Count(UnitType.WORKER),
                ["Soldiers Count"] = () => Count(UnitType.SOLDIER),
                ["Archers Count"]  = () => Count(UnitType.ARCHER),
                ["Bases Count"]    = () => Count(UnitType.BASE),
                ["Barracks Count"]  = () => Count(UnitType.BARRACKS),
                ["Archery Count"]   = () => Count(UnitType.ARCHERY),
                ["Custom Debug"]    = () => (Agent as AgentBridge)?.PlanningAgentDebugText ?? "",
            };
            _debugTextAreas = debuggerPanel != null
                ? debuggerPanel.GetComponentsInChildren<Text>()
                : System.Array.Empty<Text>();
        }

        /// <summary>
        /// InitializeMatch
        /// Called once at the beginning of each match.
        /// Multiple rounds make up a match between a single pair
        /// of agents.  Sets up any variables for the entire match.
        /// </summary>
        internal void InitializeMatch()
        {
            Agent.InitializeMatch();
        }

        /// <summary>
        /// InitializeRound
        /// Called once at the beginning of each round
        /// </summary>
        internal void InitializeRound()
        {
            Agent.Gold = GameManager.Instance.StartingPlayerGold;
            Agent.InitializeRound();
        }

        /// <summary>
        /// Learn
        /// Called once after each round before remaining units are destroyed
        /// </summary>
        internal void Learn()
        {
            Agent.Learn();
        }

        /// <summary>
        /// Updated
        /// Called once per frame
        /// </summary>
        public void Update()
		{
			if (Agent == null)
				return;

			if (debuggerPanel != null)
			debuggerPanel.SetActive(GameManager.Instance.HasAgentDebugging);

			if (!GameManager.Instance.HasAgentDebugging)
				return;

			foreach (var textArea in _debugTextAreas)
				if (_debugUpdaters.TryGetValue(textArea.name, out var getValue))
					textArea.text = getValue();
		}

		private string Count(UnitType type) =>
			GameManager.Instance.Units.GetUnitNbrsOfType(type, Agent.AgentNbr).Count.ToString();
	}
}
