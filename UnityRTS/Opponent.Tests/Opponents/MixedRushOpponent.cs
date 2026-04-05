using System.Collections.Generic;
using AgentSDK;

namespace Opponent.Tests
{
    /// <summary>
    /// [MID] Reactive counter-picker rush: 2 pawns, waits to see what
    /// the opponent builds, then picks the unit type that counters it
    /// (Warrior→Lancer, Archer→Warrior, Lancer→Archer). Builds 2 of
    /// the counter building for dual production. Attacks with 3+ units.
    /// Strategy to beat: delay your building to stall the counter-picker,
    /// or switch unit types after it commits.
    /// </summary>
    public class MixedRushOpponent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 2;
        private const int ATTACK_THRESHOLD = 3;

        private UnitType _chosenUnit = UnitType.MINE;
        private UnitType _chosenBuilding = UnitType.MINE;

        public override void InitializeMatch()
        {
            _chosenUnit = UnitType.MINE;
            _chosenBuilding = UnitType.MINE;
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

            // Scout enemy buildings and pick counter
            if (_chosenUnit == UnitType.MINE)
            {
                if (state.GetEnemyUnits(UnitType.BARRACKS).Count > 0)
                {
                    _chosenUnit = UnitType.LANCER;
                    _chosenBuilding = UnitType.TOWER;
                }
                else if (state.GetEnemyUnits(UnitType.ARCHERY).Count > 0)
                {
                    _chosenUnit = UnitType.WARRIOR;
                    _chosenBuilding = UnitType.BARRACKS;
                }
                else if (state.GetEnemyUnits(UnitType.TOWER).Count > 0)
                {
                    _chosenUnit = UnitType.ARCHER;
                    _chosenBuilding = UnitType.ARCHERY;
                }
            }

            // Build 2 of the counter building once chosen
            if (_chosenBuilding != UnitType.MINE)
            {
                int buildingCount = _chosenBuilding == UnitType.BARRACKS ? myBarracks.Count
                    : _chosenBuilding == UnitType.ARCHERY ? myArchery.Count
                    : myTowers.Count;

                if (buildingCount < 2 && HasBuiltUnit(myBases, state))
                    BuildStructure(_chosenBuilding, state, actions);

                // Train counter units from all buildings
                var buildings = _chosenBuilding == UnitType.BARRACKS ? myBarracks
                    : _chosenBuilding == UnitType.ARCHERY ? myArchery
                    : myTowers;

                foreach (int bldgNbr in buildings)
                {
                    var info = state.GetUnit(bldgNbr);
                    if (info.HasValue && info.Value.IsBuilt
                        && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[_chosenUnit])
                    {
                        actions.Train(bldgNbr, _chosenUnit);
                    }
                }

                // Attack with chosen unit type
                var army = _chosenUnit == UnitType.WARRIOR ? myWarriors
                    : _chosenUnit == UnitType.ARCHER ? myArchers
                    : myLancers;

                if (army.Count >= ATTACK_THRESHOLD)
                    AttackWithUnits(army, state, actions);
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
