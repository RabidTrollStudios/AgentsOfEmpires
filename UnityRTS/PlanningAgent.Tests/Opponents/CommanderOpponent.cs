using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// [HARD] Smart targeting with strong macro. 6 pawns,
    /// trains mostly warriors + some archers. Prioritizes killing enemy
    /// pawns first (cripple economy), then bases, then military.
    /// Attacks with 4+ troops -- does not wait as long as the turtle.
    /// Strategy to beat: protect pawns, match economy, bring a bigger army.
    /// </summary>
    public class CommanderOpponent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 6;
        private const int ATTACK_THRESHOLD = 4;
        private int trainCount;

        public override void InitializeMatch() { trainCount = 0; }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            TrainPawns(state, actions, MAX_PAWNS);
            GatherWithIdlePawns(state, actions);

            // Build barracks
            if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);

            // Train: 2 warriors then 1 archer, repeat
            foreach (int barracksNbr in myBarracks)
            {
                var info = state.GetUnit(barracksNbr);
                if (!info.HasValue || !info.Value.IsBuilt || info.Value.CurrentAction != UnitAction.IDLE)
                    continue;

                UnitType toTrain = (trainCount % 3 < 2) ? UnitType.WARRIOR : UnitType.ARCHER;
                if (state.MyGold >= GameConstants.COST[toTrain])
                {
                    actions.Train(barracksNbr, toTrain);
                    trainCount++;
                }
            }

            // Attack with smart targeting
            int armySize = myWarriors.Count + myArchers.Count;
            if (armySize >= ATTACK_THRESHOLD)
            {
                SmartAttack(myWarriors, state, actions);
                SmartAttack(myArchers, state, actions);
            }
        }

        /// <summary>
        /// Priority targeting: pawns -> bases -> barracks -> archers -> warriors.
        /// Killing pawns cripples economy; killing bases stops pawn production.
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
            // Priority: pawns > bases > barracks > archers > warriors
            foreach (UnitType ut in new[] { UnitType.PAWN, UnitType.BASE, UnitType.BARRACKS,
                                            UnitType.ARCHER, UnitType.WARRIOR })
            {
                var enemies = state.GetEnemyUnits(ut);
                if (enemies.Count > 0) return enemies[0];
            }
            return null;
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
    }
}
