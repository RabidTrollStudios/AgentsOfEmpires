using System.Collections.Generic;
using AgentSDK;

namespace Opponent.Tests
{
    /// <summary>
    /// [MEDIUM] Monk support: 4 pawns, Barracks + Monastery.
    /// Trains Warriors (2/3) and Monks (1/3). Monks heal most-wounded
    /// warrior below 80% HP. Attacks with 4+ combat troops.
    /// Tests whether Monk healing justifies the 90g unit + 350g monastery investment.
    /// Strategy to beat: burst down warriors faster than monks can heal,
    /// or snipe the monks directly.
    /// </summary>
    public class HealerSupportOpponent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 4;
        private const int ATTACK_THRESHOLD = 4;
        private int trainCycle = 0;

        public override void InitializeMatch() { trainCycle = 0; }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            TrainPawns(state, actions, MAX_PAWNS);
            GatherWithIdlePawns(state, actions);

            // Build barracks first, then monastery
            if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);
            else if (myMonasteries.Count == 0 && HasBuiltUnit(myBarracks, state))
                BuildStructure(UnitType.MONASTERY, state, actions);

            // Train warriors from barracks
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

            // Train monks from monastery (every 3rd cycle, prefer warriors)
            foreach (int monasteryNbr in myMonasteries)
            {
                var info = state.GetUnit(monasteryNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.MONK]
                    && trainCycle % 3 == 2)
                {
                    actions.Train(monasteryNbr, UnitType.MONK);
                }
            }
            trainCycle++;

            // Monks heal most-wounded friendly warrior
            HealWithMonks(state, actions);

            // Attack with warriors when threshold reached
            if (myWarriors.Count >= ATTACK_THRESHOLD)
            {
                AttackWithUnits(myWarriors, state, actions);
                // Monks follow the army but don't attack
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

                // Find most-wounded warrior below 80% HP
                int? bestTarget = null;
                float lowestHpRatio = 0.8f;

                foreach (int warriorNbr in myWarriors)
                {
                    var info = state.GetUnit(warriorNbr);
                    if (!info.HasValue) continue;
                    float ratio = info.Value.Health / GameConstants.HEALTH[UnitType.WARRIOR];
                    if (ratio < lowestHpRatio)
                    {
                        lowestHpRatio = ratio;
                        bestTarget = warriorNbr;
                    }
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
