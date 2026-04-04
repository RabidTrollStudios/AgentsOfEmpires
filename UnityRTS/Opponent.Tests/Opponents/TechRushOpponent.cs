using System.Collections.Generic;
using AgentSDK;

namespace Opponent.Tests
{
    /// <summary>
    /// [MEDIUM] Fast Lancer tech path: 3 pawns, fast Tower, mass Lancers.
    /// Builds Monastery after 4+ lancers for monk sustain.
    /// Attacks with 5+ lancers. Tests whether the Tower (300g) -> Lancer (70g)
    /// tech path is cost-effective compared to Barracks (400g) -> Warrior (100g).
    /// Strategy to beat: archers counter lancers (1.25x damage),
    /// or rush before the tower completes.
    /// </summary>
    public class TechRushOpponent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 3;
        private const int ATTACK_THRESHOLD = 5;
        private const int MONK_THRESHOLD = 4;

        public override void InitializeMatch() { }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            TrainPawns(state, actions, MAX_PAWNS);
            GatherWithIdlePawns(state, actions);

            // Fast tower for lancers
            if (myTowers.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.TOWER, state, actions);

            // Add monastery once lancer army is forming
            if (myMonasteries.Count == 0 && myLancers.Count >= MONK_THRESHOLD
                && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.MONASTERY, state, actions);

            // Train lancers from tower
            foreach (int towerNbr in myTowers)
            {
                var info = state.GetUnit(towerNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.LANCER])
                {
                    actions.Train(towerNbr, UnitType.LANCER);
                }
            }

            // Train monks for sustain
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

            // Monks heal most-wounded lancer
            HealWithMonks(state, actions);

            // Attack with lancers
            if (myLancers.Count >= ATTACK_THRESHOLD)
                AttackWithUnits(myLancers, state, actions);
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

                int? bestTarget = null;
                float lowestHpRatio = 0.8f;

                foreach (int lancerNbr in myLancers)
                {
                    var info = state.GetUnit(lancerNbr);
                    if (!info.HasValue) continue;
                    float ratio = info.Value.Health / GameConstants.HEALTH[UnitType.LANCER];
                    if (ratio < lowestHpRatio)
                    {
                        lowestHpRatio = ratio;
                        bestTarget = lancerNbr;
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
