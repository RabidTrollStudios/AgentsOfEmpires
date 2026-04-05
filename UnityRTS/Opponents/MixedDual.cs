using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [HARD] Reactive counter-picker with all 3 buildings: 6 pawns,
    /// 1 Barracks + 1 Archery + 1 Tower. Scouts enemy buildings and picks
    /// the R-P-S counter unit as primary, trains all 3 types from all buildings.
    /// The broadest army composition in the Hard tier.
    /// Attacks with all combat units when total army reaches 6+.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 6;
        private const int ATTACK_THRESHOLD = 6;

        private UnitType _priorityUnit = UnitType.MINE;

        public override void InitializeMatch()
        {
            _priorityUnit = UnitType.MINE;
        }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            TrainPawns(state, actions, MAX_PAWNS);
            GatherWithIdlePawns(state, actions);

            // Scout enemy buildings and pick counter as priority
            if (_priorityUnit == UnitType.MINE)
            {
                if (state.GetEnemyUnits(UnitType.BARRACKS).Count > 0)
                    _priorityUnit = UnitType.LANCER;
                else if (state.GetEnemyUnits(UnitType.ARCHERY).Count > 0)
                    _priorityUnit = UnitType.WARRIOR;
                else if (state.GetEnemyUnits(UnitType.TOWER).Count > 0)
                    _priorityUnit = UnitType.ARCHER;
            }

            // Build all 3 buildings — priority building first
            UnitType firstBuilding = _priorityUnit == UnitType.LANCER ? UnitType.TOWER
                : _priorityUnit == UnitType.WARRIOR ? UnitType.BARRACKS
                : _priorityUnit == UnitType.ARCHER ? UnitType.ARCHERY
                : UnitType.BARRACKS; // default if no enemy scouted yet

            if (GetBuildingCount(firstBuilding) < 1 && HasBuiltUnit(myBases, state))
                BuildStructure(firstBuilding, state, actions);
            else if (myBarracks.Count < 1 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);
            else if (myArchery.Count < 1 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.ARCHERY, state, actions);
            else if (myTowers.Count < 1 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.TOWER, state, actions);

            // Train from all buildings
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

            // Attack with full army
            int armySize = myWarriors.Count + myArchers.Count + myLancers.Count;
            if (armySize >= ATTACK_THRESHOLD)
            {
                AttackWithUnits(myWarriors, state, actions);
                AttackWithUnits(myArchers, state, actions);
                AttackWithUnits(myLancers, state, actions);
            }
        }

        private int GetBuildingCount(UnitType type)
        {
            if (type == UnitType.BARRACKS) return myBarracks.Count;
            if (type == UnitType.ARCHERY) return myArchery.Count;
            if (type == UnitType.TOWER) return myTowers.Count;
            return 0;
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
