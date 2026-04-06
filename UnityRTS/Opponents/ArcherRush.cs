using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [MEDIUM] Minimal economy, fast archery, archer rush with 3+.
    /// Mirrors WarriorRush but with Archery (350g) → Archers (80g).
    /// Cheaper units and building than WarriorRush, but archers are fragile.
    /// Tests whether archer range (9.0) compensates for low HP (600).
    /// Strategy to beat: warriors counter archers (1.25x), rush before archery is up.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 6;
        private const int ATTACK_THRESHOLD = 3;

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

            // Rush to 2 archeries
            if (myArchery.Count < 2 && HasBuiltUnit(myBases, state) && !IsPawnBuilding(state))
                BuildStructure(UnitType.ARCHERY, state, actions);

            // Train archers only
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

            // Attack early with 3+ archers
            if (myArchers.Count >= ATTACK_THRESHOLD)
                AttackWithUnits(myArchers, state, actions);
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
