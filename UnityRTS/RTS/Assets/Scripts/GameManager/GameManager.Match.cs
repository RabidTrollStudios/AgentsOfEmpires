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

		IEnumerator DropIntroVersus()
		{
			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = true;

			yield return new WaitForSeconds(1.5f);

			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = false;
		}

		/// <summary>
		/// Called once at the beginning of each match (sequence of rounds)
		/// </summary>
		private void InitializeMatch()
		{
			if (RandomizeAgentsAsOrc)
			{
				OrcDllName = "";
				dllNames = agentLoader.GetDLLNamesFromDir(this.gameObject);

				if (dllNames.Count > 0)
				{
					OrcDllName = dllNames[Random.Range(0, dllNames.Count)];
					isHumanUsingDllNames = false;
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
			AgentWins[Constants.HUMAN_ABBR] = 0;
			AgentWins[Constants.ORC_ABBR] = 0;

			unitManager.UnitPrefabs = new Dictionary<int, Dictionary<UnitType, GameObject>>();
			Agents = new Dictionary<int, GameObject>();

			// Randomly select one player to be instantiated first, for fairness
			if (Random.Range(0, 2) == 0)
			{
				CreateAgent(Constants.HUMAN_ABBR, HumanDllName, Prefabs.HumanPlayerPrefab, unitManager.HumanUnitPrefabs, HumanDebuggerPanel);
				CreateAgent(Constants.ORC_ABBR, OrcDllName, Prefabs.OrcPlayerPrefab, unitManager.OrcUnitPrefabs, OrcDebuggerPanel);
			}
			else
			{
				CreateAgent(Constants.ORC_ABBR, OrcDllName, Prefabs.OrcPlayerPrefab, unitManager.OrcUnitPrefabs, OrcDebuggerPanel);
				CreateAgent(Constants.HUMAN_ABBR, HumanDllName, Prefabs.HumanPlayerPrefab, unitManager.HumanUnitPrefabs, HumanDebuggerPanel);
			}

			HumanCustomDebugText.text = Constants.HUMAN_ABBR + " " + HumanDllName;
			OrcCustomDebugText.text = Constants.ORC_ABBR + " " + OrcDllName;

			Prefabs.GameOverUI.GetComponentInChildren<Text>().text
					= HumanCustomDebugText.text + "\nvs\n" + OrcCustomDebugText.text;

			foreach (GameObject agent in Agents.Values)
			{
				agent.GetComponent<AgentController>().InitializeMatch();
				agent.GetComponent<AgentController>().Agent.OpenCommandLog();
			}

			NbrOfRounds = 0;

			InitializeRound();

			StartCoroutine(DropIntroVersus());
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

			gameState = GameState.PLAYING;
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
		        if (isHumanUsingDllNames)
		        {
			        int agentNbr = (Agents[0].GetComponent<AgentController>().Agent.AgentDLLName == HumanDllName) ? 0 : 1;
			        HumanDllName = dllNames[Random.Range(0, dllNames.Count)];
			        RecreateAgent(Constants.HUMAN_ABBR, HumanDllName, agentNbr, Prefabs.HumanPlayerPrefab, unitManager.HumanUnitPrefabs,
				        HumanDebuggerPanel);
			        Agents[agentNbr].GetComponent<AgentController>().InitializeMatch();
		        }
		        else
		        {
			        int agentNbr = (Agents[0].GetComponent<AgentController>().Agent.AgentDLLName == OrcDllName) ? 0 : 1;
			        OrcDllName = dllNames[Random.Range(0, dllNames.Count)];
			        RecreateAgent(Constants.ORC_ABBR, OrcDllName, agentNbr, Prefabs.OrcPlayerPrefab, unitManager.OrcUnitPrefabs,
				        OrcDebuggerPanel);
			        Agents[agentNbr].GetComponent<AgentController>().InitializeMatch();
		        }

		        HumanCustomDebugText.text = Constants.HUMAN_ABBR + " " + HumanDllName;
		        OrcCustomDebugText.text = Constants.ORC_ABBR + " " + OrcDllName;

		        Prefabs.GameOverUI.GetComponentInChildren<Text>().text
			        = Constants.HUMAN_ABBR + " " + HumanDllName + "\nvs\n" + Constants.ORC_ABBR + " " + OrcDllName;
		        StartCoroutine(DropIntroVersus());
	        }
        }

        private Vector3Int FindMirroredLocation(Vector3Int position, UnitType unitType)
        {
			return new Vector3Int(mapManager.MapSize.x - Constants.UNIT_SIZE[unitType].x - position.x,
								  mapManager.MapSize.y - 2 + Constants.UNIT_SIZE[unitType].y - position.y, 0);
        }

		private void PlaceUnits()
        {
	        // Identify human and orc agent numbers
	        int humanAgentNbr = -1;
	        int orcAgentNbr = -1;
	        foreach (var kvp in Agents)
	        {
		        if (kvp.Value.GetComponent<AgentController>().Agent.AgentName == Constants.HUMAN_ABBR)
			        humanAgentNbr = kvp.Key;
		        else
			        orcAgentNbr = kvp.Key;
	        }

        // Grid positions use top-left corner convention (IsAreaBuildable extends +X and -Y).
        // World center of a unit = (topLeft.x + size.x/2, topLeft.y - size.y/2 + 1)
        //   1x1  workers/troops:          world center = grid + (0.5, 0.5)  =>  valid grid [1, 71] x [1, 40]
        //   3x3  mine/barracks/refinery:  world center = grid + (1.5,-0.5)  =>  valid top-left [1, 69] x [3, 40]
        //   4x4  base:                    world center = grid + (2, -1)      =>  valid top-left [1, 68] x [4, 40]

        // Worker spawn corners - use BASE buildability so there's room to build a base at spawn
        const int workerMinXY = 1;   // world 1.5  = first valid 1x1 position
        const int workerMaxX  = 71;  // world 71.5 = last valid 1x1 position in X
        const int workerMaxY  = 40;  // world 40.5 = last valid 1x1 position in Y

        Vector3Int humanWorkerLoc = GetBuildableLocationNearCorner(workerMinXY, workerMinXY, UnitType.BASE);
        unitManager.PlaceUnit(Agents[humanAgentNbr], humanWorkerLoc, UnitType.WORKER, Color.white);

        Vector3Int orcWorkerLoc = GetBuildableLocationNearCorner(workerMaxX, workerMaxY, UnitType.BASE);
        unitManager.PlaceUnit(Agents[orcAgentNbr], orcWorkerLoc, UnitType.WORKER, Color.white);

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
        // mine1 is near HUM spawn (lower-left); mine2 is its symmetric mirror near ORC spawn (upper-right)
        unitManager.PlaceUnit(Agents[humanAgentNbr], mine1Loc, UnitType.MINE, Color.white);
        unitManager.PlaceUnit(Agents[orcAgentNbr], mine2Loc, UnitType.MINE, Color.white);
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
