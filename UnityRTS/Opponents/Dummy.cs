using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [MEDIUM] Random-decision agent using heuristic scoring.
    /// Trains up to 10 pawns and 10 warriors (no archers despite having constants).
    /// Selects attack targets randomly from available enemy types.
    /// Uses FindClosestBuildPosition and FindClosestUnit helpers for spatial decisions.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_NBR_ARCHERS = 0;
        private const float MAX_ARCHER_MULTIPLIER = 1.5f;
        private const int MAX_NBR_WARRIORS = 10;
        private const float MAX_WARRIOR_MULTIPLIER = 1.5f;
        private const int MAX_NBR_PAWNS = 10;

        private bool lastFighterWasWarrior = false;
        private System.Random rng = new System.Random();

        #region Private Data

        /// <summary>
        /// Finds the closest build position to the gridPosition.
        /// </summary>
        public Position FindClosestBuildPosition(Position gridPosition, UnitType unitType, IGameState state)
        {
            float minDist = float.MaxValue;
            Position minBuildPosition = gridPosition;

            foreach (Position buildPosition in buildPositions)
            {
                if (Position.Distance(gridPosition, buildPosition) < minDist &&
                    state.IsBoundedAreaBuildable(unitType, buildPosition))
                {
                    minDist = Position.Distance(gridPosition, buildPosition);
                    minBuildPosition = buildPosition;
                }
            }

            return minBuildPosition;
        }

        /// <summary>
        /// Find the closest unit to the gridPosition out of a list of units.
        /// </summary>
        public int FindClosestUnit(Position gridPosition, List<int> unitNbrs, IGameState state)
        {
            int closestUnitNbr = -1;
            float closestUnitDist = float.MaxValue;

            foreach (int unitNbr in unitNbrs)
            {
                UnitInfo? unitInfo = state.GetUnit(unitNbr);
                if (!unitInfo.HasValue) continue;
                float unitDist = Position.Distance(unitInfo.Value.GridPosition, gridPosition);

                if (!(unitDist < closestUnitDist)) continue;
                closestUnitDist = unitDist;
                closestUnitNbr = unitNbr;
            }

            return closestUnitNbr;
        }

        // Process the pawns
        public void ProcessPawns(IGameState state, IAgentActions actions)
        {
            foreach (int pawn in myPawns)
            {
                UnitInfo? unitInfo = state.GetUnit(pawn);

                if (unitInfo.HasValue && unitInfo.Value.CurrentAction == UnitAction.IDLE)
                {
                    if (state.MyGold >= GameConstants.COST[UnitType.BASE]
                        && myBases.Count < 1)
                    {
                        Position toBuild = FindClosestBuildPosition(unitInfo.Value.GridPosition, UnitType.BASE, state);
                        if (toBuild != Position.Zero)
                        {
                            actions.Build(pawn, toBuild, UnitType.BASE);
                        }
                    }
                    else if (state.MyGold >= GameConstants.COST[UnitType.BARRACKS]
                             && myBarracks.Count < 1)
                    {
                        Position toBuild = FindClosestBuildPosition(unitInfo.Value.GridPosition, UnitType.BARRACKS, state);
                        if (toBuild != Position.Zero)
                        {
                            actions.Build(pawn, toBuild, UnitType.BARRACKS);
                        }
                    }
                    else if (mainBaseNbr >= 0 && mainMineNbr >= 0)
                    {
                        UnitInfo? mineUnit = state.GetUnit(mainMineNbr);
                        UnitInfo? baseUnit = state.GetUnit(mainBaseNbr);
                        if (mineUnit.HasValue && baseUnit.HasValue)
                        {
                            actions.Gather(pawn, mainMineNbr, mainBaseNbr);
                        }
                    }
                }
            }
        }

        // Process the bases
        public void ProcessBases(IGameState state, IAgentActions actions)
        {
            foreach (int baseNbr in myBases)
            {
                UnitInfo? baseUnit = state.GetUnit(baseNbr);

                if (baseUnit.HasValue && baseUnit.Value.IsBuilt
                                      && baseUnit.Value.CurrentAction == UnitAction.IDLE && myPawns.Count < MAX_NBR_PAWNS
                                      && state.MyGold >= GameConstants.COST[UnitType.PAWN])
                {
                    actions.Train(baseNbr, UnitType.PAWN);
                }
            }
        }

        // Process the barracks
        public void ProcessBarracks(IGameState state, IAgentActions actions)
        {
            foreach (int barracksNbr in myBarracks)
            {
                UnitInfo? barracksUnit = state.GetUnit(barracksNbr);

                if (!lastFighterWasWarrior && barracksUnit.HasValue && barracksUnit.Value.IsBuilt
                    && barracksUnit.Value.CurrentAction == UnitAction.IDLE
                    && (myWarriors.Count < MAX_NBR_WARRIORS
                        || myWarriors.Count <= enemyWarriors.Count * MAX_WARRIOR_MULTIPLIER)
                    && state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
                {
                    actions.Train(barracksNbr, UnitType.WARRIOR);
                    lastFighterWasWarrior = !lastFighterWasWarrior;
                }
            }
        }

        // Process the warriors
        public void ProcessWarriors(IGameState state, IAgentActions actions)
        {
            foreach (int warriorNbr in myWarriors)
            {
                UnitInfo? warriorUnit = state.GetUnit(warriorNbr);

                if (warriorUnit.HasValue && warriorUnit.Value.CurrentAction == UnitAction.IDLE)
                {
                    if (enemyWarriors.Count > 0)
                    {
                        actions.Attack(warriorNbr,
                            enemyWarriors[rng.Next(0, enemyWarriors.Count)]);
                    }
                    else if (enemyArchers.Count > 0)
                    {
                        actions.Attack(warriorNbr,
                            enemyArchers[rng.Next(0, enemyArchers.Count)]);
                    }
                    else if (enemyPawns.Count > 0)
                    {
                        actions.Attack(warriorNbr,
                            enemyPawns[rng.Next(0, enemyPawns.Count)]);
                    }
                    else if (enemyBases.Count > 0)
                    {
                        actions.Attack(warriorNbr,
                            enemyBases[rng.Next(0, enemyBases.Count)]);
                    }
                    else if (enemyBarracks.Count > 0)
                    {
                        actions.Attack(warriorNbr,
                            enemyBarracks[rng.Next(0, enemyBarracks.Count)]);
                    }
                    else if (enemyArchery.Count > 0)
                    {
                        actions.Attack(warriorNbr, enemyArchery[rng.Next(0, enemyArchery.Count)]);
                    }
                    else if (enemyLancers.Count > 0)
                    {
                        actions.Attack(warriorNbr, enemyLancers[rng.Next(0, enemyLancers.Count)]);
                    }
                    else if (enemyTowers.Count > 0)
                    {
                        actions.Attack(warriorNbr, enemyTowers[rng.Next(0, enemyTowers.Count)]);
                    }
                }
            }
        }

        // Process archers
        public void ProcessArchers(IGameState state, IAgentActions actions)
        {
            foreach (int archerNbr in myArchers)
            {
                UnitInfo? archerUnit = state.GetUnit(archerNbr);

                if (archerUnit.HasValue && archerUnit.Value.CurrentAction == UnitAction.IDLE)
                {
                    if (enemyWarriors.Count > 0)
                    {
                        actions.Attack(archerNbr,
                            enemyWarriors[rng.Next(0, enemyWarriors.Count)]);
                    }
                    else if (enemyArchers.Count > 0)
                    {
                        actions.Attack(archerNbr,
                            enemyArchers[rng.Next(0, enemyArchers.Count)]);
                    }
                    else if (enemyPawns.Count > 0)
                    {
                        actions.Attack(archerNbr,
                            enemyPawns[rng.Next(0, enemyPawns.Count)]);
                    }
                    else if (enemyBases.Count > 0)
                    {
                        actions.Attack(archerNbr,
                            enemyBases[rng.Next(0, enemyBases.Count)]);
                    }
                    else if (enemyBarracks.Count > 0)
                    {
                        actions.Attack(archerNbr,
                            enemyBarracks[rng.Next(0, enemyBarracks.Count)]);
                    }
                    else if (enemyArchery.Count > 0)
                    {
                        actions.Attack(archerNbr, enemyArchery[rng.Next(0, enemyArchery.Count)]);
                    }
                    else if (enemyLancers.Count > 0)
                    {
                        actions.Attack(archerNbr, enemyLancers[rng.Next(0, enemyLancers.Count)]);
                    }
                    else if (enemyTowers.Count > 0)
                    {
                        actions.Attack(archerNbr, enemyTowers[rng.Next(0, enemyTowers.Count)]);
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
        }

        /// <summary>
        /// Called before each match between two agents.  Matches have
        /// multiple rounds.
        /// </summary>
        public override void InitializeMatch()
        {
        }

        /// <summary>
        /// Called at the beginning of each round in a match.
        /// There are multiple rounds in a single match between two agents.
        /// </summary>
        public override void InitializeRound(IGameState state)
        {
            base.InitializeRound(state);

            lastFighterWasWarrior = false;
        }

        // Update the GameManager - called once per frame
        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);

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
                UnitInfo? baseUnit = state.GetUnit(mainBaseNbr);
                if (baseUnit.HasValue)
                {
                    mainMineNbr = FindClosestUnit(baseUnit.Value.GridPosition, mines, state);
                }
            }

            // Process all of the units, prioritize building new structures over
            // training units in terms of spending gold
            ProcessPawns(state, actions);

            ProcessWarriors(state, actions);

            ProcessArchers(state, actions);

            ProcessBarracks(state, actions);

            ProcessBases(state, actions);
        }

        #endregion
    }
}
