using System.Collections.Generic;
using AgentSDK;

namespace Opponent.Tests
{
    /// <summary>
    /// [IMPOSSIBLE] Max-spend Archer + Monk: 6 pawns, Archery + Monastery.
    /// Trains Archers from Archery and Monks from Monastery every tick —
    /// spending every gold coin as fast as income allows.
    /// Monks heal the most-wounded friendly archer below 80% HP.
    /// Attacks with all archers when total combat army reaches 6+.
    /// Tests whether archer range (9.0) combined with monk sustain
    /// creates a durable ranged force under max-spend pressure.
    /// Strategy to beat: warriors counter archers (1.25x); rush before
    /// both buildings complete (350g + 350g = 700g investment).
    /// </summary>
    public class ArcherMonkSpendOpponent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 6;
        private const int ATTACK_THRESHOLD = 6;
        private const int ATTACK_TICKS = 2;
        private const int KITE_TICKS = 1;
        private const int CYCLE_LENGTH = ATTACK_TICKS + KITE_TICKS;

        private Dictionary<int, int> _lastArcherTarget = new Dictionary<int, int>();
        private Dictionary<int, int> _archerCycleTick = new Dictionary<int, int>();

        public override void InitializeMatch()
        {
            _lastArcherTarget = new Dictionary<int, int>();
            _archerCycleTick = new Dictionary<int, int>();
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

            // Build order: archery -> monastery
            if (myArchery.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.ARCHERY, state, actions);
            else if (myMonasteries.Count == 0 && HasBuiltUnit(myArchery, state))
                BuildStructure(UnitType.MONASTERY, state, actions);

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

            // Monks heal most-wounded friendly archer below 80% HP
            HealWithMonks(state, actions);

            // Archers use volley+kite micro
            if (myArchers.Count >= ATTACK_THRESHOLD)
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

        private void HealWithMonks(IGameState state, IAgentActions actions)
        {
            foreach (int monkNbr in myMonks)
            {
                var monkInfo = state.GetUnit(monkNbr);
                if (!monkInfo.HasValue || monkInfo.Value.CurrentAction != UnitAction.IDLE)
                    continue;
                if (monkInfo.Value.Mana < GameConstants.MANA_COST)
                    continue;

                // Find most-wounded archer below 80% HP
                int? bestTarget = null;
                float lowestHpRatio = 0.8f;

                foreach (int unitNbr in myArchers)
                {
                    var info = state.GetUnit(unitNbr);
                    if (!info.HasValue) continue;
                    float ratio = info.Value.Health / GameConstants.HEALTH[UnitType.ARCHER];
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
