using AgentSDK;
using GameManager.GameElements;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameManager
{
	/// <summary>
	/// GameManager partial — game loop, tick simulation, and win condition logic.
	///
	/// Runs FixedUpdate at 20Hz (0.05s ticks) to process deferred commands,
	/// advance all units via the shared TickEngine, run movement, and perform
	/// post-tick cleanup. Also contains the Update() state machine that drives
	/// round transitions (INTRO → PLAYING → SHOWING_WINNER → RESTARTING → FINISHED)
	/// and the timeout/elimination win condition checks.
	/// </summary>
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

		/// <summary>Get the shared tick world adapter (lazy-initialized).</summary>
		internal AgentSDK.ITickWorld GetTickWorld()
		{
			if (unityTickWorld == null)
				unityTickWorld = new UnityTickWorld();
			return unityTickWorld;
		}

		/// <summary>Get the shared tick callbacks adapter (lazy-initialized).</summary>
		internal AgentSDK.ITickCallbacks GetTickCallbacks()
		{
			if (unityTickCallbacks == null)
				unityTickCallbacks = new UnityTickCallbacks();
			return unityTickCallbacks;
		}

		/// <summary>
		/// Run one game tick: process commands, advance all units via TickEngine,
		/// then run post-tick updates. Used by tests to simulate ticks without
		/// waiting for Unity's FixedUpdate cycle.
		/// </summary>
		internal void SimulateTick()
		{
			// Drive one tick through the canonical shared phase order (AgentSDK.TickSequence),
			// the SAME sequence the headless SimGame uses. This guarantees Unity and SimGame
			// execute identical per-tick phases in identical order — the source of parity.
			AgentSDK.TickSequence.RunOneTick(this, CurrentTick);
			CurrentTick++;
		}

		// --- ITickParticipant: the four canonical tick phases (see AgentSDK.TickSequence) ---

		/// <summary>Phase 0: record the parity snapshot BEFORE any command processing, so the
		/// exported state at tick N matches SimGame's pre-processing state at tick N.</summary>
		void AgentSDK.ITickParticipant.RecordSnapshot(int tick)
		{
			if (!parityExporterLookedUp)
			{
				parityExporter = FindFirstObjectByType<ParityExporter>();
				parityExporterLookedUp = true;
			}
			parityExporter?.RecordTick();
		}

		/// <summary>Phase 1: process commands queued during the previous tick's agent Update.</summary>
		void AgentSDK.ITickParticipant.ProcessQueuedCommands()
		{
			DeferredCommandQueue.ProcessAll();
		}

		/// <summary>Phase 2: advance all units by one tick (task logic + movement + post-tick).</summary>
		void AgentSDK.ITickParticipant.AdvanceUnits()
		{
			if (unityTickWorld == null) unityTickWorld = new UnityTickWorld();
			if (unityTickCallbacks == null) unityTickCallbacks = new UnityTickCallbacks();

			// Task logic via shared TickEngine
			AgentSDK.TickEngine.AdvanceAllUnits(unityTickWorld, unityTickCallbacks);

			// Movement — advance all units by one tick's worth of distance.
			// Must run per-tick (not per-frame) for deterministic parity with SimGame.
			{
				var moveUnits = unitManager.GetAllUnits();
				var moveKeys = new System.Collections.Generic.List<int>(moveUnits.Keys);
				moveKeys.Sort();
				foreach (int key in moveKeys)
				{
					if (!moveUnits.TryGetValue(key, out var go) || go == null) continue;
					var unit = go.GetComponent<GameElements.Unit>();
					if (unit == null) continue;
					try
					{
						AgentSDK.MovementSystem.Advance(unit, UnityEngine.Time.fixedDeltaTime, unityTickWorld, unityTickCallbacks);
					}
					catch (System.Exception ex)
					{
						var tu = (AgentSDK.ITickUnit)unit;
						UnityEngine.Debug.LogError(
							$"[MovementSystem NRE] unit={unit.UnitNbr} type={unit.UnitType} " +
							$"action={unit.CurrentAction} canMove={tu.CanMove} " +
							$"path={tu.TickPath?.Count} pi={tu.PathIndex} " +
							$"pos=({tu.GridPosition.X},{tu.GridPosition.Y}) " +
							$"pp={tu.PathProgress}" +
							$"\nFull exception:\n{ex}");
					}
				}
			}

			// Post-tick updates (visual/cleanup)
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
		}

		/// <summary>Phase 3: run each agent's decision Update exactly once per tick, in agent-number
		/// order. Previously each agent's IPlanningAgent.Update ran on Unity's per-FRAME
		/// MonoBehaviour.Update (AgentBridge.Update), decoupled from the fixed-timestep tick — which
		/// broke determinism and per-tick parity with SimGame. Now it is an explicit in-tick phase.</summary>
		void AgentSDK.ITickParticipant.RunAgentUpdates()
		{
			if (Agents == null) return;
			var agentKeys = new System.Collections.Generic.List<int>(Agents.Keys);
			agentKeys.Sort();
			foreach (int nbr in agentKeys)
			{
				if (!Agents.TryGetValue(nbr, out var agentGo) || agentGo == null) continue;
				var bridge = agentGo.GetComponent<AgentController>()?.Agent;
				bridge?.TickAgent();
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
