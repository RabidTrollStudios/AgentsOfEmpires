using System;
using System.Collections.Generic;
using AgentSDK;

/////////////////////////////////////////////////////////////////////////////
// This is the Dummy Agent
/////////////////////////////////////////////////////////////////////////////

namespace PlanningAgent
{
    /// <summary>
    /// A simple dummy agent for testing purposes.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_NBR_WORKERS = 20;

        private Random rng = new Random();

        #region Private Data

        /// <summary>
        /// Build a building
        /// </summary>
        public void BuildBuilding(UnitType unitType, IGameState state, IAgentActions actions)
        {
            // For each worker
            foreach (int worker in myWorkers)
            {
                // Get the unit info
                UnitInfo? unitInfo = state.GetUnit(worker);

                // Make sure this unit actually exists and we have enough gold
                if (unitInfo.HasValue && state.MyGold >= GameConstants.COST[unitType])
                {
                    // Find the closest build position to this worker's position (DUMB) and
                    // build the base there
                    foreach (Position toBuild in buildPositions)
                    {
                        if (state.IsBoundedAreaBuildable(unitType, toBuild))
                        {
                            actions.Build(worker, toBuild, unitType);
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Attack the enemy
        /// </summary>
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
                            actions.Attack(troopNbr, enemyArchers[rng.Next(0, enemyArchers.Count)]);
                        }
                        // If there are soldiers to attack
                        else if (enemySoldiers.Count > 0)
                        {
                            actions.Attack(troopNbr, enemySoldiers[rng.Next(0, enemySoldiers.Count)]);
                        }
                        // If there are workers to attack
                        else if (enemyWorkers.Count > 0)
                        {
                            actions.Attack(troopNbr, enemyWorkers[rng.Next(0, enemyWorkers.Count)]);
                        }
                        // If there are bases to attack
                        else if (enemyBases.Count > 0)
                        {
                            actions.Attack(troopNbr, enemyBases[rng.Next(0, enemyBases.Count)]);
                        }
                        // If there are barracks to attack
                        else if (enemyBarracks.Count > 0)
                        {
                            actions.Attack(troopNbr, enemyBarracks[rng.Next(0, enemyBarracks.Count)]);
                        }
                        // If there are refineries to attack
                        else if (enemyRefineries.Count > 0)
                        {
                            actions.Attack(troopNbr, enemyRefineries[rng.Next(0, enemyRefineries.Count)]);
                        }
                    }
                }
            }
            else if (myTroops.Count > 0)
            {
                // Find a good rally point
                foreach (Position toBuild in buildPositions)
                {
                    if (state.IsBoundedAreaBuildable(UnitType.BASE, toBuild))
                    {
                        // For each of my troops in this collection
                        foreach (int troopNbr in myTroops)
                        {
                            UnitInfo? troopInfo = state.GetUnit(troopNbr);
                            if (troopInfo.HasValue && troopInfo.Value.CurrentAction == UnitAction.IDLE)
                            {
                                actions.Move(troopNbr, toBuild);
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
        /// Called before each match between two agents.
        /// </summary>
        public override void InitializeMatch()
        {
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

            // If we don't have any barracks, build a barracks (requires a built base)
            if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
            {
                BuildBuilding(UnitType.BARRACKS, state, actions);
            }

            // If we don't have any refineries, build a refinery (requires built base + barracks)
            if (myRefineries.Count == 0 && HasBuiltUnit(myBases, state) && HasBuiltUnit(myBarracks, state))
            {
                BuildBuilding(UnitType.REFINERY, state, actions);
            }

            // For any troops, attack the enemy
            AttackEnemy(mySoldiers, state, actions);
            AttackEnemy(myArchers, state, actions);

            // For each barracks, determine if it should train a soldier or an archer
            foreach (int barracksNbr in myBarracks)
            {
                UnitInfo? barracksInfo = state.GetUnit(barracksNbr);

                if (barracksInfo.HasValue && barracksInfo.Value.IsBuilt
                         && barracksInfo.Value.CurrentAction == UnitAction.IDLE
                         && state.MyGold >= GameConstants.COST[UnitType.ARCHER])
                {
                    actions.Train(barracksNbr, UnitType.ARCHER);
                }
                if (barracksInfo.HasValue && barracksInfo.Value.IsBuilt
                    && barracksInfo.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.SOLDIER])
                {
                    actions.Train(barracksNbr, UnitType.SOLDIER);
                }
            }

            // For each base, determine if it should train a worker
            foreach (int baseNbr in myBases)
            {
                UnitInfo? baseInfo = state.GetUnit(baseNbr);

                if (baseInfo.HasValue && baseInfo.Value.IsBuilt
                                     && baseInfo.Value.CurrentAction == UnitAction.IDLE
                                     && state.MyGold >= GameConstants.COST[UnitType.WORKER]
                                     && myWorkers.Count < MAX_NBR_WORKERS)
                {
                    actions.Train(baseNbr, UnitType.WORKER);
                }
            }

            // For each worker
            foreach (int worker in myWorkers)
            {
                UnitInfo? unitInfo = state.GetUnit(worker);

                if (unitInfo.HasValue && unitInfo.Value.CurrentAction == UnitAction.IDLE && mainBaseNbr >= 0 && mainMineNbr >= 0)
                {
                    UnitInfo? mineInfo = state.GetUnit(mainMineNbr);
                    UnitInfo? baseInfo = state.GetUnit(mainBaseNbr);
                    if (mineInfo.HasValue && baseInfo.HasValue && mineInfo.Value.Health > 0)
                    {
                        actions.Gather(worker, mainMineNbr, mainBaseNbr);
                    }
                }
            }
        }

        #endregion
    }
}
