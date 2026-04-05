using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [HARD] Max-spend Full Army: 6 pawns, Barracks + Archery + Tower + Monastery.
    /// Trains Warriors from Barracks, Archers from Archery, Lancers from Tower,
    /// and Monks from Monastery every tick — spending every gold coin as fast
    /// as income allows across all four production buildings simultaneously.
    /// Monks heal the most-wounded friendly combat unit below 80% HP
    /// (searches warriors, archers, and lancers).
    /// Attacks with all warriors, archers, and lancers when total combat army
    /// reaches 6+ combined.
    /// Tests whether four-building max-spend pressure with monk sustain is
    /// the strongest possible composition, or whether the 1500g building cost
    /// leaves it too vulnerable to early aggression.
    /// Strategy to beat: rush before all four buildings complete
    /// (400g + 350g + 400g + 350g = 1500g investment), or exploit the
    /// rock-paper-scissors triangle with a decisive unit-type advantage.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 6;
        private const int ATTACK_THRESHOLD = 6;

        // Archer micro
        private const int ATTACK_TICKS = 2;
        private const int KITE_TICKS = 1;
        private const int CYCLE_LENGTH = ATTACK_TICKS + KITE_TICKS;
        private Dictionary<int, int> _lastArcherTarget = new Dictionary<int, int>();
        private Dictionary<int, int> _archerCycleTick = new Dictionary<int, int>();

        // Lancer micro
        private const int DISENGAGE_TICKS = 3;
        private const float RALLY_DISTANCE = 5.0f;
        private Dictionary<int, int> _lancerCombatTicks = new Dictionary<int, int>();

        public override void InitializeMatch()
        {
            _lastArcherTarget = new Dictionary<int, int>();
            _archerCycleTick = new Dictionary<int, int>();
            _lancerCombatTicks = new Dictionary<int, int>();
        }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            // Build a base first if we dont have one
            if (myBases.Count == 0)
            {
                BuildStructure(UnitType.BASE, state, actions);
                return;
            }

            TrainPawns(state, actions, MAX_PAWNS);
            GatherWithIdlePawns(state, actions);

            // Build order: barracks -> archery -> tower -> monastery
            if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);
            else if (myArchery.Count == 0 && HasBuiltUnit(myBarracks, state))
                BuildStructure(UnitType.ARCHERY, state, actions);
            else if (myTowers.Count == 0 && HasBuiltUnit(myArchery, state))
                BuildStructure(UnitType.TOWER, state, actions);
            else if (myMonasteries.Count == 0 && HasBuiltUnit(myTowers, state))
                BuildStructure(UnitType.MONASTERY, state, actions);

            // Train warriors from all barracks every tick
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

            // Train archers from all archeries every tick
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

            // Train lancers from all towers every tick
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

            // Train monks from all monasteries every tick
            foreach (int monasteryNbr in myMonasteries)
            {
                var info = state.GetUnit(monasteryNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.MONK])
                {
                    actions.Train(monasteryNbr, UnitType.MONK);
                }
            }

            // Monks heal most-wounded friendly combat unit below 80% HP
            HealWithMonks(state, actions);

            // Attack with unit-specific micro
            int armySize = myWarriors.Count + myArchers.Count + myLancers.Count;
            if (armySize < ATTACK_THRESHOLD) return;

            // Warriors in squads of 3
            SquadAttack(myWarriors, 3, state, actions);
            // Archers volley+kite
            ArcherVolleyKite(state, actions);
            // Lancers hit-and-run joust
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

                if (!_archerCycleTick.ContainsKey(archerNbr))
                    _archerCycleTick[archerNbr] = 0;

                int cycleTick = _archerCycleTick[archerNbr] % CYCLE_LENGTH;
                _archerCycleTick[archerNbr]++;

                if (cycleTick < ATTACK_TICKS)
                {
                    int lastTarget = _lastArcherTarget.ContainsKey(archerNbr)
                        ? _lastArcherTarget[archerNbr] : -1;
                    int chosenTarget = -1;
                    foreach (int enemyNbr in enemies)
                    {
                        if (enemyNbr != lastTarget) { chosenTarget = enemyNbr; break; }
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

                if (!_lancerCombatTicks.ContainsKey(lancerNbr))
                    _lancerCombatTicks[lancerNbr] = 0;

                if (info.Value.CurrentAction == UnitAction.ATTACK)
                {
                    float dist = Position.Distance(info.Value.CenterPosition, targetInfo.Value.CenterPosition);
                    if (dist < GameConstants.ATTACK_RANGE[UnitType.LANCER] + 1.0f)
                    {
                        _lancerCombatTicks[lancerNbr]++;
                        if (_lancerCombatTicks[lancerNbr] >= DISENGAGE_TICKS && mainBaseNbr >= 0)
                        {
                            var baseInfo = state.GetUnit(mainBaseNbr);
                            if (baseInfo.HasValue)
                            {
                                actions.Move(lancerNbr, baseInfo.Value.CenterPosition);
                                _lancerCombatTicks[lancerNbr] = 0;
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
                        _lancerCombatTicks[lancerNbr] = 0;
                    }
                }
                else if (info.Value.CurrentAction == UnitAction.IDLE)
                {
                    actions.Attack(lancerNbr, enemyTarget.Value);
                    _lancerCombatTicks[lancerNbr] = 0;
                }
            }
        }

        private void HealWithMonks(IGameState state, IAgentActions actions)
        {
            foreach (int monkNbr in myMonks)
            {
                var monkInfo = state.GetUnit(monkNbr);
                if (!monkInfo.HasValue || monkInfo.Value.CurrentAction != UnitAction.IDLE)
                    continue;
                if (monkInfo.Value.Mana < GameConstants.MANA_COST)
                    continue;

                // Find most-wounded combat unit below 80% HP across all unit types
                int? bestTarget = null;
                float lowestHpRatio = 0.8f;

                foreach (int unitNbr in myWarriors)
                {
                    var info = state.GetUnit(unitNbr);
                    if (!info.HasValue) continue;
                    float ratio = info.Value.Health / GameConstants.HEALTH[UnitType.WARRIOR];
                    if (ratio < lowestHpRatio) { lowestHpRatio = ratio; bestTarget = unitNbr; }
                }
                foreach (int unitNbr in myArchers)
                {
                    var info = state.GetUnit(unitNbr);
                    if (!info.HasValue) continue;
                    float ratio = info.Value.Health / GameConstants.HEALTH[UnitType.ARCHER];
                    if (ratio < lowestHpRatio) { lowestHpRatio = ratio; bestTarget = unitNbr; }
                }
                foreach (int unitNbr in myLancers)
                {
                    var info = state.GetUnit(unitNbr);
                    if (!info.HasValue) continue;
                    float ratio = info.Value.Health / GameConstants.HEALTH[UnitType.LANCER];
                    if (ratio < lowestHpRatio) { lowestHpRatio = ratio; bestTarget = unitNbr; }
                }

                if (bestTarget.HasValue)
                    actions.Heal(monkNbr, bestTarget.Value);
            }
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
                    foreach (Position pos in buildPositions)
                    {
                        if (state.IsBoundedAreaBuildable(type, pos))
                        {
                            actions.Build(pawn, pos, type);
                            return;
                        }
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
