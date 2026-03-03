using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// [MEDIUM] Minimal economy, fast barracks, soldier rush with 3+.
    /// Punishes slow builders — if you don't have defenders by tick ~100,
    /// you're in trouble. Runs out of gold quickly if the rush fails.
    /// Strategy to beat: fast barracks + a few defenders, then counter-attack
    /// once the rusher's economy collapses.
    /// </summary>
    public class SoldierRushOpponent : PlanningAgentBase
    {
        private const int MAX_WORKERS = 2;
        private const int ATTACK_THRESHOLD = 3;

        public override void InitializeMatch() { }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            TrainWorkers(state, actions, MAX_WORKERS);
            GatherWithIdleWorkers(state, actions);

            // Rush to barracks
            if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);

            // Train soldiers only — spend everything on military
            foreach (int barracksNbr in myBarracks)
            {
                var info = state.GetUnit(barracksNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.SOLDIER])
                {
                    actions.Train(barracksNbr, UnitType.SOLDIER);
                }
            }

            // Attack early with 3+ soldiers
            if (mySoldiers.Count >= ATTACK_THRESHOLD)
                AttackWithUnits(mySoldiers, state, actions);
        }

        private void TrainWorkers(IGameState state, IAgentActions actions, int max)
        {
            foreach (int baseNbr in myBases)
            {
                var info = state.GetUnit(baseNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.WORKER]
                    && myWorkers.Count < max)
                {
                    actions.Train(baseNbr, UnitType.WORKER);
                }
            }
        }

        private void GatherWithIdleWorkers(IGameState state, IAgentActions actions)
        {
            if (mainBaseNbr < 0 || mainMineNbr < 0) return;
            var mineInfo = state.GetUnit(mainMineNbr);
            if (!mineInfo.HasValue || mineInfo.Value.Health <= 0) return;

            foreach (int worker in myWorkers)
            {
                var info = state.GetUnit(worker);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                    actions.Gather(worker, mainMineNbr, mainBaseNbr);
            }
        }

        private void BuildStructure(UnitType type, IGameState state, IAgentActions actions)
        {
            foreach (int worker in myWorkers)
            {
                var info = state.GetUnit(worker);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[type])
                {
                    foreach (Position pos in buildPositions)
                    {
                        if (state.IsBoundedAreaBuildable(type, pos))
                        {
                            actions.Build(worker, pos, type);
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
            foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                            UnitType.BASE, UnitType.BARRACKS })
            {
                var enemies = state.GetEnemyUnits(ut);
                if (enemies.Count > 0) return enemies[0];
            }
            return null;
        }
    }
}
