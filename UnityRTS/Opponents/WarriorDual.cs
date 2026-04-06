using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [HARD] Triple barracks warrior flood: 6 pawns, 3 Barracks.
    /// Pure warrior production from 3 buildings for maximum throughput.
    /// Attacks with warriors when army reaches 6+.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 6;
        private const int ATTACK_THRESHOLD = 6;

        public override void InitializeMatch() { }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
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

            // Build 3 barracks for triple warrior production
            if (myBarracks.Count < 3 && HasBuiltUnit(myBases, state) && !IsPawnBuilding(state))
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

            // Attack with warriors — squads of 3 on the same target, retarget on kill
            if (myWarriors.Count >= ATTACK_THRESHOLD)
                SquadAttack(myWarriors, 3, state, actions);
        }

        private void SquadAttack(List<int> units, int squadSize, IGameState state, IAgentActions actions)
        {
            // Find all living enemy units, prioritize mobile combat units
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

            // Assign warriors in squads of N to the same target
            int targetIdx = 0;
            int assigned = 0;

            foreach (int unitNbr in units)
            {
                var info = state.GetUnit(unitNbr);
                if (!info.HasValue) continue;

                // Only reassign idle warriors (let attacking warriors finish their target)
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
