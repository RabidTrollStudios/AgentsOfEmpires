using AgentSDK;
using GameManager.GameElements;
using Preloader;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace GameManager
{
	public partial class GameManager
	{
		#region Match Initialization

		IEnumerator DropIntroVersus(string versusText)
		{
			var bannerText = Prefabs.GameOverUI.GetComponentInChildren<Text>();

			// Show game title for 3 seconds
			bannerText.text = "Agents of Empires";
			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = true;
			yield return new WaitForSeconds(3f);

			// Show battle matchup for 3 seconds
			bannerText.text = versusText;
			yield return new WaitForSeconds(3f);

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

			NbrOfAgents = 0;

			mapManager.GenerateGraph(Prefabs.Grid, this.gameObject);
			mapManager.InfluenceMap.gameObject.SetActive(false);

			AgentWins = new Dictionary<string, int>();
			AgentWins[Constants.BLUE_ABBR] = 0;
			AgentWins[Constants.RED_ABBR] = 0;

			unitManager.UnitPrefabs = new Dictionary<int, Dictionary<UnitType, GameObject>>();
			Agents = new Dictionary<int, GameObject>();

			// Randomly select one player to be instantiated first, for fairness
			if (Random.Range(0, 2) == 0)
			{
				CreateAgent(Constants.BLUE_ABBR, BlueDllName, Prefabs.BluePlayerPrefab, unitManager.BlueUnitPrefabs, BlueDebuggerPanel);
				CreateAgent(Constants.RED_ABBR, RedDllName, Prefabs.RedPlayerPrefab, unitManager.RedUnitPrefabs, RedDebuggerPanel);
			}
			else
			{
				CreateAgent(Constants.RED_ABBR, RedDllName, Prefabs.RedPlayerPrefab, unitManager.RedUnitPrefabs, RedDebuggerPanel);
				CreateAgent(Constants.BLUE_ABBR, BlueDllName, Prefabs.BluePlayerPrefab, unitManager.BlueUnitPrefabs, BlueDebuggerPanel);
			}

			BlueCustomDebugText.text = Constants.BLUE_ABBR + " " + BlueDllName;
			RedCustomDebugText.text = Constants.RED_ABBR + " " + RedDllName;

			if (Prefabs.BlueLabelText != null)
				Prefabs.BlueLabelText.text = BlueDllName;
			if (Prefabs.RedLabelText != null)
				Prefabs.RedLabelText.text = RedDllName;

			string versusText = BlueCustomDebugText.text + "\nvs\n" + RedCustomDebugText.text;

			foreach (GameObject agent in Agents.Values)
			{
				agent.GetComponent<AgentController>().InitializeMatch();
				agent.GetComponent<AgentController>().Agent.OpenCommandLog();
			}

			NbrOfRounds = 0;

			InitializeRound();

			StartCoroutine(DropIntroVersus(versusText));
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
	            agent.GetComponent<AgentController>().Agent.gameObject.SetActive(true);
				agent.GetComponent<AgentController>().Agent.OpenLogFile();
				agent.GetComponent<AgentController>().Agent.CmdLog?.StartRound(NbrOfRounds);
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
				        BlueDebuggerPanel);
			        Agents[agentNbr].GetComponent<AgentController>().InitializeMatch();
		        }
		        else
		        {
			        int agentNbr = (Agents[0].GetComponent<AgentController>().Agent.AgentDLLName == RedDllName) ? 0 : 1;
			        RedDllName = dllNames[Random.Range(0, dllNames.Count)];
			        RecreateAgent(Constants.RED_ABBR, RedDllName, agentNbr, Prefabs.RedPlayerPrefab, unitManager.RedUnitPrefabs,
				        RedDebuggerPanel);
			        Agents[agentNbr].GetComponent<AgentController>().InitializeMatch();
		        }

		        BlueCustomDebugText.text = Constants.BLUE_ABBR + " " + BlueDllName;
		        RedCustomDebugText.text = Constants.RED_ABBR + " " + RedDllName;

		        if (Prefabs.BlueLabelText != null)
			        Prefabs.BlueLabelText.text = BlueDllName;
		        if (Prefabs.RedLabelText != null)
			        Prefabs.RedLabelText.text = RedDllName;

		        string versusText = Constants.BLUE_ABBR + " " + BlueDllName + "\nvs\n" + Constants.RED_ABBR + " " + RedDllName;
		        StartCoroutine(DropIntroVersus(versusText));
	        }
        }

        private Vector3Int FindMirroredLocation(Vector3Int position, UnitType unitType)
        {
			return new Vector3Int(mapManager.MapSize.x - Constants.UNIT_SIZE[unitType].x - position.x,
								  mapManager.MapSize.y - 2 + Constants.UNIT_SIZE[unitType].y - position.y, 0);
        }

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
        // mine1 is near BLU spawn (lower-left); mine2 is its symmetric mirror near RED spawn (upper-right)
        unitManager.PlaceUnit(Agents[blueAgentNbr], mine1Loc, UnitType.MINE, Color.white);
        unitManager.PlaceUnit(Agents[redAgentNbr], mine2Loc, UnitType.MINE, Color.white);
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

			foreach (GameObject agent in Agents.Values)
			{
				agent.GetComponent<AgentController>().Agent.CmdLog?.EndRound(winnerName + " wins");
			}
		}

        #endregion

	}
}
