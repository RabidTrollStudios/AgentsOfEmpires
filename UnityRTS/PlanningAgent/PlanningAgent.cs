using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// User's primary AI agent. Uses a three-phase FSM driven by threshold tables:
    ///   - BaseBuilding: economy first — find mine, build base + barracks, train pawns
    ///   - ArmyBuilding: train warriors and archers (alternating), build second barracks
    ///   - Attacking: coordinated assault — warriors charge, archers range-fire
    ///
    /// Phase transitions are evaluated each tick via <see cref="EvaluateState"/>:
    /// if all unit counts exceed the phase's max thresholds → progress;
    /// if any count drops below its min threshold → regress.
    ///
    /// Defense is active in all phases: idle troops attack enemies within 10 cells
    /// of friendly buildings or pawns. During Attacking phase, uses
    /// <see cref="CoordinatedAttack"/> for role-based targeting.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int PAWNS_PER_BASE = 8;
        private const int MAX_NBR_BASES = 2;
        private const int MAX_NBR_BARRACKS = 5;
        private const int MAX_NBR_WARRIORS = 40;
        private const int MAX_NBR_ARCHERS = 10;

        #region Private Data

        private int secondMineNbr = -1;
        private int secondaryBaseNbr = -1;

        private Random rng = new Random();

        private enum GameState
        {
            BASE_BUILDING,
            ARMY_BUILDING,
            ATTACKING
        }

        private GameState currState = GameState.BASE_BUILDING;

        // Column indices for stateThresholds
        private const int MIN_PAWNS = 0, MAX_PAWNS = 1;
        private const int MIN_WARRIORS = 2, MAX_WARRIORS = 3;
        private const int MIN_ARCHERS = 4, MAX_ARCHERS = 5;
        private const int MIN_BASES = 6, MAX_BASES = 7;
        private const int MIN_BARRACKS = 8, MAX_BARRACKS = 9;

        private static readonly int[,] stateThresholds = new int[,]
        {
        //                                    Pawns     Warriors    Archers     Bases       Barracks
        //                                    min  max    min  max    min  max    min  max    min  max
            /* BaseBuilding */ {               0,   6,     0,   0,     0,   0,     0,   1,     0,   1  },
            /* ArmyBuilding */ {               6,   8,     0,   5,     0,   5,     1,   1,     1,   1  },
            /* Attacking     */ {              1, int.MaxValue, 1, int.MaxValue, 1, int.MaxValue, 1, int.MaxValue, 1, int.MaxValue }
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
            if (myPawns.Count < stateThresholds[s, MIN_PAWNS] ||
                myWarriors.Count < stateThresholds[s, MIN_WARRIORS] ||
                myArchers.Count < stateThresholds[s, MIN_ARCHERS] ||
                myBases.Count < stateThresholds[s, MIN_BASES] ||
                myBarracks.Count < stateThresholds[s, MIN_BARRACKS])
            {
                return -1;
            }

            // If all counts are at or above their maximum, progress
            if (myPawns.Count >= stateThresholds[s, MAX_PAWNS] &&
                myWarriors.Count >= stateThresholds[s, MAX_WARRIORS] &&
                myArchers.Count >= stateThresholds[s, MAX_ARCHERS] &&
                myBases.Count >= stateThresholds[s, MAX_BASES] &&
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
            // Collect eligible pawns (idle or gathering), then pick one at random
            var eligiblePawns = new List<int>();
            foreach (int w in myPawns)
            {
                UnitInfo? info = state.GetUnit(w);
                if (info.HasValue && (info.Value.CurrentAction == UnitAction.GATHER || info.Value.CurrentAction == UnitAction.IDLE))
                    eligiblePawns.Add(w);
            }
            if (eligiblePawns.Count == 0) return;
            int pawn = eligiblePawns[rng.Next(eligiblePawns.Count)];
            {
                UnitInfo? unitInfo = state.GetUnit(pawn);

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
                        actions.Build(pawn, buildPos, unitType);
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
            idleTroops.AddRange(myWarriors);
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
        /// Defend: only attack enemies that are within a radius of our buildings or pawns.
        /// Each idle troop attacks the closest threatening enemy.
        /// </summary>
        private void DefendBase(IGameState state, IAgentActions actions)
        {
            const float DEFEND_RADIUS = 10f;

            DefendWithTroops(myWarriors, DEFEND_RADIUS, state, actions);
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

                // Check all enemy combat units and pawns for threats near our stuff
                FindClosestThreat(enemyWarriors, troopPos, radius, state, ref closestDist, ref closestEnemy);
                FindClosestThreat(enemyArchers, troopPos, radius, state, ref closestDist, ref closestEnemy);
                FindClosestThreat(enemyPawns, troopPos, radius, state, ref closestDist, ref closestEnemy);

                if (closestEnemy != -1)
                    actions.Attack(troopNbr, closestEnemy);
            }
        }

        /// <summary>
        /// Find the closest enemy (to the troop) that is within radius of any of our buildings or pawns.
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

        /// <summary>Returns true if the enemy position is within radius of any friendly building or pawn.</summary>
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
            foreach (int unit in myPawns)
            {
                UnitInfo? info = state.GetUnit(unit);
                if (info.HasValue && Position.Distance(info.Value.CenterPosition, enemyPos) <= radius)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Split targeting by unit type: warriors charge closest enemy (melee),
        /// archers prefer closest enemy within their attack range to avoid
        /// competing for the same cells as warriors.
        /// </summary>
        private void CoordinatedAttack(IGameState state, IAgentActions actions)
        {
            // Build combined enemy lists
            var enemyTroops = new List<int>();
            enemyTroops.AddRange(enemyWarriors);
            enemyTroops.AddRange(enemyArchers);
            enemyTroops.AddRange(enemyPawns);

            var enemyBuildings = new List<int>();
            enemyBuildings.AddRange(enemyBases);
            enemyBuildings.AddRange(enemyBarracks);

            float archerRange = GameConstants.ATTACK_RANGE[UnitType.ARCHER];

            // Warriors — melee chargers, attack closest enemy regardless of distance
            foreach (int warriorNbr in myWarriors)
            {
                UnitInfo? info = state.GetUnit(warriorNbr);
                if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) continue;

                Position pos = info.Value.GridPosition;
                int target = FindClosestTo(enemyTroops, pos, state);
                if (target == -1)
                    target = FindClosestTo(enemyBuildings, pos, state);
                if (target != -1)
                    actions.Attack(warriorNbr, target);
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

        /// <summary>Find the enemy closest to our main base (for defensive prioritization).</summary>
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
            currState = GameState.BASE_BUILDING;
        }

        /// <summary>
        /// Update the GameManager - called once per frame
        /// </summary>
        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);

            switch (currState)
            {
                case GameState.BASE_BUILDING:
                    UpdateBaseBuilding(state, actions);
                    DefendBase(state, actions);
                    break;
                case GameState.ARMY_BUILDING:
                    UpdateArmyBuilding(state, actions);
                    DefendBase(state, actions);
                    break;
                case GameState.ATTACKING:
                    UpdateAttacking(state, actions);
                    CoordinatedAttack(state, actions);
                    // Rally any troops still idle after attack orders (e.g. newly spawned with no enemies nearby)
                    RallyIdleTroops(state, actions);
                    break;
            }
        }

        /// <summary>
        /// BaseBuilding phase: find a mine, build a base near it, train pawns,
        /// then build a barracks. Progresses to ArmyBuilding once thresholds are met.
        /// </summary>
        private void UpdateBaseBuilding(IGameState state, IAgentActions actions)
        {
            // Progress to ArmyBuilding once we meet BaseBuilding max thresholds
            int eval = EvaluateState(GameState.BASE_BUILDING);
            if (eval == 1)
            {
                currState = GameState.ARMY_BUILDING;
                return;
            }

            // Assign main mine
            if (mines.Count > 0)
            {
                if (mainMineNbr == -1)
                {
                    // Can't proceed without pawns
                    if (myPawns.Count == 0) return;

                    UnitInfo? pawnInfo = state.GetUnit(myPawns[0]);
                    if (!pawnInfo.HasValue) return;
                    Position pawnPos = pawnInfo.Value.GridPosition;

                    // Find closest mine by path length from starting pawn
                    int bestMine = -1;
                    int bestPathLen = int.MaxValue;
                    foreach (int mineNbr in mines)
                    {
                        UnitInfo? mineInfo = state.GetUnit(mineNbr);
                        if (!mineInfo.HasValue || mineInfo.Value.Health <= 0) continue;
                        int pathLen = state.GetPathToUnit(pawnPos, UnitType.MINE, mineInfo.Value.GridPosition).Count;
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
                            float dist = Position.Distance(pawnPos, mineInfo.Value.CenterPosition);
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

                // Priority 1: Train pawns
                if (myPawns.Count < PAWNS_PER_BASE * myBases.Count)
                {
                    foreach (int baseNbr in myBases)
                    {
                        UnitInfo? baseInfo = state.GetUnit(baseNbr);
                        if (baseInfo.HasValue && baseInfo.Value.IsBuilt
                            && baseInfo.Value.CurrentAction == UnitAction.IDLE
                            && state.MyGold >= GameConstants.COST[UnitType.PAWN])
                        {
                            actions.Train(baseNbr, UnitType.PAWN);
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

        /// <summary>
        /// ArmyBuilding phase: train warriors and archers (alternating by count),
        /// build a second barracks, rally idle troops. Regresses to BaseBuilding
        /// if economy collapses; progresses to Attacking once army thresholds are met.
        /// </summary>
        private void UpdateArmyBuilding(IGameState state, IAgentActions actions)
        {
            // Regress to BaseBuilding or progress to Attacking based on ArmyBuilding thresholds
            int eval = EvaluateState(GameState.ARMY_BUILDING);
            if (eval == -1)
            {
                currState = GameState.BASE_BUILDING;
                return;
            }
            if (eval == 1)
            {
                currState = GameState.ATTACKING;
                return;
            }

            // Set all pawns to mine
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

            // For each barracks: alternate between archers and warriors (train whichever we have fewer of)
            foreach (int barracksNbr in myBarracks)
            {
                UnitInfo? barracksInfo = state.GetUnit(barracksNbr);
                if (!barracksInfo.HasValue || !barracksInfo.Value.IsBuilt
                    || barracksInfo.Value.CurrentAction != UnitAction.IDLE)
                    continue;

                if (myArchers.Count <= myWarriors.Count && myArchers.Count < MAX_NBR_ARCHERS)
                {
                    // Archer's turn — wait for enough gold, don't fall back to warrior
                    if (state.MyGold >= GameConstants.COST[UnitType.ARCHER])
                        actions.Train(barracksNbr, UnitType.ARCHER);
                }
                else if (state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
                {
                    actions.Train(barracksNbr, UnitType.WARRIOR);
                }
            }

            // Train pawns at any idle base last
            if (myPawns.Count < PAWNS_PER_BASE * myBases.Count)
            {
                foreach (int baseNbr in myBases)
                {
                    UnitInfo? baseInfo = state.GetUnit(baseNbr);

                    if (baseInfo.HasValue && baseInfo.Value.IsBuilt
                                         && baseInfo.Value.CurrentAction == UnitAction.IDLE
                                         && state.MyGold >= GameConstants.COST[UnitType.PAWN])
                    {
                        actions.Train(baseNbr, UnitType.PAWN);
                    }
                }
            }
        }

        /// <summary>
        /// Attacking phase: continue mining and training, launch coordinated attacks.
        /// Regresses to ArmyBuilding if army drops below attacking min thresholds.
        /// </summary>
        private void UpdateAttacking(IGameState state, IAgentActions actions)
        {
            // Regress to ArmyBuilding based on Attacking thresholds
            int eval = EvaluateState(GameState.ATTACKING);
            if (eval == -1)
            {
                currState = GameState.ARMY_BUILDING;
                return;
            }

            Mine(state, actions);

            // Train pawns if below 10 per base
            if (myPawns.Count < PAWNS_PER_BASE * myBases.Count)
            {
                foreach (int baseNbr in myBases)
                {
                    UnitInfo? baseInfo = state.GetUnit(baseNbr);
                    if (baseInfo.HasValue && baseInfo.Value.IsBuilt
                        && baseInfo.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.PAWN])
                    {
                        actions.Train(baseNbr, UnitType.PAWN);
                    }
                }
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
                    && state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
                {
                    actions.Train(barracksNbr, UnitType.WARRIOR);
                }
            }

        }

        /// <summary>
        /// Send all idle pawns to gather from the closest mine, delivering to the closest base.
        /// Each pawn independently selects its nearest mine and base by path length.
        /// </summary>
        void Mine(IGameState state, IAgentActions actions)
        {
            if (mines.Count > 0)
            {
                // For each pawn
                foreach (int pawn in myPawns)
                {
                    UnitInfo? unitInfo = state.GetUnit(pawn);
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
                            actions.Gather(pawn, closestMineNbr, closestBaseNbr);
                        }
                    }
                }
            }
        }

        #endregion
    }
}
