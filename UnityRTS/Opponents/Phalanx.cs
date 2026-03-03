using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [MEDIUM] Balanced economy (5 workers), then masses soldiers.
    /// Waits for 12 soldiers (1200g) before attacking — equal gold investment
    /// to 8 archers. Soldiers have 1.5 range and 20 DPS; they must survive the
    /// archer free-damage window to win, so numbers matter.
    /// Uses a hybrid state machine:
    /// - Army-level FSM (ArmyPhase) determines overall strategy
    /// - Per-soldier FSM (SoldierTactic) determines individual behavior
    /// Each tick: evaluate army phase, evaluate each soldier's tactic, execute actions.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        #region Enums

        /// <summary>Army-level strategy phase.</summary>
        private enum ArmyPhase
        {
            /// <summary>No barracks yet — building infrastructure.</summary>
            ECONOMY,
            /// <summary>Building army, soldiers rally near barracks.</summary>
            RALLYING,
            /// <summary>Army committed — rally then attack with idle soldiers at rally.</summary>
            ATTACKING,
            /// <summary>Overwhelming advantage — all idle soldiers attack directly.</summary>
            MOPPING_UP
        }

        /// <summary>Per-soldier tactical state, evaluated fresh each tick.</summary>
        private enum SoldierTactic
        {
            /// <summary>Moving to or holding at rally point.</summary>
            RALLYING,
            /// <summary>Reacting to a nearby enemy within aggro range.</summary>
            DEFENDING,
            /// <summary>Attacking priority targets during attack/mop-up phase.</summary>
            ASSAULTING
        }

        #endregion

        #region Constants

        private const int MAX_WORKERS = 5;
        private const int ATTACK_THRESHOLD = 12;
        private const float RALLY_DISTANCE = 10f;
        private const float AGGRO_RANGE = 10f;
        private const float RALLY_PROXIMITY = 3.0f;
        private const float DEFEND_RADIUS = 5.0f;
        private const int MAX_GANG_UP = 3;

        #endregion

        #region Fields

        private Position _rallyPoint = new Position(-1, -1);
        private ArmyPhase _armyPhase = ArmyPhase.ECONOMY;
        private Dictionary<int, SoldierTactic> _soldierTactics = new Dictionary<int, SoldierTactic>();
        private Dictionary<int, float> _previousHealth = new Dictionary<int, float>();

        #endregion

        #region Lifecycle

        public override void InitializeMatch() { }

        public override void InitializeRound(IGameState state)
        {
            base.InitializeRound(state);
            _rallyPoint = new Position(-1, -1);
            _armyPhase = ArmyPhase.ECONOMY;
            _soldierTactics.Clear();
            _previousHealth.Clear();
        }

        #endregion

        #region Update (5-phase loop)

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;
            if (mainMineNbr < 0 || !state.GetUnit(mainMineNbr).HasValue || state.GetUnit(mainMineNbr).Value.Health <= 0)
                mainMineNbr = FindClosestMine(state);

            // ---- Phase 1: Economy ----
            if (myBases.Count == 0)
            {
                BuildStructure(UnitType.BASE, state, actions);
                return;
            }

            TrainWorkers(state, actions);

            if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);

            GatherWithIdleWorkers(state, actions);
            TrainSoldiers(state, actions);

            // ---- Phase 2: Evaluate army phase ----
            Position rallyPoint = ComputeRallyPoint(state);
            UpdateArmyPhase(state);

            // ---- Phase 3: Evaluate each soldier's tactical state ----
            EvaluateSoldierTactics(state, rallyPoint);

            // ---- Phase 4: Execute actions for each soldier ----
            ExecuteSoldierActions(state, actions, rallyPoint);

            // ---- Phase 5: Clean up dead soldiers ----
            CleanDeadSoldiers();

            // ---- Debug overlay ----
            BuildDebugText(state);

            // Snapshot health for next-tick damage detection
            SnapshotHealth(state);
        }

        private void SnapshotHealth(IGameState state)
        {
            _previousHealth.Clear();
            foreach (int soldierNbr in mySoldiers)
            {
                var info = state.GetUnit(soldierNbr);
                if (info.HasValue)
                    _previousHealth[soldierNbr] = info.Value.Health;
            }
        }

        #endregion

        #region Phase 2: Army Phase

        /// <summary>
        /// Evaluate the global army phase based on army size and enemy composition.
        /// Re-evaluated from scratch each tick so losing soldiers in combat
        /// naturally drops from ATTACKING back to RALLYING.
        /// </summary>
        private void UpdateArmyPhase(IGameState state)
        {
            int enemyCombat = enemySoldiers.Count + enemyArchers.Count;

            if (mySoldiers.Count >= ATTACK_THRESHOLD
                && (enemyCombat == 0 || mySoldiers.Count >= 4 * enemyCombat))
            {
                _armyPhase = ArmyPhase.MOPPING_UP;
                return;
            }

            if (mySoldiers.Count >= ATTACK_THRESHOLD)
            {
                _armyPhase = ArmyPhase.ATTACKING;
                return;
            }

            if (myBarracks.Count > 0 && HasBuiltUnit(myBarracks, state))
            {
                _armyPhase = ArmyPhase.RALLYING;
                return;
            }

            _armyPhase = ArmyPhase.ECONOMY;
        }

        #endregion

        #region Phase 3: Per-Soldier Tactical Evaluation

        /// <summary>
        /// For each living soldier, determine its tactical state for this tick.
        /// Priority order (evaluated per soldier, first match wins):
        ///   1. Hold:       enemy within AGGRO_RANGE AND soldier already ATTACK → keep current tactic
        ///   2. DEFENDING:  enemy within AGGRO_RANGE AND soldier IDLE/MOVE → attack closest
        ///   3. ASSAULTING: MOPPING_UP (any idle), or ATTACKING (idle near rally)
        ///   4. RALLYING:   default
        ///
        /// During ECONOMY: always RALLYING (no soldiers exist yet).
        /// During RALLYING: DEFENDING is active so melee units fight back if attacked.
        /// </summary>
        private void EvaluateSoldierTactics(IGameState state, Position rallyPoint)
        {
            foreach (int soldierNbr in mySoldiers)
            {
                var info = state.GetUnit(soldierNbr);
                if (!info.HasValue) continue;

                var curAction = info.Value.CurrentAction;

                // Skip soldiers doing non-combat actions
                if (curAction == UnitAction.BUILD || curAction == UnitAction.TRAIN
                    || curAction == UnitAction.GATHER)
                {
                    _soldierTactics[soldierNbr] = SoldierTactic.RALLYING;
                    continue;
                }

                Position myPos = info.Value.CenterPosition;

                // During ECONOMY: defend if enemies are close, otherwise rally
                if (_armyPhase == ArmyPhase.ECONOMY)
                {
                    if (curAction == UnitAction.ATTACK)
                    {
                        if (!_soldierTactics.ContainsKey(soldierNbr))
                            _soldierTactics[soldierNbr] = SoldierTactic.DEFENDING;
                        continue;
                    }

                    if (_previousHealth.TryGetValue(soldierNbr, out float prevHpEcon)
                        && info.Value.Health < prevHpEcon)
                    {
                        int? attacker = FindClosestEnemy(soldierNbr, state, null);
                        if (attacker.HasValue)
                        {
                            _soldierTactics[soldierNbr] = SoldierTactic.DEFENDING;
                            continue;
                        }
                    }

                    if (curAction == UnitAction.IDLE || curAction == UnitAction.MOVE)
                    {
                        int nearEnemy = FindEnemyInRadius(myPos, DEFEND_RADIUS, state);
                        if (nearEnemy >= 0)
                        {
                            _soldierTactics[soldierNbr] = SoldierTactic.DEFENDING;
                            continue;
                        }
                    }

                    _soldierTactics[soldierNbr] = SoldierTactic.RALLYING;
                    continue;
                }

                // During RALLYING: attack enemies within attack range, or fight back if taking damage
                if (_armyPhase == ArmyPhase.RALLYING)
                {
                    // Already attacking — hold, don't interrupt
                    if (curAction == UnitAction.ATTACK)
                    {
                        if (!_soldierTactics.ContainsKey(soldierNbr))
                            _soldierTactics[soldierNbr] = SoldierTactic.DEFENDING;
                        continue;
                    }

                    // Being attacked (health dropped) — fight back against closest enemy
                    if (_previousHealth.TryGetValue(soldierNbr, out float prevHp)
                        && info.Value.Health < prevHp)
                    {
                        int? attacker = FindClosestEnemy(soldierNbr, state, null);
                        if (attacker.HasValue)
                        {
                            _soldierTactics[soldierNbr] = SoldierTactic.DEFENDING;
                            continue;
                        }
                    }

                    // Enemy within defend radius — engage
                    if (curAction == UnitAction.IDLE || curAction == UnitAction.MOVE)
                    {
                        int nearEnemy = FindEnemyInRadius(myPos, DEFEND_RADIUS, state);
                        if (nearEnemy >= 0)
                        {
                            _soldierTactics[soldierNbr] = SoldierTactic.DEFENDING;
                            continue;
                        }
                    }

                    _soldierTactics[soldierNbr] = SoldierTactic.RALLYING;
                    continue;
                }

                // --- Find closest enemy within AGGRO_RANGE ---
                float closestEnemyDist = float.MaxValue;
                int closestEnemyNbr = -1;

                foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                                 UnitType.BASE, UnitType.BARRACKS })
                {
                    foreach (int enemyNbr in state.GetEnemyUnits(ut))
                    {
                        var enemyInfo = state.GetUnit(enemyNbr);
                        if (!enemyInfo.HasValue) continue;
                        float dist = Position.Distance(myPos, enemyInfo.Value.CenterPosition);
                        if (dist < AGGRO_RANGE && dist < closestEnemyDist)
                        {
                            closestEnemyDist = dist;
                            closestEnemyNbr = enemyNbr;
                        }
                    }
                }

                // Priority 1: Hold — soldier already attacking and enemy nearby, don't interrupt
                if (closestEnemyNbr >= 0 && curAction == UnitAction.ATTACK)
                {
                    if (!_soldierTactics.ContainsKey(soldierNbr))
                        _soldierTactics[soldierNbr] = SoldierTactic.DEFENDING;
                    continue;
                }

                // Priority 2: DEFENDING — enemy nearby, soldier idle or mid-rally
                if (closestEnemyNbr >= 0
                    && (curAction == UnitAction.IDLE || curAction == UnitAction.MOVE))
                {
                    _soldierTactics[soldierNbr] = SoldierTactic.DEFENDING;
                    continue;
                }

                // Priority 3: ASSAULTING — army phase says attack and soldier is idle
                if (curAction == UnitAction.IDLE)
                {
                    if (_armyPhase == ArmyPhase.MOPPING_UP)
                    {
                        _soldierTactics[soldierNbr] = SoldierTactic.ASSAULTING;
                        continue;
                    }

                    if (_armyPhase == ArmyPhase.ATTACKING && rallyPoint.X >= 0)
                    {
                        float distToRally = Position.Distance(myPos, rallyPoint);
                        if (distToRally <= RALLY_PROXIMITY)
                        {
                            _soldierTactics[soldierNbr] = SoldierTactic.ASSAULTING;
                            continue;
                        }
                    }
                }

                // Priority 4: RALLYING — default
                _soldierTactics[soldierNbr] = SoldierTactic.RALLYING;
            }
        }

        #endregion

        #region Phase 4: Execute Actions

        /// <summary>
        /// Execute the action for each soldier based on its resolved tactical state.
        /// Each soldier is processed exactly once via a single switch dispatch.
        /// </summary>
        private void ExecuteSoldierActions(IGameState state, IAgentActions actions, Position rallyPoint)
        {
            var gangUpCounts = new Dictionary<int, int>();

            foreach (int soldierNbr in mySoldiers)
            {
                if (!_soldierTactics.TryGetValue(soldierNbr, out var tactic)) continue;

                switch (tactic)
                {
                    case SoldierTactic.DEFENDING:
                        ExecuteDefending(soldierNbr, state, actions, gangUpCounts);
                        break;
                    case SoldierTactic.ASSAULTING:
                        ExecuteAssaulting(soldierNbr, state, actions, gangUpCounts);
                        break;
                    case SoldierTactic.RALLYING:
                        ExecuteRallying(soldierNbr, state, actions, rallyPoint);
                        break;
                }
            }
        }

        /// <summary>
        /// Attack closest enemy within AGGRO_RANGE. Prefers enemies already being
        /// attacked by other soldiers (up to MAX_GANG_UP per target) so they gang
        /// up and kill targets faster. Handles both IDLE and MOVE soldiers.
        /// </summary>
        private void ExecuteDefending(int soldierNbr, IGameState state, IAgentActions actions,
            Dictionary<int, int> gangUpCounts)
        {
            var info = state.GetUnit(soldierNbr);
            if (!info.HasValue) return;

            var curAction = info.Value.CurrentAction;
            if (curAction != UnitAction.IDLE && curAction != UnitAction.MOVE) return;

            Position myPos = info.Value.CenterPosition;

            // First pass: closest enemy already being attacked but under the cap
            int? bestTarget = null;
            float bestDist = float.MaxValue;

            foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                            UnitType.BASE, UnitType.BARRACKS })
            {
                foreach (int enemyNbr in state.GetEnemyUnits(ut))
                {
                    if (!gangUpCounts.TryGetValue(enemyNbr, out int count) || count >= MAX_GANG_UP) continue;
                    var enemyInfo = state.GetUnit(enemyNbr);
                    if (!enemyInfo.HasValue) continue;
                    float dist = Position.Distance(myPos, enemyInfo.Value.CenterPosition);
                    if (dist <= AGGRO_RANGE && dist < bestDist)
                    {
                        bestDist = dist;
                        bestTarget = enemyNbr;
                    }
                }
            }

            // Fallback: closest enemy within AGGRO_RANGE
            if (!bestTarget.HasValue)
            {
                bestDist = float.MaxValue;
                foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                                UnitType.BASE, UnitType.BARRACKS })
                {
                    foreach (int enemyNbr in state.GetEnemyUnits(ut))
                    {
                        var enemyInfo = state.GetUnit(enemyNbr);
                        if (!enemyInfo.HasValue) continue;
                        float dist = Position.Distance(myPos, enemyInfo.Value.CenterPosition);
                        if (dist <= AGGRO_RANGE && dist < bestDist)
                        {
                            bestDist = dist;
                            bestTarget = enemyNbr;
                        }
                    }
                }
            }

            if (bestTarget.HasValue)
            {
                actions.Attack(soldierNbr, bestTarget.Value);
                gangUpCounts[bestTarget.Value] = gangUpCounts.TryGetValue(bestTarget.Value, out int c) ? c + 1 : 1;
            }
        }

        /// <summary>
        /// Attack closest enemy with priority: combat > worker > building.
        /// Only acts on IDLE soldiers. Prefers enemies already being attacked
        /// (up to MAX_GANG_UP per target) so soldiers gang up and eliminate targets faster.
        /// </summary>
        private void ExecuteAssaulting(int soldierNbr, IGameState state, IAgentActions actions,
            Dictionary<int, int> gangUpCounts)
        {
            var info = state.GetUnit(soldierNbr);
            if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) return;

            // Prefer an enemy already being attacked but under the cap, fallback to closest
            int? target = FindClosestEnemy(soldierNbr, state, gangUpCounts, preferUnderCap: true)
                       ?? FindClosestEnemy(soldierNbr, state, null, preferUnderCap: false);
            if (target.HasValue)
            {
                actions.Attack(soldierNbr, target.Value);
                gangUpCounts[target.Value] = gangUpCounts.TryGetValue(target.Value, out int c) ? c + 1 : 1;
            }
        }

        /// <summary>
        /// If idle and not yet near the rally point, move toward it.
        /// Once within RALLY_PROXIMITY, stay put — don't keep re-issuing Move commands.
        /// Targets a buildable cell on the far side of the 4x4 rally area so units spread out.
        /// </summary>
        private void ExecuteRallying(int soldierNbr, IGameState state, IAgentActions actions,
            Position rallyPoint)
        {
            if (rallyPoint.X < 0) return;

            var info = state.GetUnit(soldierNbr);
            if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) return;

            Position myPos = info.Value.CenterPosition;
            float distToRally = Position.Distance(myPos, rallyPoint);
            if (distToRally <= RALLY_PROXIMITY) return;

            Position targetCell = FindRallyCell(myPos, rallyPoint, state);
            actions.Move(soldierNbr, targetCell);
        }

        #endregion

        #region Phase 5: Cleanup

        /// <summary>
        /// Remove entries for soldiers that no longer exist (died this tick).
        /// </summary>
        private void CleanDeadSoldiers()
        {
            var soldierSet = new HashSet<int>(mySoldiers);
            var deadKeys = new List<int>();

            foreach (int key in _soldierTactics.Keys)
            {
                if (!soldierSet.Contains(key))
                    deadKeys.Add(key);
            }

            foreach (int key in deadKeys)
            {
                _soldierTactics.Remove(key);
            }
        }

        /// <summary>
        /// Build the debug overlay text showing army phase, soldier counts per tactic,
        /// and economy summary.
        /// </summary>
        private void BuildDebugText(IGameState state)
        {
            int defending = 0, assaulting = 0, rallying = 0;
            foreach (var tactic in _soldierTactics.Values)
            {
                switch (tactic)
                {
                    case SoldierTactic.DEFENDING:  defending++;  break;
                    case SoldierTactic.ASSAULTING: assaulting++; break;
                    case SoldierTactic.RALLYING:   rallying++;   break;
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("Phase: ").AppendLine(_armyPhase.ToString());
            sb.Append("Gold: ").AppendLine(state.MyGold.ToString());
            sb.Append("Workers: ").Append(myWorkers.Count)
              .Append("  Soldiers: ").AppendLine(mySoldiers.Count.ToString());
            sb.Append("  Rally: ").Append(rallying)
              .Append("  Defend: ").Append(defending)
              .Append("  Assault: ").Append(assaulting);

            DebugText = sb.ToString();
        }

        #endregion

        #region Economy Helpers

        private void TrainWorkers(IGameState state, IAgentActions actions)
        {
            foreach (int baseNbr in myBases)
            {
                var info = state.GetUnit(baseNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.WORKER]
                    && myWorkers.Count < MAX_WORKERS)
                {
                    actions.Train(baseNbr, UnitType.WORKER);
                }
            }
        }

        private void TrainSoldiers(IGameState state, IAgentActions actions)
        {
            foreach (int barracksNbr in myBarracks)
            {
                var info = state.GetUnit(barracksNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.SOLDIER])
                {
                    actions.Train(barracksNbr, UnitType.SOLDIER);
                }
            }
        }

        private void GatherWithIdleWorkers(IGameState state, IAgentActions actions)
        {
            if (mainBaseNbr < 0 || mainMineNbr < 0) return;
            var mineInfo = state.GetUnit(mainMineNbr);
            if (!mineInfo.HasValue || mineInfo.Value.Health <= 0) return;

            foreach (int worker in myWorkers)
            {
                var info = state.GetUnit(worker);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                    actions.Gather(worker, mainMineNbr, mainBaseNbr);
            }
        }

        private int FindClosestMine(IGameState state)
        {
            if (mines.Count == 0) return -1;
            if (myWorkers.Count == 0) return -1;
            var workerInfo = state.GetUnit(myWorkers[0]);
            if (!workerInfo.HasValue) return -1;

            Position workerPos = workerInfo.Value.GridPosition;
            int bestMine = -1;
            int bestPathLen = int.MaxValue;
            foreach (int mineNbr in mines)
            {
                var mineInfo = state.GetUnit(mineNbr);
                if (mineInfo.HasValue && mineInfo.Value.Health > 0)
                {
                    int pathLen = state.GetPathToUnit(workerPos, UnitType.MINE, mineInfo.Value.GridPosition).Count;
                    if (pathLen > 0 && pathLen < bestPathLen)
                    {
                        bestPathLen = pathLen;
                        bestMine = mineNbr;
                    }
                }
            }

            if (bestMine == -1)
            {
                float bestDist = float.MaxValue;
                foreach (int mineNbr in mines)
                {
                    var mineInfo = state.GetUnit(mineNbr);
                    if (mineInfo.HasValue && mineInfo.Value.Health > 0)
                    {
                        float dist = Position.Distance(workerPos, mineInfo.Value.CenterPosition);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestMine = mineNbr;
                        }
                    }
                }
            }

            return bestMine;
        }

        #endregion

        #region Building Helpers

        private void BuildStructure(UnitType type, IGameState state, IAgentActions actions)
        {
            foreach (int worker in myWorkers)
            {
                var info = state.GetUnit(worker);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[type])
                {
                    Position buildPos = FindBestBuildPosition(type, state);
                    if (buildPos.X >= 0)
                    {
                        actions.Build(worker, buildPos, type);
                        return;
                    }
                }
            }
        }

        private Position FindBestBuildPosition(UnitType type, IGameState state)
        {
            var freshPositions = state.FindProspectiveBuildPositions(type);

            if (type == UnitType.BASE && mainMineNbr >= 0)
            {
                var mineInfo = state.GetUnit(mainMineNbr);
                if (mineInfo.HasValue)
                {
                    Position minePos = mineInfo.Value.GridPosition;
                    var mineNeighbors = state.GetBuildablePositionsNearUnit(UnitType.MINE, minePos);
                    if (mineNeighbors.Count > 0)
                    {
                        Position mineRef = mineNeighbors[0];
                        var sorted = new System.Collections.Generic.List<Position>(freshPositions);
                        sorted.Sort((a, b) => Position.Distance(a, mineRef).CompareTo(Position.Distance(b, mineRef)));
                        int bestPathLen = int.MaxValue;
                        Position bestPos = new Position(-1, -1);
                        foreach (Position pos in sorted)
                        {
                            int pathLen = state.GetPathBetween(pos, mineRef).Count;
                            if (pathLen > 0 && pathLen < bestPathLen)
                            {
                                bestPathLen = pathLen;
                                bestPos = pos;
                            }
                        }
                        if (bestPos.X >= 0) return bestPos;
                    }
                }
            }
            else if (type == UnitType.BARRACKS && mainBaseNbr >= 0)
            {
                var baseInfo = state.GetUnit(mainBaseNbr);
                if (baseInfo.HasValue)
                {
                    Position basePos = baseInfo.Value.GridPosition;
                    Position mapCenter = new Position(state.MapSize.X / 2, state.MapSize.Y / 2);
                    Position target = new Position((basePos.X + mapCenter.X) / 2, (basePos.Y + mapCenter.Y) / 2);
                    float minClear = GameConstants.UNIT_SIZE[UnitType.BASE].X + 1f;
                    float bestDist = float.MaxValue;
                    Position bestPos = new Position(-1, -1);
                    foreach (Position pos in freshPositions)
                    {
                        float distToBase = Position.Distance(pos, baseInfo.Value.CenterPosition);
                        if (distToBase >= minClear)
                        {
                            float dist = Position.Distance(pos, target);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestPos = pos;
                            }
                        }
                    }
                    if (bestPos.X >= 0) return bestPos;
                }
            }

            return freshPositions.Count > 0 ? freshPositions[0] : new Position(-1, -1);
        }

        /// <summary>
        /// Computes a rally point RALLY_DISTANCE path-steps from the barracks along the
        /// navigable route toward the map center. Picks the first position where a 4x4
        /// area is fully buildable. Cached after first successful computation.
        /// </summary>
        private Position ComputeRallyPoint(IGameState state)
        {
            if (_rallyPoint.X >= 0) return _rallyPoint;
            if (myBarracks.Count == 0) return new Position(-1, -1);
            var info = state.GetUnit(myBarracks[0]);
            if (!info.HasValue) return new Position(-1, -1);

            Position barracks = info.Value.GridPosition;
            Position mapCenter = new Position(state.MapSize.X / 2, state.MapSize.Y / 2);

            var path = state.GetPathBetween(barracks, mapCenter);
            if (path.Count == 0) return new Position(-1, -1);

            int startIdx = System.Math.Min((int)RALLY_DISTANCE - 1, path.Count - 1);

            // Search forward from RALLY_DISTANCE for a 4x4 buildable area
            for (int i = startIdx; i < path.Count; i++)
            {
                if (IsAreaBuildable(path[i], 4, state))
                {
                    _rallyPoint = path[i];
                    return _rallyPoint;
                }
            }

            // Fallback: search backward
            for (int i = startIdx - 1; i >= 0; i--)
            {
                if (IsAreaBuildable(path[i], 4, state))
                {
                    _rallyPoint = path[i];
                    return _rallyPoint;
                }
            }

            // Last resort: use the original position
            _rallyPoint = path[startIdx];
            return _rallyPoint;
        }

        /// <summary>
        /// Check if a size x size area centered on the given position is fully buildable.
        /// </summary>
        private bool IsAreaBuildable(Position center, int size, IGameState state)
        {
            int half = size / 2;
            for (int dx = -(half - 1); dx <= half; dx++)
            {
                for (int dy = -(half - 1); dy <= half; dy++)
                {
                    if (!state.IsPositionBuildable(new Position(center.X + dx, center.Y + dy)))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Find the best buildable cell within the 4x4 rally area for a unit to move to.
        /// Prefers cells on the far side of the rally point from the unit's position,
        /// so units spread across the area rather than clustering on the approach side.
        /// </summary>
        private Position FindRallyCell(Position unitPos, Position rallyCenter, IGameState state)
        {
            Position bestCell = new Position(-1, -1);
            float bestDist = float.MinValue;

            for (int dx = -1; dx <= 2; dx++)
            {
                for (int dy = -1; dy <= 2; dy++)
                {
                    Position cell = new Position(rallyCenter.X + dx, rallyCenter.Y + dy);
                    if (!state.IsPositionBuildable(cell)) continue;

                    float dist = Position.Distance(unitPos, cell);
                    if (dist > bestDist)
                    {
                        bestDist = dist;
                        bestCell = cell;
                    }
                }
            }

            return bestCell.X >= 0 ? bestCell : rallyCenter;
        }

        #endregion

        #region Targeting Helpers

        /// <summary>
        /// Find the closest enemy within effective attack range of the given attacker type.
        /// Returns the enemy unit number, or -1 if none found.
        /// </summary>
        private int FindEnemyInAttackRange(UnitType attackerType, Position myPos, IGameState state)
        {
            float bestDist = float.MaxValue;
            int bestEnemy = -1;

            foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                             UnitType.BASE, UnitType.BARRACKS })
            {
                float range = GameConstants.EffectiveAttackRange(attackerType, ut);
                foreach (int enemyNbr in state.GetEnemyUnits(ut))
                {
                    var enemyInfo = state.GetUnit(enemyNbr);
                    if (!enemyInfo.HasValue) continue;
                    float dist = Position.Distance(myPos, enemyInfo.Value.CenterPosition);
                    if (dist <= range && dist < bestDist)
                    {
                        bestDist = dist;
                        bestEnemy = enemyNbr;
                    }
                }
            }

            return bestEnemy;
        }

        /// <summary>
        /// Find the closest enemy unit within a fixed radius.
        /// Returns the enemy unit number, or -1 if none found.
        /// </summary>
        private int FindEnemyInRadius(Position myPos, float radius, IGameState state)
        {
            float bestDist = float.MaxValue;
            int bestEnemy = -1;

            foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                             UnitType.BASE, UnitType.BARRACKS })
            {
                foreach (int enemyNbr in state.GetEnemyUnits(ut))
                {
                    var enemyInfo = state.GetUnit(enemyNbr);
                    if (!enemyInfo.HasValue) continue;
                    float dist = Position.Distance(myPos, enemyInfo.Value.CenterPosition);
                    if (dist <= radius && dist < bestDist)
                    {
                        bestDist = dist;
                        bestEnemy = enemyNbr;
                    }
                }
            }

            return bestEnemy;
        }

        /// <summary>
        /// Find closest enemy with priority: combat > worker > building.
        /// When gangUpCounts is non-null and preferUnderCap is true, only considers
        /// enemies already being attacked but under MAX_GANG_UP (gang-up).
        /// When gangUpCounts is null, considers all enemies.
        /// </summary>
        private int? FindClosestEnemy(int attackerNbr, IGameState state,
            Dictionary<int, int> gangUpCounts, bool preferUnderCap = false)
        {
            var attackerInfo = state.GetUnit(attackerNbr);
            if (!attackerInfo.HasValue) return null;
            Position attackerPos = attackerInfo.Value.GridPosition;

            // Priority: combat units > workers > buildings
            int? bestCombat = null;
            float bestCombatDist = float.MaxValue;
            int? bestWorker = null;
            float bestWorkerDist = float.MaxValue;
            int? bestBuilding = null;
            float bestBuildingDist = float.MaxValue;

            foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                            UnitType.BASE, UnitType.BARRACKS })
            {
                bool isCombat = ut == UnitType.SOLDIER || ut == UnitType.ARCHER;
                bool isWorker = ut == UnitType.WORKER;
                var enemies = state.GetEnemyUnits(ut);
                foreach (int enemyNbr in enemies)
                {
                    if (gangUpCounts != null && preferUnderCap)
                    {
                        // Only consider enemies already targeted but under the cap
                        if (!gangUpCounts.TryGetValue(enemyNbr, out int count) || count >= MAX_GANG_UP)
                            continue;
                    }
                    var enemyInfo = state.GetUnit(enemyNbr);
                    if (!enemyInfo.HasValue) continue;
                    float dist = Position.Distance(attackerPos, enemyInfo.Value.CenterPosition);
                    if (isCombat && dist < bestCombatDist) { bestCombatDist = dist; bestCombat = enemyNbr; }
                    else if (isWorker && dist < bestWorkerDist) { bestWorkerDist = dist; bestWorker = enemyNbr; }
                    else if (!isCombat && !isWorker && dist < bestBuildingDist) { bestBuildingDist = dist; bestBuilding = enemyNbr; }
                }
            }
            return bestCombat ?? bestWorker ?? bestBuilding;
        }

        #endregion
    }
}
