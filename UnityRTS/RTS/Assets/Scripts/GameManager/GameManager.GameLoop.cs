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
		/// Fixed-timestep simulation: process deferred commands before units advance.
		/// Runs at 50 Hz (matching SimGame's step rate and Unity's default fixedDeltaTime).
		/// Must run BEFORE individual Unit.FixedUpdate() calls — set via
		/// [DefaultExecutionOrder(-100)] on the GameManager class.
		/// </summary>
		private UnitySimWorld unitySimWorld;
		private UnitySimCallbacks unitySimCallbacks;

		/// <summary>Get the shared sim world adapter (lazy-initialized).</summary>
		internal AgentSDK.ISimWorld GetTickWorld()
		{
			if (unitySimWorld == null)
				unitySimWorld = new UnitySimWorld();
			return unitySimWorld;
		}

		/// <summary>Get the shared sim callbacks adapter (lazy-initialized).</summary>
		internal AgentSDK.ISimCallbacks GetTickCallbacks()
		{
			if (unitySimCallbacks == null)
				unitySimCallbacks = new UnitySimCallbacks();
			return unitySimCallbacks;
		}

		/// <summary>
		/// Run one game tick: process commands, advance all units via shared
		/// SimulationRunner, then run post-tick updates. Used by tests to
		/// simulate ticks without waiting for Unity's FixedUpdate cycle.
		/// </summary>
		internal void SimulateTick()
		{
			// Phase 1: Process commands queued during previous tick's Agent Update
			DeferredCommandQueue.ProcessAll();

			if (unitySimWorld == null)
				unitySimWorld = new UnitySimWorld();
			if (unitySimCallbacks == null)
				unitySimCallbacks = new UnitySimCallbacks();

			// Phase 2: Advance all units — shared SimulationRunner handles
			// StepEngine (task logic, mana, death) + MovementSystem (per-unit
			// movement) in deterministic order, identical to SimGame.
			AgentSDK.SimulationRunner.AdvanceStep(unitySimWorld, unitySimCallbacks);

			// Phase 3: Post-tick updates (Unity-specific: sync cached references)
			var allUnits = unitManager.GetAllUnits();
			var sortedKeys = new System.Collections.Generic.List<int>(allUnits.Keys);
			sortedKeys.Sort();
			foreach (int key in sortedKeys)
			{
				if (!allUnits.TryGetValue(key, out var go)) continue;
				var unit = go.GetComponent<GameElements.Unit>();
				if (unit != null)
					unit.PostTickUpdate();
			}

			// Phase 4: Agent Update — agents observe post-advance state and
			// queue commands for the next tick. Matches SimGame's tick order:
			// ProcessCommands → AdvanceAllUnits → AgentUpdate.
			if (Agents != null)
			{
				foreach (var agentGo in Agents.Values)
				{
					var agent = agentGo.GetComponent<AgentController>()?.Agent;
					if (agent != null && agent.gameObject.activeInHierarchy)
						agent.TickUpdate();
				}
			}
		}

		private void FixedUpdate()
		{
			if (gameState != GameState.PLAYING) return;

			// Clear failed commands from previous tick before processing new commands
			if (Agents != null)
			{
				foreach (var agentGo in Agents.Values)
				{
					var bridge = agentGo.GetComponent<AgentController>()?.Agent as AgentBridge;
					if (bridge != null)
						bridge.ClearFailedCommands();
				}
			}

			SimulateTick();
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

						TimeToDisplayBanner = BannerDuration;
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
