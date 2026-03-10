// -territoryMap: generates influence based on proximety to own structures and mines using linear falloff, bases have higher intensity
// -enemyMap: generates influence based on proximety to enemy structures using linear falloff, bases have higher intensity
// -Functions for ideal build positions and unit positions subtract territoryMap influence from enemyMap influence for decision making
// -Pawns use influence to determine where to build bases
// -Warriors and archers use influence to prioritize which enemy unit to attack first

using System.Collections.Generic;
using System.Linq;
using GameManager.EnumTypes;
using GameManager.GameElements;
using UnityEngine;

namespace GameManager
{
	// Planning Agent is the over-head planner that decides where
	// individual units go and what tasks they perform.  Low-level
	// AI is handled by other classes (like pathfinding).
	public class PlanningAgent : Agent
	{
		private const int MAX_NBR_ARCHERS = 20;
		private const float MAX_ARCHER_MULTIPLIER = 2.0f;
		private const int MAX_NBR_WARRIORS = 10;
		private const float MAX_WARRIOR_MULTIPLIER = 2.0f;
		private const int MAX_NBR_PAWNS = 15;

		#region Private Methods

		// Handy short-cuts for pulling all of the relevant data that you
		// might use for each decision.  Feel free to add your own.
		public int enemyAgentNbr { get; set; }
		public int mainMineNbr { get; set; }
		public int mainBaseNbr { get; set; }
		public bool lastFighterWasWarrior { get; set; }

		public List<int> mines { get; set; }

		public List<int> myPawns { get; set; }
		public List<int> myWarriors { get; set; }
		public List<int> myArchers { get; set; }
		public List<int> myBases { get; set; }
		public List<int> myBarracks { get; set; }

		public List<int> enemyPawns { get; set; }
		public List<int> enemyWarriors { get; set; }
		public List<int> enemyArchers { get; set; }
		public List<int> enemyBases { get; set; }
		public List<int> enemyBarracks { get; set; }

		public List<Vector3Int> buildPositions { get; set; }

		// Added list for heuristic value storage
		private List<float> heuristics;
		private int maxIndex;
		private float highestCost;

		// Influence Maps
		private float[,] territoryMap;
		private float[,] enemyMap;

		/// <summary>
		/// Finds all of the possible build locations for a specific UnitType.
		/// Currently, all structures are 3x3, so these positions can be reused
		/// for all structures (Base, Barracks)
		/// Run this once at the beginning of the game and have a list of
		/// locations that you can use to reduce later computation.  When you
		/// need a location for a build-site, simply pull one off of this list,
		/// determine if it is still buildable, determine if you want to use it
		/// (perhaps it is too far away or too close or not close enough to a mine),
		/// and then simply remove it from the list and build on it!
		/// This method is called from the Awake() method to run only once at the
		/// beginning of the game.
		/// </summary>
		/// <param name="unitType">the type of unit you want to build</param>
		public void FindProspectiveBuildPositions(UnitType unitType)
		{
			// For the entire map
			for (int i = 0; i < GameManager.Instance.MapSize.x; ++i)
			{
				for (int j = 0; j < GameManager.Instance.MapSize.y; ++j)
				{
					// Construct a new point near gridPosition
					Vector3Int testGridPosition = new Vector3Int(i, j, 0);

					// Test if that position can be used to build the unit
					if (Utility.IsValidGridLocation(testGridPosition)
						&& GameManager.Instance.IsBoundedAreaBuildable(unitType, testGridPosition))
					{
						// If this position is buildable, add it to the list
						buildPositions.Add(testGridPosition);
					}
				}
			}
		}

		/// <summary>
		/// Assuming you run the FindProspectiveBuildPositions, this method takes that
		/// list and finds the closest build position to the gridPosition.  This can be
		/// used to find a position close to a mine, close to the enemy base, close to
		/// your barracks, close to your base, close to a troop, etc.
		/// </summary>
		/// <param name="gridPosition">position that you want to build near</param>
		/// <param name="unitType">type of unit you want to build</param>
		/// <returns></returns>
		public Vector3Int FindClosestBuildPosition(Vector3Int gridPosition, UnitType unitType)
		{
			// Variables to store the closest position as we find it
			float minDist = float.MaxValue;
			Vector3Int minBuildPosition = gridPosition;

			// For all the possible build postions that we already found
			foreach (Vector3Int buildPosition in buildPositions)
			{
				// if the distance to that build position is closer than any other seen so far
				if (Vector3.Distance(gridPosition, buildPosition) < minDist && GameManager.Instance.IsBoundedAreaBuildable(unitType, buildPosition))
				{
					// Store this build position as the closest seen so far
					minDist = Vector3.Distance(gridPosition, buildPosition);
					minBuildPosition = buildPosition;
				}
			}

			// Return the closest build position
			return minBuildPosition;
		}

		/// <summary>
		/// Method compares the influence maps to the prospective build locations list
		/// to find an optimal location to build a structure.
		/// </summary>
		/// <param name="unitType">type of unit you want to build</param>
		/// <returns></returns>
		public Vector3Int FindBestBuildPosition(UnitType unitType)
		{
			// Variables to store the optimal position as we find it
			float minInf = float.MaxValue;
			Vector3Int minBuildPosition = new Vector3Int();

			// For all the possible build postions that we already found
			foreach (Vector3Int buildPosition in buildPositions)
			{
				float influence;
				if (territoryMap[buildPosition.x, buildPosition.y] < 1)
				{
					influence = enemyMap[buildPosition.x, buildPosition.y] - territoryMap[buildPosition.x, buildPosition.y];
				}
				else
				{
					influence = enemyMap[buildPosition.x, buildPosition.y];
				}
				// if the influence on that build position is more advantageous for the player than any other so far
				if (influence < minInf && GameManager.Instance.IsBoundedAreaBuildable(unitType, buildPosition))
				{
					// Store this build position as the best seen so far
					minInf = influence;
					minBuildPosition = buildPosition;
				}
			}

			// Return the best build position
			return minBuildPosition;
		}

		/// <summary>
		/// Find the closest unit to the gridPosition out of a list of units.
		/// Use this method to find the enemy warrior closest to your archer,
		/// or the closest base to a mine, or the closest mine to a base, etc.
		/// </summary>
		/// <param name="gridPosition">position of an agent or base</param>
		public int FindClosestUnit(Vector3Int gridPosition, List<int> unitNbrs)
		{
			// Variables to store the closest unit as we find it
			int closestUnitNbr = -1;
			float closestUnitDist = float.MaxValue;

			// Iterate through all of the units
			foreach (int unitNbr in unitNbrs)
			{
				Unit unit = GameManager.Instance.GetUnit(unitNbr);
				float unitDist = Vector3.Distance(unit.GridPosition, gridPosition);

				// If this object is closer than any seen so far, save it
				if (!(unitDist < closestUnitDist)) continue;
				closestUnitDist = unitDist;
				closestUnitNbr = unitNbr;
			}

			// Return the closest unit's number
			return closestUnitNbr;
		}

		/// <summary>
		/// Method compares the influence maps to the list of units provided
		/// to find an optimally placed unit to attack or move to.
		/// </summary>
		/// <param name="unitNbrs">list of units to analyze</param>
		/// <returns></returns>
		public int FindBestPlacedUnit(List<int> unitNbrs)
		{
			// Variables to store the closest unit as we find it
			int bestUnitNbr = -1;
			float bestUnitInf = float.MaxValue;

			// Iterate through all of the units
			foreach (int unitNbr in unitNbrs)
			{
				Unit unit = GameManager.Instance.GetUnit(unitNbr);
				float influence;
				if (territoryMap[unit.GridPosition.x, unit.GridPosition.y] < 1)
				{
					influence = enemyMap[unit.GridPosition.x, unit.GridPosition.y] - territoryMap[unit.GridPosition.x, unit.GridPosition.y];
				}
				else
				{
					influence = enemyMap[unit.GridPosition.x, unit.GridPosition.y];
				}

				// If this object is closer than any seen so far, save it
				if (!(influence < bestUnitInf)) continue;
				bestUnitInf = influence;
				bestUnitNbr = unitNbr;
			}

			// Return the closest unit's number
			return bestUnitNbr;
		}

		/// <summary>
		/// Update heuristics for evaluating potential actions
		/// </summary>
		private void UpdateHeuristics()
		{
			int count = 0;

			// Build a Base
			heuristics[count++] = Mathf.Clamp01(Gold / Constants.COST[UnitType.BASE])
								  * Mathf.Clamp01((mines.Count - myBases.Count) / 2.0f);

			// Build a Barracks
			heuristics[count++] = Mathf.Clamp01(Gold / Constants.COST[UnitType.BARRACKS])
								  * Gold / ((Constants.COST[UnitType.BARRACKS]
								  * (myBarracks.Count + ((myWarriors.Count + myArchers.Count) / 10))) + Gold + 1);

			// Gather
			heuristics[count++] = (Constants.COST[UnitType.BASE] + Constants.COST[UnitType.BARRACKS])
								  / (Gold + Constants.COST[UnitType.BASE] + Constants.COST[UnitType.BARRACKS]);

			// Move
			heuristics[count++] = 0;

			// Train a Pawn
			heuristics[count++] = Mathf.Clamp01(myBases.Count) * Mathf.Clamp01(Gold / Constants.COST[UnitType.PAWN])
								  * 1 - ((highestCost * myPawns.Count)
								  / (Gold * ((enemyPawns.Count * 3) + myPawns.Count + 1)));

			// Train a Warrior
			heuristics[count++] = Mathf.Clamp01(myBarracks.Count) * Mathf.Clamp01(Gold / Constants.COST[UnitType.WARRIOR])
								  * 1 - (myWarriors.Count / ((myArchers.Count * 3) + myWarriors.Count + 1));

			// Train an Archer

			heuristics[count++] = Mathf.Clamp01(myBarracks.Count) * Mathf.Clamp01(Gold / Constants.COST[UnitType.ARCHER])
								  * 1 - (myArchers.Count / ((enemyWarriors.Count + enemyArchers.Count) + myArchers.Count + 1));

			// Attack the Enemy
			heuristics[count++] = 1 - (1 / (myWarriors.Count + myArchers.Count - 1));

			// Calculate next decision
			maxIndex = heuristics.IndexOf(heuristics.Max());
		}

		/// <summary>
		/// Update the influence map based on your structures
		/// </summary>
		private void UpdateTerritoryMap()
		{
			for (int i = 0; i < GameManager.Instance.MapSize.x; i++)
			{
				for (int j = 0; j < GameManager.Instance.MapSize.y; j++)
				{
					Vector3Int gridPosition = new Vector3Int(i, j, 0);
					float total = 0;
					if (Utility.IsValidGridLocation(gridPosition))
					{
						if (myBases.Count + myBarracks.Count + mines.Count > 0)
						{
							foreach (int unitID in myBases)
							{
								Unit unit = GameManager.Instance.GetUnit(unitID);
								total += 3 / (Vector3.Distance(gridPosition, unit.GridPosition) - 1);
							}
							foreach (int unitID in myBarracks)
							{
								Unit unit = GameManager.Instance.GetUnit(unitID);
								total += 2 / (Vector3.Distance(gridPosition, unit.GridPosition) - 1);
							}
							foreach (int mineID in mines)
							{
								Unit mine = GameManager.Instance.GetUnit(mineID);
								total += 2 / (Vector3.Distance(gridPosition, mine.GridPosition) - 1);
							}
							total /= (myBases.Count * 3) + (myBarracks.Count * 2) + (mines.Count * 2);
						}
					}
					else
					{
						total = 1;
					}
					territoryMap[i, j] = total;
				}
			}
		}

		/// <summary>
		/// Update the influence map based on the opponent's structures
		/// </summary>
		private void UpdateEnemyMap()
		{
			for (int i = 0; i < GameManager.Instance.MapSize.x; i++)
			{
				for (int j = 0; j < GameManager.Instance.MapSize.y; j++)
				{
					Vector3Int gridPosition = new Vector3Int(i, j, 0);
					float total = 0;
					if (Utility.IsValidGridLocation(gridPosition))
					{
						if (enemyBases.Count + enemyBarracks.Count > 0)
						{
							foreach (int unitID in enemyBases)
							{
								Unit unit = GameManager.Instance.GetUnit(unitID);
								total += 3 / (Vector3.Distance(gridPosition, unit.GridPosition) - 1);
							}
							foreach (int unitID in enemyBarracks)
							{
								Unit unit = GameManager.Instance.GetUnit(unitID);
								total += 2 / (Vector3.Distance(gridPosition, unit.GridPosition) - 1);
							}
							total /= (enemyBases.Count * 3) + (enemyBarracks.Count * 2);
						}
					}
					else
					{
						total = 1;
					}
					enemyMap[i, j] = total;
				}
			}
		}

		// Stupid method to process the pawns
		public void ProcessPawns()
		{
			// For each pawn
			foreach (int pawn in myPawns)
			{
				// Grab the unit we need for this function
				Unit unit = GameManager.Instance.GetUnit(pawn);

				// Make sure this unit actually exists and is idle
				if (unit != null && unit.CurrentAction == UnitAction.IDLE)
				{
					UpdateTerritoryMap();
					UpdateEnemyMap();
					// If we have enough gold and need a base, build a base
					if (maxIndex == 2)
					{
						// Find the best build position to build a base and 
						// build the base there
						Vector3Int toBuild = FindBestBuildPosition(UnitType.BASE);
						if (toBuild != Vector3Int.zero)
						{
							Build(unit, toBuild, UnitType.BASE);
						}
					}
					//If we have enough gold and need a barracks, build a barracks
					else if (maxIndex == 3)
					{
						// Find the closest build position to an enemy base and build
						// the barracks there
						Vector3Int toBuild;
						if (enemyBases.Count > 0)
						{
							toBuild = FindClosestBuildPosition(
								GameManager.Instance.GetUnit(FindClosestUnit(unit.GridPosition, enemyBases)).GridPosition,
								UnitType.BARRACKS);
						}
						else
						{
							toBuild = FindBestBuildPosition(UnitType.BARRACKS);
						}
						if (toBuild != Vector3Int.zero)
						{
							Build(unit, toBuild, UnitType.BARRACKS);
						}
					}
					// Otherwise, just mine
					else if (mainBaseNbr >= 0 && mainMineNbr >= 0)
					{
						// Grab the mine for this agent
						Unit mineUnit = GameManager.Instance.GetUnit(mainMineNbr);
						Unit baseUnit = GameManager.Instance.GetUnit(mainBaseNbr);
						if (mineUnit != null && baseUnit != null)
						{ Gather(unit, mineUnit, baseUnit); }
					}
				}
			}
		}

		// Process the bases
		public void ProcessBases()
		{
			// For each base, determine if it should train a pawn
			foreach (int baseNbr in myBases)
			{
				// Get the base unit
				Unit baseUnit = GameManager.Instance.GetUnit(baseNbr);

				// If the base exists, is idle, we need a pawn, and we have gold
				if (baseUnit != null && baseUnit.IsBuilt
					&& baseUnit.CurrentAction == UnitAction.IDLE && maxIndex == 5)
				{
					Train(baseUnit, UnitType.PAWN);
				}
			}
		}

		// Process the barracks
		public void ProcessBarracks()
		{
			// For each barracks, determine if it should train a warrior or an archer
			foreach (int barracksNbr in myBarracks)
			{
				// Get the barracks
				Unit barracksUnit = GameManager.Instance.GetUnit(barracksNbr);

				// If this barracks still exists, is idle, we need warriors, and have gold
				if (barracksUnit != null && barracksUnit.IsBuilt
					&& barracksUnit.CurrentAction == UnitAction.IDLE
					&& maxIndex == 6)
				{
					Train(barracksUnit, UnitType.WARRIOR);
					lastFighterWasWarrior = true;
				}
				// If this barracks still exists, is idle, we need archers, and have gold
				else if (barracksUnit != null && barracksUnit.IsBuilt
						 && barracksUnit.CurrentAction == UnitAction.IDLE
						 && maxIndex == 7)
				{
					Train(barracksUnit, UnitType.ARCHER);
					lastFighterWasWarrior = false;
				}
			}
		}

		// Process the warriors
		public void ProcessWarriors()
		{
			// For each warrior, determine what they should attack
			foreach (int warriorNbr in myWarriors)
			{
				// Get this warrior
				Unit warriorUnit = GameManager.Instance.GetUnit(warriorNbr);

				// If this warrior still exists and is idle
				if (warriorUnit != null && warriorUnit.CurrentAction == UnitAction.IDLE)
				{
					bool tooClose = false;
					foreach (int enemy in enemyWarriors)
					{
						Unit enemyUnit = GameManager.Instance.GetUnit(enemy);
						if (Vector3.Distance(warriorUnit.GridPosition, enemyUnit.GridPosition)
							< Constants.UNIT_SIZE[UnitType.WARRIOR].sqrMagnitude && !tooClose)
						{
							tooClose = true;
							Attack(warriorUnit, enemyUnit);
						}
					}
					// If there are enemy archers, find the one in the best position to be attacked and attack it
					if (enemyArchers.Count > 0 && !tooClose)
					{
						Attack(warriorUnit, GameManager.Instance.GetUnit(
							FindBestPlacedUnit(enemyArchers)));
					}
					// If there are enemy warriors, find the one in the best position to be attacked and attack it
					else if (enemyWarriors.Count > 0 && !tooClose)
					{
						Attack(warriorUnit, GameManager.Instance.GetUnit(
							FindBestPlacedUnit(enemyWarriors)));
					}
					// If there are enemy bases, find the one in the best position to be attacked and attack it
					else if (enemyBases.Count > 0 && !tooClose)
					{
						Attack(warriorUnit, GameManager.Instance.GetUnit(
							FindBestPlacedUnit(enemyBases)));
					}
					// If there are enemy pawns, find the one in the best position to be attacked and attack it
					else if (enemyPawns.Count > 0 && !tooClose)
					{
						Attack(warriorUnit, GameManager.Instance.GetUnit(
							FindBestPlacedUnit(enemyPawns)));
					}
					// If there are enemy barracks, find the one in the best position to be attacked and attack it
					else if (enemyBarracks.Count > 0 && !tooClose)
					{
						Attack(warriorUnit, GameManager.Instance.GetUnit(
							FindBestPlacedUnit(enemyBarracks)));
					}
				}
			}
		}

		// Process archers
		public void ProcessArchers()
		{
			// For each warrior, determine what they should attack
			foreach (int archerNbr in myArchers)
			{
				// Get the unit
				Unit archerUnit = GameManager.Instance.GetUnit(archerNbr);

				// If the archer still exists and is idle
				if (archerUnit != null && archerUnit.CurrentAction == UnitAction.IDLE)
				{
					// If there are enemy warriors, find the one in the best position to be attacked and attack it
					if (enemyWarriors.Count > 0)
					{
						Attack(archerUnit, GameManager.Instance.GetUnit(
							FindBestPlacedUnit(enemyWarriors)));
					}
					// If there are enemy archers, find the one in the best position to be attacked and attack it
					else if (enemyArchers.Count > 0)
					{
						Attack(archerUnit, GameManager.Instance.GetUnit(
							FindBestPlacedUnit(enemyArchers)));
					}
					// If there are enemy pawns, find the one in the best position to be attacked and attack it
					else if (enemyPawns.Count > 0)
					{
						Attack(archerUnit, GameManager.Instance.GetUnit(
							FindBestPlacedUnit(enemyPawns)));
					}
					// If there are enemy bases, find the one in the best position to be attacked and attack it
					else if (enemyBases.Count > 0)
					{
						Attack(archerUnit, GameManager.Instance.GetUnit(
							FindBestPlacedUnit(enemyBases)));
					}
					// If there are enemy barracks, find the one in the best position to be attacked and attack it
					else if (enemyBarracks.Count > 0)
					{
						Attack(archerUnit, GameManager.Instance.GetUnit(
							FindBestPlacedUnit(enemyBarracks)));
					}
				}
			}
		}

		/// <summary>
		/// Creates a bitmap file of your influence map.
		/// </summary>
		/// <param name="map">The float array used to represent the influence map. If you used a different data structure, modify this method to accept it.</param>
		/// <param name="fileName">The name of the file. Do not include a file extension</param>
		private void CreateBitmapOfInfluenceMap(float[,] map, string fileName)
		{
			System.Drawing.Bitmap b = new System.Drawing.Bitmap(GameManager.Instance.MapSize.x, GameManager.Instance.MapSize.y);
			for (int x = 0; x < GameManager.Instance.MapSize.x; x++)
			{
				for (int y = 0; y < GameManager.Instance.MapSize.y; y++)
				{
					int value = (int)(map[x, y] * 255);
					if (map[x, y] < 0.0f)
						value = 0;
					if (map[x, y] > 1.0f)
						value = 255;
					b.SetPixel(x, y, System.Drawing.Color.FromArgb(value, value, value));
				}
			}
			b.Save(fileName + ".bmp", System.Drawing.Imaging.ImageFormat.Bmp);
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Called when the object is instantiated in the scene 
		/// </summary>
		public void Awake()
		{
			lastFighterWasWarrior = false;

			buildPositions = new List<Vector3Int>();

			FindProspectiveBuildPositions(UnitType.BASE);

			// Set the main mine and base to "non-existant"
			mainMineNbr = -1;
			mainBaseNbr = -1;

			// Initialize all of the unit lists
			mines = new List<int>();

			myPawns = new List<int>();
			myWarriors = new List<int>();
			myArchers = new List<int>();
			myBases = new List<int>();
			myBarracks = new List<int>();

			enemyPawns = new List<int>();
			enemyWarriors = new List<int>();
			enemyArchers = new List<int>();
			enemyBases = new List<int>();
			enemyBarracks = new List<int>();

			// Initialize the list of heuristics
			heuristics = new List<float>();
			for (int i = 0; i < 8; i++)
			{
				heuristics.Add(0.0f);
			}

			// Determine cost for the most expensive type of unit
			highestCost = 0.0f;
			foreach (float cost in Constants.COST.Keys)
			{
				if (cost > highestCost)
				{
					highestCost = cost;
				}
			}

			// Initialize the influence maps
			territoryMap = new float[GameManager.Instance.MapSize.x, GameManager.Instance.MapSize.y];
			enemyMap = new float[GameManager.Instance.MapSize.x, GameManager.Instance.MapSize.y];
			UpdateTerritoryMap();
			UpdateEnemyMap();
		}

		/// <summary>
		/// Updates the game state for the Agent - called once per frame for GameManager
		/// Pulls all of the agents from the game and identifies who they belong to
		/// </summary>
		public void UpdateGameState()
		{
			// Update the common resources
			mines = GameManager.Instance.GetUnitNbrsOfType(UnitType.MINE);

			// Update all of my unitNbrs
			myPawns = GameManager.Instance.GetUnitNbrsOfType(UnitType.PAWN, AgentNbr);
			myWarriors = GameManager.Instance.GetUnitNbrsOfType(UnitType.WARRIOR, AgentNbr);
			myArchers = GameManager.Instance.GetUnitNbrsOfType(UnitType.ARCHER, AgentNbr);
			myBarracks = GameManager.Instance.GetUnitNbrsOfType(UnitType.BARRACKS, AgentNbr);
			myBases = GameManager.Instance.GetUnitNbrsOfType(UnitType.BASE, AgentNbr);

			// Update the enemy agents & unitNbrs
			List<int> enemyAgentNbrs = GameManager.Instance.GetEnemyAgentNbrs(AgentNbr);
			if (enemyAgentNbrs.Any())
			{
				enemyAgentNbr = enemyAgentNbrs[0];
				enemyPawns = GameManager.Instance.GetUnitNbrsOfType(UnitType.PAWN, enemyAgentNbr);
				enemyWarriors = GameManager.Instance.GetUnitNbrsOfType(UnitType.WARRIOR, enemyAgentNbr);
				enemyArchers = GameManager.Instance.GetUnitNbrsOfType(UnitType.ARCHER, enemyAgentNbr);
				enemyBarracks = GameManager.Instance.GetUnitNbrsOfType(UnitType.BARRACKS, enemyAgentNbr);
				enemyBases = GameManager.Instance.GetUnitNbrsOfType(UnitType.BASE, enemyAgentNbr);
			}
		}

		/// <summary>
		/// Called at the end of each round before remaining units are
		/// destroyed to allow the agent to observe the "win/loss" state
		/// </summary>
		public override void Learn()
		{
			Debug.Log("Nbr Wins: " + AgentNbrWins);

			//Debug.Log("PlanningAgent::Learn");
			Log("value 1");
			Log("value 2");
			Log("value 3a, 3b");
			Log("value 4");
		}

		/// <summary>
		/// Called before each match between two agents.  Matches have
		/// multiple rounds. 
		/// </summary>
		public override void InitializeMatch()
		{
			Debug.Log("Moron's: " + AgentName);
			//Debug.Log("PlanningAgent::InitializeMatch");
		}

		/// <summary>
		/// Called at the beginning of each round in a match.
		/// There are multiple rounds in a single match between two agents.
		/// </summary>
		public override void InitializeRound()
		{
			//Debug.Log("PlanningAgent::InitializeRound");
			buildPositions = new List<Vector3Int>();

			FindProspectiveBuildPositions(UnitType.BASE);

			// Set the main mine and base to "non-existent"
			mainMineNbr = -1;
			mainBaseNbr = -1;

			// Initialize all of the unit lists
			mines = new List<int>();

			myPawns = new List<int>();
			myWarriors = new List<int>();
			myArchers = new List<int>();
			myBases = new List<int>();
			myBarracks = new List<int>();

			enemyPawns = new List<int>();
			enemyWarriors = new List<int>();
			enemyArchers = new List<int>();
			enemyBases = new List<int>();
			enemyBarracks = new List<int>();
		}


		// Update the GameManager - called once per frame
		public void Update()
		{
			UpdateGameState();

			// If we have at least one base, assume the first one is our "main" base
			if (myBases.Count > 0)
			{
				mainBaseNbr = myBases[0];
			}
			else
			{
				mainBaseNbr = -1;
			}

			// If we have a base, find the closest mine to the base
			if (mines.Count > 0 && mainBaseNbr >= 0)
			{
				Unit baseUnit = GameManager.Instance.GetUnit(mainBaseNbr);
				mainMineNbr = FindClosestUnit(baseUnit.GridPosition, mines);
			}

			// Update heuristic values for decision making
			UpdateHeuristics();

			// Process all of the units, prioritize building new structures over
			// training units in terms of spending gold
			ProcessPawns();

			ProcessWarriors();

			ProcessArchers();

			ProcessBarracks();

			ProcessBases();

			if (Input.GetKeyDown(KeyCode.Space))
			{
				CreateBitmapOfInfluenceMap(territoryMap, "PlayerTerritoryInfluenceMap");
				CreateBitmapOfInfluenceMap(enemyMap, "EnemyTerritoryInfluenceMap");
			}
		}

		#endregion
	}
}

