using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// [HARD] Smart targeting with strong macro. 6 workers, builds refinery,
    /// trains mostly soldiers + some archers. Prioritizes killing enemy
    /// workers first (cripple economy), then bases, then military.
    /// Attacks with 4+ troops — doesn't wait as long as the turtle.
    /// Strategy to beat: protect workers, match economy, bring a bigger army.
    /// </summary>
    public class CommanderOpponent : PlanningAgentBase
    {
        private const int MAX_WORKERS = 6;
        private const int ATTACK_THRESHOLD = 4;
        private int trainCount;

        public override void InitializeMatch() { trainCount = 0; }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            TrainWorkers(state, actions, MAX_WORKERS);
            GatherWithIdleWorkers(state, actions);

            // Build order: barracks -> refinery
            if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);
            else if (myRefineries.Count == 0 && HasBuiltUnit(myBases, state) && HasBuiltUnit(myBarracks, state))
                BuildStructure(UnitType.REFINERY, state, actions);

            // Train: 2 soldiers then 1 archer, repeat
            foreach (int barracksNbr in myBarracks)
            {
                var info = state.GetUnit(barracksNbr);
                if (!info.HasValue || !info.Value.IsBuilt || info.Value.CurrentAction != UnitAction.IDLE)
                    continue;

                UnitType toTrain = (trainCount % 3 < 2) ? UnitType.SOLDIER : UnitType.ARCHER;
                if (state.MyGold >= GameConstants.COST[toTrain])
                {
                    actions.Train(barracksNbr, toTrain);
                    trainCount++;
                }
            }

            // Attack with smart targeting
            int armySize = mySoldiers.Count + myArchers.Count;
            if (armySize >= ATTACK_THRESHOLD)
            {
                SmartAttack(mySoldiers, state, actions);
                SmartAttack(myArchers, state, actions);
            }
        }

        /// <summary>
        /// Priority targeting: workers -> bases -> barracks -> archers -> soldiers.
        /// Killing workers cripples economy; killing bases stops worker production.
        /// </summary>
        private void SmartAttack(List<int> units, IGameState state, IAgentActions actions)
        {
            int? target = FindPriorityTarget(state);
            if (!target.HasValue) return;

            foreach (int unitNbr in units)
            {
                var info = state.GetUnit(unitNbr);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                    actions.Attack(unitNbr, target.Value);
            }
        }

        private int? FindPriorityTarget(IGameState state)
        {
            // Priority: workers > bases > barracks > archers > soldiers > refineries
            foreach (UnitType ut in new[] { UnitType.WORKER, UnitType.BASE, UnitType.BARRACKS,
                                            UnitType.ARCHER, UnitType.SOLDIER, UnitType.REFINERY })
            {
                var enemies = state.GetEnemyUnits(ut);
                if (enemies.Count > 0) return enemies[0];
            }
            return null;
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
    }
}
