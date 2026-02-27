// -territoryMap: generates influence based on proximety to own structures and mines using linear falloff, bases have higher intensity
// -enemyMap: generates influence based on proximety to enemy structures using linear falloff, bases have higher intensity
// -Functions for ideal build positions and unit positions subtract territoryMap influence from enemyMap influence for decision making
// -Workers use influence to determine where to build bases and refineries
// -Soldiers and archers use influence to prioritize which enemy unit to attack first

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

        private enum HeuristicTasks {
            BASE_BUILDING,
            BARRACKS_BUILDING,
            REFINERY_BUILDING,
            ATTACKING,
            TRAIN_ARCHER,
            TRAIN_SOLDIER,
            TRAIN_WORKER,
            GATHER,
            MOVE
        }

		private const int MAX_NBR_ARCHERS = 20;
		private const float MAX_ARCHER_MULTIPLIER = 2.0f;
		private const int MAX_NBR_SOLDIERS = 10;
		private const float MAX_SOLDIER_MULTIPLIER = 2.0f;
		private const int MAX_NBR_WORKERS = 15;

		#region Private Methods

		// Handy short-cuts for pulling all of the relevant data that you
		// might use for each decision.  Feel free to add your own.
		public int enemyAgentNbr { get; set; }
		public int mainMineNbr { get; set; }
		public int mainBaseNbr { get; set; }

		public List<int> mines { get; set; }

		public List<int> myWorkers { get; set; }
		public List<int> mySoldiers { get; set; }
		public List<int> myArchers { get; set; }
		public List<int> myBases { get; set; }
		public List<int> myBarracks { get; set; }
		public List<int> myRefineries { get; set; }

		public List<int> enemyWorkers { get; set; }
		public List<int> enemySoldiers { get; set; }
		public List<int> enemyArchers { get; set; }
		public List<int> enemyBases { get; set; }
		public List<int> enemyBarracks { get; set; }
		public List<int> enemyRefineries { get; set; }
        public List<int> enemyUnits { get; set; }
        public List<int> enemyBuildings { get; set; }

		public List<Vector3Int> buildPositions { get; set; }

        // Added list for heuristic value storage
        private Dictionary<HeuristicTasks, float> heuristics;
        private HeuristicTasks maxIndex;
        private float highestCost;

        // Influence Maps
        private float[,] territoryMap;
        private float[,] enemyMap;

		/// <summary>
		/// Finds all of the possible build locations for a specific UnitType.
		/// Currently, all structures are 3x3, so these positions can be reused
		/// for all structures (Base, Barracks, Refinery)
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
        /// Update heuristics for evaluating potential actions
        /// </summary>
        private void UpdateHeuristics()
        {
             // Build a Base
            if (Gold > Constants.COST[UnitType.BASE])
            {
                heuristics[HeuristicTasks.BASE_BUILDING] = 1;
            }
            else
            {
                heuristics[HeuristicTasks.BASE_BUILDING] = 0;
            }

            // 3: Build a Barracks
            if (Gold > Constants.COST[UnitType.BARRACKS])
            {
                heuristics[HeuristicTasks.BARRACKS_BUILDING] = 1;
            }
            else
            {
                heuristics[HeuristicTasks.BARRACKS_BUILDING] = 0;
            }

            // 4: Build a Refinery
            if (Gold > Constants.COST[UnitType.REFINERY])
            {
                heuristics[HeuristicTasks.REFINERY_BUILDING] = 1;
            }
            else
            {
                heuristics[HeuristicTasks.REFINERY_BUILDING] = 0;
            }

            // 5: Train a Worker
            if (Gold >= Constants.COST[UnitType.WORKER])
            {
                heuristics[HeuristicTasks.TRAIN_WORKER] = 1;
            }
            else
            {
                heuristics[HeuristicTasks.TRAIN_WORKER] = 0;
            }

            // 6: Train a Soldier
            if (Gold >= Constants.COST[UnitType.SOLDIER])
            {
                heuristics[HeuristicTasks.TRAIN_SOLDIER] = 1;
            }
            else
            {
                heuristics[HeuristicTasks.TRAIN_SOLDIER] = 0;
            }

            // 7: Train an Archer
            if (Gold >= Constants.COST[UnitType.ARCHER])
            {
                heuristics[HeuristicTasks.TRAIN_ARCHER] = 1;
            }
            else
            {
                heuristics[HeuristicTasks.TRAIN_ARCHER] = 0;
            }

            // 8: Attack the Enemy
            if (mySoldiers.Count + myArchers.Count > 0)
            {
                heuristics[HeuristicTasks.ATTACKING] = 1;
            }
            else
            {
                heuristics[HeuristicTasks.ATTACKING] = 0;
            }

            // 0: Gather
            heuristics[HeuristicTasks.GATHER] = 0;

            // 1: Move
            heuristics[HeuristicTasks.MOVE] = 0;


            // Calculate next decision
            maxIndex = heuristics.FirstOrDefault(x => x.Value == heuristics.Values.Max()).Key;
        }

		//// Stupid method to process the workers
		//public void ProcessWorkers()
		//{
		//	// For each worker
		//	foreach (int worker in myWorkers)
		//	{
		//		// Grab the unit we need for this function
		//		Unit unit = GameManager.Instance.GetUnit(worker);

		//		// Make sure this unit actually exists and is idle
		//		if (unit != null && unit.CurrentAction == UnitAction.IDLE)
		//		{
		//			// If we have enough gold and need a base, build a base
		//			if (maxIndex == 2)
		//			{
  //                      // Find the best build position to build a base and 
  //                      // build the base there
  //                      Vector3Int toBuild = FindBestBuildPosition(UnitType.BASE);
		//				if (toBuild != Vector3Int.zero)
		//				{
		//					Build(unit, toBuild, UnitType.BASE);
		//				}
		//			}
		//			//If we have enough gold and need a barracks, build a barracks
		//			else if (maxIndex == 3)
		//			{
  //                      // Find the closest build position to an enemy base and build
  //                      // the barracks there
  //                      Vector3Int toBuild = FindBestBuildPosition(UnitType.BARRACKS);
		//				if (toBuild != Vector3Int.zero)
		//				{
		//					Build(unit, toBuild, UnitType.BARRACKS);
		//				}
		//			}
		//			// If we have enough gold and need a refinery, build a refinery
		//			else if (maxIndex == 4)
		//			{
  //                      // Find the best build position for a refinery and build
  //                      // the refinery there
  //                      Vector3Int toBuild = FindBestBuildPosition(UnitType.REFINERY);
		//				if (toBuild != Vector3Int.zero)
		//				{
		//					Build(unit, toBuild, UnitType.REFINERY);
		//				}
		//			}
		//			// Otherwise, just mine
		//			else if (mainBaseNbr >= 0 && mainMineNbr >= 0)
		//			{
		//				// Grab the mine for this agent
		//				Unit mineUnit = GameManager.Instance.GetUnit(mainMineNbr);
		//				Unit baseUnit = GameManager.Instance.GetUnit(mainBaseNbr);
		//				if (mineUnit != null && baseUnit != null)
		//				{
  //                          Gather(unit, mineUnit, baseUnit);
  //                      }
		//			}
		//		}
		//	}
		//}

		//// Process the bases
		//public void ProcessBases()
		//{
		//	// For each base, determine if it should train a worker
		//	foreach (int baseNbr in myBases)
		//	{
		//		// Get the base unit
		//		Unit baseUnit = GameManager.Instance.GetUnit(baseNbr);

		//		// If the base exists, is idle, we need a worker, and we have gold
		//		if (baseUnit != null && baseUnit.IsBuilt
		//			&& baseUnit.CurrentAction == UnitAction.IDLE && maxIndex == 5)
		//		{
		//			Train(baseUnit, UnitType.WORKER);
		//		}
		//	}
		//}

		//// Process the barracks
		//public void ProcessBarracks()
		//{
		//	// For each barracks, determine if it should train a soldier or an archer
		//	foreach (int barracksNbr in myBarracks)
		//	{
		//		// Get the barracks
		//		Unit barracksUnit = GameManager.Instance.GetUnit(barracksNbr);

		//		// If this barracks still exists, is idle, we need soldiers, and have gold
		//		if (barracksUnit != null && barracksUnit.IsBuilt
		//		    && barracksUnit.CurrentAction == UnitAction.IDLE
		//			&& maxIndex == 6)
		//		{
		//			Train(barracksUnit, UnitType.SOLDIER);
		//		}
		//		// If this barracks still exists, is idle, we need archers, and have gold
		//		else if (barracksUnit != null && barracksUnit.IsBuilt
		//		         && barracksUnit.CurrentAction == UnitAction.IDLE
		//				 && maxIndex == 7)
		//		{
		//			Train(barracksUnit, UnitType.ARCHER);
		//		}
		//	}
		//}

		//// Process the soldiers
		//public void ProcessSoldiers()
		//{
		//	// For each soldier, determine what they should attack
		//	foreach (int soldierNbr in mySoldiers)
		//	{
		//		// Get this soldier
		//		Unit soldierUnit = GameManager.Instance.GetUnit(soldierNbr);

		//		// If this soldier still exists and is idle, attack something
		//		if (soldierUnit != null && soldierUnit.CurrentAction == UnitAction.IDLE)
		//		{
  //                  if (enemyUnits.Count > 0)
  //                  {
  //                      Attack(soldierUnit, GameManager.Instance.GetUnit(enemyUnits.First()));
  //                  }
  //                  else if (enemyBuildings.Count > 0)
  //                  {
  //                      Attack(soldierUnit, GameManager.Instance.GetUnit(enemyBuildings.First()));
  //                  }
		//		}
		//	}
		//}

		//// Process archers
		//public void ProcessArchers()
		//{
		//	// For each soldier, determine what they should attack
		//	foreach (int archerNbr in myArchers)
		//	{
		//		// Get the unit
		//		Unit archerUnit = GameManager.Instance.GetUnit(archerNbr);

		//		// If the archer still exists and is idle
		//		if (archerUnit != null && archerUnit.CurrentAction == UnitAction.IDLE)
		//		{
  //                  if (enemyUnits.Count > 0)
  //                  {
  //                      Attack(archerUnit, GameManager.Instance.GetUnit(enemyUnits.First()));
  //                  }
  //                  else if (enemyBuildings.Count > 0)
  //                  {
  //                      Attack(archerUnit, GameManager.Instance.GetUnit(enemyBuildings.First()));
  //                  }
  //              }
		//	}
		//}

        #endregion

        #region Public Methods

        /// <summary>
        /// Called when the object is instantiated in the scene 
        /// </summary>
        public void Awake()
		{
            Debug.Log("Awake");

            buildPositions = new List<Vector3Int>();

			FindProspectiveBuildPositions(UnitType.BASE);

			// Set the main mine and base to "non-existant"
			mainMineNbr = -1;
			mainBaseNbr = -1;

			// Initialize all of the unit lists
			mines = new List<int>();

			myWorkers = new List<int>();
			mySoldiers = new List<int>();
			myArchers = new List<int>();
			myBases = new List<int>();
			myBarracks = new List<int>();
			myRefineries = new List<int>();

			enemyWorkers = new List<int>();
			enemySoldiers = new List<int>();
			enemyArchers = new List<int>();
			enemyBases = new List<int>();
			enemyBarracks = new List<int>();
			enemyRefineries = new List<int>();
            enemyUnits = new List<int>();
            enemyBuildings = new List<int>();

            // Initialize the list of heuristics
            heuristics = new Dictionary<HeuristicTasks, float>();

            // Determine cost for the most expensive type of unit
            highestCost = 0.0f;
            foreach (float cost in Constants.COST.Keys)
            {
                if (cost > highestCost)
                {
                    highestCost = cost;
                }
            }
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
			myWorkers = GameManager.Instance.GetUnitNbrsOfType(UnitType.WORKER, AgentNbr);
			mySoldiers = GameManager.Instance.GetUnitNbrsOfType(UnitType.SOLDIER, AgentNbr);
			myArchers = GameManager.Instance.GetUnitNbrsOfType(UnitType.ARCHER, AgentNbr);
			myBarracks = GameManager.Instance.GetUnitNbrsOfType(UnitType.BARRACKS, AgentNbr);
			myBases = GameManager.Instance.GetUnitNbrsOfType(UnitType.BASE, AgentNbr);
			myRefineries = GameManager.Instance.GetUnitNbrsOfType(UnitType.REFINERY, AgentNbr);

			// Update the enemy agents & unitNbrs
			List<int> enemyAgentNbrs = GameManager.Instance.GetEnemyAgentNbrs(AgentNbr);
			if (enemyAgentNbrs.Any())
			{
				enemyAgentNbr = enemyAgentNbrs[0];
				enemyWorkers = GameManager.Instance.GetUnitNbrsOfType(UnitType.WORKER, enemyAgentNbr);
				enemySoldiers = GameManager.Instance.GetUnitNbrsOfType(UnitType.SOLDIER, enemyAgentNbr);
				enemyArchers = GameManager.Instance.GetUnitNbrsOfType(UnitType.ARCHER, enemyAgentNbr);
				enemyBarracks = GameManager.Instance.GetUnitNbrsOfType(UnitType.BARRACKS, enemyAgentNbr);
				enemyBases = GameManager.Instance.GetUnitNbrsOfType(UnitType.BASE, enemyAgentNbr);
				enemyRefineries = GameManager.Instance.GetUnitNbrsOfType(UnitType.REFINERY, enemyAgentNbr);

                enemyUnits.AddRange(enemySoldiers);
                enemyUnits.AddRange(enemyArchers);
                enemyBuildings.AddRange(enemyBases);
                enemyBuildings.AddRange(enemyBarracks);
                enemyBuildings.AddRange(enemyRefineries);
            }
        }

		// Update the GameManager - called once per frame
		public void Update()
		{
            Debug.Log("Update");

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
                mainMineNbr = mines.First();
			}

            // Update heuristic values for decision making
            UpdateHeuristics();

			// Process all of the units, prioritize building new structures over
			// training units in terms of spending gold
			ProcessWorkers();

			ProcessSoldiers();

			ProcessArchers();

			ProcessBarracks();

			ProcessBases();
		}

		#endregion
	}
}

