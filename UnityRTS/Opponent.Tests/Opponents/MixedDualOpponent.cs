using System.Collections.Generic;
using AgentSDK;

namespace Opponent.Tests
{
    /// <summary>
    /// [HARD] Reactive counter-picker with all 3 buildings: 6 pawns,
    /// 1 Barracks + 1 Archery + 1 Tower. Scouts enemy buildings and picks
    /// the R-P-S counter unit as primary, trains all 3 types from all buildings.
    /// Archers use volley+kite micro, lancers use hit-and-run joust,
    /// warriors attack normally.
    /// Attacks with all combat units when total army reaches 6+.
    /// </summary>
    public class MixedDualOpponent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 6;
        private const int ATTACK_THRESHOLD = 6;

        // Archer micro
        private const int ATTACK_FRAMES = 2;
        private const int KITE_FRAMES = 1;
        private const int CYCLE_LENGTH = ATTACK_FRAMES + KITE_FRAMES;
        private Dictionary<int, int> _lastArcherTarget = new Dictionary<int, int>();
        private Dictionary<int, int> _archerCycleFrame = new Dictionary<int, int>();

        // Lancer micro
        private const int DISENGAGE_FRAMES = 3;
        private const float RALLY_DISTANCE = 5.0f;
        private Dictionary<int, int> _lancerCombatFrames = new Dictionary<int, int>();

        // Reactive scouting
        private UnitType _priorityUnit = UnitType.MINE;

        private bool _buildQueued;

        public override void InitializeMatch()
        {
            _priorityUnit = UnitType.MINE;
            _lastArcherTarget = new Dictionary<int, int>();
            _archerCycleFrame = new Dictionary<int, int>();
            _lancerCombatFrames = new Dictionary<int, int>();
        }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            _buildQueued = false;
            mainMineNbr = FindClosestMine(state);
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            // Build a base first if we dont have one
            if (myBases.Count == 0)
            {
                BuildStructure(UnitType.BASE, state, actions);
                return;
            }

            TrainPawns(state, actions, MAX_PAWNS);
            GatherWithIdlePawns(state, actions);

            // Scout enemy buildings and pick counter as priority
            if (_priorityUnit == UnitType.MINE)
            {
                if (state.GetEnemyUnits(UnitType.BARRACKS).Count > 0)
                    _priorityUnit = UnitType.LANCER;
                else if (state.GetEnemyUnits(UnitType.ARCHERY).Count > 0)
                    _priorityUnit = UnitType.WARRIOR;
                else if (state.GetEnemyUnits(UnitType.TOWER).Count > 0)
                    _priorityUnit = UnitType.ARCHER;
            }

            // Build all 3 buildings — priority building first
            UnitType firstBuilding = _priorityUnit == UnitType.LANCER ? UnitType.TOWER
                : _priorityUnit == UnitType.WARRIOR ? UnitType.BARRACKS
                : _priorityUnit == UnitType.ARCHER ? UnitType.ARCHERY
                : UnitType.BARRACKS;

            if (GetBuildingCount(firstBuilding) < 1 && HasBuiltUnit(myBases, state) && !IsPawnBuilding(state) && !_buildQueued)
                BuildStructure(firstBuilding, state, actions);
            else if (myBarracks.Count < 1 && HasBuiltUnit(myBases, state) && !IsPawnBuilding(state) && !_buildQueued)
                BuildStructure(UnitType.BARRACKS, state, actions);
            else if (myArchery.Count < 1 && HasBuiltUnit(myBases, state) && !IsPawnBuilding(state) && !_buildQueued)
                BuildStructure(UnitType.ARCHERY, state, actions);
            else if (myTowers.Count < 1 && HasBuiltUnit(myBases, state) && !IsPawnBuilding(state) && !_buildQueued)
                BuildStructure(UnitType.TOWER, state, actions);

            // Train from all buildings
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

            foreach (int archeryNbr in myArchery)
            {
                var info = state.GetUnit(archeryNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.ARCHER])
                {
                    actions.Train(archeryNbr, UnitType.ARCHER);
                }
            }

            foreach (int towerNbr in myTowers)
            {
                var info = state.GetUnit(towerNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.LANCER])
                {
                    actions.Train(towerNbr, UnitType.LANCER);
                }
            }

            // Attack with full army using unit-specific micro
            int armySize = myWarriors.Count + myArchers.Count + myLancers.Count;
            if (armySize < ATTACK_THRESHOLD) return;

            // Warriors in squads of 3, retarget on kill
            SquadAttack(myWarriors, 3, state, actions);

            // Archers use volley+kite micro
            ArcherVolleyKite(state, actions);

            // Lancers use hit-and-run joust
            LancerJoust(state, actions);
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
                    {
                        targetIdx++;
                        assigned = 0;
                    }
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

                if (!_archerCycleFrame.ContainsKey(archerNbr))
                    _archerCycleFrame[archerNbr] = 0;

                int cycleFrame = _archerCycleFrame[archerNbr] % CYCLE_LENGTH;
                _archerCycleFrame[archerNbr]++;

                if (cycleFrame < ATTACK_FRAMES)
                {
                    int lastTarget = _lastArcherTarget.ContainsKey(archerNbr)
                        ? _lastArcherTarget[archerNbr] : -1;

                    int chosenTarget = -1;
                    foreach (int enemyNbr in enemies)
                    {
                        if (enemyNbr != lastTarget)
                        {
                            chosenTarget = enemyNbr;
                            break;
                        }
                    }
                    if (chosenTarget < 0) chosenTarget = enemies[0];

                    actions.Attack(archerNbr, chosenTarget);
                    _lastArcherTarget[archerNbr] = chosenTarget;
                }
                else
                {
                    if (mainBaseNbr >= 0)
                    {
                        var baseInfo = state.GetUnit(mainBaseNbr);
                        if (baseInfo.HasValue)
                            actions.Move(archerNbr, baseInfo.Value.CenterPosition);
                    }
                }
            }
        }

        private void LancerJoust(IGameState state, IAgentActions actions)
        {
            int? enemyTarget = FindAnyEnemy(state);
            if (!enemyTarget.HasValue) return;
            var targetInfo = state.GetUnit(enemyTarget.Value);
            if (!targetInfo.HasValue) return;

            foreach (int lancerNbr in myLancers)
            {
                var info = state.GetUnit(lancerNbr);
                if (!info.HasValue) continue;

                if (!_lancerCombatFrames.ContainsKey(lancerNbr))
                    _lancerCombatFrames[lancerNbr] = 0;

                if (info.Value.CurrentAction == UnitAction.ATTACK)
                {
                    float dist = Position.Distance(info.Value.CenterPosition, targetInfo.Value.CenterPosition);
                    if (dist < GameConstants.ATTACK_RANGE[UnitType.LANCER] + 1.0f)
                    {
                        _lancerCombatFrames[lancerNbr]++;
                        if (_lancerCombatFrames[lancerNbr] >= DISENGAGE_FRAMES && mainBaseNbr >= 0)
                        {
                            var baseInfo = state.GetUnit(mainBaseNbr);
                            if (baseInfo.HasValue)
                            {
                                actions.Move(lancerNbr, baseInfo.Value.CenterPosition);
                                _lancerCombatFrames[lancerNbr] = 0;
                            }
                        }
                    }
                }
                else if (info.Value.CurrentAction == UnitAction.MOVE)
                {
                    float dist = Position.Distance(info.Value.CenterPosition, targetInfo.Value.CenterPosition);
                    if (dist >= RALLY_DISTANCE)
                    {
                        actions.Attack(lancerNbr, enemyTarget.Value);
                        _lancerCombatFrames[lancerNbr] = 0;
                    }
                }
                else if (info.Value.CurrentAction == UnitAction.IDLE)
                {
                    actions.Attack(lancerNbr, enemyTarget.Value);
                    _lancerCombatFrames[lancerNbr] = 0;
                }
            }
        }

        private int GetBuildingCount(UnitType type)
        {
            if (type == UnitType.BARRACKS) return myBarracks.Count;
            if (type == UnitType.ARCHERY) return myArchery.Count;
            if (type == UnitType.TOWER) return myTowers.Count;
            return 0;
        }

        private void TrainPawns(IGameState state, IAgentActions actions, int max)
        {
            foreach (int baseNbr in myBases)
            {
                var info = state.GetUnit(baseNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.PAWN]
                    && myPawns.Count < max)
                {
                    actions.Train(baseNbr, UnitType.PAWN);
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
                        _buildQueued = true;
                        return;
                    }
                }
            }
        }

        private void AttackWithUnits(List<int> units, IGameState state, IAgentActions actions)
        {
            int? target = FindAnyEnemy(state);
            if (!target.HasValue) return;

            foreach (int unitNbr in units)
            {
                var info = state.GetUnit(unitNbr);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                    actions.Attack(unitNbr, target.Value);
            }
        }

        private bool IsPawnBuilding(IGameState state)
        {
            foreach (int pawn in myPawns)
            {
                var info = state.GetUnit(pawn);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.BUILD)
                    return true;
            }
            return false;
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
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER,
                                            UnitType.MONK, UnitType.PAWN, UnitType.BASE,
                                            UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER,
                                            UnitType.MONASTERY })
            {
                var enemies = state.GetEnemyUnits(ut);
                if (enemies.Count > 0) return enemies[0];
            }
            return null;
        }
    }
}
