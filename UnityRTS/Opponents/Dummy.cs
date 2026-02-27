using System.Collections.Generic;
using System.Linq;
using GameManager.EnumTypes;
using GameManager.GameElements;
using UnityEngine;

namespace GameManager
{
    // Planning Agent is the over-head planner that decided where
    // individual units go and what tasks they perform.  Low-level
    // AI is handled by other classes (like pathfinding).
    public class PlanningAgent : Agent
    {
        private const int MAX_NBR_ARCHERS = 0;
        private const float MAX_ARCHER_MULTIPLIER = 1.5f;
        private const int MAX_NBR_SOLDIERS = 10;
        private const float MAX_SOLDIER_MULTIPLIER = 1.5f;
        private const int MAX_NBR_WORKERS = 10;

        #region Private Data

        ///////////////////////////////////////////////////////////////////////
        // Handy short-cuts for pulling all of the relevant data that you
        // might use for each decision.  Feel free to add your own.
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The enemy's agent number
        /// </summary>
        private int enemyAgentNbr { get; set; }

        /// <summary>
        /// My primary mine number
        /// </summary>
        private int mainMineNbr { get; set; }

        /// <summary>
        /// My primary base number
        /// </summary>
        private int mainBaseNbr { get; set; }

        /// <summary>
        /// List of all the mines on the map
        /// </summary>
        private List<int> mines { get; set; }

        /// <summary>
        /// List of all of my workers
        /// </summary>
        private List<int> myWorkers { get; set; }

        /// <summary>
        /// List of all of my soldiers
        /// </summary>
        private List<int> mySoldiers { get; set; }

        /// <summary>
        /// List of all of my archers
        /// </summary>
        private List<int> myArchers { get; set; }

        /// <summary>
        /// List of all of my bases
        /// </summary>
        private List<int> myBases { get; set; }

        /// <summary>
        /// List of all of my barracks
        /// </summary>
        private List<int> myBarracks { get; set; }

        /// <summary>
        /// List of all of my refineries
        /// </summary>
        private List<int> myRefineries { get; set; }

        /// <summary>
        /// List of the enemy's workers
        /// </summary>
        private List<int> enemyWorkers { get; set; }

        /// <summary>
        /// List of the enemy's soldiers
        /// </summary>
        private List<int> enemySoldiers { get; set; }

        /// <summary>
        /// List of enemy's archers
        /// </summary>
        private List<int> enemyArchers { get; set; }

        /// <summary>
        /// List of the enemy's bases
        /// </summary>
        private List<int> enemyBases { get; set; }

        /// <summary>
        /// List of the enemy's barracks
        /// </summary>
        private List<int> enemyBarracks { get; set; }

        /// <summary>
        /// List of the enemy's refineries
        /// </summary>
        private List<int> enemyRefineries { get; set; }

        /// <summary>
        /// List of the possible build positions for a 3x3 unit
        /// </summary>
        private List<Vector3Int> buildPositions { get; set; }

        private bool lastFighterWasSoldier = false;

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
                if (Vector3.Distance(gridPosition, buildPosition) < minDist &&
                    GameManager.Instance.IsBoundedAreaBuildable(unitType, buildPosition))
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
        /// Find the closest unit to the gridPosition out of a list of units.
        /// Use this method to find the enemy soldier closest to your archer,
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

        // Stupid method to process the workers
        public void ProcessWorkers()
        {
            // For each worker
            foreach (int worker in myWorkers)
            {
                // Grab the unit we need for this function
                Unit unit = GameManager.Instance.GetUnit(worker);

                // Make sure this unit actually exists and is idle
                if (unit != null && unit.CurrentAction == UnitAction.IDLE)
                {
                    // If we have enough gold and need a base, build a base
                    if (Gold >= Constants.COST[UnitType.BASE]
                        && myBases.Count < 1)
                    {
                        // Find the closest build position to this worker's position (DUMB) and 
                        // build the base there
                        Vector3Int toBuild = FindClosestBuildPosition(unit.GridPosition, UnitType.BASE);
                        if (toBuild != Vector3Int.zero)
                        {
                            Build(unit, toBuild, UnitType.BASE);
                        }
                    }
                    //If we have enough gold and need a barracks, build a barracks
                    else if (Gold >= Constants.COST[UnitType.BARRACKS]
                             && myBarracks.Count < 1)
                    {
                        // Find the closest build position to this worker's position and build
                        // the barracks there
                        Vector3Int toBuild = FindClosestBuildPosition(unit.GridPosition, UnitType.BARRACKS);
                        if (toBuild != Vector3Int.zero)
                        {
                            Build(unit, toBuild, UnitType.BARRACKS);
                        }
                    }
                    // If we have enough gold and need a refinery, build a refinery
                    else if (Gold >= Constants.COST[UnitType.REFINERY]
                             && myRefineries.Count < 1)
                    {
                        // Find the closest build position to this worker's position and build
                        // the refinery there
                        Vector3Int toBuild = FindClosestBuildPosition(unit.GridPosition, UnitType.REFINERY);
                        if (toBuild != Vector3Int.zero)
                        {
                            Build(unit, toBuild, UnitType.REFINERY);
                        }
                    }
                    // Otherwise, just mine
                    else if (mainBaseNbr >= 0 && mainMineNbr >= 0)
                    {
                        // Grab the mine for this agent
                        Unit mineUnit = GameManager.Instance.GetUnit(mainMineNbr);
                        Unit baseUnit = GameManager.Instance.GetUnit(mainBaseNbr);
                        if (mineUnit != null && baseUnit != null)
                        {
                            Gather(unit, mineUnit, baseUnit);
                        }
                    }
                }
            }
        }

        // Process the bases
        public void ProcessBases()
        {
            // For each base, determine if it should train a worker
            foreach (int baseNbr in myBases)
            {
                // Get the base unit
                Unit baseUnit = GameManager.Instance.GetUnit(baseNbr);

                // If the base exists, is idle, we need a worker, and we have gold
                if (baseUnit != null && baseUnit.IsBuilt
                                     && baseUnit.CurrentAction == UnitAction.IDLE && myWorkers.Count < MAX_NBR_WORKERS
                                     && Gold >= Constants.COST[UnitType.WORKER])
                {
                    Train(baseUnit, UnitType.WORKER);
                }
            }
        }

        // Process the barracks
        public void ProcessBarracks()
        {
            // For each barracks, determine if it should train a soldier or an archer
            foreach (int barracksNbr in myBarracks)
            {
                // Get the barracks
                Unit barracksUnit = GameManager.Instance.GetUnit(barracksNbr);

                // If this barracks still exists, is idle, we need soldiers, and have gold
                if (!lastFighterWasSoldier && barracksUnit != null && barracksUnit.IsBuilt
                    && barracksUnit.CurrentAction == UnitAction.IDLE
                    && (mySoldiers.Count < MAX_NBR_SOLDIERS
                        || mySoldiers.Count <= enemySoldiers.Count * MAX_SOLDIER_MULTIPLIER)
                    && Gold >= Constants.COST[UnitType.SOLDIER])
                {
                    Train(barracksUnit, UnitType.SOLDIER);
                    lastFighterWasSoldier = !lastFighterWasSoldier;
                }
                // If this barracks still exists, is idle, we need archers, and have gold
                else if (lastFighterWasSoldier && barracksUnit != null && barracksUnit.IsBuilt
                         && barracksUnit.CurrentAction == UnitAction.IDLE
                         && (myArchers.Count < MAX_NBR_ARCHERS
                             || myArchers.Count <= enemyArchers.Count * MAX_ARCHER_MULTIPLIER)
                         && Gold >= Constants.COST[UnitType.ARCHER])
                {
                    Train(barracksUnit, UnitType.ARCHER);
                    lastFighterWasSoldier = !lastFighterWasSoldier;
                }
            }
        }

        // Process the soldiers - this method is naive and stupid!
        // Soldiers randomly select a unit to attack (you should probably
        // attack the "closest" unit) but they do prioritize somewhat strategically
        // by attacking offensive units before structures or workers.
        public void ProcessSoldiers()
        {
            // For each soldier, determine what they should attack
            foreach (int soldierNbr in mySoldiers)
            {
                // Get this soldier
                Unit soldierUnit = GameManager.Instance.GetUnit(soldierNbr);

                // If this soldier still exists and is idle
                if (soldierUnit != null && soldierUnit.CurrentAction == UnitAction.IDLE)
                {
                    // If there are enemy soldiers, randomly select one and attack it
                    if (enemySoldiers.Count > 0)
                    {
                        Attack(soldierUnit, GameManager.Instance.GetUnit(
                            enemySoldiers[UnityEngine.Random.Range(0, enemySoldiers.Count)]));
                    }
                    // If there are enemy archers, randomly select one and attack it
                    else if (enemyArchers.Count > 0)
                    {
                        Attack(soldierUnit, GameManager.Instance.GetUnit(
                            enemyArchers[UnityEngine.Random.Range(0, enemyArchers.Count)]));
                    }
                    // If there are enemy workers, randomly select one and attack it
                    else if (enemyWorkers.Count > 0)
                    {
                        Attack(soldierUnit, GameManager.Instance.GetUnit(
                            enemyWorkers[UnityEngine.Random.Range(0, enemyWorkers.Count)]));
                    }
                    // If there are enemy bases, randomly select one and attack it
                    else if (enemyBases.Count > 0)
                    {
                        Attack(soldierUnit, GameManager.Instance.GetUnit(
                            enemyBases[UnityEngine.Random.Range(0, enemyBases.Count)]));
                    }
                    // If there are enemy barracks, randomly select one and attack it
                    else if (enemyBarracks.Count > 0)
                    {
                        Attack(soldierUnit, GameManager.Instance.GetUnit(
                            enemyBarracks[UnityEngine.Random.Range(0, enemyBarracks.Count)]));
                    }
                    // If there are enemy refineries, randomly select one and attack it
                    else if (enemyRefineries.Count > 0)
                    {
                        Attack(soldierUnit, GameManager.Instance.GetUnit(
                            enemyRefineries[UnityEngine.Random.Range(0, enemyRefineries.Count)]));
                    }
                }
            }
        }

        // Process archers
        public void ProcessArchers()
        {
            // For each soldier, determine what they should attack
            foreach (int archerNbr in myArchers)
            {
                // Get the unit
                Unit archerUnit = GameManager.Instance.GetUnit(archerNbr);

                // If the archer still exists and is idle
                if (archerUnit != null && archerUnit.CurrentAction == UnitAction.IDLE)
                {
                    // If there are enemy soldiers, randomly select one and attack it
                    if (enemySoldiers.Count > 0)
                    {
                        Attack(archerUnit, GameManager.Instance.GetUnit(
                            enemySoldiers[UnityEngine.Random.Range(0, enemySoldiers.Count)]));
                    }
                    // If there are enemy archers, randomly select one and attack it
                    else if (enemyArchers.Count > 0)
                    {
                        Attack(archerUnit, GameManager.Instance.GetUnit(
                            enemyArchers[UnityEngine.Random.Range(0, enemyArchers.Count)]));
                    }
                    // If there are enemy workers, randomly select one and attack it
                    else if (enemyWorkers.Count > 0)
                    {
                        Attack(archerUnit, GameManager.Instance.GetUnit(
                            enemyWorkers[UnityEngine.Random.Range(0, enemyWorkers.Count)]));
                    }
                    // If there are enemy bases, randomly select one and attack it
                    else if (enemyBases.Count > 0)
                    {
                        Attack(archerUnit, GameManager.Instance.GetUnit(
                            enemyBases[UnityEngine.Random.Range(0, enemyBases.Count)]));
                    }
                    // If there are enemy barracks, randomly select one and attack it
                    else if (enemyBarracks.Count > 0)
                    {
                        Attack(archerUnit, GameManager.Instance.GetUnit(
                            enemyBarracks[UnityEngine.Random.Range(0, enemyBarracks.Count)]));
                    }
                    // If there are enemy refineries, randomly select one and attack it
                    else if (enemyRefineries.Count > 0)
                    {
                        Attack(archerUnit, GameManager.Instance.GetUnit(
                            enemyRefineries[UnityEngine.Random.Range(0, enemyRefineries.Count)]));
                    }
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Called at the end of each round before remaining units are
        /// destroyed to allow the agent to observe the "win/loss" state
        /// </summary>
        public override void Learn()
        {
            Debug.Log("PlanningAgent::Learn");
        }

        /// <summary>
        /// Called before each match between two agents.  Matches have
        /// multiple rounds. 
        /// </summary>
        public override void InitializeMatch()
        {
            Debug.Log("PlanningAgent::InitializeMatch");
        }

        /// <summary>
        /// Called at the beginning of each round in a match.
        /// There are multiple rounds in a single match between two agents.
        /// </summary>
        public override void InitializeRound()
        {
            Debug.Log("PlanningAgent::InitializeRound");

            lastFighterWasSoldier = false;

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
            }
        }

        // Update the GameManager - called once per frame
        public override void Update()
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

