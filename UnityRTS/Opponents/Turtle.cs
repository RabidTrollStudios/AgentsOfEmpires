using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [MEDIUM] Greedy economy: 8 pawns, then masses warriors.
    /// Waits for 12+ warriors before attacking.
    /// Uses a hybrid state machine (army-level FSM + per-warrior FSM) without
    /// kiting, engaging, or gang-up mechanics. Warriors rally before attacking
    /// and fight anything in their face.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        #region Enums

        /// <summary>Army-level strategy phase.</summary>
        private enum ArmyPhase
        {
            /// <summary>No barracks yet — building infrastructure.</summary>
            ECONOMY,
            /// <summary>Barracks built, accumulating warriors at rally.</summary>
            RALLYING,
            /// <summary>Army ready — rally then attack with idle warriors at rally.</summary>
            ATTACKING,
            /// <summary>Overwhelming advantage — all idle warriors attack directly.</summary>
            MOPPING_UP
        }

        /// <summary>Per-warrior tactical state, evaluated fresh each tick.</summary>
        private enum WarriorTactic
        {
            /// <summary>Moving to or holding at rally point.</summary>
            RALLYING,
            /// <summary>Reacting to a nearby enemy within aggro/defend range.</summary>
            DEFENDING,
            /// <summary>Attacking priority targets during attack/mop-up phase.</summary>
            ASSAULTING
        }

        #endregion

        #region Constants

        private const int MAX_PAWNS = 6;
        private const int ATTACK_THRESHOLD = 10;
        private const float RALLY_DISTANCE = 10f;
        private const float AGGRO_RANGE = 12f;
        private const float RALLY_PROXIMITY = 3.0f;
        private const float DEFEND_RADIUS = 8.0f;

        #endregion

        #region Fields

        private Position _rallyPoint = new Position(-1, -1);
        private ArmyPhase _armyPhase = ArmyPhase.ECONOMY;
        private Dictionary<int, WarriorTactic> _warriorTactics = new Dictionary<int, WarriorTactic>();
        private Dictionary<int, float> _previousHealth = new Dictionary<int, float>();

        #endregion

        #region Lifecycle

        public override void InitializeMatch() { }

        public override void InitializeRound(IGameState state)
        {
            base.InitializeRound(state);
            _rallyPoint = new Position(-1, -1);
            _armyPhase = ArmyPhase.ECONOMY;
            _warriorTactics.Clear();
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

            TrainPawns(state, actions);

            if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);

            AssistConstruction(state, actions);
            RepairDamagedBuildings(state, actions);
            GatherWithIdlePawns(state, actions);
            TrainWarriors(state, actions);

            // ---- Phase 2: Evaluate army phase ----
            Position rallyPoint = ComputeRallyPoint(state);
            UpdateArmyPhase(state);

            // ---- Phase 3: Evaluate each warrior's tactical state ----
            EvaluateWarriorTactics(state, rallyPoint);

            // ---- Phase 4: Execute actions for each warrior ----
            ExecuteWarriorActions(state, actions, rallyPoint);

            // ---- Phase 5: Clean up dead warriors ----
            CleanDeadWarriors();

            // ---- Debug overlay ----
            BuildDebugText(state);

            // Snapshot health for next-tick damage detection
            SnapshotHealth(state);
        }

        private void SnapshotHealth(IGameState state)
        {
            _previousHealth.Clear();
            foreach (int warriorNbr in myWarriors)
            {
                var info = state.GetUnit(warriorNbr);
                if (info.HasValue)
                    _previousHealth[warriorNbr] = info.Value.Health;
            }
        }

        #endregion

        #region Phase 2: Army Phase

        /// <summary>
        /// Evaluate the global army phase based on army size and enemy composition.
        /// Re-evaluated from scratch each tick so losing warriors in combat
        /// naturally drops from ATTACKING back to RALLYING.
        /// </summary>
        private void UpdateArmyPhase(IGameState state)
        {
            int enemyCombat = enemyWarriors.Count + enemyArchers.Count;

            if (myWarriors.Count >= ATTACK_THRESHOLD
                && (enemyCombat == 0 || myWarriors.Count >= 4 * enemyCombat))
            {
                _armyPhase = ArmyPhase.MOPPING_UP;
                return;
            }

            if (myWarriors.Count >= ATTACK_THRESHOLD)
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

        #region Phase 3: Per-Warrior Tactical Evaluation

        /// <summary>
        /// For each living warrior, determine its tactical state for this tick.
        /// During ECONOMY/RALLYING: defend if attacked or enemy in radius, otherwise rally.
        /// During ATTACKING: defend nearby, assault if idle near rally, else rally.
        /// During MOPPING_UP: defend nearby, assault all idle.
        /// </summary>
        private void EvaluateWarriorTactics(IGameState state, Position rallyPoint)
        {
            foreach (int warriorNbr in myWarriors)
            {
                var info = state.GetUnit(warriorNbr);
                if (!info.HasValue) continue;

                var curAction = info.Value.CurrentAction;

                // Skip warriors doing non-combat actions
                if (curAction == UnitAction.BUILD || curAction == UnitAction.TRAIN
                    || curAction == UnitAction.GATHER)
                {
                    _warriorTactics[warriorNbr] = WarriorTactic.RALLYING;
                    continue;
                }

                Position myPos = info.Value.CenterPosition;

                // During ECONOMY/RALLYING: defend only, no assaulting
                if (_armyPhase == ArmyPhase.ECONOMY || _armyPhase == ArmyPhase.RALLYING)
                {
                    // Already attacking — hold
                    if (curAction == UnitAction.ATTACK)
                    {
                        if (!_warriorTactics.ContainsKey(warriorNbr))
                            _warriorTactics[warriorNbr] = WarriorTactic.DEFENDING;
                        continue;
                    }

                    // Being attacked (health dropped) — fight back
                    if (_previousHealth.TryGetValue(warriorNbr, out float prevHp)
                        && info.Value.Health < prevHp)
                    {
                        _warriorTactics[warriorNbr] = WarriorTactic.DEFENDING;
                        continue;
                    }

                    // Enemy within defend radius
                    if (curAction == UnitAction.IDLE || curAction == UnitAction.MOVE)
                    {
                        int nearEnemy = FindEnemyInRadius(myPos, DEFEND_RADIUS, state);
                        if (nearEnemy >= 0)
                        {
                            _warriorTactics[warriorNbr] = WarriorTactic.DEFENDING;
                            continue;
                        }
                    }

                    _warriorTactics[warriorNbr] = WarriorTactic.RALLYING;
                    continue;
                }

                // ---- ATTACKING / MOPPING_UP ----

                // Already attacking and enemy nearby — hold
                if (curAction == UnitAction.ATTACK)
                {
                    if (!_warriorTactics.ContainsKey(warriorNbr))
                        _warriorTactics[warriorNbr] = WarriorTactic.DEFENDING;
                    continue;
                }

                // Enemy within AGGRO_RANGE — defend
                if (curAction == UnitAction.IDLE || curAction == UnitAction.MOVE)
                {
                    int nearEnemy = FindEnemyInRadius(myPos, AGGRO_RANGE, state);
                    if (nearEnemy >= 0)
                    {
                        _warriorTactics[warriorNbr] = WarriorTactic.DEFENDING;
                        continue;
                    }
                }

                // ASSAULTING — army phase says attack and warrior is idle
                if (curAction == UnitAction.IDLE)
                {
                    if (_armyPhase == ArmyPhase.MOPPING_UP)
                    {
                        _warriorTactics[warriorNbr] = WarriorTactic.ASSAULTING;
                        continue;
                    }

                    if (_armyPhase == ArmyPhase.ATTACKING && rallyPoint.X >= 0)
                    {
                        float distToRally = Position.Distance(myPos, rallyPoint);
                        if (distToRally <= RALLY_PROXIMITY)
                        {
                            _warriorTactics[warriorNbr] = WarriorTactic.ASSAULTING;
                            continue;
                        }
                    }
                }

                // Default: rally
                _warriorTactics[warriorNbr] = WarriorTactic.RALLYING;
            }
        }

        #endregion

        #region Phase 4: Execute Actions

        /// <summary>
        /// Execute the action for each warrior based on its resolved tactical state.
        /// Each warrior is processed exactly once via a single switch dispatch.
        /// </summary>
        private void ExecuteWarriorActions(IGameState state, IAgentActions actions, Position rallyPoint)
        {
            var assignedTargets = new HashSet<int>();

            foreach (int warriorNbr in myWarriors)
            {
                if (!_warriorTactics.TryGetValue(warriorNbr, out var tactic)) continue;

                switch (tactic)
                {
                    case WarriorTactic.DEFENDING:
                        ExecuteDefending(warriorNbr, state, actions, assignedTargets);
                        break;
                    case WarriorTactic.ASSAULTING:
                        ExecuteAssaulting(warriorNbr, state, actions, assignedTargets);
                        break;
                    case WarriorTactic.RALLYING:
                        ExecuteRallying(warriorNbr, state, actions, rallyPoint);
                        break;
                }
            }
        }

        /// <summary>
        /// Attack closest enemy within AGGRO_RANGE. Uses simple target spreading
        /// via HashSet to avoid all warriors piling on the same enemy.
        /// </summary>
        private void ExecuteDefending(int warriorNbr, IGameState state, IAgentActions actions,
            HashSet<int> assignedTargets)
        {
            var info = state.GetUnit(warriorNbr);
            if (!info.HasValue) return;

            var curAction = info.Value.CurrentAction;
            if (curAction != UnitAction.IDLE && curAction != UnitAction.MOVE) return;

            Position myPos = info.Value.CenterPosition;

            int? bestTarget = null;
            float bestDist = float.MaxValue;

            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.PAWN,
                                            UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY })
            {
                foreach (int enemyNbr in state.GetEnemyUnits(ut))
                {
                    if (assignedTargets.Contains(enemyNbr)) continue;
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

            // Fallback: closest enemy in AGGRO_RANGE ignoring spreading
            if (!bestTarget.HasValue)
            {
                bestDist = float.MaxValue;
                foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.PAWN,
                                                UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY })
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
                actions.Attack(warriorNbr, bestTarget.Value);
                assignedTargets.Add(bestTarget.Value);
            }
        }

        /// <summary>
        /// Attack closest enemy with priority: combat > pawn > building.
        /// Only acts on IDLE warriors. Uses simple target spreading.
        /// </summary>
        private void ExecuteAssaulting(int warriorNbr, IGameState state, IAgentActions actions,
            HashSet<int> assignedTargets)
        {
            var info = state.GetUnit(warriorNbr);
            if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) return;

            int? target = FindClosestEnemy(warriorNbr, state, assignedTargets)
                       ?? FindClosestEnemy(warriorNbr, state, null);
            if (target.HasValue)
            {
                actions.Attack(warriorNbr, target.Value);
                assignedTargets.Add(target.Value);
            }
        }

        /// <summary>
        /// If idle and not yet near the rally point, move toward it.
        /// Once within RALLY_PROXIMITY, stay put.
        /// </summary>
        private void ExecuteRallying(int warriorNbr, IGameState state, IAgentActions actions,
            Position rallyPoint)
        {
            if (rallyPoint.X < 0) return;

            var info = state.GetUnit(warriorNbr);
            if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) return;

            Position myPos = info.Value.CenterPosition;
            float distToRally = Position.Distance(myPos, rallyPoint);
            if (distToRally <= RALLY_PROXIMITY) return;

            Position targetCell = FindRallyCell(myPos, rallyPoint, state);
            actions.Move(warriorNbr, targetCell);
        }

        #endregion

        #region Phase 5: Cleanup

        /// <summary>
        /// Remove entries for warriors that no longer exist (died this tick).
        /// </summary>
        private void CleanDeadWarriors()
        {
            var warriorSet = new HashSet<int>(myWarriors);
            var deadKeys = new List<int>();

            foreach (int key in _warriorTactics.Keys)
            {
                if (!warriorSet.Contains(key))
                    deadKeys.Add(key);
            }

            foreach (int key in deadKeys)
            {
                _warriorTactics.Remove(key);
            }
        }

        /// <summary>
        /// Build the debug overlay text showing army phase, warrior counts per tactic,
        /// and economy summary.
        /// </summary>
        private void BuildDebugText(IGameState state)
        {
            int defending = 0, assaulting = 0, rallying = 0;
            foreach (var tactic in _warriorTactics.Values)
            {
                switch (tactic)
                {
                    case WarriorTactic.DEFENDING:  defending++;  break;
                    case WarriorTactic.ASSAULTING: assaulting++; break;
                    case WarriorTactic.RALLYING:   rallying++;   break;
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("Phase: ").AppendLine(_armyPhase.ToString());
            sb.Append("Gold: ").AppendLine(state.MyGold.ToString());
            sb.Append("Pawns: ").Append(myPawns.Count)
              .Append("  Warriors: ").AppendLine(myWarriors.Count.ToString());
            sb.Append("  Rally: ").Append(rallying)
              .Append("  Defend: ").Append(defending)
              .Append("  Assault: ").Append(assaulting);

            DebugText = sb.ToString();
        }

        #endregion

        #region Economy Helpers

        private void TrainPawns(IGameState state, IAgentActions actions)
        {
            foreach (int baseNbr in myBases)
            {
                var info = state.GetUnit(baseNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.PAWN]
                    && myPawns.Count < MAX_PAWNS)
                {
                    actions.Train(baseNbr, UnitType.PAWN);
                }
            }
        }

        private void TrainWarriors(IGameState state, IAgentActions actions)
        {
            foreach (int barracksNbr in myBarracks)
            {
                var info = state.GetUnit(barracksNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
                {
                    actions.Train(barracksNbr, UnitType.WARRIOR);
                }
            }
        }

        private void AssistConstruction(IGameState state, IAgentActions actions)
        {
            // Find any unfinished barracks or base and send idle pawns to help build
            foreach (UnitType buildingType in new[] { UnitType.BASE, UnitType.BARRACKS })
            {
                var buildingList = buildingType == UnitType.BASE ? myBases : myBarracks;
                foreach (int buildingNbr in buildingList)
                {
                    var info = state.GetUnit(buildingNbr);
                    if (!info.HasValue || info.Value.IsBuilt) continue;

                    // Send idle pawns to help build this structure
                    foreach (int pawn in myPawns)
                    {
                        var wInfo = state.GetUnit(pawn);
                        if (wInfo.HasValue && wInfo.Value.CurrentAction == UnitAction.IDLE)
                            actions.Build(pawn, info.Value.GridPosition, buildingType);
                    }
                    return; // Only assist one building at a time
                }
            }
        }

        private void RepairDamagedBuildings(IGameState state, IAgentActions actions)
        {
            if (state.MyGold <= 1000) return;

            foreach (var buildingList in new[] { myBases, myBarracks })
            {
                foreach (int buildingNbr in buildingList)
                {
                    var info = state.GetUnit(buildingNbr);
                    if (!info.HasValue || !info.Value.IsBuilt) continue;
                    float maxHp = GameConstants.HEALTH[info.Value.UnitType];
                    if (info.Value.Health >= maxHp * 0.5f) continue;

                    int sent = 0;
                    foreach (int pawn in myPawns)
                    {
                        if (sent >= 2) break;
                        var wInfo = state.GetUnit(pawn);
                        if (wInfo.HasValue && (wInfo.Value.CurrentAction == UnitAction.IDLE
                            || wInfo.Value.CurrentAction == UnitAction.GATHER))
                        {
                            actions.Repair(pawn, buildingNbr);
                            sent++;
                        }
                    }
                }
            }
        }

        private void GatherWithIdlePawns(IGameState state, IAgentActions actions)
        {
            if (mainBaseNbr < 0 || mainMineNbr < 0) return;
            var mineInfo = state.GetUnit(mainMineNbr);
            if (!mineInfo.HasValue || mineInfo.Value.Health <= 0) return;

            foreach (int pawn in myPawns)
            {
                var info = state.GetUnit(pawn);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                    actions.Gather(pawn, mainMineNbr, mainBaseNbr);
            }
        }

        private int FindClosestMine(IGameState state)
        {
            if (mines.Count == 0) return -1;
            if (myPawns.Count == 0) return mines[0];
            var pawnInfo = state.GetUnit(myPawns[0]);
            if (!pawnInfo.HasValue) return mines[0];

            Position pawnPos = pawnInfo.Value.GridPosition;
            int bestMine = -1;
            int bestPathLen = int.MaxValue;
            foreach (int mineNbr in mines)
            {
                var mineInfo = state.GetUnit(mineNbr);
                if (mineInfo.HasValue && mineInfo.Value.Health > 0)
                {
                    int pathLen = state.GetPathToUnit(pawnPos, UnitType.MINE, mineInfo.Value.GridPosition).Count;
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
                        float dist = Position.Distance(pawnPos, mineInfo.Value.CenterPosition);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestMine = mineNbr;
                        }
                    }
                }
            }

            return bestMine >= 0 ? bestMine : mines[0];
        }

        #endregion

        #region Building Helpers

        private void BuildStructure(UnitType type, IGameState state, IAgentActions actions)
        {
            foreach (int pawn in myPawns)
            {
                var info = state.GetUnit(pawn);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[type])
                {
                    Position buildPos = FindBestBuildPosition(type, state);
                    if (buildPos.X >= 0)
                    {
                        actions.Build(pawn, buildPos, type);
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

            for (int i = startIdx; i < path.Count; i++)
            {
                if (IsAreaBuildable(path[i], 4, state))
                {
                    _rallyPoint = path[i];
                    return _rallyPoint;
                }
            }

            for (int i = startIdx - 1; i >= 0; i--)
            {
                if (IsAreaBuildable(path[i], 4, state))
                {
                    _rallyPoint = path[i];
                    return _rallyPoint;
                }
            }

            _rallyPoint = path[startIdx];
            return _rallyPoint;
        }

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
        /// Find the closest enemy unit within a fixed radius.
        /// Returns the enemy unit number, or -1 if none found.
        /// </summary>
        private int FindEnemyInRadius(Position myPos, float radius, IGameState state)
        {
            float bestDist = float.MaxValue;
            int bestEnemy = -1;

            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.PAWN,
                                             UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY })
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
        /// Find closest enemy with priority: combat > pawn > building.
        /// When excluded is non-null, skips enemies in the set (target spreading).
        /// </summary>
        private int? FindClosestEnemy(int attackerNbr, IGameState state, HashSet<int> excluded)
        {
            var attackerInfo = state.GetUnit(attackerNbr);
            if (!attackerInfo.HasValue) return null;
            Position attackerPos = attackerInfo.Value.GridPosition;

            int? bestCombat = null;
            float bestCombatDist = float.MaxValue;
            int? bestPawn = null;
            float bestPawnDist = float.MaxValue;
            int? bestBuilding = null;
            float bestBuildingDist = float.MaxValue;

            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.PAWN,
                                            UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY })
            {
                bool isCombat = ut == UnitType.WARRIOR || ut == UnitType.ARCHER;
                bool isPawn = ut == UnitType.PAWN;
                var enemies = state.GetEnemyUnits(ut);
                foreach (int enemyNbr in enemies)
                {
                    if (excluded != null && excluded.Contains(enemyNbr)) continue;
                    var enemyInfo = state.GetUnit(enemyNbr);
                    if (!enemyInfo.HasValue) continue;
                    float dist = Position.Distance(attackerPos, enemyInfo.Value.CenterPosition);
                    if (isCombat && dist < bestCombatDist) { bestCombatDist = dist; bestCombat = enemyNbr; }
                    else if (isPawn && dist < bestPawnDist) { bestPawnDist = dist; bestPawn = enemyNbr; }
                    else if (!isCombat && !isPawn && dist < bestBuildingDist) { bestBuildingDist = dist; bestBuilding = enemyNbr; }
                }
            }
            return bestCombat ?? bestPawn ?? bestBuilding;
        }

        #endregion
    }
}
