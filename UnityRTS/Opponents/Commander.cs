using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [HARD] Smart targeting with strong macro. 6 pawns,
    /// trains mostly warriors + some archers + monks for healing.
    /// Prioritizes killing enemy pawns first (cripple economy),
    /// then bases, then military. Monks keep the army alive.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 6;
        private const int MAX_MONKS = 2;
        private int trainCount;

        public override void InitializeMatch() { trainCount = 0; }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;
            if (mainMineNbr < 0 || !state.GetUnit(mainMineNbr).HasValue || state.GetUnit(mainMineNbr).Value.Health <= 0)
                mainMineNbr = FindClosestMine(state);

            // Build a base first — game starts with only a pawn and a mine
            if (myBases.Count == 0)
            {
                BuildStructure(UnitType.BASE, state, actions);
                return;
            }

            TrainPawns(state, actions);

            if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);

            if (myArchery.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.ARCHERY, state, actions);

            // Build monastery once military infrastructure is up
            if (myMonasteries.Count == 0 && HasBuiltUnit(myBarracks, state) && HasBuiltUnit(myArchery, state))
                BuildStructure(UnitType.MONASTERY, state, actions);

            GatherWithIdlePawns(state, actions);

            // Train rotation: warrior, warrior, archer, monk (repeat)
            // Monks cap at MAX_MONKS; if capped, train a warrior instead
            // Reserve gold for monastery if not yet built, but still train if surplus allows
            float monasteryReserve = (myMonasteries.Count == 0
                && HasBuiltUnit(myBarracks, state) && HasBuiltUnit(myArchery, state))
                ? GameConstants.COST[UnitType.MONASTERY] : 0f;

            int slot = trainCount % 4;
            UnitType trainType;
            if (slot < 2)
                trainType = UnitType.WARRIOR;
            else if (slot == 2)
                trainType = UnitType.ARCHER;
            else
                trainType = (myMonks.Count < MAX_MONKS) ? UnitType.MONK : UnitType.WARRIOR;

            // Only train if we can afford the unit AND still have enough reserved for monastery
            if (state.MyGold >= GameConstants.COST[trainType] + monasteryReserve)
            {
                bool trained = false;
                if (trainType == UnitType.WARRIOR)
                    trained = TrainFromBarracks(UnitType.WARRIOR, state, actions);
                else if (trainType == UnitType.ARCHER)
                    trained = TrainFromArchery(UnitType.ARCHER, state, actions);
                else
                    trained = TrainMonks(state, actions);

                // Advance rotation even if building was busy, so we don't get stuck
                // retrying the same type every tick while other buildings sit idle
                if (!trained)
                    trainCount++;
            }

            // Monks heal wounded allies
            ExecuteMonkActions(state, actions);

            DefendTroops(myWarriors, state, actions);
            DefendTroops(myArchers, state, actions);
            DefendTroops(myLancers, state, actions);
            int armySize = myWarriors.Count + myArchers.Count + myLancers.Count;
            if (armySize >= 4)
            {
                AttackWithUnits(myWarriors, state, actions);
                AttackWithUnits(myArchers, state, actions);
                AttackWithUnits(myLancers, state, actions);
            }
        }

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

        private bool TrainFromBarracks(UnitType unitType, IGameState state, IAgentActions actions)
        {
            foreach (int barracksNbr in myBarracks)
            {
                var info = state.GetUnit(barracksNbr);
                if (!info.HasValue || !info.Value.IsBuilt || info.Value.CurrentAction != UnitAction.IDLE)
                    continue;
                if (state.MyGold >= GameConstants.COST[unitType])
                {
                    actions.Train(barracksNbr, unitType);
                    trainCount++;
                    return true;
                }
            }
            return false;
        }

        private bool TrainFromArchery(UnitType unitType, IGameState state, IAgentActions actions)
        {
            foreach (int archeryNbr in myArchery)
            {
                var info = state.GetUnit(archeryNbr);
                if (!info.HasValue || !info.Value.IsBuilt || info.Value.CurrentAction != UnitAction.IDLE)
                    continue;
                if (state.MyGold >= GameConstants.COST[unitType])
                {
                    actions.Train(archeryNbr, unitType);
                    trainCount++;
                    return true;
                }
            }
            return false;
        }

        private bool TrainMonks(IGameState state, IAgentActions actions)
        {
            foreach (int monasteryNbr in myMonasteries)
            {
                var info = state.GetUnit(monasteryNbr);
                if (!info.HasValue || !info.Value.IsBuilt || info.Value.CurrentAction != UnitAction.IDLE)
                    continue;
                if (state.MyGold >= GameConstants.COST[UnitType.MONK])
                {
                    actions.Train(monasteryNbr, UnitType.MONK);
                    trainCount++;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Monks heal the most-wounded friendly mobile unit, prioritising
        /// warriors (melee front line) over archers/lancers. Idle monks
        /// move toward the army's center of mass to stay in support range.
        /// </summary>
        private void ExecuteMonkActions(IGameState state, IAgentActions actions)
        {
            foreach (int monkNbr in myMonks)
            {
                var monkInfo = state.GetUnit(monkNbr);
                if (!monkInfo.HasValue) continue;
                if (monkInfo.Value.CurrentAction == UnitAction.HEAL) continue;

                // Try to heal the most-wounded ally
                if (monkInfo.Value.Mana >= GameConstants.MANA_COST)
                {
                    int bestTarget = -1;
                    float lowestHealth = float.MaxValue;

                    // Prioritise warriors (front line, high HP pool)
                    foreach (var unitList in new[] { myWarriors, myArchers, myLancers })
                    {
                        foreach (int unitNbr in unitList)
                        {
                            var info = state.GetUnit(unitNbr);
                            if (!info.HasValue) continue;
                            float maxHp = GameConstants.HEALTH[info.Value.UnitType];
                            if (info.Value.Health > maxHp - GameConstants.HEAL_AMOUNT) continue;
                            if (info.Value.Health < lowestHealth)
                            {
                                lowestHealth = info.Value.Health;
                                bestTarget = unitNbr;
                            }
                        }
                    }

                    if (bestTarget >= 0)
                    {
                        actions.Heal(monkNbr, bestTarget);
                        continue;
                    }
                }

                // No heal target — move toward army center
                if (monkInfo.Value.CurrentAction == UnitAction.IDLE)
                {
                    Position center = ArmyCenter(state);
                    if (center.X >= 0)
                    {
                        float dist = Position.Distance(monkInfo.Value.CenterPosition, center);
                        if (dist > 3.0f)
                            actions.Move(monkNbr, center);
                    }
                }
            }
        }

        private Position ArmyCenter(IGameState state)
        {
            int sumX = 0, sumY = 0, count = 0;
            foreach (var unitList in new[] { myWarriors, myArchers, myLancers })
            {
                foreach (int unitNbr in unitList)
                {
                    var info = state.GetUnit(unitNbr);
                    if (!info.HasValue) continue;
                    sumX += info.Value.CenterPosition.X;
                    sumY += info.Value.CenterPosition.Y;
                    count++;
                }
            }
            return count > 0 ? new Position(sumX / count, sumY / count) : new Position(-1, -1);
        }

        private void BuildStructure(UnitType type, IGameState state, IAgentActions actions)
        {
            if (state.MyGold < GameConstants.COST[type]) return;

            // Prefer idle pawns, fall back to gathering pawns
            int chosenPawn = -1;
            foreach (int pawn in myPawns)
            {
                var info = state.GetUnit(pawn);
                if (!info.HasValue) continue;
                if (info.Value.CurrentAction == UnitAction.IDLE)
                {
                    chosenPawn = pawn;
                    break;
                }
                if (chosenPawn < 0 && info.Value.CurrentAction == UnitAction.GATHER)
                    chosenPawn = pawn;
            }
            if (chosenPawn < 0) return;

            // Try multiple positions — the best one may be blocked by a mobile unit
            var positions = state.FindProspectiveBuildPositions(type);
            if (positions.Count == 0) return;

            var baseInfo = mainBaseNbr >= 0 ? state.GetUnit(mainBaseNbr) : null;
            Position baseCenter = baseInfo.HasValue ? baseInfo.Value.CenterPosition : new Position(-1, -1);

            var sorted = new List<Position>(positions);
            if (type == UnitType.BASE && mainMineNbr >= 0)
            {
                // Place base near the mine
                var mineInfo = state.GetUnit(mainMineNbr);
                if (mineInfo.HasValue)
                {
                    Position mineCenter = mineInfo.Value.CenterPosition;
                    sorted.Sort((a, b) => Position.Distance(a, mineCenter).CompareTo(Position.Distance(b, mineCenter)));
                }
            }
            else if (baseCenter.X >= 0)
            {
                // Place other buildings near the base
                sorted.Sort((a, b) => Position.Distance(a, baseCenter).CompareTo(Position.Distance(b, baseCenter)));
            }

            foreach (Position pos in sorted)
            {
                // Skip positions too close to the base
                if (type != UnitType.BASE && baseCenter.X >= 0
                    && Position.Distance(pos, baseCenter) < 2f)
                    continue;

                // Pre-check buildability to avoid triggering cooldown on a blocked cell
                if (!state.IsAreaBuildable(type, pos))
                    continue;

                actions.Build(chosenPawn, pos, type);
                return;
            }
        }

        private void AttackWithUnits(List<int> units, IGameState state, IAgentActions actions)
        {
            foreach (int unitNbr in units)
            {
                var info = state.GetUnit(unitNbr);
                if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) continue;
                int? target = FindClosestEnemy(unitNbr, state);
                if (target.HasValue)
                    actions.Attack(unitNbr, target.Value);
            }
        }

        private void DefendTroops(List<int> units, IGameState state, IAgentActions actions)
        {
            foreach (int unitNbr in units)
            {
                var info = state.GetUnit(unitNbr);
                if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) continue;
                Position myPos = info.Value.GridPosition;
                float myRange = GameConstants.ATTACK_RANGE[info.Value.UnitType];

                int? bestTarget = null;
                float bestDist = float.MaxValue;
                foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER, UnitType.MONK, UnitType.PAWN,
                                                UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER, UnitType.MONASTERY })
                {
                    var enemies = state.GetEnemyUnits(ut);
                    foreach (int enemyNbr in enemies)
                    {
                        var enemyInfo = state.GetUnit(enemyNbr);
                        if (!enemyInfo.HasValue) continue;
                        float dist = Position.Distance(myPos, enemyInfo.Value.CenterPosition);
                        if (dist <= myRange && dist < bestDist)
                        {
                            bestDist = dist;
                            bestTarget = enemyNbr;
                        }
                    }
                }
                if (bestTarget.HasValue)
                    actions.Attack(unitNbr, bestTarget.Value);
            }
        }

        private int? FindClosestEnemy(int attackerNbr, IGameState state)
        {
            var attackerInfo = state.GetUnit(attackerNbr);
            if (!attackerInfo.HasValue) return null;
            Position attackerPos = attackerInfo.Value.GridPosition;

            int? bestTarget = null;
            float bestDist = float.MaxValue;
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER, UnitType.PAWN,
                                            UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER })
            {
                var enemies = state.GetEnemyUnits(ut);
                foreach (int enemyNbr in enemies)
                {
                    var enemyInfo = state.GetUnit(enemyNbr);
                    if (!enemyInfo.HasValue) continue;
                    float dist = Position.Distance(attackerPos, enemyInfo.Value.CenterPosition);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestTarget = enemyNbr;
                    }
                }
            }
            return bestTarget;
        }
    }
}
