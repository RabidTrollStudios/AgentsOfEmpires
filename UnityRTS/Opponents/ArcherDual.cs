using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [HARD] Archer-primary dual with lancer support: 6 pawns, 2 Archery + 1 Tower.
    /// Trains archers from both archeries and lancers from the tower.
    /// Archers use volley micro — each archer cycles to a different enemy target
    /// every tick to maximize volley first-hit bonus procs. Lancers cover the
    /// archer weakness to warriors.
    /// Attacks with all combat units when total army reaches 6+.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 6;
        private const int ATTACK_THRESHOLD = 6;

        // Track which target each archer last attacked so they can rotate
        private Dictionary<int, int> _lastArcherTarget = new Dictionary<int, int>();

        public override void InitializeMatch()
        {
            _lastArcherTarget = new Dictionary<int, int>();
        }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            TrainPawns(state, actions, MAX_PAWNS);
            GatherWithIdlePawns(state, actions);

            // Build 2 archeries then 1 tower
            if (myArchery.Count < 2 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.ARCHERY, state, actions);
            else if (myTowers.Count < 1 && HasBuiltUnit(myArchery, state))
                BuildStructure(UnitType.TOWER, state, actions);

            // Train archers from all archeries
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

            // Attack with full army
            int armySize = myArchers.Count + myLancers.Count;
            if (armySize < ATTACK_THRESHOLD) return;

            // Lancers attack normally
            AttackWithUnits(myLancers, state, actions);

            // Archers use volley micro — cycle targets for maximum first-hit bonus
            VolleyMicro(state, actions);
        }

        private void VolleyMicro(IGameState state, IAgentActions actions)
        {
            // Build list of living enemies
            var enemies = new List<int>();
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER,
                                            UnitType.MONK, UnitType.PAWN, UnitType.BASE,
                                            UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER,
                                            UnitType.MONASTERY })
            {
                foreach (int enemyNbr in state.GetEnemyUnits(ut))
                    enemies.Add(enemyNbr);
            }
            if (enemies.Count == 0) return;

            foreach (int archerNbr in myArchers)
            {
                var info = state.GetUnit(archerNbr);
                if (!info.HasValue) continue;

                // Find a target this archer hasn't hit recently
                int lastTarget = _lastArcherTarget.ContainsKey(archerNbr)
                    ? _lastArcherTarget[archerNbr] : -1;

                // Pick a different target than last time (cycle for volley bonus)
                int chosenTarget = -1;
                foreach (int enemyNbr in enemies)
                {
                    if (enemyNbr != lastTarget)
                    {
                        chosenTarget = enemyNbr;
                        break;
                    }
                }
                // If only one enemy left, just keep hitting it
                if (chosenTarget < 0) chosenTarget = enemies[0];

                if (info.Value.CurrentAction == UnitAction.IDLE
                    || info.Value.CurrentAction == UnitAction.ATTACK)
                {
                    actions.Attack(archerNbr, chosenTarget);
                    _lastArcherTarget[archerNbr] = chosenTarget;
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
