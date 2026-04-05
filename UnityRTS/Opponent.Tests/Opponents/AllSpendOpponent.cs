using System.Collections.Generic;
using AgentSDK;

namespace Opponent.Tests
{
    /// <summary>
    /// [IMPOSSIBLE] Adaptive all-unit agent with monk sustain and reactive scouting.
    /// Scouts enemy buildings to pick counter-unit priority. Scales economy
    /// when gold-starved, builds more production when gold-rich, adds monks
    /// when taking losses. Uses all micro tactics: warrior squads, archer
    /// volley+kite, lancer joust. Guaranteed at least 1 monastery.
    /// </summary>
    public class AllSpendOpponent : PlanningAgentBase
    {
        private const int ATTACK_THRESHOLD = 4;
        private const float GOLD_STARVED = 100f;
        private const float GOLD_RICH = 150f;
        private const int ATTACK_TICKS = 2;
        private const int KITE_TICKS = 1;
        private const int CYCLE_LENGTH = ATTACK_TICKS + KITE_TICKS;
        private const int RETREAT_DISTANCE = 2;
        private const float ANIM_DURATION_BASE = 0.5f; // 6 frames at 12 FPS
        private const float FRAME_DURATION = 0.02f;  // 50 Hz fixed update

        private enum JoustState { Charging, Striking, Retreating }

        private int _lastArmySize;
        private int _ticksSinceArmyShrunk;
        private UnitType _priorityUnit = UnitType.MINE;
        private Dictionary<int, int> _lastArcherTarget = new Dictionary<int, int>();
        private Dictionary<int, int> _archerCycleTick = new Dictionary<int, int>();
        private Dictionary<int, JoustState> _joustState = new Dictionary<int, JoustState>();
        private Dictionary<int, int> _strikeTicks = new Dictionary<int, int>();

        public override void InitializeMatch()
        {
            _lastArmySize = 0;
            _ticksSinceArmyShrunk = 999;
            _priorityUnit = UnitType.MINE;
            _lastArcherTarget = new Dictionary<int, int>();
            _archerCycleTick = new Dictionary<int, int>();
            _joustState = new Dictionary<int, JoustState>();
            _strikeTicks = new Dictionary<int, int>();
        }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = FindClosestMine(state);
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            if (myBases.Count == 0)
            {
                BuildStructure(UnitType.BASE, state, actions);
                return;
            }

            int armySize = myWarriors.Count + myArchers.Count + myLancers.Count;
            if (armySize < _lastArmySize) _ticksSinceArmyShrunk = 0;
            else _ticksSinceArmyShrunk++;
            _lastArmySize = armySize;

            // Scout enemy and pick counter priority
            if (_priorityUnit == UnitType.MINE)
            {
                if (state.GetEnemyUnits(UnitType.BARRACKS).Count > 0)
                    _priorityUnit = UnitType.LANCER;
                else if (state.GetEnemyUnits(UnitType.ARCHERY).Count > 0)
                    _priorityUnit = UnitType.WARRIOR;
                else if (state.GetEnemyUnits(UnitType.TOWER).Count > 0)
                    _priorityUnit = UnitType.ARCHER;
            }

            int enemyArmy = state.GetEnemyUnits(UnitType.WARRIOR).Count
                + state.GetEnemyUnits(UnitType.ARCHER).Count
                + state.GetEnemyUnits(UnitType.LANCER).Count;
            bool goldStarved = state.MyGold < GOLD_STARVED;
            bool goldRich = state.MyGold > GOLD_RICH;
            bool takingLosses = _ticksSinceArmyShrunk < 20;
            bool outnumbered = enemyArmy > armySize;
            bool needMorePawns = myPawns.Count < 5 || (goldStarved && myPawns.Count < 8);

            if (needMorePawns)
            {
                foreach (int baseNbr in myBases)
                {
                    var info = state.GetUnit(baseNbr);
                    if (info.HasValue && info.Value.IsBuilt
                        && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.PAWN])
                    {
                        actions.Train(baseNbr, UnitType.PAWN);
                    }
                }
            }

            // Build priority building first, then monastery, then fill out all 3 combat buildings
            UnitType priorityBuilding = _priorityUnit == UnitType.LANCER ? UnitType.TOWER
                : _priorityUnit == UnitType.WARRIOR ? UnitType.BARRACKS
                : _priorityUnit == UnitType.ARCHER ? UnitType.ARCHERY
                : UnitType.BARRACKS;

            int totalCombatBuildings = myBarracks.Count + myArchery.Count + myTowers.Count;

            if (GetBuildingCount(priorityBuilding) == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(priorityBuilding, state, actions);
            else if (myMonasteries.Count == 0 && totalCombatBuildings > 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.MONASTERY, state, actions);
            else if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);
            else if (myArchery.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.ARCHERY, state, actions);
            else if (myTowers.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.TOWER, state, actions);
            else if (goldRich && GetBuildingCount(priorityBuilding) < 2 && HasBuiltUnit(myBases, state))
                BuildStructure(priorityBuilding, state, actions);

            GatherWithIdlePawns(state, actions);

            // Train from all combat buildings
            foreach (int barracksNbr in myBarracks)
            {
                var info = state.GetUnit(barracksNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
                    actions.Train(barracksNbr, UnitType.WARRIOR);
            }
            foreach (int archeryNbr in myArchery)
            {
                var info = state.GetUnit(archeryNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.ARCHER])
                    actions.Train(archeryNbr, UnitType.ARCHER);
            }
            foreach (int towerNbr in myTowers)
            {
                var info = state.GetUnit(towerNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.LANCER])
                    actions.Train(towerNbr, UnitType.LANCER);
            }

            // Monks — more when taking losses
            if ((takingLosses && myMonks.Count < 3) || myMonks.Count < 1)
            {
                foreach (int monasteryNbr in myMonasteries)
                {
                    var info = state.GetUnit(monasteryNbr);
                    if (info.HasValue && info.Value.IsBuilt
                        && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.MONK])
                        actions.Train(monasteryNbr, UnitType.MONK);
                }
            }

            HealWithMonks(state, actions);

            // Attack with unit-specific micro
            if (armySize < ATTACK_THRESHOLD) return;

            SquadAttack(myWarriors, 3, state, actions);
            ArcherVolleyKite(state, actions);
            LancerJoust(state, actions);
        }

        private int GetBuildingCount(UnitType type)
        {
            if (type == UnitType.BARRACKS) return myBarracks.Count;
            if (type == UnitType.ARCHERY) return myArchery.Count;
            if (type == UnitType.TOWER) return myTowers.Count;
            return 0;
        }

        private void SquadAttack(List<int> units, int squadSize, IGameState state, IAgentActions actions)
        {
            var enemies = new List<int>();
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER,
                                            UnitType.MONK, UnitType.PAWN, UnitType.BASE,
                                            UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER,
                                            UnitType.MONASTERY })
            {
                foreach (int enemyNbr in state.GetEnemyUnits(ut))
                    enemies.Add(enemyNbr);
            }
            if (enemies.Count == 0) return;
            int targetIdx = 0;
            int assigned = 0;
            foreach (int unitNbr in units)
            {
                var info = state.GetUnit(unitNbr);
                if (!info.HasValue) continue;
                if (info.Value.CurrentAction == UnitAction.IDLE)
                {
                    actions.Attack(unitNbr, enemies[targetIdx]);
                    assigned++;
                    if (assigned >= squadSize && targetIdx < enemies.Count - 1)
                    { targetIdx++; assigned = 0; }
                }
            }
        }

        private void ArcherVolleyKite(IGameState state, IAgentActions actions)
        {
            var enemies = new List<int>();
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER,
                                            UnitType.MONK, UnitType.PAWN, UnitType.BASE,
                                            UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER,
                                            UnitType.MONASTERY })
            {
                foreach (int enemyNbr in state.GetEnemyUnits(ut))
                    enemies.Add(enemyNbr);
            }
            if (enemies.Count == 0) return;
            foreach (int archerNbr in myArchers)
            {
                var info = state.GetUnit(archerNbr);
                if (!info.HasValue) continue;
                if (!_archerCycleTick.ContainsKey(archerNbr)) _archerCycleTick[archerNbr] = 0;
                int cycleTick = _archerCycleTick[archerNbr] % CYCLE_LENGTH;
                _archerCycleTick[archerNbr]++;
                if (cycleTick < ATTACK_TICKS)
                {
                    int lastTarget = _lastArcherTarget.ContainsKey(archerNbr) ? _lastArcherTarget[archerNbr] : -1;
                    int chosenTarget = -1;
                    foreach (int enemyNbr in enemies) { if (enemyNbr != lastTarget) { chosenTarget = enemyNbr; break; } }
                    if (chosenTarget < 0) chosenTarget = enemies[0];
                    actions.Attack(archerNbr, chosenTarget);
                    _lastArcherTarget[archerNbr] = chosenTarget;
                }
                else
                {
                    if (mainBaseNbr >= 0) { var b = state.GetUnit(mainBaseNbr); if (b.HasValue) actions.Move(archerNbr, b.Value.CenterPosition); }
                }
            }
        }

        private void LancerJoust(IGameState state, IAgentActions actions)
        {
            foreach (int lancerNbr in myLancers)
            {
                var info = state.GetUnit(lancerNbr);
                if (!info.HasValue) continue;

                if (!_joustState.ContainsKey(lancerNbr))
                {
                    _joustState[lancerNbr] = JoustState.Charging;
                    _strikeTicks[lancerNbr] = 0;
                }

                switch (_joustState[lancerNbr])
                {
                    case JoustState.Charging:
                        if (info.Value.CurrentAction == UnitAction.IDLE)
                        {
                            int? target = FindPriorityTarget(state, info.Value.CenterPosition);
                            if (target.HasValue)
                                actions.Attack(lancerNbr, target.Value);
                        }
                        else if (info.Value.CurrentAction == UnitAction.ATTACK)
                        {
                            var atkTarget = info.Value.AttackTargetNbr >= 0
                                ? state.GetUnit(info.Value.AttackTargetNbr) : null;
                            if (atkTarget.HasValue)
                            {
                                float dist = Position.Distance(info.Value.CenterPosition,
                                    atkTarget.Value.CenterPosition);
                                float range = GameConstants.EffectiveAttackRange(
                                    UnitType.LANCER, atkTarget.Value.UnitType) + 0.5f;
                                if (dist < range)
                                {
                                    _joustState[lancerNbr] = JoustState.Striking;
                                    _strikeTicks[lancerNbr] = 0;
                                }
                            }
                        }
                        break;

                    case JoustState.Striking:
                        _strikeTicks[lancerNbr]++;
                        int framesPerAnim = System.Math.Max(1,
                            (int)System.Math.Ceiling(ANIM_DURATION_BASE / (state.GameSpeed * FRAME_DURATION)));
                        if (_strikeTicks[lancerNbr] >= framesPerAnim)
                        {
                            var atkTarget = info.Value.AttackTargetNbr >= 0
                                ? state.GetUnit(info.Value.AttackTargetNbr) : null;
                            Position enemyPos = atkTarget.HasValue
                                ? atkTarget.Value.GridPosition : info.Value.GridPosition;
                            Position? retreat = FindRetreatPosition(
                                info.Value.GridPosition, enemyPos, RETREAT_DISTANCE, state);
                            if (retreat.HasValue)
                            {
                                actions.Move(lancerNbr, retreat.Value);
                                _joustState[lancerNbr] = JoustState.Retreating;
                            }
                            else
                            {
                                _strikeTicks[lancerNbr] = 0;
                            }
                        }
                        break;

                    case JoustState.Retreating:
                        if (info.Value.CurrentAction == UnitAction.IDLE)
                        {
                            int? target = FindPriorityTarget(state, info.Value.CenterPosition);
                            if (target.HasValue)
                                actions.Attack(lancerNbr, target.Value);
                            _joustState[lancerNbr] = JoustState.Charging;
                        }
                        break;
                }
            }
        }

        private int? FindPriorityTarget(IGameState state, Position fromPos)
        {
            int? best = null;
            float bestDist = float.MaxValue;
            Position myPos = fromPos;

            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER,
                                            UnitType.MONK, UnitType.PAWN })
            {
                foreach (int enemyNbr in state.GetEnemyUnits(ut))
                {
                    var info = state.GetUnit(enemyNbr);
                    if (!info.HasValue) continue;
                    float dist = Position.Distance(info.Value.CenterPosition, myPos);
                    if (dist < bestDist) { bestDist = dist; best = enemyNbr; }
                }
            }
            if (best.HasValue) return best;

            foreach (UnitType ut in new[] { UnitType.BASE, UnitType.BARRACKS,
                                            UnitType.ARCHERY, UnitType.TOWER, UnitType.MONASTERY })
            {
                foreach (int enemyNbr in state.GetEnemyUnits(ut))
                {
                    var info = state.GetUnit(enemyNbr);
                    if (!info.HasValue) continue;
                    float dist = Position.Distance(info.Value.CenterPosition, myPos);
                    if (dist < bestDist) { bestDist = dist; best = enemyNbr; }
                }
            }
            return best;
        }

        private Position? FindRetreatPosition(Position from, Position enemy, int distance, IGameState state)
        {
            int dx = from.X - enemy.X;
            int dy = from.Y - enemy.Y;
            int sx = dx > 0 ? 1 : dx < 0 ? -1 : 0;
            int sy = dy > 0 ? 1 : dy < 0 ? -1 : 0;
            if (sx == 0 && sy == 0) sx = 1;
            int[][] dirs = new int[][] {
                new[] { sx, sy }, new[] { sx, 0 }, new[] { 0, sy },
                new[] { -sy, sx }, new[] { sy, -sx },
            };
            foreach (var dir in dirs)
            {
                bool clear = true;
                for (int i = 1; i <= distance; i++)
                {
                    if (!state.IsPositionBuildable(new Position(from.X + dir[0] * i, from.Y + dir[1] * i)))
                    { clear = false; break; }
                }
                if (clear) return new Position(from.X + dir[0] * distance, from.Y + dir[1] * distance);
            }
            return null;
        }

        private void HealWithMonks(IGameState state, IAgentActions actions)
        {
            foreach (int monkNbr in myMonks)
            {
                var monkInfo = state.GetUnit(monkNbr);
                if (!monkInfo.HasValue || monkInfo.Value.CurrentAction != UnitAction.IDLE) continue;
                if (monkInfo.Value.Mana < GameConstants.MANA_COST) continue;
                int? bestTarget = null;
                float lowestHpRatio = 0.8f;
                foreach (int unitNbr in myWarriors) { var info = state.GetUnit(unitNbr); if (!info.HasValue) continue; float ratio = info.Value.Health / GameConstants.HEALTH[UnitType.WARRIOR]; if (ratio < lowestHpRatio) { lowestHpRatio = ratio; bestTarget = unitNbr; } }
                foreach (int unitNbr in myArchers) { var info = state.GetUnit(unitNbr); if (!info.HasValue) continue; float ratio = info.Value.Health / GameConstants.HEALTH[UnitType.ARCHER]; if (ratio < lowestHpRatio) { lowestHpRatio = ratio; bestTarget = unitNbr; } }
                foreach (int unitNbr in myLancers) { var info = state.GetUnit(unitNbr); if (!info.HasValue) continue; float ratio = info.Value.Health / GameConstants.HEALTH[UnitType.LANCER]; if (ratio < lowestHpRatio) { lowestHpRatio = ratio; bestTarget = unitNbr; } }
                if (bestTarget.HasValue) actions.Heal(monkNbr, bestTarget.Value);
            }
        }

        private void GatherWithIdlePawns(IGameState state, IAgentActions actions)
        {
            if (mainBaseNbr < 0 || mainMineNbr < 0) return;
            var mineInfo = state.GetUnit(mainMineNbr);
            if (!mineInfo.HasValue || mineInfo.Value.Health <= 0) return;
            foreach (int pawn in myPawns) { var info = state.GetUnit(pawn); if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE) actions.Gather(pawn, mainMineNbr, mainBaseNbr); }
        }

        private void BuildStructure(UnitType type, IGameState state, IAgentActions actions)
        {
            foreach (int pawn in myPawns)
            {
                var info = state.GetUnit(pawn);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[type])
                {
                    // Pick the closest buildable position to this pawn
                    Position pawnPos = info.Value.GridPosition;
                    Position? bestPos = null;
                    float bestDist = float.MaxValue;
                    foreach (Position pos in buildPositions)
                    {
                        if (state.IsBoundedAreaBuildable(type, pos))
                        {
                            float dist = Position.Distance(pos, pawnPos);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestPos = pos;
                            }
                        }
                    }
                    if (bestPos.HasValue)
                    {
                        actions.Build(pawn, bestPos.Value, type);
                        return;
                    }
                }
            }
        }

        private int FindClosestMine(IGameState state)
        {
            Position refPos = mainBaseNbr >= 0 && state.GetUnit(mainBaseNbr).HasValue
                ? state.GetUnit(mainBaseNbr).Value.CenterPosition
                : (myPawns.Count > 0 && state.GetUnit(myPawns[0]).HasValue
                    ? state.GetUnit(myPawns[0]).Value.GridPosition
                    : new Position(0, 0));
            int bestMine = -1;
            float bestDist = float.MaxValue;
            foreach (int mineNbr in mines)
            {
                var info = state.GetUnit(mineNbr);
                if (!info.HasValue || info.Value.Health <= 0) continue;
                float dist = Position.Distance(info.Value.CenterPosition, refPos);
                if (dist < bestDist) { bestDist = dist; bestMine = mineNbr; }
            }
            return bestMine;
        }

        private int? FindAnyEnemy(IGameState state)
        {
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER, UnitType.MONK, UnitType.PAWN, UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER, UnitType.MONASTERY })
            { var enemies = state.GetEnemyUnits(ut); if (enemies.Count > 0) return enemies[0]; }
            return null;
        }
    }
}
