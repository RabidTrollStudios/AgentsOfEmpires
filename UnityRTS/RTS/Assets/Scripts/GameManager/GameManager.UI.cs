using System;
using System.Collections.Generic;
using AgentSDK;
using UnityEngine;
using UnityEngine.InputSystem;
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
		/// Maps an InputAction to the debug action it triggers.
		/// Populated by InitializeDebugToggles; consumed by ProcessUserInput.
		/// </summary>
		private (InputAction Action, Action Execute)[] _debugBindings;

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

			if (_input == null) return;

			var gp = _input.Gameplay;
			_debugBindings = new (InputAction, Action)[]
			{
				(gp.DebugToggle1, () => { HasAgentDebugging = !HasAgentDebugging; if (AgentToggle != null) AgentToggle.isOn = HasAgentDebugging; }),
				(gp.DebugToggle2, () => { HasUnitDebugging = !HasUnitDebugging; if (UnitToggle != null) UnitToggle.isOn = HasUnitDebugging; }),
				(gp.DebugToggle3, () => { bool v = !mapManager.InfluenceMap.gameObject.activeSelf; mapManager.InfluenceMap.gameObject.SetActive(v); if (InfluenceToggle != null) InfluenceToggle.isOn = v; }),
				(gp.DebugToggle4, () => { HasMoveTint = !HasMoveTint; if (MoveTintToggle != null) MoveTintToggle.isOn = HasMoveTint; }),
				(gp.DebugToggle5, () => { HasGatherTint = !HasGatherTint; if (GatherTintToggle != null) GatherTintToggle.isOn = HasGatherTint; }),
				(gp.DebugToggle6, () => { HasBuildTint = !HasBuildTint; if (BuildTintToggle != null) BuildTintToggle.isOn = HasBuildTint; }),
				(gp.DebugToggle7, () => { HasAttackTint = !HasAttackTint; if (AttackTintToggle != null) AttackTintToggle.isOn = HasAttackTint; }),
				(gp.DebugToggle8, () => { HasPathTint = !HasPathTint; if (PathTintToggle != null) PathTintToggle.isOn = HasPathTint; }),
				(gp.DebugToggle9, () => { HasTargetLineTint = !HasTargetLineTint; if (TargetLineTintToggle != null) TargetLineTintToggle.isOn = HasTargetLineTint; }),
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

		private void ProcessUserInput()
		{
			if (_debugBindings == null) return;

			foreach (var (action, execute) in _debugBindings)
			{
				if (action.WasPressedThisFrame())
					execute();
			}

			HandleSpeedInput();
		}

		private void HandleSpeedInput()
		{
			if (_input == null) return;

			bool speedUp = _input.Gameplay.SpeedUp.WasPressedThisFrame();
			bool speedDown = _input.Gameplay.SpeedDown.WasPressedThisFrame();

			if (speedUp && Constants.GAME_SPEED < Constants.MAX_GAME_SPEED)
			{
				var oldCreationTimes = new Dictionary<UnitType, float>(Constants.CREATION_TIME);
				Constants.GAME_SPEED++;
				Log("Increasing GameSpeed: " + Constants.GAME_SPEED, gameObject);
				Constants.CalculateGameConstants();
				RescaleBuildProgress(oldCreationTimes);
			}

			if (speedDown && Constants.GAME_SPEED > 1)
			{
				var oldCreationTimes = new Dictionary<UnitType, float>(Constants.CREATION_TIME);
				Constants.GAME_SPEED--;
				Log("Decreasing GameSpeed: " + Constants.GAME_SPEED, gameObject);
				Constants.CalculateGameConstants();
				RescaleBuildProgress(oldCreationTimes);
			}
		}

		/// <summary>
		/// When game speed changes, rescale BuildProgress and taskTime on all units
		/// so progress bar ratios stay the same. Without this, changing speed makes
		/// bars jump because progress / newCreationTime ≠ progress / oldCreationTime.
		/// </summary>
		private void RescaleBuildProgress(Dictionary<UnitType, float> oldCreationTimes)
		{
			foreach (var kvp in unitManager.GetAllUnits())
			{
				var unit = kvp.Value.GetComponent<GameElements.Unit>();
				if (unit == null) continue;

				// Rescale building construction progress
				if (!unit.IsBuilt)
				{
					float oldTime = oldCreationTimes.ContainsKey(unit.UnitType) ? oldCreationTimes[unit.UnitType] : 1f;
					float newTime = Constants.CREATION_TIME.ContainsKey(unit.UnitType) ? Constants.CREATION_TIME[unit.UnitType] : 1f;
					if (oldTime > 0f)
						unit.BuildProgress = unit.BuildProgress / oldTime * newTime;
				}

				// Rescale training timer (taskTime uses taskUnitType's creation time)
				if (unit.CurrentAction == UnitAction.TRAIN)
				{
					var tt = unit.taskUnitType;
					float oldTime = oldCreationTimes.ContainsKey(tt) ? oldCreationTimes[tt] : 1f;
					float newTime = Constants.CREATION_TIME.ContainsKey(tt) ? Constants.CREATION_TIME[tt] : 1f;
					if (oldTime > 0f)
						unit.taskTime = unit.taskTime / oldTime * newTime;
				}
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
			TimeToDisplayBanner = BannerDuration * 0.5f;
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
			if (blueCustomDebugText == null && redCustomDebugText == null) return;

			if (!HasAgentDebugging || Agents == null || Agents.Count == 0)
			{
				if (blueCustomDebugText != null) blueCustomDebugText.text = "";
				if (redCustomDebugText != null) redCustomDebugText.text = "";
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

				if (controller.Agent.AgentName == Constants.BLUE_ABBR && blueCustomDebugText != null)
					blueCustomDebugText.text = display;
				else if (controller.Agent.AgentName == Constants.RED_ABBR && redCustomDebugText != null)
					redCustomDebugText.text = display;
			}
		}

		/// <summary>
		/// Build the debug-toggle panel at runtime and wire each toggle into its
		/// corresponding [SerializeField] private Toggle field on this GameManager.
		/// Must be called BEFORE InitializeDebugToggles(), which adds listeners
		/// and sets initial isOn values on the same fields.
		/// </summary>
		private void BuildDebugTogglePanel()
		{
			var parent = Prefabs != null ? Prefabs.UnitInfoCanvas : null;
			if (parent == null) return;

			var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

			// Container — bottom-right of the canvas
			var panelGo = new GameObject("Debug Toggle Panel");
			var panelRect = panelGo.AddComponent<RectTransform>();
			panelRect.SetParent(parent, false);
			panelRect.anchorMin = new Vector2(1f, 0f);
			panelRect.anchorMax = new Vector2(1f, 0f);
			panelRect.pivot = new Vector2(1f, 0f);
			panelRect.anchoredPosition = new Vector2(-10f, 10f);
			panelRect.sizeDelta = new Vector2(180f, 0f); // height auto-fits

			panelGo.AddComponent<CanvasRenderer>();
			var panelBg = panelGo.AddComponent<Image>();
			panelBg.color = new Color(0f, 0f, 0f, 0.55f);
			panelBg.raycastTarget = true;

			var layout = panelGo.AddComponent<VerticalLayoutGroup>();
			layout.padding = new RectOffset(8, 8, 8, 8);
			layout.spacing = 2f;
			layout.childAlignment = TextAnchor.UpperLeft;
			layout.childControlWidth = true;
			layout.childControlHeight = false;
			layout.childForceExpandWidth = true;
			layout.childForceExpandHeight = false;

			var fitter = panelGo.AddComponent<ContentSizeFitter>();
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			// Build each toggle and assign it to the matching [SerializeField] field.
			// These fields live on this same partial class, so direct assignment works.
			// The (N) suffix matches the DebugToggleN hotkey in the input actions.
			AgentToggle        = CreateDebugToggle(panelGo.transform, "Agent Debug (1)",   font);
			UnitToggle         = CreateDebugToggle(panelGo.transform, "Unit Debug (2)",    font);
			InfluenceToggle    = CreateDebugToggle(panelGo.transform, "Influence Map (3)", font);
			MoveTintToggle     = CreateDebugToggle(panelGo.transform, "Move Tint (4)",     font);
			GatherTintToggle   = CreateDebugToggle(panelGo.transform, "Gather Tint (5)",   font);
			BuildTintToggle    = CreateDebugToggle(panelGo.transform, "Build Tint (6)",    font);
			AttackTintToggle   = CreateDebugToggle(panelGo.transform, "Attack Tint (7)",   font);
			PathTintToggle     = CreateDebugToggle(panelGo.transform, "Path Line (8)",     font);
			TargetLineTintToggle = CreateDebugToggle(panelGo.transform, "Target Line (9)", font);
		}

		/// <summary>
		/// Create a single labelled Toggle row for the debug panel.
		/// Uses Unity's default Toggle layout: a checkbox box on the left and a
		/// label to its right.
		/// </summary>
		private Toggle CreateDebugToggle(Transform parent, string label, Font font)
		{
			var rowGo = new GameObject(label + " Toggle");
			var rowRect = rowGo.AddComponent<RectTransform>();
			rowRect.SetParent(parent, false);
			rowRect.sizeDelta = new Vector2(0f, 22f);

			var toggle = rowGo.AddComponent<Toggle>();

			// Checkbox background (16x16, left side)
			var bgGo = new GameObject("Background");
			var bgRect = bgGo.AddComponent<RectTransform>();
			bgRect.SetParent(rowGo.transform, false);
			bgRect.anchorMin = new Vector2(0f, 0.5f);
			bgRect.anchorMax = new Vector2(0f, 0.5f);
			bgRect.pivot = new Vector2(0f, 0.5f);
			bgRect.sizeDelta = new Vector2(16f, 16f);
			bgRect.anchoredPosition = new Vector2(0f, 0f);
			bgGo.AddComponent<CanvasRenderer>();
			var bgImg = bgGo.AddComponent<Image>();
			bgImg.color = new Color(1f, 1f, 1f, 0.9f);

			// Checkmark (filled when on, hidden when off)
			var checkGo = new GameObject("Checkmark");
			var checkRect = checkGo.AddComponent<RectTransform>();
			checkRect.SetParent(bgGo.transform, false);
			checkRect.anchorMin = new Vector2(0.5f, 0.5f);
			checkRect.anchorMax = new Vector2(0.5f, 0.5f);
			checkRect.pivot = new Vector2(0.5f, 0.5f);
			checkRect.sizeDelta = new Vector2(12f, 12f);
			checkRect.anchoredPosition = Vector2.zero;
			checkGo.AddComponent<CanvasRenderer>();
			var checkImg = checkGo.AddComponent<Image>();
			checkImg.color = new Color(0.15f, 0.85f, 0.15f, 1f);

			// Label text (right of the checkbox)
			var labelGo = new GameObject("Label");
			var labelRect = labelGo.AddComponent<RectTransform>();
			labelRect.SetParent(rowGo.transform, false);
			labelRect.anchorMin = new Vector2(0f, 0f);
			labelRect.anchorMax = new Vector2(1f, 1f);
			labelRect.pivot = new Vector2(0f, 0.5f);
			labelRect.offsetMin = new Vector2(22f, 0f);
			labelRect.offsetMax = new Vector2(0f, 0f);
			labelGo.AddComponent<CanvasRenderer>();
			var labelText = labelGo.AddComponent<Text>();
			labelText.font = font;
			labelText.fontSize = 13;
			labelText.alignment = TextAnchor.MiddleLeft;
			labelText.color = Color.white;
			labelText.text = label;
			labelText.raycastTarget = false;

			toggle.targetGraphic = bgImg;
			toggle.graphic = checkImg;
			toggle.transition = Selectable.Transition.ColorTint;
			toggle.isOn = false;

			return toggle;
		}

		#endregion

	}
}
