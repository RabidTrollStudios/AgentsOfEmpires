using System.Collections.Generic;
using System.Linq;
using AgentSDK;

/////////////////////////////////////////////////////////////////////////////
// This is the Moron Agent
/////////////////////////////////////////////////////////////////////////////

namespace PlanningAgent
{
    ///<summary>Planning Agent is the over-head planner that decided where
    /// individual units go and what tasks they perform.  Low-level
    /// AI is handled by other classes (like pathfinding).
    ///</summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_NBR_PAWNS = 20;

        private System.Random _rng = new System.Random();

        #region Private Data

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
        /// This method is called from InitializeRound() to run only once at the
        /// beginning of each round.
        /// </summary>
        /// <param name="unitType">the type of unit you want to build</param>
        /// <param name="state">the current game state</param>
        public void FindProspectiveBuildPositions(UnitType unitType, IGameState state)
        {
            // For the entire map
            for (int i = 0; i < state.MapSize.X; ++i)
            {
                for (int j = 0; j < state.MapSize.Y; ++j)
                {
                    // Construct a new point near gridPosition
                    Position testGridPosition = new Position(i, j);

                    // Test if that position can be used to build the unit
                    if (state.IsAreaBuildable(unitType, testGridPosition))
                    {
                        // If this position is buildable, add it to the list
                        buildPositions.Add(testGridPosition);
                    }
                }
            }
        }

        /// <summary>
        /// Build a building
        /// </summary>
        /// <param name="unitType"></param>
        /// <param name="state"></param>
        /// <param name="actions"></param>
        public void BuildBuilding(UnitType unitType, IGameState state, IAgentActions actions)
        {
            // For each pawn
            foreach (int pawn in myPawns)
            {
                // Grab the unit we need for this function
                UnitInfo? unitInfo = state.GetUnit(pawn);

                // Make sure this unit actually exists and we have enough gold
                if (unitInfo.HasValue && state.MyGold >= GameConstants.COST[unitType])
                {
                    // Find the closest build position to this pawn's position (DUMB) and
                    // build the base there
                    foreach (Position toBuild in buildPositions)
                    {
                        if (state.IsAreaBuildable(unitType, toBuild))
                        {
                            actions.Build(pawn, toBuild, unitType);
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Attack the enemy
        /// </summary>
        /// <param name="myTroops"></param>
        /// <param name="state"></param>
        /// <param name="actions"></param>
        public void AttackEnemy(List<int> myTroops, IGameState state, IAgentActions actions)
        {
            if (myTroops.Count > 3)
            {
                // For each of my troops in this collection
                foreach (int troopNbr in myTroops)
                {
                    // If this troop is idle, give him something to attack
                    UnitInfo? troopInfo = state.GetUnit(troopNbr);
                    if (troopInfo.HasValue && troopInfo.Value.CurrentAction == UnitAction.IDLE)
                    {
                        // If there are archers to attack
                        if (enemyArchers.Count > 0)
                        {
                            actions.Attack(troopNbr, enemyArchers[_rng.Next(0, enemyArchers.Count)]);
                        }
                        // If there are warriors to attack
                        else if (enemyWarriors.Count > 0)
                        {
                            actions.Attack(troopNbr, enemyWarriors[_rng.Next(0, enemyWarriors.Count)]);
                        }
                        // If there are pawns to attack
                        else if (enemyPawns.Count > 0)
                        {
                            actions.Attack(troopNbr, enemyPawns[_rng.Next(0, enemyPawns.Count)]);
                        }
                        // If there are bases to attack
                        else if (enemyBases.Count > 0)
                        {
                            actions.Attack(troopNbr, enemyBases[_rng.Next(0, enemyBases.Count)]);
                        }
                        // If there are barracks to attack
                        else if (enemyBarracks.Count > 0)
                        {
                            actions.Attack(troopNbr, enemyBarracks[_rng.Next(0, enemyBarracks.Count)]);
                        }
                        else if (enemyArchery.Count > 0)
                        {
                            actions.Attack(troopNbr, enemyArchery[_rng.Next(0, enemyArchery.Count)]);
                        }
                        else if (enemyLancers.Count > 0)
                        {
                            actions.Attack(troopNbr, enemyLancers[_rng.Next(0, enemyLancers.Count)]);
                        }
                        else if (enemyTowers.Count > 0)
                        {
                            actions.Attack(troopNbr, enemyTowers[_rng.Next(0, enemyTowers.Count)]);
                        }
                    }
                }
            }
            else if (myTroops.Count > 0)
            {
                // Find a good rally point
                Position rallyPoint = new Position(0, 0);
                foreach (Position toBuild in buildPositions)
                {
                    if (state.IsAreaBuildable(UnitType.BASE, toBuild))
                    {
                        rallyPoint = toBuild;
                        // For each of my troops in this collection
                        foreach (int troopNbr in myTroops)
                        {
                            // If this troop is idle, give him something to attack
                            UnitInfo? troopInfo = state.GetUnit(troopNbr);
                            if (troopInfo.HasValue && troopInfo.Value.CurrentAction == UnitAction.IDLE)
                            {
                                actions.Move(troopNbr, rallyPoint);
                            }
                        }
                        break;
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
        public override void Learn(IGameState state)
        {
            // Learning logic removed — no actions parameter available for logging
        }

        /// <summary>
        /// Called before each match between two agents.  Matches have
        /// multiple rounds.
        /// </summary>
        public override void InitializeMatch()
        {
            // Match initialization
        }

        /// <summary>
        /// Called at the beginning of each round in a match.
        /// There are multiple rounds in a single match between two agents.
        /// </summary>
        public override void InitializeRound(IGameState state)
        {
            base.InitializeRound(state);

            // Set the main mine and base to "non-existent"
            mainMineNbr = -1;
            mainBaseNbr = -1;
        }

        /// <summary>
        /// Update the GameManager - called once per frame
        /// </summary>
        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);

            if (mines.Count > 0)
            {
                mainMineNbr = mines[0];
            }
            else
            {
                mainMineNbr = -1;
            }

            // If we have at least one base, assume the first one is our "main" base
            if (myBases.Count > 0)
            {
                mainBaseNbr = myBases[0];
            }

            // If we don't have any bases, build a base
            if (myBases.Count == 0)
            {
                mainBaseNbr = -1;

                BuildBuilding(UnitType.BASE, state, actions);
            }

            // If we don't have any barracks, build a barracks
            if (myBarracks.Count == 0)
            {
                BuildBuilding(UnitType.BARRACKS, state, actions);
            }

            // For any troops, attack the enemy
            AttackEnemy(myWarriors, state, actions);
            AttackEnemy(myArchers, state, actions);

            // For each barracks, determine if it should train a warrior or an archer
            foreach (int barracksNbr in myBarracks)
            {
                // Get the barracks
                UnitInfo? barracksInfo = state.GetUnit(barracksNbr);

                // If this barracks still exists, is idle, we need warriors, and have gold
                if (barracksInfo.HasValue && barracksInfo.Value.IsBuilt
                    && barracksInfo.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
                {
                    actions.Train(barracksNbr, UnitType.WARRIOR);
                }
            }

            // For each base, determine if it should train a pawn
            foreach (int baseNbr in myBases)
            {
                // Get the base unit
                UnitInfo? baseInfo = state.GetUnit(baseNbr);

                // If the base exists, is idle, we need a pawn, and we have gold
                if (baseInfo.HasValue && baseInfo.Value.IsBuilt
                                     && baseInfo.Value.CurrentAction == UnitAction.IDLE
                                     && state.MyGold >= GameConstants.COST[UnitType.PAWN]
                                     && myPawns.Count < MAX_NBR_PAWNS)
                {
                    actions.Train(baseNbr, UnitType.PAWN);
                }
            }

            // For each pawn
            foreach (int pawn in myPawns)
            {
                // Grab the unit we need for this function
                UnitInfo? unitInfo = state.GetUnit(pawn);

                // Make sure this unit actually exists and is idle
                if (unitInfo.HasValue && unitInfo.Value.CurrentAction == UnitAction.IDLE && mainBaseNbr >= 0 && mainMineNbr >= 0)
                {
                    // Grab the mine
                    UnitInfo? mineInfo = state.GetUnit(mainMineNbr);
                    UnitInfo? baseInfo = state.GetUnit(mainBaseNbr);
                    if (mineInfo.HasValue && baseInfo.HasValue && mineInfo.Value.Health > 0)
                    {
                        actions.Gather(pawn, mainMineNbr, mainBaseNbr);
                    }
                }
            }
        }

        #endregion
    }
}
