using AgentSDK;
using GameManager.GameElements;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameManager
{
	public partial class GameManager
	{
		#region Game Loop

		/// <summary>
		/// Fixed-timestep tick: process deferred commands before units advance.
		/// Runs at 0.05s intervals (matching SimGame's tick rate).
		/// Must run BEFORE individual Unit.FixedUpdate() calls — set via
		/// [DefaultExecutionOrder(-100)] on the GameManager class.
		/// </summary>
		private UnityTickWorld unityTickWorld;
		private UnityTickCallbacks unityTickCallbacks;

		private void FixedUpdate()
		{
			if (gameState != GameState.PLAYING) return;

			// Clear failed commands from previous tick before processing new commands
			foreach (var agentGo in Agents.Values)
			{
				var bridge = agentGo.GetComponent<AgentController>()?.Agent as AgentBridge;
				if (bridge != null)
					bridge.ClearFailedCommands();
			}

			// Phase 1: Process queued commands in deterministic order
			DeferredCommandQueue.ProcessAll();

			// Phase 2+3: Advance all units via shared TickEngine.
			// This runs identical logic to SimGame, guaranteeing parity.
			if (unityTickWorld == null)
			{
				unityTickWorld = new UnityTickWorld();
				unityTickCallbacks = new UnityTickCallbacks();
			}

			// Run per-unit IDLE cleanup and mana regen before TickEngine
			// (these are Unity-specific and not in the shared logic)
			var allUnits = unitManager.GetAllUnits();
			var sortedKeys = new System.Collections.Generic.List<int>(allUnits.Keys);
			sortedKeys.Sort();
			foreach (int key in sortedKeys)
			{
				if (!allUnits.TryGetValue(key, out var go)) continue;
				var unit = go.GetComponent<GameElements.Unit>();
				if (unit != null)
					unit.PreTickUpdate();
			}

			AgentSDK.TickEngine.AdvanceAllUnits(unityTickWorld, unityTickCallbacks);
		}

		/// <summary>
		/// Main game loop - checks for winners, manages state transitions
		/// </summary>
		internal void Update()
        {
			if (gameState == GameState.INTRO)
			{
				// Wait for the intro banner to disappear before starting gameplay.
				// For rounds without an intro banner, transition immediately.
				if (!Prefabs.GameOverUI.GetComponent<Canvas>().enabled)
					gameState = GameState.PLAYING;
			}
			else if (gameState == GameState.PLAYING)
			{
				UpdateTimerUI();
				ProcessUserInput();
				UpdateCustomDebugUI();

				roundWinner = DetermineRoundWinner();
				if (roundWinner != null)
				{
					DeclareRoundWinner(roundWinner);
					Learn();
					SetAllAgentsInactive();
					unitManager.SetAllUnitsInactive();
					gameState = GameState.SHOWING_WINNER;
				}
			}
			else if (gameState == GameState.SHOWING_WINNER)
            {
				TimeToDisplayBanner -= Time.deltaTime;
                if (TimeToDisplayBanner < 0.0)
                {
					unitManager.DestroyAllUnits();

					int sum = DetermineRoundsCompleted();

	                if (sum == TotalNbrOfRounds)
					{
						foreach (GameObject agent in Agents.Values)
						{
							agent.GetComponent<AgentController>().Agent.SaveAnalytics();
							agent.GetComponent<AgentController>().Agent.CloseCommandLog();
						}

						if (dllNames == null)
						{
							DisplaySingleAgentResults();
						}
						else
						{
							DisplayMultiAgentResults();
						}

						TimeToDisplayBanner = 3.0f;
						gameState = GameState.FINISHED;
					}
					else
	                {
		                gameState = GameState.RESTARTING;
	                }
				}
			}
            else if (gameState == GameState.RESTARTING)
            {
                Prefabs.GameOverUI.GetComponent<Canvas>().enabled = false;
                InitializeRound();
            }
			else if (gameState == GameState.FINISHED)
			{
				TimeToDisplayBanner -= Time.deltaTime;
				if (TimeToDisplayBanner < 0.0f && _input != null && _input.Gameplay.DismissGameOver.WasPressedThisFrame())
				{
#if UNITY_EDITOR
					UnityEditor.EditorApplication.isPlaying = false;
#else
					Application.Quit();
#endif
				}
			}
		}

		#endregion

		#region Win Condition

		GameObject CalcScorePerUnit(Dictionary<GameObject, List<GameObject>> agentUnits,
			Dictionary<GameObject, int> agentScore)
		{
			foreach (GameObject agent in Agents.Values)
			{
				agentScore.Add(agent, 0);
				foreach(UnitType unitType in Enum.GetValues(typeof(UnitType)))
				{
					int value = agentUnits[agent].Count(unit => unit.GetComponent<Unit>().UnitType == unitType)
					            * Constants.UNIT_VALUE[unitType];
					agentScore[agent] += value;
				}
			}

			if (agentScore[Agents[0]] > agentScore[Agents[1]])
			{
				return Agents[0];
			}
			else if (agentScore[Agents[0]] < agentScore[Agents[1]])
			{
				return Agents[1];
			}
			else if (Agents[0].GetComponent<AgentController>().Agent.GetComponent<Agent>().Gold
				> Agents[1].GetComponent<AgentController>().Agent.GetComponent<Agent>().Gold)
			{
				return Agents[0];
			}
			else
			{
				return Agents[1];
			}
		}

        /// <summary>
        /// Determines if there is a game winner or not
        /// </summary>
        private GameObject DetermineRoundWinner()
		{
			int countActiveAgents = 0;
			Dictionary<GameObject, List<GameObject>> agentUnits = new Dictionary<GameObject, List<GameObject>>();
			Dictionary<GameObject, int> agentScore = new Dictionary<GameObject, int>();
			var allUnits = unitManager.GetAllUnits();

			foreach (GameObject agent in Agents.Values)
			{
				agentUnits.Add(agent, allUnits.Values.Where(
                    y => (y.GetComponent<Unit>().OwnerAgentNbr
							== agent.GetComponent<AgentController>().Agent.AgentNbr
						  && y.GetComponent<Unit>().UnitType != UnitType.MINE)).ToList());
				if (agentUnits[agent].Count > 0)
				{
					++countActiveAgents;
				}
			}

			if (TotalGameTime > MaxNbrOfSeconds)
			{
				return CalcScorePerUnit(agentUnits, agentScore);
			}

			if (countActiveAgents > 1)
			{
				return null;
			}

			foreach (GameObject agent in agentUnits.Keys)
			{
				if (agentUnits[agent].Count > 0)
				{
                    return agent;
				}
			}

			return null;
		}

		private int DetermineRoundsCompleted()
        {
	        int sum = 0;
	        foreach (string agentName in AgentWins.Keys)
	        {
		        sum += AgentWins[agentName];
	        }
	        return sum;
        }

		#endregion

	}
}
