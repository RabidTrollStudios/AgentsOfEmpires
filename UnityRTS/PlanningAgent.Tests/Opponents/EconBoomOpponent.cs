using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// [HARD] Economic powerhouse: races to 10 workers,
    /// then builds barracks and floods soldiers. Outproduces most opponents.
    /// Strategy to beat: early aggression before the boom kicks in, or match
    /// the economy and fight with better unit composition.
    /// </summary>
    public class EconBoomOpponent : PlanningAgentBase
    {
        private const int MAX_WORKERS = 10;
        private const int ATTACK_THRESHOLD = 6;

        public override void InitializeMatch() { }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            TrainWorkers(state, actions, MAX_WORKERS);
            GatherWithIdleWorkers(state, actions);

            // Build barracks
            if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);

            // Flood soldiers (cheap and effective)
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
