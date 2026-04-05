using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [IMPOSSIBLE] Adaptive warrior-focused agent with monk sustain.
    /// Makes decisions based on game state: scales economy when gold-starved,
    /// builds more production when gold-rich, adds monks when taking losses.
    /// Starts with warrior focus but adapts building count to match income.
    /// Guaranteed at least 1 monastery for healing.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int ATTACK_THRESHOLD = 4;
        private const float GOLD_STARVED = 100f;
        private const float GOLD_RICH = 400f;

        private int _lastArmySize;
        private int _ticksSinceArmyShrunk;

        public override void InitializeMatch()
        {
            _lastArmySize = 0;
            _ticksSinceArmyShrunk = 999;
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

            // Track army losses
            int armySize = myWarriors.Count + myArchers.Count + myLancers.Count;
            if (armySize < _lastArmySize)
                _ticksSinceArmyShrunk = 0;
            else
                _ticksSinceArmyShrunk++;
            _lastArmySize = armySize;

            GatherWithIdlePawns(state, actions);

            // --- Adaptive decisions ---
            int enemyArmy = state.GetEnemyUnits(UnitType.WARRIOR).Count
                + state.GetEnemyUnits(UnitType.ARCHER).Count
                + state.GetEnemyUnits(UnitType.LANCER).Count;
            bool goldStarved = state.MyGold < GOLD_STARVED;
            bool goldRich = state.MyGold > GOLD_RICH;
            bool takingLosses = _ticksSinceArmyShrunk < 20;
            bool outnumbered = enemyArmy > armySize;
            bool needMorePawns = myPawns.Count < 3 || (goldStarved && myPawns.Count < 8);
            bool needMonastery = myMonasteries.Count == 0;
            bool needMoreMonks = takingLosses && myMonks.Count < 3;
            bool needMoreBarracks = goldRich && myBarracks.Count < 3;

            // Economy: train pawns if starved or few gatherers
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

            // Build priority: first barracks, then monastery, then scale up barracks
            if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);
            else if (needMonastery && HasBuiltUnit(myBarracks, state))
                BuildStructure(UnitType.MONASTERY, state, actions);
            else if (needMoreBarracks && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);

            // Train warriors from all barracks
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

            // Train monks — more aggressively when taking losses
            if (needMoreMonks || myMonks.Count < 1)
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

            // Attack: go when we have enough, or when outnumbered (desperation)
            if (myWarriors.Count >= ATTACK_THRESHOLD || (armySize > 0 && outnumbered))
                SquadAttack(myWarriors, 3, state, actions);
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

        private void HealWithMonks(IGameState state, IAgentActions actions)
        {
            foreach (int monkNbr in myMonks)
            {
                var monkInfo = state.GetUnit(monkNbr);
                if (!monkInfo.HasValue || monkInfo.Value.CurrentAction != UnitAction.IDLE) continue;
                if (monkInfo.Value.Mana < GameConstants.MANA_COST) continue;

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
