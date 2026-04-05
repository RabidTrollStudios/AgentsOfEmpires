using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [MID] Hit-and-run lancer rush: 2 pawns, 2 towers, lancers use joust
    /// tactics — charge in for bonus damage, then disengage to reset joust
    /// distance. Each lancer alternates between attacking and retreating.
    /// Lancers track ticks spent in melee; after a few hits they pull back
    /// to their rally point, then charge again for another joust bonus.
    /// Strategy to beat: archers counter lancers (1.25x), or pin them down
    /// so they can't disengage.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 2;
        private const int ATTACK_THRESHOLD = 3;
        private const int DISENGAGE_TICKS = 3; // pull back after this many ticks in combat
        private const float RALLY_DISTANCE = 5.0f; // how far to retreat before re-engaging

        // Track per-lancer combat ticks
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

            TrainPawns(state, actions, MAX_PAWNS);
            GatherWithIdlePawns(state, actions);

            // Rush to 2 towers
            if (myTowers.Count < 2 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.TOWER, state, actions);

            // Train lancers
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
                    // In combat — count ticks
                    float dist = Position.Distance(info.Value.CenterPosition, targetInfo.Value.CenterPosition);
                    if (dist < GameConstants.ATTACK_RANGE[UnitType.LANCER] + 1.0f)
                    {
                        _combatTicks[lancerNbr]++;

                        // After enough hits, disengage — move toward our base to reset joust
                        if (_combatTicks[lancerNbr] >= DISENGAGE_TICKS && mainBaseNbr >= 0)
                        {
                            var baseInfo = state.GetUnit(mainBaseNbr);
                            if (baseInfo.HasValue)
                            {
                                // Move toward base (rally point)
                                actions.Move(lancerNbr, baseInfo.Value.CenterPosition);
                                _combatTicks[lancerNbr] = 0;
                            }
                        }
                    }
                }
                else if (info.Value.CurrentAction == UnitAction.MOVE)
                {
                    // Disengaging — check if far enough from enemy to re-engage
                    float dist = Position.Distance(info.Value.CenterPosition, targetInfo.Value.CenterPosition);
                    if (dist >= RALLY_DISTANCE)
                    {
                        // Far enough — charge back in for joust bonus
                        actions.Attack(lancerNbr, enemyTarget.Value);
                        _combatTicks[lancerNbr] = 0;
                    }
                }
                else if (info.Value.CurrentAction == UnitAction.IDLE)
                {
                    // Idle — send to attack
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
