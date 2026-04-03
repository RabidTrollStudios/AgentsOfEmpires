using AgentSDK;
using GameManager.GameElements;
using Preloader;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace GameManager
{
	/// <summary>
	/// GameManager partial — match and round initialization.
	///
	/// Handles one-time match setup (agent loading, DLL randomization, scoreboard)
	/// and per-round initialization (map generation, unit spawning, agent reset,
	/// parity exporter setup). Also manages the learning callback between rounds.
	/// </summary>
	public partial class GameManager
	{
		#region Match Initialization

		IEnumerator DropIntroVersus(string versusText)
		{
			var bannerText = Prefabs.GameOverUI.GetComponentInChildren<Text>();

			bannerText.text = "Agents of Empires";
			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = true;
			yield return new WaitForSeconds(BannerDuration);

			bannerText.text = versusText;
			yield return new WaitForSeconds(BannerDuration);

			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = false;
			gameState = GameState.PLAYING;
		}

		/// <summary>
		/// Called once at the beginning of each match (sequence of rounds)
		/// </summary>
		private void InitializeMatch()
		{
			if (RandomizeAgentsAsRed)
			{
				RedDllName = "";
				dllNames = agentLoader.GetDLLNamesFromDir(this.gameObject);

				if (dllNames.Count > 0)
				{
					RedDllName = dllNames[Random.Range(0, dllNames.Count)];
					isBlueUsingDllNames = false;
				}
				else
				{
					Log("ERROR: No DLLs to play against, exiting.", this.gameObject);
					Application.Quit();
				}
			}

			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = false;
			HasUnitDebugging = false;
			HasAgentDebugging = true;

			InstantiateDebugPanels();
			InstantiateScoreboard();

			NbrOfAgents = 0;

			SetupMap();
			mapManager.InfluenceMap.gameObject.SetActive(false);

			AgentWins = new Dictionary<string, int>();
			AgentWins[Constants.BLUE_ABBR] = 0;
			AgentWins[Constants.RED_ABBR] = 0;

			unitManager.UnitPrefabs = new Dictionary<int, Dictionary<UnitType, GameObject>>();
			Agents = new Dictionary<int, GameObject>();

			// Randomly select one player to be instantiated first, for fairness
			if (Random.Range(0, 2) == 0)
			{
				CreateAgent(Constants.BLUE_ABBR, BlueDllName, Prefabs.BluePlayerPrefab, unitManager.BlueUnitPrefabs, blueDebuggerPanel);
				CreateAgent(Constants.RED_ABBR, RedDllName, Prefabs.RedPlayerPrefab, unitManager.RedUnitPrefabs, redDebuggerPanel);
			}
			else
			{
				CreateAgent(Constants.RED_ABBR, RedDllName, Prefabs.RedPlayerPrefab, unitManager.RedUnitPrefabs, redDebuggerPanel);
				CreateAgent(Constants.BLUE_ABBR, BlueDllName, Prefabs.BluePlayerPrefab, unitManager.BlueUnitPrefabs, blueDebuggerPanel);
			}

			if (blueCustomDebugText != null)
				blueCustomDebugText.text = Constants.BLUE_ABBR + " " + BlueDllName;
			if (redCustomDebugText != null)
				redCustomDebugText.text = Constants.RED_ABBR + " " + RedDllName;

			string blueLabel = Constants.BLUE_ABBR + " " + BlueDllName;
			string redLabel = Constants.RED_ABBR + " " + RedDllName;
			string versusText = blueLabel + "\nvs\n" + redLabel;

			foreach (GameObject agent in Agents.Values)
			{
				agent.GetComponent<AgentController>().InitializeMatch();
				agent.GetComponent<AgentController>().Agent.OpenCommandLog();
			}

			// Set opponent DLL names on each agent's analytics
			SetOpponentDLLNames();

			NbrOfRounds = 0;

			InitializeRound();

			StartCoroutine(DropIntroVersus(versusText));
		}

		/// <summary>
		/// Instantiate Blue and Red Agent Debugging Panels from the prefab and
		/// position them at bottom-left and top-right of the screen.
		/// </summary>
		private void InstantiateDebugPanels()
		{
			if (blueDebuggerPanel != null) Destroy(blueDebuggerPanel);
			if (redDebuggerPanel != null) Destroy(redDebuggerPanel);

			var prefab = Prefabs.AgentDebuggingPanelPrefab;
			var parent = Prefabs.UnitInfoCanvas;
			if (prefab == null || parent == null) return;

			// Blue panel: bottom-left
			blueDebuggerPanel = Instantiate(prefab, parent);
			blueDebuggerPanel.name = "Blue Debugging Panel";
			var blueRect = blueDebuggerPanel.GetComponent<RectTransform>();
			blueRect.anchorMin = Vector2.zero;
			blueRect.anchorMax = Vector2.zero;
			blueRect.pivot = new Vector2(0, 0);
			blueRect.anchoredPosition = new Vector2(10f, 10f);

			// Red panel: top-right
			redDebuggerPanel = Instantiate(prefab, parent);
			redDebuggerPanel.name = "Red Debugging Panel";
			var redRect = redDebuggerPanel.GetComponent<RectTransform>();
			redRect.anchorMin = Vector2.one;
			redRect.anchorMax = Vector2.one;
			redRect.pivot = new Vector2(1, 1);
			redRect.anchoredPosition = new Vector2(-10f, -10f);

			// Drop shadows to help panels stand out against the game
			AddDropShadow(blueDebuggerPanel);
			AddDropShadow(redDebuggerPanel);

			// Alternating row backgrounds for readability
			AddAlternatingRowBackgrounds(blueDebuggerPanel);
			AddAlternatingRowBackgrounds(redDebuggerPanel);

			// Extract Custom Debug Text from each panel
			blueCustomDebugText = blueDebuggerPanel.transform
				.Find("Custom Debug Text")?.GetComponent<Text>();
			redCustomDebugText = redDebuggerPanel.transform
				.Find("Custom Debug Text")?.GetComponent<Text>();
		}

		/// <summary>
		/// Build the ribbon scoreboard at the top center of the screen.
		/// Layout (left to right):
		///   [Timer outer] [Timer inner] [Blue score] [Score center] [Red score] [Speed inner] [Speed outer]
		/// Arrow points overlap between adjacent ribbons.
		/// </summary>
		private GameObject scoreboardContainer;

		private void InstantiateScoreboard()
		{
			var parent = Prefabs.UnitInfoCanvas;
			if (parent == null) return;
			if (scoreboardContainer != null) Destroy(scoreboardContainer);

			scoreboardContainer = new GameObject("Scoreboard");
			var containerRect = scoreboardContainer.AddComponent<RectTransform>();
			containerRect.SetParent(parent, false);
			containerRect.anchorMin = new Vector2(0.5f, 1f);
			containerRect.anchorMax = new Vector2(0.5f, 1f);
			containerRect.pivot = new Vector2(0.5f, 1f);
			containerRect.anchoredPosition = Vector2.zero;

			float h = 45f, overlap = 15f;
			float textPad = 10f;
			float ppuMult = 2f;
			int fs = 14;
			var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

			float outerBorder = 41f * 100f / (64f * ppuMult);
			float innerBorder = 38f * 100f / (64f * ppuMult);

			// 8 ribbon slots (left to right):
			//   0:BlueName  1:BlueScore  2:RedName  3:RedScore  4:Speed  5:SpeedVal  6:Timer  7:TimerVal
			string[] texts = {
				BlueDllName, "00", RedDllName, "00",
				"Speed", Constants.GAME_SPEED.ToString(),
				"Timer", "000.00000"
			};
			bool[] isOuter   = { true, false, true, false, true, false, true, false };
			Sprite[] sprites = {
				Prefabs.BlueSmallRibbon,        Prefabs.BlueSmallRibbonInner,
				Prefabs.RedSmallRibbon,         Prefabs.RedSmallRibbonInner,
				Prefabs.BlackSmallRibbon,       Prefabs.BlackSmallRibbonInner,
				Prefabs.BlackSmallRibbon,       Prefabs.BlackSmallRibbonInner,
			};
			string[] goNames = {
				"Blue Name Ribbon", "Blue Score Ribbon", "Red Name Ribbon", "Red Score Ribbon",
				"Speed Ribbon", "Speed Val Ribbon", "Timer Ribbon", "Timer Val Ribbon"
			};

			int n = texts.Length;
			float[] widths = new float[n];
			for (int i = 0; i < n; i++)
			{
				font.RequestCharactersInTexture(texts[i], fs);
				float tw = 0f;
				foreach (char c in texts[i])
					if (font.GetCharacterInfo(c, out var ci, fs))
						tw += ci.advance;
				float border = isOuter[i] ? outerBorder : innerBorder;
				widths[i] = tw + textPad * 2f + border * 2f;
			}

			// Compute X centers left-to-right, then shift so the gap center is at x=0
			float gap = 80f; // visual gap between score ribbons and speed/timer ribbons
			float[] centers = new float[n];
			float x = widths[0] / 2f;
			centers[0] = x;
			for (int i = 1; i < n; i++)
			{
				x += widths[i - 1] / 2f - overlap + widths[i] / 2f;
				if (i == 4) x += gap; // gap between RedScore (3) and Speed (4)
				centers[i] = x;
			}
			// Center on the midpoint of the gap (between right edge of slot 3 and left edge of slot 4)
			float gapCenter = (centers[3] + widths[3] / 2f + centers[4] - widths[4] / 2f) / 2f;
			for (int i = 0; i < n; i++)
				centers[i] -= gapCenter;

			float leftEdge = centers[0] - widths[0] / 2f;
			float rightEdge = centers[n - 1] + widths[n - 1] / 2f;
			containerRect.sizeDelta = new Vector2(rightEdge - leftEdge, h);

			// Draw back layer (outer: even indices) then front layer (inner: odd indices)
			var t = scoreboardContainer.transform;
			for (int i = 0; i < n; i += 2)
				PlaceRibbon(t, goNames[i], sprites[i], widths[i], h, centers[i], 0f, ppuMult);
			for (int i = 1; i < n; i += 2)
				PlaceRibbon(t, goNames[i], sprites[i], widths[i], h, centers[i], 0f, ppuMult);

			// Text
			string[] textNames = {
				"Blue Label", "Blue Score", "Red Label", "Red Score",
				"Speed Label", "Speed Value", "Timer Label", "Timer Value"
			};
			Text[] tc = new Text[n];
			for (int i = 0; i < n; i++)
				tc[i] = CreateCenteredText(t, textNames[i], widths[i], h, centers[i], 0f, fs, font);

			tc[4].text = "Speed";
			tc[6].text = "Timer";

			Prefabs.BlueLabelText = tc[0]; Prefabs.BlueLabelText.text = BlueDllName;
			Prefabs.BlueScoreText = tc[1]; Prefabs.BlueScoreText.text = "0";
			Prefabs.RedLabelText  = tc[2]; Prefabs.RedLabelText.text  = RedDllName;
			Prefabs.RedScoreText  = tc[3]; Prefabs.RedScoreText.text  = "0";
			Prefabs.SpeedText     = tc[5]; Prefabs.SpeedText.text     = Constants.GAME_SPEED.ToString();
			Prefabs.TimerText     = tc[7]; Prefabs.TimerText.text     = "0";
		}

		private void PlaceRibbon(Transform parent, string name, Sprite sprite,
			float width, float height, float x, float y, float ppuMultiplier = 1f)
		{
			var go = new GameObject(name);
			var rect = go.AddComponent<RectTransform>();
			rect.SetParent(parent, false);
			rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(width, height);
			rect.anchoredPosition = new Vector2(x, y);

			go.AddComponent<CanvasRenderer>();
			var img = go.AddComponent<Image>();
			img.sprite = sprite;
			img.type = Image.Type.Sliced;
			img.pixelsPerUnitMultiplier = ppuMultiplier;
			img.raycastTarget = false;
		}

		private Text CreateCenteredText(Transform parent, string name,
			float width, float height, float x, float y, int fontSize, Font font)
		{
			var go = new GameObject(name);
			var rect = go.AddComponent<RectTransform>();
			rect.SetParent(parent, false);
			rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(width, height);
			rect.anchoredPosition = new Vector2(x, y);

			go.AddComponent<CanvasRenderer>();
			var text = go.AddComponent<Text>();
			text.font = font;
			text.fontSize = fontSize;
			text.alignment = TextAnchor.MiddleCenter;
			text.color = Color.white;
			text.raycastTarget = false;
			return text;
		}

		private void AddDropShadow(GameObject panel)
		{
			var panelRect = panel.GetComponent<RectTransform>();

			var shadowGo = new GameObject("Drop Shadow");
			var shadowRect = shadowGo.AddComponent<RectTransform>();
			shadowRect.SetParent(panelRect, false);
			// Stretch to fill the panel, then extend beyond each edge.
			// The Banner_Slots sprite has asymmetric 9-slice borders (L:22,B:18,R:35,T:38)
			// so we offset the shadow to compensate and look equal on all sides.
			shadowRect.anchorMin = Vector2.zero;
			shadowRect.anchorMax = Vector2.one;
			shadowRect.sizeDelta = new Vector2(20f, 20f);
			shadowRect.anchoredPosition = new Vector2(1f, -2f);
			shadowRect.SetAsFirstSibling();

			// Ignore the VerticalLayoutGroup so anchoring works
			var layout = shadowGo.AddComponent<LayoutElement>();
			layout.ignoreLayout = true;

			shadowGo.AddComponent<CanvasRenderer>();
			var img = shadowGo.AddComponent<Image>();
			// Reuse the panel's background sprite so the shadow has matching rounded corners
			var bgTransform = panel.transform.Find("Background");
			var bgImage = bgTransform != null ? bgTransform.GetComponent<Image>() : null;
			if (bgImage != null && bgImage.sprite != null)
			{
				img.sprite = bgImage.sprite;
				img.type = Image.Type.Sliced;
				img.pixelsPerUnitMultiplier = bgImage.pixelsPerUnitMultiplier;
			}
			img.color = new Color(0f, 0f, 0f, 0.5f);
			img.raycastTarget = false;
		}

		private void AddAlternatingRowBackgrounds(GameObject panel)
		{
			var agentData = panel.transform.Find("Agent Data");
			if (agentData == null) return;

			var stripe = new Color(0.85f, 0.85f, 0.85f, 0.5f);
			int index = 0;
			foreach (Transform row in agentData)
			{
				if (index % 2 == 1)
				{
					// Each row already has an Image component (alpha 0) — reuse it
					var img = row.GetComponent<Image>();
					if (img != null)
					{
						img.sprite = null;
						img.type = Image.Type.Simple;
						img.color = stripe;
					}
				}
				index++;
			}
		}

		/// <summary>
		/// Instantiate an agent
		/// </summary>
		private void CreateAgent(string agentName, string agentDLLName,
			GameObject playerPrefab, Dictionary<UnitType, GameObject> playerPrefabs, GameObject debuggerPanel)
		{
			GameObject agentObject = Instantiate(playerPrefab);
			agentObject.GetComponent<AgentController>().InitializeAgent(
				agentLoader.LoadDLL(agentName, agentDLLName, this.gameObject),
				agentName, agentDLLName, NbrOfAgents++, debuggerPanel, agentLoader.PathToDLLs);
			Agents.Add(agentObject.GetComponent<AgentController>().Agent.AgentNbr, agentObject);
			unitManager.UnitPrefabs.Add(agentObject.GetComponent<AgentController>().Agent.AgentNbr, playerPrefabs);
		}

		private void RecreateAgent(string agentName, string agentDLLName, int agentNbr,
			GameObject playerPrefab, Dictionary<UnitType, GameObject> playerPrefabs, GameObject debuggerPanel)
		{
			Agents[agentNbr].GetComponent<AgentController>().Agent.SaveAnalytics();
			Agents[agentNbr].GetComponent<AgentController>().Agent.CloseCommandLog();
			Destroy(Agents[agentNbr].GetComponent<AgentController>().Agent.gameObject);
			Destroy(Agents[agentNbr].GetComponent<AgentController>().gameObject);

			GameObject agentObject = Instantiate(playerPrefab);
			agentObject.GetComponent<AgentController>().InitializeAgent(
				agentLoader.LoadDLL(agentName, agentDLLName, this.gameObject),
				agentName, agentDLLName, agentNbr, debuggerPanel, agentLoader.PathToDLLs);
			Agents[agentNbr] = agentObject;
			unitManager.UnitPrefabs[agentNbr] = playerPrefabs;
			agentObject.GetComponent<AgentController>().Agent.OpenCommandLog();
		}

		#endregion

		#region Round Initialization

		/// <summary>
		/// Called once at the start of each round
		/// </summary>
		private void InitializeRound()
		{
			Log("********************************** InitializeRound **********************************", gameObject);

			gameState = GameState.INTRO;
			TotalGameTime = 0;
			TimeToDisplayBanner = 0f;
			unitManager.ResetForRound();

			PickNextRandomAgent();

			NbrOfRounds++;

			foreach (GameObject agent in Agents.Values)
            {
	            var agentComp = agent.GetComponent<AgentController>().Agent;
	            agentComp.gameObject.SetActive(true);
				agentComp.OpenLogFile();
				agentComp.CmdLog?.StartRound(NbrOfRounds);
				agentComp.Analytics?.BeginRound(NbrOfRounds, agentComp.Gold);
				agent.GetComponent<AgentController>().InitializeRound();
			}

			PlaceUnits();
		}

        private void PickNextRandomAgent()
        {
	        if (NbrOfRounds > 0 && dllNames != null)
	        {
		        if (isBlueUsingDllNames)
		        {
			        int agentNbr = (Agents[0].GetComponent<AgentController>().Agent.AgentDLLName == BlueDllName) ? 0 : 1;
			        BlueDllName = dllNames[Random.Range(0, dllNames.Count)];
			        RecreateAgent(Constants.BLUE_ABBR, BlueDllName, agentNbr, Prefabs.BluePlayerPrefab, unitManager.BlueUnitPrefabs,
				        blueDebuggerPanel);
			        Agents[agentNbr].GetComponent<AgentController>().InitializeMatch();
		        }
		        else
		        {
			        int agentNbr = (Agents[0].GetComponent<AgentController>().Agent.AgentDLLName == RedDllName) ? 0 : 1;
			        RedDllName = dllNames[Random.Range(0, dllNames.Count)];
			        RecreateAgent(Constants.RED_ABBR, RedDllName, agentNbr, Prefabs.RedPlayerPrefab, unitManager.RedUnitPrefabs,
				        redDebuggerPanel);
			        Agents[agentNbr].GetComponent<AgentController>().InitializeMatch();
		        }

		        if (blueCustomDebugText != null)
			        blueCustomDebugText.text = Constants.BLUE_ABBR + " " + BlueDllName;
		        if (redCustomDebugText != null)
			        redCustomDebugText.text = Constants.RED_ABBR + " " + RedDllName;

		        // Rebuild scoreboard so ribbon widths fit new DLL names
		        InstantiateScoreboard();

		        string versusText = Constants.BLUE_ABBR + " " + BlueDllName + "\nvs\n" + Constants.RED_ABBR + " " + RedDllName;
		        StartCoroutine(DropIntroVersus(versusText));
	        }
        }

        private Vector3Int FindMirroredLocation(Vector3Int position, UnitType unitType)
        {
			// Bottom-left anchor mirror across map center
			return new Vector3Int(mapManager.MapSize.x - Constants.UNIT_SIZE[unitType].x - position.x,
								  mapManager.MapSize.y - Constants.UNIT_SIZE[unitType].y - position.y, 0);
        }

        #region Map Setup

        /// <summary>
        /// Set up the map grid based on the selected map mode (HandMade or Procedural).
        /// </summary>
        private void SetupMap()
        {
	        proceduralMapResult = null;

	        if (mapMode == MapMode.Procedural)
	        {
		        SetupProceduralMap();
	        }
	        else
	        {
		        SetupHandMadeMap();
	        }

	        StartCoroutine(CenterCameraOnMapDeferred());
        }

        private IEnumerator CenterCameraOnMapDeferred()
        {
	        // Wait one frame so Camera.main and CameraController are fully initialized
	        yield return null;
	        CenterCameraOnMap();
        }

        /// <summary>
        /// Center the camera on the ground area and configure pan/zoom limits.
        ///
        /// Two sets of bounds:
        /// - Ground bounds: the "Ground 1" tilemap area. Panning keeps the ground inside
        ///   the empty UI area (between the debug panels and below the ribbon).
        /// - Water bounds: the full extent of all tilemaps including water background.
        ///   The camera viewport must never extend past the water so no black shows.
        ///
        /// Initial zoom: the ground fills the empty UI area (max of width or height).
        /// </summary>
        private void CenterCameraOnMap()
        {
	        var cameraController = FindFirstObjectByType<GameElements.CameraController>();
	        if (cameraController == null)
	        {
		        Log("CenterCameraOnMap: No CameraController found in scene", gameObject);
		        return;
	        }

	        var cam = cameraController.GetComponent<Camera>();
	        if (cam == null)
	        {
		        Log("CenterCameraOnMap: CameraController has no Camera component", gameObject);
		        return;
	        }

	        // Enable Y-axis sprite sorting so buildings with lower pivot Y render
	        // in front of units walking behind their top row (higher Y = further back).
	        cam.transparencySortMode = TransparencySortMode.CustomAxis;
	        cam.transparencySortAxis = new Vector3(0, 1, 0);

	        // --- Measure ground bounds (from "Ground 1" tilemap only) ---
	        var grid = Prefabs.Grid;
	        float gMinX = 0, gMinY = 0, gMaxX = mapManager.MapSize.x, gMaxY = mapManager.MapSize.y;
	        // --- Measure water/total bounds (union of all tilemaps) ---
	        float wMinX = gMinX, wMinY = gMinY, wMaxX = gMaxX, wMaxY = gMaxY;

	        if (grid != null)
	        {
		        Vector3 gridPos = grid.transform.position;
		        bool foundGround = false;
		        bool foundAny = false;

		        foreach (var tilemap in grid.GetComponentsInChildren<Tilemap>())
		        {
			        tilemap.CompressBounds();
			        var b = tilemap.cellBounds;
			        float tmMinX = gridPos.x + b.xMin;
			        float tmMinY = gridPos.y + b.yMin;
			        float tmMaxX = gridPos.x + b.xMax;
			        float tmMaxY = gridPos.y + b.yMax;

			        if (tilemap.gameObject.name == "Ground 1")
			        {
				        gMinX = tmMinX; gMinY = tmMinY;
				        gMaxX = tmMaxX; gMaxY = tmMaxY;
				        foundGround = true;
			        }

			        if (!foundAny) { wMinX = tmMinX; wMinY = tmMinY; wMaxX = tmMaxX; wMaxY = tmMaxY; foundAny = true; }
			        else
			        {
				        if (tmMinX < wMinX) wMinX = tmMinX;
				        if (tmMinY < wMinY) wMinY = tmMinY;
				        if (tmMaxX > wMaxX) wMaxX = tmMaxX;
				        if (tmMaxY > wMaxY) wMaxY = tmMaxY;
			        }
		        }

		        if (!foundGround)
		        { gMinX = wMinX; gMinY = wMinY; gMaxX = wMaxX; gMaxY = wMaxY; }
	        }

	        // --- Measure UI insets in screen pixels ---
	        float leftPx = 0f, rightPx = 0f, topPx = 0f;
	        float canvasScale = 1f;
	        if (Prefabs.UnitInfoCanvas != null)
	        {
		        var canvas = Prefabs.UnitInfoCanvas.GetComponentInParent<Canvas>();
		        if (canvas != null) canvasScale = canvas.scaleFactor;
	        }

	        if (blueDebuggerPanel != null)
	        {
		        var rt = blueDebuggerPanel.GetComponent<RectTransform>();
		        leftPx = (rt.rect.width + 20f) * canvasScale;
	        }
	        if (redDebuggerPanel != null)
	        {
		        var rt = redDebuggerPanel.GetComponent<RectTransform>();
		        rightPx = (rt.rect.width + 20f) * canvasScale;
	        }
	        if (scoreboardContainer != null)
	        {
		        var rt = scoreboardContainer.GetComponent<RectTransform>();
		        topPx = (rt.sizeDelta.y + 10f) * canvasScale;
	        }

	        // --- Initial zoom: ground fills the empty UI area ---
	        // The empty area is the viewport minus the UI insets.
	        float emptyFractionX = 1f - (leftPx + rightPx) / Screen.width;
	        float emptyFractionY = 1f - topPx / Screen.height;
	        float groundW = gMaxX - gMinX;
	        float groundH = gMaxY - gMinY;
	        // orthoSize needed to fit ground width in the empty horizontal area
	        float fitW = (groundW / emptyFractionX) / (2f * cam.aspect);
	        // orthoSize needed to fit ground height in the empty vertical area
	        float fitH = (groundH / emptyFractionY) / 2f;
	        cam.orthographicSize = Mathf.Max(fitW, fitH);

	        float centerX = (gMinX + gMaxX) * 0.5f;
	        float centerY = (gMinY + gMaxY) * 0.5f;
	        cam.transform.position = new Vector3(centerX, centerY, cam.transform.position.z);

	        // Pass ground bounds for panning + water bounds for viewport clamping
	        cameraController.SetBounds(gMinX, gMinY, gMaxX, gMaxY,
		        wMinX, wMinY, wMaxX, wMaxY,
		        leftPx, rightPx, topPx, 0f);
        }

        private void SetupHandMadeMap()
        {
	        // selectedMapIndex 0 = scene Grid (default), >0 = instantiate from mapPrefabs
	        if (selectedMapIndex > 0 && mapPrefabs != null && selectedMapIndex <= mapPrefabs.Length)
	        {
		        var prefab = mapPrefabs[selectedMapIndex - 1];
		        if (prefab != null)
		        {
			        // Deactivate the scene Grid and instantiate the prefab
			        if (Prefabs.Grid != null)
				        Prefabs.Grid.SetActive(false);

			        if (runtimeGrid != null)
				        Destroy(runtimeGrid);

			        runtimeGrid = Instantiate(prefab);
			        runtimeGrid.name = "Grid (Hand-Made)";
			        Prefabs.Grid = runtimeGrid;
		        }
	        }

	        mapManager.GenerateGraph(Prefabs.Grid, this.gameObject);
        }

        private void SetupProceduralMap()
        {
	        // Deactivate the scene Grid
	        if (Prefabs.Grid != null)
		        Prefabs.Grid.SetActive(false);

	        if (runtimeGrid != null)
		        Destroy(runtimeGrid);

	        // Generate map data
	        proceduralMapResult = ProceduralMapGenerator.Generate(
		        mapWidth, mapHeight, treeDensity, mapSeed, mapTemplate, mapSymmetry);

	        // Build a Grid with Ground 1 + InfluenceMap (no Trees tilemap)
	        runtimeGrid = BuildProceduralGrid(proceduralMapResult);
	        Prefabs.Grid = runtimeGrid;

	        // GenerateGraph reads Ground 1 to create the walkable grid.
	        // Since ground covers every cell, all cells start walkable.
	        mapManager.GenerateGraph(Prefabs.Grid, this.gameObject);

	        // Mark blocked cells and create tree sprites directly
	        // (skips the Trees tilemap → TreeSprites conversion that GenerateGraph does for hand-made maps)
	        ApplyProceduralTrees(proceduralMapResult, runtimeGrid.transform);
        }

        /// <summary>
        /// Create a Grid GameObject with Ground 1 and InfluenceMap tilemaps.
        /// Ground covers every cell. No Trees tilemap — tree sprites are created separately.
        /// </summary>
        private GameObject BuildProceduralGrid(ProceduralMapResult result)
        {
	        var gridGo = new GameObject("Grid (Procedural)");
	        gridGo.AddComponent<Grid>();

	        // --- Ground 1 (defines playable area — every cell gets a tile) ---
	        var ground1Go = new GameObject("Ground 1");
	        ground1Go.transform.SetParent(gridGo.transform);
	        var ground1 = ground1Go.AddComponent<Tilemap>();
	        var ground1Renderer = ground1Go.AddComponent<TilemapRenderer>();
	        ground1Renderer.sortingOrder = 0;

	        TileBase groundTile = Prefabs.GroundTile;
	        if (groundTile == null)
	        {
		        Log("WARNING: No GroundTile assigned in PrefabLoader. Procedural map will lack ground visuals.", gameObject);
		        groundTile = ScriptableObject.CreateInstance<Tile>();
	        }
	        for (int x = 0; x < result.Width; x++)
		        for (int y = 0; y < result.Height; y++)
			        ground1.SetTile(new Vector3Int(x, y, 0), groundTile);

	        // --- InfluenceMap ---
	        var influenceGo = new GameObject("InfluenceMap");
	        influenceGo.transform.SetParent(gridGo.transform);
	        influenceGo.tag = "InfluenceMap";
	        influenceGo.AddComponent<Tilemap>();
	        var influenceRenderer = influenceGo.AddComponent<TilemapRenderer>();
	        influenceRenderer.sortingOrder = 10;

	        // --- Optional water background ---
	        if (Prefabs.WaterTile != null)
	        {
		        var waterGo = new GameObject("Water Background");
		        waterGo.transform.SetParent(gridGo.transform);
		        var water = waterGo.AddComponent<Tilemap>();
		        var waterRenderer = waterGo.AddComponent<TilemapRenderer>();
		        waterRenderer.sortingOrder = -10;

		        // Extend water far beyond the map so it covers the full viewport
		        // even when panned to the edge with UI panel insets.
		        int border = Mathf.Max(result.Width, result.Height);
		        for (int x = -border; x < result.Width + border; x++)
			        for (int y = -border; y < result.Height + border; y++)
				        if (x < 0 || x >= result.Width || y < 0 || y >= result.Height)
					        water.SetTile(new Vector3Int(x, y, 0), Prefabs.WaterTile);
	        }

	        // --- Water effects (shore/foam) under every edge ground tile ---
	        if (Prefabs.WaterEffectTile != null)
	        {
		        var effectGo = new GameObject("Water Effects");
		        effectGo.transform.SetParent(gridGo.transform);
		        var effects = effectGo.AddComponent<Tilemap>();
		        var effectRenderer = effectGo.AddComponent<TilemapRenderer>();
		        effectRenderer.sortingOrder = -5; // between water background (-10) and ground (0)

		        for (int x = 0; x < result.Width; x++)
			        for (int y = 0; y < result.Height; y++)
			        {
				        // Only place under ground tiles that border at least one non-ground cell
				        if (!ground1.HasTile(new Vector3Int(x, y, 0))) continue;
				        if (!IsEdgeGroundTile(ground1, x, y))          continue;
				        effects.SetTile(new Vector3Int(x, y, 0), Prefabs.WaterEffectTile);
			        }
	        }

	        return gridGo;
        }

        /// <summary>
        /// Mark tree cells as unwalkable/unbuildable in the grid and create individual
        /// SpriteRenderers on the Agents sorting layer (matching hand-made map TreeSprites).
        /// </summary>
        private void ApplyProceduralTrees(ProceduralMapResult result, Transform gridParent)
        {
	        // Mark blocked cells in the MapManager grid
	        foreach (var pos in result.BlockedCells)
	        {
		        if (Utility.IsValidGridLocation(pos, mapManager.MapSize))
		        {
			        mapManager.GridCells[pos.x, pos.y].SetBuildable(false);
			        mapManager.GridCells[pos.x, pos.y].SetWalkable(false);
			        mapManager.Grid.SetCellBlocked(pos.x, pos.y);
		        }
	        }

	        // Create tree sprites grouped under a TreeSprites parent
	        var treeParent = new GameObject("TreeSprites");
	        treeParent.transform.SetParent(gridParent);

	        Sprite[] treeSprites = GetTreeSpritesFromTiles();
	        if (treeSprites.Length == 0) return;

	        foreach (var grove in result.Groves)
	        {
		        int spriteIdx = Mathf.Clamp(grove.TreeType - 1, 0, treeSprites.Length - 1);
		        Sprite sprite = treeSprites[spriteIdx];
		        if (sprite == null) continue;

		        foreach (var cell in grove.Cells)
		        {
			        var treeGo = new GameObject($"Tree_{cell.x}_{cell.y}");
			        treeGo.transform.SetParent(treeParent.transform);
			        treeGo.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

			        var sr = treeGo.AddComponent<SpriteRenderer>();
			        sr.sprite = sprite;
			        sr.sortingLayerName = "Agents";
			        sr.sortingOrder = 0;
			        sr.spriteSortPoint = SpriteSortPoint.Pivot;
		        }
	        }
        }

        /// <summary>
        /// Extract the rendered sprite from each TreeTile in PrefabLoader by painting
        /// onto a temporary tilemap and reading back via GetSprite(). Works with any
        /// tile type (Tile, RuleTile, AnimatedTile, etc.).
        /// </summary>
        private Sprite[] GetTreeSpritesFromTiles()
        {
	        TileBase[] tiles = Prefabs.TreeTiles;
	        if (tiles == null || tiles.Length == 0) return new Sprite[0];

	        var tempGo = new GameObject("_TempTileExtractor");
	        tempGo.AddComponent<Grid>();
	        var mapGo = new GameObject("_TempTilemap");
	        mapGo.transform.SetParent(tempGo.transform);
	        var tempMap = mapGo.AddComponent<Tilemap>();

	        var sprites = new Sprite[tiles.Length];
	        for (int i = 0; i < tiles.Length; i++)
	        {
		        if (tiles[i] == null) continue;
		        var pos = new Vector3Int(i, 0, 0);
		        tempMap.SetTile(pos, tiles[i]);
		        sprites[i] = tempMap.GetSprite(pos);
	        }

	        DestroyImmediate(tempGo);
	        return sprites;
        }

        /// <summary>
        /// A ground tile is an "edge" tile if any of its 8 neighbors has no ground tile
        /// (out of bounds or empty cell). These are the tiles that border water/void.
        /// </summary>
        private static bool IsEdgeGroundTile(Tilemap ground, int x, int y)
        {
	        for (int dx = -1; dx <= 1; dx++)
		        for (int dy = -1; dy <= 1; dy++)
		        {
			        if (dx == 0 && dy == 0) continue;
			        if (!ground.HasTile(new Vector3Int(x + dx, y + dy, 0)))
				        return true;
		        }
	        return false;
        }

        #endregion

		private void PlaceUnits()
        {
	        // Identify blue and red agent numbers
	        int blueAgentNbr = -1;
	        int redAgentNbr = -1;
	        foreach (var kvp in Agents)
	        {
		        if (kvp.Value.GetComponent<AgentController>().Agent.AgentName == Constants.BLUE_ABBR)
			        blueAgentNbr = kvp.Key;
		        else
			        redAgentNbr = kvp.Key;
	        }

	        if (proceduralMapResult != null)
	        {
		        PlaceUnitsFromProceduralMap(blueAgentNbr, redAgentNbr);
	        }
	        else
	        {
		        PlaceUnitsOnHandMadeMap(blueAgentNbr, redAgentNbr);
	        }
        }

        /// <summary>Place pawns and mines using pre-computed procedural positions.</summary>
        private void PlaceUnitsFromProceduralMap(int blueAgentNbr, int redAgentNbr)
        {
	        var r = proceduralMapResult;

	        // Spawn 0 = blue (bottom-left), Spawn 1 = red (top-right)
	        unitManager.PlaceUnit(Agents[blueAgentNbr], r.SpawnPositions[0], UnitType.PAWN, Color.white);
	        unitManager.PlaceUnit(Agents[redAgentNbr], r.SpawnPositions[1], UnitType.PAWN, Color.white);

	        // Mines are neutral (not owned by either agent)
	        unitManager.PlaceNeutralUnit(r.MinePositions[0], UnitType.MINE, Color.white);
	        unitManager.PlaceNeutralUnit(r.MinePositions[1], UnitType.MINE, Color.white);
        }

        /// <summary>Place pawns and mines using the original hand-made map placement logic.</summary>
        private void PlaceUnitsOnHandMadeMap(int blueAgentNbr, int redAgentNbr)
        {
        // Grid positions use top-left corner convention (IsAreaBuildable extends +X and -Y).
        // World center of a unit = (topLeft.x + size.x/2, topLeft.y - size.y/2 + 1)
        //   1x1  pawns/troops:          world center = grid + (0.5, 0.5)  =>  valid grid [1, 71] x [1, 40]
        //   3x3  mine/barracks/archery:   world center = grid + (1.5,-0.5)  =>  valid top-left [1, 69] x [3, 40]
        //   4x4  base:                    world center = grid + (2, -1)      =>  valid top-left [1, 68] x [4, 40]

        // Pawn spawn corners - use BASE buildability so there's room to build a base at spawn
        const int pawnMinXY = 1;   // world 1.5  = first valid 1x1 position
        const int pawnMaxX  = 71;  // world 71.5 = last valid 1x1 position in X
        const int pawnMaxY  = 40;  // world 40.5 = last valid 1x1 position in Y

        Vector3Int bluePawnLoc = GetBuildableLocationNearCorner(pawnMinXY, pawnMinXY, UnitType.BASE);
        unitManager.PlaceUnit(Agents[blueAgentNbr], bluePawnLoc, UnitType.PAWN, Color.white);

        Vector3Int redPawnLoc = GetBuildableLocationNearCorner(pawnMaxX, pawnMaxY, UnitType.BASE);
        unitManager.PlaceUnit(Agents[redAgentNbr], redPawnLoc, UnitType.PAWN, Color.white);

        // Mine placement - top-left of 3x3 footprint. World center = (topLeft.x + 1.5, topLeft.y - 0.5)
        //   Valid world centers: (2.5, 2.5) to (70.5, 39.5)
        //   Valid top-left range: X in [1, 69], Y in [3, 40]
        const int mineMinX = 1;   // world center x = 1 + 1.5 = 2.5
        const int mineMaxX = 69;  // world center x = 69 + 1.5 = 70.5
        const int mineMinY = 3;   // world center y = 3 - 0.5 = 2.5
        const int mineMaxY = 40;  // world center y = 40 - 0.5 = 39.5

        int halfMineX = (mineMinX + mineMaxX) / 2;  // = 35
        int halfMineY = (mineMinY + mineMaxY) / 2;  // = 21

        // Spawn mine1 in the bottom-left quadrant of valid mine top-lefts; mirror mine2 to upper-right
        Vector3Int mine1Loc = Vector3Int.zero;
        Vector3Int mine2Loc = Vector3Int.zero;
        int mineAttempts = 0;
        do
        {
	        mine1Loc = new Vector3Int(
		        Random.Range(mineMinX, halfMineX + 1),   // [1, 35]
		        Random.Range(mineMinY, halfMineY + 1), 0);  // [3, 21]
	        // Symmetric mirror: mineMin + mineMax - coord
	        mine2Loc = new Vector3Int(
		        mineMinX + mineMaxX - mine1Loc.x,   // 1 + 69 - x = 70 - x  =>  [35, 69]
		        mineMinY + mineMaxY - mine1Loc.y, 0);  // 3 + 40 - y = 43 - y  =>  [22, 40]
	        mineAttempts++;
        } while ((!mapManager.IsBoundedAreaBuildable(UnitType.MINE, mine1Loc)
	        || !mapManager.IsBoundedAreaBuildable(UnitType.MINE, mine2Loc)
	        || IsInCorner(mine1Loc, 5)
	        || IsInCorner(mine2Loc, 5)
	        || (Mathf.Abs(mine1Loc.x - mine2Loc.x) <= 2 && Mathf.Abs(mine1Loc.y - mine2Loc.y) <= 2)) && mineAttempts < 1000);
        // Mines are neutral (not owned by either agent)
        unitManager.PlaceNeutralUnit(mine1Loc, UnitType.MINE, Color.white);
        unitManager.PlaceNeutralUnit(mine2Loc, UnitType.MINE, Color.white);
        }

        /// <summary>
        /// Search outward from a corner position for the first buildable location.
        /// </summary>
        private Vector3Int GetBuildableLocationNearCorner(int cornerX, int cornerY, UnitType sizeType)
        {
	        int maxRadius = Mathf.Max(mapManager.MapSize.x, mapManager.MapSize.y);
	        for (int radius = 0; radius < maxRadius; radius++)
	        {
		        for (int dx = -radius; dx <= radius; dx++)
		        {
			        for (int dy = -radius; dy <= radius; dy++)
			        {
				        // Only check the perimeter of each ring
				        if (radius > 0 && Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
					        continue;

				        Vector3Int pos = new Vector3Int(cornerX + dx, cornerY + dy, 0);
				        if (Utility.IsValidGridLocation(pos, mapManager.MapSize)
				            && mapManager.IsAreaBuildable(sizeType, pos))
					        return pos;
			        }
		        }
	        }
	        // Fallback to random
	        return mapManager.GetRandomBuildableLocation(sizeType);
        }

        /// <summary>
        /// Find a random buildable location that is not in the upper-right or lower-left corners.
        /// </summary>
        private Vector3Int GetRandomBuildableLocationExcludingCorners(UnitType unitType)
        {
	        const int CORNER_MARGIN = 5;
	        Vector3Int location;
	        int attempts = 0;
	        do
	        {
		        location = mapManager.GetRandomBuildableLocation(unitType);
		        attempts++;
	        } while (IsInCorner(location, CORNER_MARGIN) && attempts < 1000);
	        return location;
        }

        private bool IsInCorner(Vector3Int pos, int margin)
        {
	        bool nearLeft = pos.x < margin;
	        bool nearRight = pos.x >= mapManager.MapSize.x - margin;
	        bool nearBottom = pos.y < margin;
	        bool nearTop = pos.y >= mapManager.MapSize.y - margin;
	        return (nearLeft && nearBottom) || (nearRight && nearTop);
        }

        /// <summary>
        /// Called after each round so agents can learn from the outcome
        /// </summary>
        private void Learn()
        {
			string winnerName = roundWinner != null
				? roundWinner.GetComponent<AgentController>().Agent.AgentName + " " + roundWinner.GetComponent<AgentController>().Agent.AgentDLLName
				: "unknown";

			if (EnableLearning)
			{
				foreach (GameObject agent in Agents.Values)
				{
		            agent.GetComponent<AgentController>().Learn();
					agent.GetComponent<AgentController>().Agent.EndLogLine();
				}
			}

			// Finalize analytics for each agent's round
			FinalizeRoundAnalytics();

			foreach (GameObject agent in Agents.Values)
			{
				agent.GetComponent<AgentController>().Agent.CmdLog?.EndRound(winnerName + " wins");
			}
		}

		/// <summary>
		/// Captures end-of-round state into each agent's MatchAnalytics.
		/// Called from Learn() while units are still alive.
		/// </summary>
		private void FinalizeRoundAnalytics()
		{
			if (Agents == null || unitManager == null) return;

			var allUnits = unitManager.GetAllUnits();

			foreach (GameObject agentGo in Agents.Values)
			{
				var agentComp = agentGo.GetComponent<AgentController>().Agent;
				var analytics = agentComp.Analytics;
				if (analytics?.CurrentRound == null) continue;

				int agentNbr = agentComp.AgentNbr;

				// Compute unit counts and score for this agent
				var unitCounts = new Dictionary<UnitType, int>();
				int score = 0;
				foreach (UnitType ut in Enum.GetValues(typeof(UnitType)))
				{
					int count = 0;
					foreach (var kvp in allUnits)
					{
						var unit = kvp.Value.GetComponent<GameElements.Unit>();
						if (unit.OwnerAgentNbr == agentNbr
							&& unit.UnitType == ut)
							count++;
					}
					unitCounts[ut] = count;
					score += count * Constants.UNIT_VALUE[ut];
				}

				bool won = roundWinner != null
					&& roundWinner.GetComponent<AgentController>().Agent.AgentNbr == agentNbr;

				analytics.TotalRounds++;
				analytics.EndRound(
					won ? "WIN" : "LOSS",
					TotalGameTime,
					agentComp.Gold,
					unitCounts,
					score);
			}
		}

		/// <summary>
		/// Sets the OpponentDLLName on each agent's analytics by finding the other agent.
		/// </summary>
		private void SetOpponentDLLNames()
		{
			var agentList = new List<Agent>();
			foreach (GameObject agentGo in Agents.Values)
				agentList.Add(agentGo.GetComponent<AgentController>().Agent);

			if (agentList.Count == 2)
			{
				if (agentList[0].Analytics != null)
					agentList[0].Analytics.OpponentDLLName = agentList[1].AgentDLLName;
				if (agentList[1].Analytics != null)
					agentList[1].Analytics.OpponentDLLName = agentList[0].AgentDLLName;
			}
		}

        #endregion

	}
}
