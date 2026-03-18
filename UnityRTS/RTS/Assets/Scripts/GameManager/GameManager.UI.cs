using System;
using AgentSDK;
using UnityEngine;
using UnityEngine.UI;

namespace GameManager
{
	public partial class GameManager
	{
		#region UI and Input

		/// <summary>
		/// One-time game-over UI setup (banner and font are configured in the editor).
		/// </summary>
		private void SetupGameOverBanner()
		{
		}

		private void UpdateTimerUI()
		{
			TotalGameTime += Time.deltaTime * Constants.GAME_SPEED;
			Prefabs.TimerText.text = TotalGameTime.ToString("0.00000");
			Prefabs.SpeedText.text = Constants.GAME_SPEED.ToString();
		}

		/// <summary>
		/// Maps a pair of keycodes (alpha row + numpad) to the debug action they trigger.
		/// Populated by InitializeDebugToggles; consumed by ProcessUserInput.
		/// </summary>
		private (KeyCode Main, KeyCode Numpad, Action Execute)[] _debugBindings;

		private void InitializeDebugToggles()
		{
			HasAgentDebugging = true;
			HasUnitDebugging = true;
			HasMoveTint = false;
			HasGatherTint = false;
			HasAttackTint = false;
			HasPathTint = true;
			HasBuildTint = false;
			HasTargetLineTint = true;

			if (AgentToggle != null)
			{
				AgentToggle.onValueChanged.AddListener(OnAgentToggleChanged);
				AgentToggle.isOn = true;
			}
			if (UnitToggle != null)
			{
				UnitToggle.onValueChanged.AddListener(OnUnitToggleChanged);
				UnitToggle.isOn = false;
			}
			if (InfluenceToggle != null)
			{
				InfluenceToggle.onValueChanged.AddListener(OnInfluenceToggleChanged);
				InfluenceToggle.isOn = false;
			}
			if (MoveTintToggle != null)
			{
				MoveTintToggle.onValueChanged.AddListener(OnMoveTintToggleChanged);
				MoveTintToggle.isOn = false;
			}
			if (GatherTintToggle != null)
			{
				GatherTintToggle.onValueChanged.AddListener(OnGatherTintToggleChanged);
				GatherTintToggle.isOn = false;
			}
			if (AttackTintToggle != null)
			{
				AttackTintToggle.onValueChanged.AddListener(OnAttackTintToggleChanged);
				AttackTintToggle.isOn = false;
			}
			if (PathTintToggle != null)
			{
				PathTintToggle.onValueChanged.AddListener(OnPathTintToggleChanged);
				PathTintToggle.isOn = true;
			}
			if (BuildTintToggle != null)
			{
				BuildTintToggle.onValueChanged.AddListener(OnBuildTintToggleChanged);
				BuildTintToggle.isOn = false;
			}
			if (TargetLineTintToggle != null)
			{
				TargetLineTintToggle.onValueChanged.AddListener(OnTargetLineTintToggleChanged);
				TargetLineTintToggle.isOn = true;
			}
			_debugBindings = new (KeyCode, KeyCode, Action)[]
			{
				(KeyCode.Alpha1, KeyCode.Keypad1, () => { HasAgentDebugging = !HasAgentDebugging; if (AgentToggle != null) AgentToggle.isOn = HasAgentDebugging; }),
				(KeyCode.Alpha2, KeyCode.Keypad2, () => { HasUnitDebugging = !HasUnitDebugging; if (UnitToggle != null) UnitToggle.isOn = HasUnitDebugging; }),
				(KeyCode.Alpha3, KeyCode.Keypad3, () => { bool v = !mapManager.InfluenceMap.gameObject.activeSelf; mapManager.InfluenceMap.gameObject.SetActive(v); if (InfluenceToggle != null) InfluenceToggle.isOn = v; }),
				(KeyCode.Alpha4, KeyCode.Keypad4, () => { HasMoveTint = !HasMoveTint; if (MoveTintToggle != null) MoveTintToggle.isOn = HasMoveTint; }),
				(KeyCode.Alpha5, KeyCode.Keypad5, () => { HasGatherTint = !HasGatherTint; if (GatherTintToggle != null) GatherTintToggle.isOn = HasGatherTint; }),
				(KeyCode.Alpha6, KeyCode.Keypad6, () => { HasBuildTint = !HasBuildTint; if (BuildTintToggle != null) BuildTintToggle.isOn = HasBuildTint; }),
				(KeyCode.Alpha7, KeyCode.Keypad7, () => { HasAttackTint = !HasAttackTint; if (AttackTintToggle != null) AttackTintToggle.isOn = HasAttackTint; }),
				(KeyCode.Alpha8, KeyCode.Keypad8, () => { HasPathTint = !HasPathTint; if (PathTintToggle != null) PathTintToggle.isOn = HasPathTint; }),
				(KeyCode.Alpha9, KeyCode.Keypad9, () => { HasTargetLineTint = !HasTargetLineTint; if (TargetLineTintToggle != null) TargetLineTintToggle.isOn = HasTargetLineTint; }),
			};
		}

		public void OnAgentToggleChanged(bool val) { HasAgentDebugging = val; }
		public void OnUnitToggleChanged(bool val) { HasUnitDebugging = val; }
		public void OnInfluenceToggleChanged(bool val) { mapManager.InfluenceMap.gameObject.SetActive(val); }
		public void OnMoveTintToggleChanged(bool val) { HasMoveTint = val; }
		public void OnGatherTintToggleChanged(bool val) { HasGatherTint = val; }
		public void OnAttackTintToggleChanged(bool val) { HasAttackTint = val; }
		public void OnPathTintToggleChanged(bool val) { HasPathTint = val; }
		public void OnBuildTintToggleChanged(bool val) { HasBuildTint = val; }
		public void OnTargetLineTintToggleChanged(bool val) { HasTargetLineTint = val; }

		private static bool IsKeyDown(KeyCode main, KeyCode numpad)
			=> Input.GetKeyDown(main) || Input.GetKeyDown(numpad);

		private void ProcessUserInput()
		{
			if (_debugBindings == null) return;

			foreach (var (main, numpad, execute) in _debugBindings)
				if (IsKeyDown(main, numpad))
					execute();

			HandleSpeedInput();
		}

		private void HandleSpeedInput()
		{
			bool speedUp = Input.GetKeyDown(KeyCode.Equals)
			            || Input.GetKeyDown(KeyCode.Plus)
			            || Input.GetKeyDown(KeyCode.KeypadPlus);

			bool speedDown = Input.GetKeyDown(KeyCode.Minus)
			              || Input.GetKeyDown(KeyCode.KeypadMinus);

			if (speedUp && Constants.GAME_SPEED < Constants.MAX_GAME_SPEED)
			{
				Constants.GAME_SPEED++;
				Log("Increasing GameSpeed: " + Constants.GAME_SPEED, gameObject);
				Constants.CalculateGameConstants();
			}

			if (speedDown && Constants.GAME_SPEED > 1)
			{
				Constants.GAME_SPEED--;
				Log("Decreasing GameSpeed: " + Constants.GAME_SPEED, gameObject);
				Constants.CalculateGameConstants();
			}
		}

		private void SetAllAgentsInactive()
		{
			foreach (GameObject agent in Agents.Values)
			{
				agent.GetComponent<AgentController>().Agent.gameObject.SetActive(false);
				agent.GetComponent<AgentController>().Agent.CloseLogFile();
			}
		}

		private void DeclareRoundWinner(GameObject winner)
		{
			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = true;

			AgentWins[winner.GetComponent<AgentController>().Agent.AgentName] += 1;
			winner.GetComponent<AgentController>().Agent.AgentNbrWins += 1;

			Prefabs.GameOverUI.GetComponentInChildren<Text>().text
				= winner.GetComponent<AgentController>().Agent.AgentName + " "
				  + winner.GetComponent<AgentController>().Agent.AgentDLLName + "\nWins Round";
			gameState = GameState.SHOWING_WINNER;

			if (winner.GetComponent<AgentController>().Agent.AgentName == Constants.BLUE_ABBR)
			{
				Prefabs.BlueScoreText.text = AgentWins[Constants.BLUE_ABBR].ToString();
			}
			else
			{
				Prefabs.RedScoreText.text = AgentWins[Constants.RED_ABBR].ToString();
			}
			TimeToDisplayBanner = 1.5f;
		}

        private void DisplaySingleAgentResults()
        {
	        string winnerAbbr = AgentWins[Constants.BLUE_ABBR] >= AgentWins[Constants.RED_ABBR]
				? Constants.BLUE_ABBR
				: Constants.RED_ABBR;

	        GameObject winner = null;
	        if (Agents[0].GetComponent<AgentController>().Agent.AgentName == winnerAbbr)
	        {
		        winner = Agents[0];
	        }
	        else
	        {
		        winner = Agents[1];
	        }

	        Prefabs.GameOverUI.GetComponentInChildren<Text>().text
		        = winner.GetComponent<AgentController>().Agent.AgentName
		          + " "+ winner.GetComponent<AgentController>().Agent.AgentDLLName
		          + "\nwon " + AgentWins[winnerAbbr] + " of " + TotalNbrOfRounds + "!";
        }

        private void DisplayMultiAgentResults()
        {
	        GameObject singleAgent = null;
	        int nbrWins = 0;

		    if (Agents[0].GetComponent<AgentController>().Agent.AgentName == Constants.RED_ABBR)
		    {
			    singleAgent = Agents[1];
			    nbrWins = AgentWins[Constants.BLUE_ABBR];
		    }
		    else
		    {
			    singleAgent = Agents[0];
			    nbrWins = AgentWins[Constants.BLUE_ABBR];
		    }

	        Prefabs.GameOverUI.GetComponentInChildren<Text>().text
		        = singleAgent.GetComponent<AgentController>().Agent.AgentName + " "
	                  + singleAgent.GetComponent<AgentController>()
	                      .Agent.AgentDLLName + " won\n" + nbrWins +
	                  " of " + TotalNbrOfRounds + " rounds!";
        }

		private void UpdateCustomDebugUI()
		{
			if (BlueCustomDebugText == null && RedCustomDebugText == null) return;

			if (!HasAgentDebugging || Agents == null || Agents.Count == 0)
			{
				if (BlueCustomDebugText != null) BlueCustomDebugText.text = "";
				if (RedCustomDebugText != null) RedCustomDebugText.text = "";
				return;
			}

			foreach (var kvp in Agents)
			{
				var controller = kvp.Value.GetComponent<AgentController>();
				if (controller?.Agent == null) continue;
				var bridge = controller.Agent as AgentBridge;
				if (bridge == null) continue;

				string debugText = bridge.PlanningAgentDebugText;
				string display = string.IsNullOrEmpty(debugText)
					? ""
					: controller.Agent.AgentDLLName + "\n" + debugText;

				if (controller.Agent.AgentName == Constants.BLUE_ABBR && BlueCustomDebugText != null)
					BlueCustomDebugText.text = display;
				else if (controller.Agent.AgentName == Constants.RED_ABBR && RedCustomDebugText != null)
					RedCustomDebugText.text = display;
			}
		}

		#endregion

	}
}
