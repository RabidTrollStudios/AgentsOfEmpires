using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [MEDIUM] Balanced economy (5 pawns), then masses archers.
    /// Waits for 16 archers before attacking. Uses a hybrid state machine:
    /// - Army-level FSM (ArmyPhase) determines overall strategy
    /// - Per-archer FSM (ArcherTactic) determines individual behavior
    /// Each tick: evaluate army phase, evaluate each archer's tactic, execute actions.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        #region Enums

        /// <summary>Army-level strategy phase.</summary>
        private enum ArmyPhase
        {
            /// <summary>No barracks yet — building infrastructure.</summary>
            ECONOMY,
            /// <summary>Building army, archers rally near barracks.</summary>
            RALLYING,
            /// <summary>Army committed — rally then attack with idle archers at rally.</summary>
            ATTACKING,
            /// <summary>Overwhelming advantage — all idle archers attack directly.</summary>
            MOPPING_UP
        }

        /// <summary>Per-archer tactical state, evaluated fresh each tick.</summary>
        private enum ArcherTactic
        {
            /// <summary>Moving to or holding at rally point.</summary>
            RALLYING,
            /// <summary>Stepping back from a warrior that is too close.</summary>
            KITING,
            /// <summary>Attacking a warrior from safe range (stationary fire).</summary>
            ENGAGING,
            /// <summary>Reacting to a non-warrior enemy within aggro range.</summary>
            DEFENDING,
            /// <summary>Attacking priority targets during attack/mop-up phase.</summary>
            ASSAULTING
        }

        #endregion

        #region Constants

        private const int MAX_PAWNS = 5;
        private const int ATTACK_THRESHOLD = 16;
        private const float RALLY_DISTANCE = 10f;
        private const float AGGRO_RANGE = 10f;
        private const float RETREAT_DIST = 3.0f;
        private const float RALLY_PROXIMITY = 3.0f;
        private const float DEFEND_RADIUS = 5.0f;
        private const int MAX_GANG_UP = 3;

        #endregion

        #region Fields

        private Position _rallyPoint = new Position(-1, -1);
        private ArmyPhase _armyPhase = ArmyPhase.ECONOMY;
        private Dictionary<int, ArcherTactic> _archerTactics = new Dictionary<int, ArcherTactic>();
        private Dictionary<int, int> _archerThreatTarget = new Dictionary<int, int>();
        private Dictionary<int, float> _previousHealth = new Dictionary<int, float>();
        private HashSet<int> _wasKiting = new HashSet<int>();

        #endregion

        #region Lifecycle

        public override void InitializeMatch() { }

        public override void InitializeRound(IGameState state)
        {
            base.InitializeRound(state);
            _rallyPoint = new Position(-1, -1);
            _armyPhase = ArmyPhase.ECONOMY;
            _archerTactics.Clear();
            _archerThreatTarget.Clear();
            _previousHealth.Clear();
            _wasKiting.Clear();
        }

        #endregion

        #region Update (5-phase loop)

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;
            if (mainMineNbr < 0 || !state.GetUnit(mainMineNbr).HasValue || state.GetUnit(mainMineNbr).Value.Health <= 0)
                mainMineNbr = FindClosestMine(state);

            // ---- Phase 1: Economy (unchanged) ----
            if (myBases.Count == 0)
            {
                BuildStructure(UnitType.BASE, state, actions);
                return;
            }

            TrainPawns(state, actions);

            if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);

            GatherWithIdlePawns(state, actions);
            TrainArchers(state, actions);

            // ---- Phase 2: Evaluate army phase ----
            Position rallyPoint = ComputeRallyPoint(state);
            UpdateArmyPhase(state);

            // ---- Phase 3: Evaluate each archer's tactical state ----
            EvaluateArcherTactics(state, rallyPoint);

            // ---- Phase 4: Execute actions for each archer ----
            ExecuteArcherActions(state, actions, rallyPoint);

            // ---- Phase 5: Clean up dead archers ----
            CleanDeadArchers();

            // Snapshot which archers are kiting this tick (before next tick's MOVE check)
            SnapshotKiting();

            // ---- Debug overlay ----
            BuildDebugText(state);

            // Snapshot health for next-tick damage detection
            SnapshotHealth(state);
        }

        private void SnapshotKiting()
        {
            _wasKiting.Clear();
            foreach (var kvp in _archerTactics)
            {
                if (kvp.Value == ArcherTactic.KITING)
                    _wasKiting.Add(kvp.Key);
            }
        }

        private void SnapshotHealth(IGameState state)
        {
            _previousHealth.Clear();
            foreach (int archerNbr in myArchers)
            {
                var info = state.GetUnit(archerNbr);
                if (info.HasValue)
                    _previousHealth[archerNbr] = info.Value.Health;
            }
        }

        #endregion

        #region Phase 2: Army Phase

        /// <summary>
        /// Evaluate the global army phase based on army size and enemy composition.
        /// Re-evaluated from scratch each tick (not sticky) so losing archers in
        /// combat naturally drops from ATTACKING back to RALLYING.
        /// </summary>
        private void UpdateArmyPhase(IGameState state)
        {
            int enemyCombat = enemyWarriors.Count + enemyArchers.Count;

            // MOPPING_UP: overwhelming advantage — requires full army to prevent
            // early archers from rushing buildings before enemy warriors spawn
            if (myArchers.Count >= ATTACK_THRESHOLD
                && (enemyCombat == 0 || myArchers.Count >= 4 * enemyCombat))
            {
                _armyPhase = ArmyPhase.MOPPING_UP;
                return;
            }

            // ATTACKING: army ready
            if (myArchers.Count >= ATTACK_THRESHOLD)
            {
                _armyPhase = ArmyPhase.ATTACKING;
                return;
            }

            // RALLYING: have built barracks, building army
            if (myBarracks.Count > 0 && HasBuiltUnit(myBarracks, state))
            {
                _armyPhase = ArmyPhase.RALLYING;
                return;
            }

            // ECONOMY: still building infrastructure
            _armyPhase = ArmyPhase.ECONOMY;
        }

        #endregion

        #region Phase 3: Per-Archer Tactical Evaluation

        /// <summary>
        /// For each living archer, determine its tactical state for this tick.
        /// Warrior threat handling (KITING/ENGAGING) is phase-independent so
        /// archers always kite when a warrior gets close, regardless of army phase.
        /// Phase-specific logic handles non-warrior threats and rally/assault decisions.
        /// </summary>
        private void EvaluateArcherTactics(IGameState state, Position rallyPoint)
        {
            float archerRange = GameConstants.EffectiveAttackRange(UnitType.ARCHER, UnitType.WARRIOR);

            foreach (int archerNbr in myArchers)
            {
                var info = state.GetUnit(archerNbr);
                if (!info.HasValue) continue;

                var curAction = info.Value.CurrentAction;

                // Skip archers doing non-combat actions
                if (curAction == UnitAction.BUILD || curAction == UnitAction.TRAIN
                    || curAction == UnitAction.GATHER)
                {
                    _archerTactics[archerNbr] = ArcherTactic.RALLYING;
                    _archerThreatTarget.Remove(archerNbr);
                    continue;
                }

                Position myPos = info.Value.CenterPosition;

                // ---- Warrior threat evaluation (all phases) ----
                float closestWarriorDist = float.MaxValue;
                int closestWarriorNbr = -1;

                foreach (int enemyNbr in state.GetEnemyUnits(UnitType.WARRIOR))
                {
                    var enemyInfo = state.GetUnit(enemyNbr);
                    if (!enemyInfo.HasValue) continue;
                    float dist = Position.Distance(myPos, enemyInfo.Value.CenterPosition);
                    if (dist < AGGRO_RANGE && dist < closestWarriorDist)
                    {
                        closestWarriorDist = dist;
                        closestWarriorNbr = enemyNbr;
                    }
                }

                // KITING — warrior dangerously close
                if (closestWarriorNbr >= 0 && closestWarriorDist <= RETREAT_DIST)
                {
                    _archerTactics[archerNbr] = ArcherTactic.KITING;
                    _archerThreatTarget[archerNbr] = closestWarriorNbr;
                    continue;
                }

                // ENGAGING — warrior within attack range, archer can fire
                if (closestWarriorNbr >= 0 && closestWarriorDist <= archerRange
                    && curAction == UnitAction.IDLE)
                {
                    _archerTactics[archerNbr] = ArcherTactic.ENGAGING;
                    _archerThreatTarget[archerNbr] = closestWarriorNbr;
                    continue;
                }

                // Hold — warrior within attack range and archer mid-action
                // (kiting, chasing, or attacking from range). Keep current tactic.
                if (closestWarriorNbr >= 0 && closestWarriorDist <= archerRange
                    && (curAction == UnitAction.MOVE || curAction == UnitAction.ATTACK))
                {
                    if (!_archerTactics.ContainsKey(archerNbr))
                        _archerTactics[archerNbr] = curAction == UnitAction.ATTACK
                            ? ArcherTactic.ENGAGING : ArcherTactic.RALLYING;
                    _archerThreatTarget[archerNbr] = closestWarriorNbr;
                    continue;
                }

                // ---- Phase-specific non-warrior evaluation ----

                if (_armyPhase == ArmyPhase.ECONOMY || _armyPhase == ArmyPhase.RALLYING)
                {
                    // Already attacking a non-warrior — hold
                    if (curAction == UnitAction.ATTACK)
                    {
                        if (!_archerTactics.ContainsKey(archerNbr))
                            _archerTactics[archerNbr] = ArcherTactic.DEFENDING;
                        continue;
                    }

                    // Being attacked (health dropped) — fight back
                    if (_previousHealth.TryGetValue(archerNbr, out float prevHp)
                        && info.Value.Health < prevHp)
                    {
                        int? attacker = FindClosestEnemy(archerNbr, state, null, false);
                        if (attacker.HasValue)
                        {
                            _archerTactics[archerNbr] = ArcherTactic.DEFENDING;
                            _archerThreatTarget[archerNbr] = attacker.Value;
                            continue;
                        }
                    }

                    // Non-warrior enemy within defend radius
                    if (curAction == UnitAction.IDLE || curAction == UnitAction.MOVE)
                    {
                        int nearEnemy = FindEnemyInRadius(myPos, DEFEND_RADIUS, state);
                        if (nearEnemy >= 0)
                        {
                            _archerTactics[archerNbr] = ArcherTactic.DEFENDING;
                            _archerThreatTarget[archerNbr] = nearEnemy;
                            continue;
                        }
                    }

                    _archerTactics[archerNbr] = ArcherTactic.RALLYING;
                    _archerThreatTarget.Remove(archerNbr);
                    continue;
                }

                // ---- ATTACKING / MOPPING_UP ----

                // Hold — warrior within AGGRO_RANGE and archer mid-action
                if (closestWarriorNbr >= 0 && (curAction == UnitAction.MOVE || curAction == UnitAction.ATTACK))
                {
                    if (!_archerTactics.ContainsKey(archerNbr))
                        _archerTactics[archerNbr] = curAction == UnitAction.ATTACK
                            ? ArcherTactic.ENGAGING : ArcherTactic.RALLYING;
                    _archerThreatTarget[archerNbr] = closestWarriorNbr;
                    continue;
                }

                // Find closest non-warrior enemy within AGGRO_RANGE
                float closestEnemyDist = float.MaxValue;
                int closestEnemyNbr = -1;

                foreach (UnitType ut in new[] { UnitType.ARCHER, UnitType.PAWN,
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

                // DEFENDING — non-warrior enemy nearby
                if (closestEnemyNbr >= 0
                    && (curAction == UnitAction.IDLE || curAction == UnitAction.MOVE))
                {
                    _archerTactics[archerNbr] = ArcherTactic.DEFENDING;
                    _archerThreatTarget[archerNbr] = closestEnemyNbr;
                    continue;
                }

                // ASSAULTING — army phase says attack and archer is idle
                if (curAction == UnitAction.IDLE)
                {
                    if (_armyPhase == ArmyPhase.MOPPING_UP)
                    {
                        _archerTactics[archerNbr] = ArcherTactic.ASSAULTING;
                        _archerThreatTarget.Remove(archerNbr);
                        continue;
                    }

                    if (_armyPhase == ArmyPhase.ATTACKING && rallyPoint.X >= 0)
                    {
                        float distToRally = Position.Distance(myPos, rallyPoint);
                        if (distToRally <= RALLY_PROXIMITY)
                        {
                            _archerTactics[archerNbr] = ArcherTactic.ASSAULTING;
                            _archerThreatTarget.Remove(archerNbr);
                            continue;
                        }
                    }
                }

                // RALLYING — default
                _archerTactics[archerNbr] = ArcherTactic.RALLYING;
                _archerThreatTarget.Remove(archerNbr);
            }
        }

        #endregion

        #region Phase 4: Execute Actions

        /// <summary>
        /// Execute the action for each archer based on its resolved tactical state.
        /// Each archer is processed exactly once via a single switch dispatch.
        /// </summary>
        private void ExecuteArcherActions(IGameState state, IAgentActions actions, Position rallyPoint)
        {
            var gangUpCounts = new Dictionary<int, int>();

            foreach (int archerNbr in myArchers)
            {
                if (!_archerTactics.TryGetValue(archerNbr, out var tactic)) continue;

                switch (tactic)
                {
                    case ArcherTactic.KITING:
                        ExecuteKiting(archerNbr, state, actions);
                        break;
                    case ArcherTactic.ENGAGING:
                        ExecuteEngaging(archerNbr, state, actions);
                        break;
                    case ArcherTactic.DEFENDING:
                        ExecuteDefending(archerNbr, state, actions, gangUpCounts);
                        break;
                    case ArcherTactic.ASSAULTING:
                        ExecuteAssaulting(archerNbr, state, actions, gangUpCounts);
                        break;
                    case ArcherTactic.RALLYING:
                        ExecuteRallying(archerNbr, state, actions, rallyPoint);
                        break;
                }
            }
        }

        /// <summary>
        /// Step back from nearest warrior. Find best adjacent buildable cell away from
        /// threat using dot-product scoring. If direct retreat fails (score too low or
        /// no buildable cell), circle laterally around the warrior to find an escape route.
        /// </summary>
        private void ExecuteKiting(int archerNbr, IGameState state, IAgentActions actions)
        {
            var info = state.GetUnit(archerNbr);
            if (!info.HasValue) return;

            // If already moving from a previous kite step, don't interrupt — let it finish.
            // But if MOVE comes from an Attack-chase (archer was ASSAULTING/ENGAGING last tick),
            // we DO want to interrupt it so the archer kites instead of running into melee.
            if (info.Value.CurrentAction == UnitAction.MOVE && _wasKiting.Contains(archerNbr)) return;

            if (!_archerThreatTarget.TryGetValue(archerNbr, out int threatNbr)) return;
            var threatInfo = state.GetUnit(threatNbr);
            if (!threatInfo.HasValue) return;

            Position myPos = info.Value.CenterPosition;
            Position threatPos = threatInfo.Value.CenterPosition;

            // Retreat direction (away from threat)
            float retreatX = myPos.X - threatPos.X;
            float retreatY = myPos.Y - threatPos.Y;

            // Perpendicular direction (for circling). Pick the perpendicular that
            // points more toward map center to avoid circling into corners.
            Position mapCenter = new Position(state.MapSize.X / 2, state.MapSize.Y / 2);
            float perpX1 = -retreatY;
            float perpY1 = retreatX;
            float perpX2 = retreatY;
            float perpY2 = -retreatX;
            // Pick the perpendicular that points more toward map center
            float dot1 = perpX1 * (mapCenter.X - myPos.X) + perpY1 * (mapCenter.Y - myPos.Y);
            float dot2 = perpX2 * (mapCenter.X - myPos.X) + perpY2 * (mapCenter.Y - myPos.Y);
            float circleX = dot1 >= dot2 ? perpX1 : perpX2;
            float circleY = dot1 >= dot2 ? perpY1 : perpY2;

            Position bestRetreat = new Position(-1, -1);
            float bestRetreatScore = float.MinValue;
            Position bestCircle = new Position(-1, -1);
            float bestCircleScore = float.MinValue;

            for (int ddx = -1; ddx <= 1; ddx++)
            {
                for (int ddy = -1; ddy <= 1; ddy++)
                {
                    if (ddx == 0 && ddy == 0) continue;
                    Position cell = new Position(myPos.X + ddx, myPos.Y + ddy);
                    if (!state.IsPositionBuildable(cell)) continue;

                    // Retreat score: dot product with retreat direction
                    float retreatScore = ddx * retreatX + ddy * retreatY;
                    if (retreatScore > bestRetreatScore)
                    {
                        bestRetreatScore = retreatScore;
                        bestRetreat = cell;
                    }

                    // Circle score: dot product with perpendicular direction
                    float circleScore = ddx * circleX + ddy * circleY;
                    if (circleScore > bestCircleScore)
                    {
                        bestCircleScore = circleScore;
                        bestCircle = cell;
                    }
                }
            }

            // Use retreat if it actually moves away from threat (score > 0),
            // otherwise circle around to find an escape route
            if (bestRetreatScore > 0 && bestRetreat.X >= 0)
                actions.Move(archerNbr, bestRetreat);
            else if (bestCircle.X >= 0)
                actions.Move(archerNbr, bestCircle);
            // else: completely boxed in — hold position
        }

        /// <summary>
        /// Attack the warrior from current position. Only fires if IDLE — if already
        /// attacking from range, the engine handles it and we don't interfere.
        /// </summary>
        private void ExecuteEngaging(int archerNbr, IGameState state, IAgentActions actions)
        {
            var info = state.GetUnit(archerNbr);
            if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) return;

            if (_archerThreatTarget.TryGetValue(archerNbr, out int threatNbr))
            {
                var threatInfo = state.GetUnit(threatNbr);
                if (threatInfo.HasValue && threatInfo.Value.Health > 0)
                    actions.Attack(archerNbr, threatNbr);
            }
        }

        /// <summary>
        /// Attack closest non-warrior enemy within aggro range. Skips warriors beyond
        /// effective attack range to prevent pursuit into melee. Uses target spreading.
        /// </summary>
        private void ExecuteDefending(int archerNbr, IGameState state, IAgentActions actions,
            Dictionary<int, int> gangUpCounts)
        {
            var info = state.GetUnit(archerNbr);
            if (!info.HasValue) return;

            var curAction = info.Value.CurrentAction;
            if (curAction != UnitAction.IDLE && curAction != UnitAction.MOVE) return;

            if (_archerThreatTarget.TryGetValue(archerNbr, out int threatNbr))
            {
                var threatInfo = state.GetUnit(threatNbr);
                if (threatInfo.HasValue && threatInfo.Value.Health > 0)
                {
                    // Don't chase warriors — only engage them from within attack range
                    if (threatInfo.Value.UnitType == UnitType.WARRIOR)
                    {
                        float range = GameConstants.EffectiveAttackRange(UnitType.ARCHER, UnitType.WARRIOR);
                        float dist = Position.Distance(info.Value.CenterPosition, threatInfo.Value.CenterPosition);
                        if (dist > range) return;
                    }

                    // Prefer an enemy already being ganged up on (under the cap)
                    if (gangUpCounts.TryGetValue(threatNbr, out int count) && count >= MAX_GANG_UP)
                        return;

                    actions.Attack(archerNbr, threatNbr);
                    gangUpCounts[threatNbr] = gangUpCounts.TryGetValue(threatNbr, out int c) ? c + 1 : 1;
                }
            }
        }

        /// <summary>
        /// Attack closest enemy with priority: combat > pawn > building.
        /// Only acts on IDLE archers. Uses target spreading so archers don't all
        /// pile on the same enemy.
        /// </summary>
        private void ExecuteAssaulting(int archerNbr, IGameState state, IAgentActions actions,
            Dictionary<int, int> gangUpCounts)
        {
            var info = state.GetUnit(archerNbr);
            if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) return;

            int? target = FindClosestEnemy(archerNbr, state, gangUpCounts, preferUnderCap: true)
                       ?? FindClosestEnemy(archerNbr, state, null, preferUnderCap: false);
            if (target.HasValue)
            {
                actions.Attack(archerNbr, target.Value);
                gangUpCounts[target.Value] = gangUpCounts.TryGetValue(target.Value, out int c) ? c + 1 : 1;
            }
        }

        /// <summary>
        /// If idle and not yet near the rally point, move toward it.
        /// Once within RALLY_PROXIMITY, stay put — don't keep re-issuing Move commands.
        /// Targets a buildable cell on the far side of the 4x4 rally area so units spread out.
        /// </summary>
        private void ExecuteRallying(int archerNbr, IGameState state, IAgentActions actions,
            Position rallyPoint)
        {
            if (rallyPoint.X < 0) return;

            var info = state.GetUnit(archerNbr);
            if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) return;

            Position myPos = info.Value.CenterPosition;
            float distToRally = Position.Distance(myPos, rallyPoint);
            if (distToRally <= RALLY_PROXIMITY) return;

            Position targetCell = FindRallyCell(myPos, rallyPoint, state);
            actions.Move(archerNbr, targetCell);
        }

        #endregion

        #region Phase 5: Cleanup

        /// <summary>
        /// Remove entries for archers that no longer exist (died this tick).
        /// </summary>
        private void CleanDeadArchers()
        {
            var archerSet = new HashSet<int>(myArchers);
            var deadKeys = new List<int>();

            foreach (int key in _archerTactics.Keys)
            {
                if (!archerSet.Contains(key))
                    deadKeys.Add(key);
            }

            foreach (int key in deadKeys)
            {
                _archerTactics.Remove(key);
                _archerThreatTarget.Remove(key);
            }
        }

        /// <summary>
        /// Build the debug overlay text showing army phase, archer counts per tactic,
        /// and economy summary.
        /// </summary>
        private void BuildDebugText(IGameState state)
        {
            int kiting = 0, engaging = 0, defending = 0, assaulting = 0, rallying = 0;
            foreach (var tactic in _archerTactics.Values)
            {
                switch (tactic)
                {
                    case ArcherTactic.KITING:    kiting++;    break;
                    case ArcherTactic.ENGAGING:   engaging++;   break;
                    case ArcherTactic.DEFENDING:  defending++;  break;
                    case ArcherTactic.ASSAULTING: assaulting++; break;
                    case ArcherTactic.RALLYING:   rallying++;   break;
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("Phase: ").AppendLine(_armyPhase.ToString());
            sb.Append("Gold: ").AppendLine(state.MyGold.ToString());
            sb.Append("Pawns: ").Append(myPawns.Count)
              .Append("  Archers: ").AppendLine(myArchers.Count.ToString());
            sb.Append("  Rally: ").Append(rallying)
              .Append("  Kite: ").Append(kiting)
              .Append("  Engage: ").Append(engaging)
              .Append("  Defend: ").Append(defending)
              .Append("  Assault: ").Append(assaulting);

            DebugText = sb.ToString();
        }

        #endregion

        #region Economy Helpers (unchanged)

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

        private void TrainArchers(IGameState state, IAgentActions actions)
        {
            foreach (int barracksNbr in myBarracks)
            {
                var info = state.GetUnit(barracksNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.ARCHER])
                {
                    actions.Train(barracksNbr, UnitType.ARCHER);
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

        #region Building Helpers (unchanged)

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

            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.PAWN,
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

            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.PAWN,
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

        private int? FindClosestEnemy(int attackerNbr, IGameState state,
            Dictionary<int, int> gangUpCounts, bool preferUnderCap = false)
        {
            var attackerInfo = state.GetUnit(attackerNbr);
            if (!attackerInfo.HasValue) return null;
            Position attackerPos = attackerInfo.Value.GridPosition;

            // Priority: combat units > pawns > buildings
            int? bestCombat = null;
            float bestCombatDist = float.MaxValue;
            int? bestPawn = null;
            float bestPawnDist = float.MaxValue;
            int? bestBuilding = null;
            float bestBuildingDist = float.MaxValue;

            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.PAWN,
                                            UnitType.BASE, UnitType.BARRACKS })
            {
                bool isCombat = ut == UnitType.WARRIOR || ut == UnitType.ARCHER;
                bool isPawn = ut == UnitType.PAWN;
                var enemies = state.GetEnemyUnits(ut);
                foreach (int enemyNbr in enemies)
                {
                    if (gangUpCounts != null && preferUnderCap)
                    {
                        if (gangUpCounts.TryGetValue(enemyNbr, out int count) && count >= MAX_GANG_UP)
                            continue;
                    }
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
