using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [HARD] Triple tower lancer flood: 6 pawns, 3 Towers.
    /// Pure lancer production from 3 buildings with hit-and-run joust
    /// micro — lancers charge in for bonus damage, disengage after a
    /// few hits, then charge again for another joust proc.
    /// Attacks with lancers when army reaches 6+.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 6;
        private const int ATTACK_THRESHOLD = 6;
        private const int DISENGAGE_TICKS = 3;
        private const float RALLY_DISTANCE = 5.0f;

        private Dictionary<int, int> _combatTicks = new Dictionary<int, int>();

        public override void InitializeMatch()
        {
            _combatTicks = new Dictionary<int, int>();
        }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            // Build a base first if we dont have one
            if (myBases.Count == 0)
            {
                BuildStructure(UnitType.BASE, state, actions);
                return;
            }

            TrainPawns(state, actions, MAX_PAWNS);
            GatherWithIdlePawns(state, actions);

            // Build 3 towers for triple lancer production
            if (myTowers.Count < 3 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.TOWER, state, actions);

            // Train lancers from all towers
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

            if (myLancers.Count < ATTACK_THRESHOLD) return;

            // Find enemy target
            int? enemyTarget = FindAnyEnemy(state);
            if (!enemyTarget.HasValue) return;
            var targetInfo = state.GetUnit(enemyTarget.Value);
            if (!targetInfo.HasValue) return;

            // Hit-and-run micro for each lancer
            foreach (int lancerNbr in myLancers)
            {
                var info = state.GetUnit(lancerNbr);
                if (!info.HasValue) continue;

                if (!_combatTicks.ContainsKey(lancerNbr))
                    _combatTicks[lancerNbr] = 0;

                if (info.Value.CurrentAction == UnitAction.ATTACK)
                {
                    float dist = Position.Distance(info.Value.CenterPosition, targetInfo.Value.CenterPosition);
                    if (dist < GameConstants.ATTACK_RANGE[UnitType.LANCER] + 1.0f)
                    {
                        _combatTicks[lancerNbr]++;

                        if (_combatTicks[lancerNbr] >= DISENGAGE_TICKS && mainBaseNbr >= 0)
                        {
                            var baseInfo = state.GetUnit(mainBaseNbr);
                            if (baseInfo.HasValue)
                            {
                                actions.Move(lancerNbr, baseInfo.Value.CenterPosition);
                                _combatTicks[lancerNbr] = 0;
                            }
                        }
                    }
                }
                else if (info.Value.CurrentAction == UnitAction.MOVE)
                {
                    float dist = Position.Distance(info.Value.CenterPosition, targetInfo.Value.CenterPosition);
                    if (dist >= RALLY_DISTANCE)
                    {
                        actions.Attack(lancerNbr, enemyTarget.Value);
                        _combatTicks[lancerNbr] = 0;
                    }
                }
                else if (info.Value.CurrentAction == UnitAction.IDLE)
                {
                    actions.Attack(lancerNbr, enemyTarget.Value);
                    _combatTicks[lancerNbr] = 0;
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
