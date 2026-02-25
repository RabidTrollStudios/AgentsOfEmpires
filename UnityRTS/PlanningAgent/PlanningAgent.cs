using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace PlanningAgent
{
    ///<summary>Planning Agent is the over-head planner that decided where
    /// individual units go and what tasks they perform.  Low-level
    /// AI is handled by other classes (like pathfinding).
    ///</summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int WORKERS_PER_BASE = 8;
        private const int MAX_NBR_BASES = 2;
        private const int MAX_NBR_BARRACKS = 5;
        private const int MAX_NBR_REFINERIES = 2;
        private const int MAX_NBR_SOLDIERS = 40;
        private const int MAX_NBR_ARCHERS = 10;

        #region Private Data

        private int secondMineNbr = -1;
        private int secondaryBaseNbr = -1;

        private Random rng = new Random();

        private enum GameState
        {
            BaseBuilding,
            ArmyBuilding,
            Attacking
        }

        private GameState currState = GameState.BaseBuilding;

        // Column indices for stateThresholds
        private const int MIN_WORKERS = 0, MAX_WORKERS = 1;
        private const int MIN_SOLDIERS = 2, MAX_SOLDIERS = 3;
        private const int MIN_ARCHERS = 4, MAX_ARCHERS = 5;
        private const int MIN_BASES = 6, MAX_BASES = 7;
        private const int MIN_REFINERIES = 8, MAX_REFINERIES = 9;
        private const int MIN_BARRACKS = 10, MAX_BARRACKS = 11;

        private static readonly int[,] stateThresholds = new int[,]
        {
        //                                    Workers     Soldiers    Archers     Bases       Refineries  Barracks
        //                                    min  max    min  max    min  max    min  max    min  max    min  max
            /* BaseBuilding */ {               0,   6,     0,   0,     0,   0,     0,   1,     0,   0,     0,   1  },
            /* ArmyBuilding */ {               6,   8,     0,   5,     0,   5,     1,   1,     0,   0,     1,   1  },
            /* Attacking     */ {              1, int.MaxValue, 1, int.MaxValue, 1, int.MaxValue, 1, int.MaxValue, 0, int.MaxValue, 1, int.MaxValue }
        };

        private Position unitToBuildAroundPos = Position.Zero;

        private Position buildPos = Position.Zero;

        /// <summary>
        /// Evaluate whether the agent should regress (-1), stay (0), or progress (1)
        /// based on the min/max thresholds defined in stateThresholds for the given state.
        /// Returns -1 if any actual count is below its min.
        /// Returns  1 if all actual counts are at or above their max.
        /// Returns  0 otherwise.
        /// </summary>
        private int EvaluateState(GameState gameState)
        {
            int s = (int)gameState;

            // If any count is below its minimum, regress
            if (myWorkers.Count < stateThresholds[s, MIN_WORKERS] ||
                mySoldiers.Count < stateThresholds[s, MIN_SOLDIERS] ||
                myArchers.Count < stateThresholds[s, MIN_ARCHERS] ||
                myBases.Count < stateThresholds[s, MIN_BASES] ||
                myRefineries.Count < stateThresholds[s, MIN_REFINERIES] ||
                myBarracks.Count < stateThresholds[s, MIN_BARRACKS])
            {
                return -1;
            }

            // If all counts are at or above their maximum, progress
            if (myWorkers.Count >= stateThresholds[s, MAX_WORKERS] &&
                mySoldiers.Count >= stateThresholds[s, MAX_SOLDIERS] &&
                myArchers.Count >= stateThresholds[s, MAX_ARCHERS] &&
                myBases.Count >= stateThresholds[s, MAX_BASES] &&
                myRefineries.Count >= stateThresholds[s, MAX_REFINERIES] &&
                myBarracks.Count >= stateThresholds[s, MAX_BARRACKS])
            {
                return 1;
            }

            // Otherwise, stay
            return 0;
        }

        /// <summary>
        /// Build a building
        /// </summary>
        public void BuildBuilding(UnitType unitType, IGameState state, IAgentActions actions)
        {
            // Collect eligible workers (idle or gathering), then pick one at random
            var eligibleWorkers = new List<int>();
            foreach (int w in myWorkers)
            {
                UnitInfo? info = state.GetUnit(w);
                if (info.HasValue && (info.Value.CurrentAction == UnitAction.GATHER || info.Value.CurrentAction == UnitAction.IDLE))
                    eligibleWorkers.Add(w);
            }
            if (eligibleWorkers.Count == 0) return;
            int worker = eligibleWorkers[rng.Next(eligibleWorkers.Count)];
            {
                UnitInfo? unitInfo = state.GetUnit(worker);

                if (unitInfo.HasValue)
                {
                    Position unitPos = unitInfo.Value.GridPosition;

                    if (unitType == UnitType.BASE)
                    {
                        // Find closest buildable position at least 2 cells from the mine
                        // (unitToBuildAroundPos set by caller to mine position)
                        float bestDist = float.MaxValue;
                        for (int i = 0; i < buildPositions.Count; i++)
                        {
                            Position candidate = buildPositions[i];
                            float distToMine = Position.Distance(unitToBuildAroundPos, candidate);
                            if (distToMine >= 2 && distToMine < bestDist
                                && state.IsBoundedAreaBuildable(unitType, candidate))
                            {
                                bestDist = distToMine;
                                buildPos = candidate;
                            }
                        }

                        // Fallback: if no position >= 2 cells, take closest buildable position to the mine
                        if (bestDist == float.MaxValue)
                        {
                            for (int i = 0; i < buildPositions.Count; i++)
                            {
                                Position candidate = buildPositions[i];
                                float distToMine = Position.Distance(unitToBuildAroundPos, candidate);
                                if (distToMine < bestDist
                                    && state.IsBoundedAreaBuildable(unitType, candidate))
                                {
                                    bestDist = distToMine;
                                    buildPos = candidate;
                                }
                            }
                        }
                    }
                    else
                    {
                        // For non-BASE buildings, find closest buildable position to unitToBuildAroundPos
                        float bestDist = float.MaxValue;
                        for (int i = 0; i < buildPositions.Count; i++)
                        {
                            Position candidate = buildPositions[i];
                            float distToTarget = Position.Distance(unitToBuildAroundPos, candidate);
                            if (distToTarget < bestDist
                                && state.IsBoundedAreaBuildable(unitType, candidate))
                            {
                                bestDist = distToTarget;
                                buildPos = candidate;
                            }
                        }
                    }

                    if (state.IsBoundedAreaBuildable(unitType, buildPos))
                    {
                        actions.Build(worker, buildPos, unitType);
                    }
                }
            }
        }

        /// <summary>
        /// Move idle troops that are near a barracks to a nearby build position so they don't block spawns.
        /// </summary>
        private void RallyIdleTroops(IGameState state, IAgentActions actions)
        {
            if (myBarracks.Count == 0) return;

            const int CANDIDATE_COUNT = 7;
            const float CLOSENESS_WEIGHT = 4f;

            // Compute midpoint of all barracks
            int sumX = 0, sumY = 0, count = 0;
            foreach (int bNbr in myBarracks)
            {
                UnitInfo? bInfo = state.GetUnit(bNbr);
                if (!bInfo.HasValue) continue;
                sumX += bInfo.Value.GridPosition.X;
                sumY += bInfo.Value.GridPosition.Y;
                count++;
            }
            if (count == 0) return;
            Position barracksMidpoint = new Position(sumX / count, sumY / count);

            // Compute midpoint of base and mine
            UnitInfo? baseInfo = state.GetUnit(mainBaseNbr);
            Position basePos = baseInfo.HasValue ? baseInfo.Value.GridPosition : barracksMidpoint;
            Position minePos = barracksMidpoint;
            if (mainMineNbr >= 0)
            {
                UnitInfo? mineInfo = state.GetUnit(mainMineNbr);
                if (mineInfo.HasValue)
                    minePos = mineInfo.Value.GridPosition;
            }
            Position econMidpoint = new Position((basePos.X + minePos.X) / 2, (basePos.Y + minePos.Y) / 2);

            // Select the 7 closest build positions to the barracks midpoint
            var candidates = new List<(Position pos, float dist)>();
            for (int i = 0; i < buildPositions.Count; i++)
            {
                float d = Position.Distance(barracksMidpoint, buildPositions[i]);
                candidates.Add((buildPositions[i], d));
            }
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
            if (candidates.Count > CANDIDATE_COUNT)
                candidates.RemoveRange(CANDIDATE_COUNT, candidates.Count - CANDIDATE_COUNT);

            // Score: favor closeness to barracks midpoint, favor distance from base/mine midpoint
            float bestScore = float.MinValue;
            Position rallyPoint = barracksMidpoint;
            bool foundRally = false;

            foreach (var (pos, distFromBarracks) in candidates)
            {
                float distFromEcon = Position.Distance(pos, econMidpoint);
                float score = distFromEcon - CLOSENESS_WEIGHT * distFromBarracks;
                if (score > bestScore)
                {
                    bestScore = score;
                    rallyPoint = pos;
                    foundRally = true;
                }
            }

            if (!foundRally) return;

            var idleTroops = new List<int>();
            idleTroops.AddRange(mySoldiers);
            idleTroops.AddRange(myArchers);

            foreach (int troopNbr in idleTroops)
            {
                UnitInfo? troopInfo = state.GetUnit(troopNbr);
                if (!troopInfo.HasValue || troopInfo.Value.CurrentAction != UnitAction.IDLE)
                    continue;

                // Use random spread so units don't all target the same cell
                int offsetX = rng.Next(-4, 5);
                int offsetY = rng.Next(-4, 5);
                int tx = Math.Max(0, Math.Min(state.MapSize.X - 1, rallyPoint.X + offsetX));
                int ty = Math.Max(0, Math.Min(state.MapSize.Y - 1, rallyPoint.Y + offsetY));
                Position target = new Position(tx, ty);
                if (!state.IsPositionBuildable(target))
                    target = rallyPoint;

                // Try to move toward rally point using buildable pathfinding
                // so units don't try to walk through other rallying troops
                var path = state.GetPathBetween(troopInfo.Value.GridPosition, target, avoidUnits: true);
                if (path.Count > 0)
                    actions.Move(troopNbr, target);
            }
        }

        /// <summary>
        /// Defend: only attack enemies that are within a radius of our buildings or workers.
        /// Each idle troop attacks the closest threatening enemy.
        /// </summary>
        private void DefendBase(IGameState state, IAgentActions actions)
        {
            const float DEFEND_RADIUS = 10f;

            DefendWithTroops(mySoldiers, DEFEND_RADIUS, state, actions);
            DefendWithTroops(myArchers, DEFEND_RADIUS, state, actions);
        }

        private void DefendWithTroops(List<int> troops, float radius, IGameState state, IAgentActions actions)
        {
            foreach (int troopNbr in troops)
            {
                UnitInfo? troopInfo = state.GetUnit(troopNbr);
                if (!troopInfo.HasValue || troopInfo.Value.CurrentAction != UnitAction.IDLE)
                    continue;

                Position troopPos = troopInfo.Value.GridPosition;
                float closestDist = float.MaxValue;
                int closestEnemy = -1;

                // Check all enemy combat units and workers for threats near our stuff
                FindClosestThreat(enemySoldiers, troopPos, radius, state, ref closestDist, ref closestEnemy);
                FindClosestThreat(enemyArchers, troopPos, radius, state, ref closestDist, ref closestEnemy);
                FindClosestThreat(enemyWorkers, troopPos, radius, state, ref closestDist, ref closestEnemy);

                if (closestEnemy != -1)
                    actions.Attack(troopNbr, closestEnemy);
            }
        }

        /// <summary>
        /// Find the closest enemy (to the troop) that is within radius of any of our buildings or workers.
        /// </summary>
        private void FindClosestThreat(List<int> enemies, Position troopPos, float radius,
            IGameState state, ref float closestDist, ref int closestEnemy)
        {
            foreach (int enemy in enemies)
            {
                UnitInfo? enemyInfo = state.GetUnit(enemy);
                if (!enemyInfo.HasValue) continue;
                Position enemyPos = enemyInfo.Value.CenterPosition;

                if (!IsNearOurUnits(enemyPos, radius, state)) continue;

                float dist = Position.Distance(troopPos, enemyPos);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestEnemy = enemy;
                }
            }
        }

        private bool IsNearOurUnits(Position enemyPos, float radius, IGameState state)
        {
            foreach (int unit in myBases)
            {
                UnitInfo? info = state.GetUnit(unit);
                if (info.HasValue && Position.Distance(info.Value.CenterPosition, enemyPos) <= radius)
                    return true;
            }
            foreach (int unit in myBarracks)
            {
                UnitInfo? info = state.GetUnit(unit);
                if (info.HasValue && Position.Distance(info.Value.CenterPosition, enemyPos) <= radius)
                    return true;
            }
            foreach (int unit in myRefineries)
            {
                UnitInfo? info = state.GetUnit(unit);
                if (info.HasValue && Position.Distance(info.Value.CenterPosition, enemyPos) <= radius)
                    return true;
            }
            foreach (int unit in myWorkers)
            {
                UnitInfo? info = state.GetUnit(unit);
                if (info.HasValue && Position.Distance(info.Value.CenterPosition, enemyPos) <= radius)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Split targeting by unit type: soldiers charge closest enemy (melee),
        /// archers prefer closest enemy within their attack range to avoid
        /// competing for the same cells as soldiers.
        /// </summary>
        private void CoordinatedAttack(IGameState state, IAgentActions actions)
        {
            // Build combined enemy lists
            var enemyTroops = new List<int>();
            enemyTroops.AddRange(enemySoldiers);
            enemyTroops.AddRange(enemyArchers);
            enemyTroops.AddRange(enemyWorkers);

            var enemyBuildings = new List<int>();
            enemyBuildings.AddRange(enemyBases);
            enemyBuildings.AddRange(enemyBarracks);
            enemyBuildings.AddRange(enemyRefineries);

            float archerRange = GameConstants.ATTACK_RANGE[UnitType.ARCHER];

            // Soldiers — melee chargers, attack closest enemy regardless of distance
            foreach (int soldierNbr in mySoldiers)
            {
                UnitInfo? info = state.GetUnit(soldierNbr);
                if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) continue;

                Position pos = info.Value.GridPosition;
                int target = FindClosestTo(enemyTroops, pos, state);
                if (target == -1)
                    target = FindClosestTo(enemyBuildings, pos, state);
                if (target != -1)
                    actions.Attack(soldierNbr, target);
            }

            // Archers — ranged, prefer closest enemy already within attack range
            foreach (int archerNbr in myArchers)
            {
                UnitInfo? info = state.GetUnit(archerNbr);
                if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) continue;

                Position pos = info.Value.GridPosition;

                // First try to find an enemy troop within attack range
                int target = FindClosestWithinRange(enemyTroops, pos, archerRange, state);

                // If none in range, try a building within range
                if (target == -1)
                    target = FindClosestWithinRange(enemyBuildings, pos, archerRange, state);

                // Fallback: charge closest enemy troop, then building
                if (target == -1)
                    target = FindClosestTo(enemyTroops, pos, state);
                if (target == -1)
                    target = FindClosestTo(enemyBuildings, pos, state);

                if (target != -1)
                    actions.Attack(archerNbr, target);
            }
        }

        /// <summary>
        /// Find the closest enemy to a given position.
        /// </summary>
        private int FindClosestTo(List<int> enemies, Position pos, IGameState state)
        {
            float closestDist = float.MaxValue;
            int closest = -1;

            foreach (int enemy in enemies)
            {
                UnitInfo? info = state.GetUnit(enemy);
                if (!info.HasValue) continue;
                float dist = Position.Distance(pos, info.Value.CenterPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = enemy;
                }
            }
            return closest;
        }

        /// <summary>
        /// Find the closest enemy within a maximum range of a given position.
        /// Returns -1 if no enemy is within range.
        /// </summary>
        private int FindClosestWithinRange(List<int> enemies, Position pos, float maxRange, IGameState state)
        {
            float closestDist = float.MaxValue;
            int closest = -1;

            foreach (int enemy in enemies)
            {
                UnitInfo? info = state.GetUnit(enemy);
                if (!info.HasValue) continue;
                float dist = Position.Distance(pos, info.Value.CenterPosition);
                if (dist <= maxRange && dist < closestDist)
                {
                    closestDist = dist;
                    closest = enemy;
                }
            }
            return closest;
        }

        private int FindClosestToBase(List<int> enemies, IGameState state)
        {
            UnitInfo? baseInfo = state.GetUnit(mainBaseNbr);
            if (!baseInfo.HasValue) return enemies.Count > 0 ? enemies[0] : -1;

            Position basePos = baseInfo.Value.GridPosition;
            float closestDist = float.MaxValue;
            int closest = -1;

            foreach (int enemy in enemies)
            {
                UnitInfo? info = state.GetUnit(enemy);
                if (!info.HasValue) continue;
                float dist = Position.Distance(baseInfo.Value.CenterPosition, info.Value.CenterPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = enemy;
                }
            }
            return closest;
        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Called before each match between two agents.  Matches have
        /// multiple rounds.
        /// </summary>
        public override void InitializeMatch()
        {
        }

        /// <summary>
        /// Called at the beginning of each round in a match.
        /// </summary>
        public override void InitializeRound(IGameState state)
        {
            base.InitializeRound(state);
            currState = GameState.BaseBuilding;
        }

        /// <summary>
        /// Update the GameManager - called once per frame
        /// </summary>
        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);

            switch (currState)
            {
                case GameState.BaseBuilding:
                    UpdateBaseBuilding(state, actions);
                    DefendBase(state, actions);
                    break;
                case GameState.ArmyBuilding:
                    UpdateArmyBuilding(state, actions);
                    DefendBase(state, actions);
                    break;
                case GameState.Attacking:
                    UpdateAttacking(state, actions);
                    CoordinatedAttack(state, actions);
                    // Rally any troops still idle after attack orders (e.g. newly spawned with no enemies nearby)
                    RallyIdleTroops(state, actions);
                    break;
            }
        }

        private void UpdateBaseBuilding(IGameState state, IAgentActions actions)
        {
            // Progress to ArmyBuilding once we meet BaseBuilding max thresholds
            int eval = EvaluateState(GameState.BaseBuilding);
            if (eval == 1)
            {
                currState = GameState.ArmyBuilding;
                return;
            }

            // Assign main mine
            if (mines.Count > 0)
            {
                if (mainMineNbr == -1)
                {
                    // Can't proceed without workers
                    if (myWorkers.Count == 0) return;

                    UnitInfo? workerInfo = state.GetUnit(myWorkers[0]);
                    if (!workerInfo.HasValue) return;
                    Position workerPos = workerInfo.Value.GridPosition;

                    // Find closest mine by path length from starting worker
                    int bestMine = -1;
                    int bestPathLen = int.MaxValue;
                    foreach (int mineNbr in mines)
                    {
                        UnitInfo? mineInfo = state.GetUnit(mineNbr);
                        if (!mineInfo.HasValue || mineInfo.Value.Health <= 0) continue;
                        int pathLen = state.GetPathToUnit(workerPos, UnitType.MINE, mineInfo.Value.GridPosition).Count;
                        if (pathLen > 0 && pathLen < bestPathLen)
                        {
                            bestPathLen = pathLen;
                            bestMine = mineNbr;
                        }
                    }

                    // Euclidean distance fallback if no path was found
                    if (bestMine == -1)
                    {
                        float bestDist = float.MaxValue;
                        foreach (int mineNbr in mines)
                        {
                            UnitInfo? mineInfo = state.GetUnit(mineNbr);
                            if (!mineInfo.HasValue || mineInfo.Value.Health <= 0) continue;
                            float dist = Position.Distance(workerPos, mineInfo.Value.CenterPosition);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestMine = mineNbr;
                            }
                        }
                    }

                    mainMineNbr = bestMine;
                    if (mainMineNbr == -1) return;
                }
                // If main mine doesn't exist (became empty), choose a new mine to use
                else if (!state.GetUnit(mainMineNbr).HasValue)
                {
                    if (state.GetUnit(secondMineNbr).HasValue)
                    {
                        mainMineNbr = secondMineNbr;
                        secondMineNbr = -1;
                    }
                    else
                        mainMineNbr = -1;
                }
            }
            else
            {
                mainMineNbr = -1;
                secondMineNbr = -1;
            }

            if (myBases.Count == 0 && state.MyGold >= GameConstants.COST[UnitType.BASE] && mainMineNbr != -1)
            {
                UnitInfo? mineInfo = state.GetUnit(mainMineNbr);
                if (mineInfo.HasValue)
                {
                    unitToBuildAroundPos = mineInfo.Value.CenterPosition;
                    BuildBuilding(UnitType.BASE, state, actions);
                }
            }

            // If we have at least one base
            if (myBases.Count > 0)
            {
                // Assume the first one is our "main" base
                if (mainBaseNbr == -1)
                {
                    if (secondaryBaseNbr != -1)
                        secondaryBaseNbr = -1;

                    mainBaseNbr = myBases[0];
                }

                // Priority 1: Train workers
                if (myWorkers.Count < WORKERS_PER_BASE * myBases.Count)
                {
                    foreach (int baseNbr in myBases)
                    {
                        UnitInfo? baseInfo = state.GetUnit(baseNbr);
                        if (baseInfo.HasValue && baseInfo.Value.IsBuilt
                            && baseInfo.Value.CurrentAction == UnitAction.IDLE
                            && state.MyGold >= GameConstants.COST[UnitType.WORKER])
                        {
                            actions.Train(baseNbr, UnitType.WORKER);
                        }
                    }
                }

                // Priority 2: Build a barracks if we don't have one
                if (HasBuiltUnit(myBases, state) &&
                    myBarracks.Count == 0 && state.MyGold >= GameConstants.COST[UnitType.BARRACKS])
                {
                    UnitInfo? mainBaseInfo = state.GetUnit(mainBaseNbr);
                    if (mainBaseInfo.HasValue)
                    {
                        unitToBuildAroundPos = mainBaseInfo.Value.CenterPosition;
                        BuildBuilding(UnitType.BARRACKS, state, actions);
                    }
                }

            }

            Mine(state, actions);
        }

        private void UpdateArmyBuilding(IGameState state, IAgentActions actions)
        {
            // Regress to BaseBuilding or progress to Attacking based on ArmyBuilding thresholds
            int eval = EvaluateState(GameState.ArmyBuilding);
            if (eval == -1)
            {
                currState = GameState.BaseBuilding;
                return;
            }
            if (eval == 1)
            {
                currState = GameState.Attacking;
                return;
            }

            // Set all workers to mine
            Mine(state, actions);

            // Move idle troops away from barracks
            RallyIdleTroops(state, actions);

            // Build a second barracks once we can afford it
            if (myBarracks.Count < 2
                && HasBuiltUnit(myBases, state)
                && state.MyGold >= GameConstants.COST[UnitType.BARRACKS])
            {
                UnitInfo? mainBaseInfo = state.GetUnit(mainBaseNbr);
                if (mainBaseInfo.HasValue)
                {
                    unitToBuildAroundPos = mainBaseInfo.Value.CenterPosition;
                    BuildBuilding(UnitType.BARRACKS, state, actions);
                }
            }

            // For each barracks: alternate between archers and soldiers (train whichever we have fewer of)
            foreach (int barracksNbr in myBarracks)
            {
                UnitInfo? barracksInfo = state.GetUnit(barracksNbr);
                if (!barracksInfo.HasValue || !barracksInfo.Value.IsBuilt
                    || barracksInfo.Value.CurrentAction != UnitAction.IDLE)
                    continue;

                if (myArchers.Count <= mySoldiers.Count && myArchers.Count < MAX_NBR_ARCHERS)
                {
                    // Archer's turn — wait for enough gold, don't fall back to soldier
                    if (state.MyGold >= GameConstants.COST[UnitType.ARCHER])
                        actions.Train(barracksNbr, UnitType.ARCHER);
                }
                else if (state.MyGold >= GameConstants.COST[UnitType.SOLDIER])
                {
                    actions.Train(barracksNbr, UnitType.SOLDIER);
                }
            }

            // Train workers at any idle base last
            if (myWorkers.Count < WORKERS_PER_BASE * myBases.Count)
            {
                foreach (int baseNbr in myBases)
                {
                    UnitInfo? baseInfo = state.GetUnit(baseNbr);

                    if (baseInfo.HasValue && baseInfo.Value.IsBuilt
                                         && baseInfo.Value.CurrentAction == UnitAction.IDLE
                                         && state.MyGold >= GameConstants.COST[UnitType.WORKER])
                    {
                        actions.Train(baseNbr, UnitType.WORKER);
                    }
                }
            }
        }

        private void UpdateAttacking(IGameState state, IAgentActions actions)
        {
            // Regress to ArmyBuilding based on Attacking thresholds
            int eval = EvaluateState(GameState.Attacking);
            if (eval == -1)
            {
                currState = GameState.ArmyBuilding;
                return;
            }

            Mine(state, actions);

            // Train workers if below 10 per base
            if (myWorkers.Count < WORKERS_PER_BASE * myBases.Count)
            {
                foreach (int baseNbr in myBases)
                {
                    UnitInfo? baseInfo = state.GetUnit(baseNbr);
                    if (baseInfo.HasValue && baseInfo.Value.IsBuilt
                        && baseInfo.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.WORKER])
                    {
                        actions.Train(baseNbr, UnitType.WORKER);
                    }
                }
            }

            // Build refinery once we have 2 barracks
            if (HasBuiltUnit(myBases, state) && myBarracks.Count >= 2
                && myRefineries.Count < MAX_NBR_REFINERIES
                && state.MyGold >= GameConstants.COST[UnitType.REFINERY]
                && (myRefineries.Count == 0 || state.MyGold < 500))
            {
                BuildBuilding(UnitType.REFINERY, state, actions);
            }

            // Keep training attack units with remaining funds
            foreach (int barracksNbr in myBarracks)
            {
                UnitInfo? barracksInfo = state.GetUnit(barracksNbr);

                if (barracksInfo.HasValue && barracksInfo.Value.IsBuilt
                    && barracksInfo.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.ARCHER]
                    && myArchers.Count < MAX_NBR_ARCHERS)
                {
                    actions.Train(barracksNbr, UnitType.ARCHER);
                }
                else if (barracksInfo.HasValue && barracksInfo.Value.IsBuilt
                    && barracksInfo.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.SOLDIER])
                {
                    actions.Train(barracksNbr, UnitType.SOLDIER);
                }
            }

        }

        // Sends all workers to go mine
        void Mine(IGameState state, IAgentActions actions)
        {
            if (mines.Count > 0)
            {
                // For each worker
                foreach (int worker in myWorkers)
                {
                    UnitInfo? unitInfo = state.GetUnit(worker);
                    if (!unitInfo.HasValue) continue;

                    Position unitPos = unitInfo.Value.GridPosition;

                    // Make sure this unit actually exists and is idle
                    if (unitInfo.Value.CurrentAction == UnitAction.IDLE && mainBaseNbr >= 0 && mines.Count >= 0)
                    {
                        UnitInfo? mineInfo = state.GetUnit(mainMineNbr);
                        if (!mineInfo.HasValue) continue;

                        int closestMineNbr = mainMineNbr;
                        float mainMineDist = state.GetPathToUnit(unitPos, UnitType.MINE,
                            mineInfo.Value.GridPosition).Count;

                        // Grab the closest mine
                        for (int i = 0; i < mines.Count; i++)
                        {
                            UnitInfo? checkedMineInfo = state.GetUnit(mines[i]);
                            if (!checkedMineInfo.HasValue) continue;

                            Position minePos = checkedMineInfo.Value.GridPosition;
                            float mineDist = state.GetPathToUnit(unitPos, UnitType.MINE, minePos).Count;

                            if (mineDist > 0 && (mainMineDist == 0 || mineDist < mainMineDist))
                            {
                                closestMineNbr = mines[i];
                                mainMineDist = mineDist;
                            }
                        }

                        UnitInfo? baseInfo = state.GetUnit(mainBaseNbr);
                        if (!baseInfo.HasValue) continue;

                        int closestBaseNbr = mainBaseNbr;
                        float mainBaseDist = state.GetPathToUnit(unitPos, UnitType.BASE,
                            baseInfo.Value.GridPosition).Count;

                        // Grab the closest base
                        for (int i = 0; i < myBases.Count; i++)
                        {
                            UnitInfo? checkedBaseInfo = state.GetUnit(myBases[i]);
                            if (!checkedBaseInfo.HasValue) continue;

                            Position basePos = checkedBaseInfo.Value.GridPosition;
                            float baseDist = state.GetPathToUnit(unitPos, UnitType.BASE, basePos).Count;

                            if (baseDist > 0 && (mainBaseDist == 0 || baseDist < mainBaseDist))
                            {
                                closestBaseNbr = myBases[i];
                                mainBaseDist = baseDist;
                            }
                        }

                        UnitInfo? closestMineInfo = state.GetUnit(closestMineNbr);
                        if (closestMineInfo.HasValue && closestMineInfo.Value.Health > 0)
                        {
                            actions.Gather(worker, closestMineNbr, closestBaseNbr);
                        }
                    }
                }
            }
        }

        #endregion
    }
}
