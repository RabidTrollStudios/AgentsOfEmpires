using System.Collections.Generic;
using AgentSDK;

namespace Opponent.Tests
{
    /// <summary>
    /// [IMPOSSIBLE] Adaptive archer-focused agent with monk sustain.
    /// Makes decisions based on game state: scales economy when gold-starved,
    /// builds more production when gold-rich, adds monks when taking losses.
    /// Archers use volley+kite micro. Guaranteed at least 1 monastery.
    /// </summary>
    public class ArcherMonkSpendOpponent : PlanningAgentBase
    {
        private const int ATTACK_THRESHOLD = 4;
        private const float GOLD_STARVED = 100f;
        private const float GOLD_RICH = 400f;
        private const int ATTACK_TICKS = 2;
        private const int KITE_TICKS = 1;
        private const int CYCLE_LENGTH = ATTACK_TICKS + KITE_TICKS;

        private int _lastArmySize;
        private int _ticksSinceArmyShrunk;
        private Dictionary<int, int> _lastArcherTarget = new Dictionary<int, int>();
        private Dictionary<int, int> _archerCycleTick = new Dictionary<int, int>();

        public override void InitializeMatch()
        {
            _lastArmySize = 0;
            _ticksSinceArmyShrunk = 999;
            _lastArcherTarget = new Dictionary<int, int>();
            _archerCycleTick = new Dictionary<int, int>();
        }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
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

            GatherWithIdlePawns(state, actions);

            int enemyArmy = state.GetEnemyUnits(UnitType.WARRIOR).Count
                + state.GetEnemyUnits(UnitType.ARCHER).Count
                + state.GetEnemyUnits(UnitType.LANCER).Count;
            bool goldStarved = state.MyGold < GOLD_STARVED;
            bool goldRich = state.MyGold > GOLD_RICH;
            bool takingLosses = _ticksSinceArmyShrunk < 20;
            bool outnumbered = enemyArmy > armySize;
            bool needMorePawns = myPawns.Count < 3 || (goldStarved && myPawns.Count < 8);

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

            // Build: archery first, then monastery, then scale archeries
            if (myArchery.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.ARCHERY, state, actions);
            else if (myMonasteries.Count == 0 && HasBuiltUnit(myArchery, state))
                BuildStructure(UnitType.MONASTERY, state, actions);
            else if (goldRich && myArchery.Count < 3 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.ARCHERY, state, actions);

            // Train archers
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

            // Train monks when taking losses or have none
            if ((takingLosses && myMonks.Count < 3) || myMonks.Count < 1)
            {
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
            }

            HealWithMonks(state, actions);

            if (myArchers.Count >= ATTACK_THRESHOLD || (armySize > 0 && outnumbered))
                ArcherVolleyKite(state, actions);
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

        private void HealWithMonks(IGameState state, IAgentActions actions)
        {
            foreach (int monkNbr in myMonks)
            {
                var monkInfo = state.GetUnit(monkNbr);
                if (!monkInfo.HasValue || monkInfo.Value.CurrentAction != UnitAction.IDLE) continue;
                if (monkInfo.Value.Mana < GameConstants.MANA_COST) continue;

                int? bestTarget = null;
                float lowestHpRatio = 0.8f;
                foreach (int unitNbr in myArchers)
                {
                    var info = state.GetUnit(unitNbr);
                    if (!info.HasValue) continue;
                    float ratio = info.Value.Health / GameConstants.HEALTH[UnitType.ARCHER];
                    if (ratio < lowestHpRatio) { lowestHpRatio = ratio; bestTarget = unitNbr; }
                }
                foreach (int unitNbr in myWarriors)
                {
                    var info = state.GetUnit(unitNbr);
                    if (!info.HasValue) continue;
                    float ratio = info.Value.Health / GameConstants.HEALTH[UnitType.WARRIOR];
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
