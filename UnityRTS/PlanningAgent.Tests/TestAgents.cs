using AgentSDK;

namespace PlanningAgent.Tests
{
    // ==================================================================
    // Training agents
    // ==================================================================

    internal class TrainOnceAgent : IPlanningAgent
    {
        private readonly UnitType trainType;
        private bool trained;

        public TrainOnceAgent(UnitType trainType) { this.trainType = trainType; }
        public void InitializeMatch() { trained = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (trained) return;
            var bases = state.GetMyUnits(UnitType.BASE);
            if (bases.Count > 0)
            {
                var info = state.GetUnit(bases[0]);
                if (info.HasValue && info.Value.IsBuilt && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[trainType])
                {
                    actions.Train(bases[0], trainType);
                    trained = true;
                }
            }
        }
    }

    internal class TrainFromBarracksAgent : IPlanningAgent
    {
        private readonly UnitType trainType;
        private bool trained;

        public TrainFromBarracksAgent(UnitType trainType) { this.trainType = trainType; }
        public void InitializeMatch() { trained = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (trained) return;
            var barracks = state.GetMyUnits(UnitType.BARRACKS);
            if (barracks.Count > 0)
            {
                var info = state.GetUnit(barracks[0]);
                if (info.HasValue && info.Value.IsBuilt && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[trainType])
                {
                    actions.Train(barracks[0], trainType);
                    trained = true;
                }
            }
        }
    }

    internal class TrainFromArcheryAgent : IPlanningAgent
    {
        private readonly UnitType trainType;
        private bool trained;

        public TrainFromArcheryAgent(UnitType trainType) { this.trainType = trainType; }
        public void InitializeMatch() { trained = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (trained) return;
            var archeries = state.GetMyUnits(UnitType.ARCHERY);
            if (archeries.Count > 0)
            {
                var info = state.GetUnit(archeries[0]);
                if (info.HasValue && info.Value.IsBuilt && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[trainType])
                {
                    actions.Train(archeries[0], trainType);
                    trained = true;
                }
            }
        }
    }

    internal class TrainFromBaseAgent : IPlanningAgent
    {
        private readonly UnitType trainType;
        private bool trained;

        public TrainFromBaseAgent(UnitType trainType) { this.trainType = trainType; }
        public void InitializeMatch() { trained = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (trained) return;
            var bases = state.GetMyUnits(UnitType.BASE);
            if (bases.Count > 0)
            {
                actions.Train(bases[0], trainType);
                trained = true;
            }
        }
    }

    internal class SpamTrainAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var bases = state.GetMyUnits(UnitType.BASE);
            foreach (int baseNbr in bases)
                actions.Train(baseNbr, UnitType.PAWN);
        }
    }

    internal class TrainNPawnsAgent : IPlanningAgent
    {
        private readonly int max;
        private int trained;

        public TrainNPawnsAgent(int max) { this.max = max; }
        public void InitializeMatch() { trained = 0; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (trained >= max) return;
            var bases = state.GetMyUnits(UnitType.BASE);
            if (bases.Count > 0)
            {
                var info = state.GetUnit(bases[0]);
                if (info.HasValue && info.Value.IsBuilt && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.PAWN])
                {
                    actions.Train(bases[0], UnitType.PAWN);
                    trained++;
                }
            }
        }
    }

    // ==================================================================
    // Building agents
    // ==================================================================

    internal class BuildOnceAgent : IPlanningAgent
    {
        private readonly UnitType buildType;
        private readonly Position buildPos;
        private bool built;

        public BuildOnceAgent(UnitType buildType, Position buildPos)
        {
            this.buildType = buildType;
            this.buildPos = buildPos;
        }

        public void InitializeMatch() { built = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (built) return;
            var pawns = state.GetMyUnits(UnitType.PAWN);
            if (pawns.Count > 0)
            {
                var info = state.GetUnit(pawns[0]);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                {
                    actions.Build(pawns[0], buildPos, buildType);
                    built = true;
                }
            }
        }
    }

    internal class BuildWithWarriorAgent : IPlanningAgent
    {
        private bool tried;
        public void InitializeMatch() { tried = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (tried) return;
            var warriors = state.GetMyUnits(UnitType.WARRIOR);
            if (warriors.Count > 0)
            {
                actions.Build(warriors[0], new Position(15, 15), UnitType.BARRACKS);
                tried = true;
            }
        }
    }

    internal class BuildMultipleAgent : IPlanningAgent
    {
        private int buildIndex;
        private readonly Position[] buildSites = new[]
        {
            new Position(15, 15),
            new Position(20, 15),
            new Position(15, 20),
        };

        public void InitializeMatch() { buildIndex = 0; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (buildIndex >= buildSites.Length) return;

            var pawns = state.GetMyUnits(UnitType.PAWN);
            foreach (int wNbr in pawns)
            {
                if (buildIndex >= buildSites.Length) break;
                var info = state.GetUnit(wNbr);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.BARRACKS]
                    && state.IsAreaBuildable(UnitType.BARRACKS, buildSites[buildIndex]))
                {
                    actions.Build(wNbr, buildSites[buildIndex], UnitType.BARRACKS);
                    buildIndex++;
                }
            }
        }
    }

    // ==================================================================
    // Gathering agents
    // ==================================================================

    internal class GatherAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var pawns = state.GetMyUnits(UnitType.PAWN);
            var mines = state.GetAllUnits(UnitType.MINE);
            var bases = state.GetMyUnits(UnitType.BASE);

            if (mines.Count == 0 || bases.Count == 0) return;

            foreach (int wNbr in pawns)
            {
                var info = state.GetUnit(wNbr);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                {
                    actions.Gather(wNbr, mines[0], bases[0]);
                }
            }
        }
    }

    internal class GatherWithWarriorAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var warriors = state.GetMyUnits(UnitType.WARRIOR);
            var mines = state.GetAllUnits(UnitType.MINE);
            var bases = state.GetMyUnits(UnitType.BASE);
            if (warriors.Count > 0 && mines.Count > 0 && bases.Count > 0)
                actions.Gather(warriors[0], mines[0], bases[0]);
        }
    }

    internal class GatherFromBarracksAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var pawns = state.GetMyUnits(UnitType.PAWN);
            var barracks = state.GetMyUnits(UnitType.BARRACKS);
            var bases = state.GetMyUnits(UnitType.BASE);
            if (pawns.Count > 0 && barracks.Count > 0 && bases.Count > 0)
                actions.Gather(pawns[0], barracks[0], bases[0]);
        }
    }

    // ==================================================================
    // Combat agents
    // ==================================================================

    internal class AttackFirstEnemyAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            // Find any enemy unit
            int? targetNbr = null;
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.PAWN,
                                            UnitType.BASE, UnitType.BARRACKS })
            {
                var enemies = state.GetEnemyUnits(ut);
                if (enemies.Count > 0) { targetNbr = enemies[0]; break; }
            }

            if (!targetNbr.HasValue) return;

            // All my attack-capable units attack that target
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER })
            {
                foreach (int unitNbr in state.GetMyUnits(ut))
                {
                    var info = state.GetUnit(unitNbr);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                        actions.Attack(unitNbr, targetNbr.Value);
                }
            }
        }
    }

    internal class AttackOnceAgent : IPlanningAgent
    {
        private bool attacked;
        public void InitializeMatch() { attacked = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (attacked) return;
            var warriors = state.GetMyUnits(UnitType.WARRIOR);
            var enemies = state.GetEnemyUnits(UnitType.PAWN);
            if (warriors.Count > 0 && enemies.Count > 0)
            {
                actions.Attack(warriors[0], enemies[0]);
                attacked = true;
            }
        }
    }

    internal class AttackOwnPawnAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var warriors = state.GetMyUnits(UnitType.WARRIOR);
            var pawns = state.GetMyUnits(UnitType.PAWN);
            if (warriors.Count > 0 && pawns.Count > 0)
                actions.Attack(warriors[0], pawns[0]);
        }
    }

    internal class PawnAttackAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var pawns = state.GetMyUnits(UnitType.PAWN);
            var enemies = state.GetEnemyUnits(UnitType.PAWN);
            if (pawns.Count > 0 && enemies.Count > 0)
                actions.Attack(pawns[0], enemies[0]);
        }
    }

    internal class AttackMineAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var warriors = state.GetMyUnits(UnitType.WARRIOR);
            var mines = state.GetAllUnits(UnitType.MINE);
            if (warriors.Count > 0 && mines.Count > 0)
                actions.Attack(warriors[0], mines[0]);
        }
    }

    internal class AttackAllEnemiesAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            int? target = null;
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER, UnitType.PAWN,
                                            UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER, UnitType.MONASTERY })
            {
                var enemies = state.GetEnemyUnits(ut);
                if (enemies.Count > 0) { target = enemies[0]; break; }
            }

            if (!target.HasValue) return;

            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER })
            {
                foreach (int unitNbr in state.GetMyUnits(ut))
                {
                    var info = state.GetUnit(unitNbr);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                        actions.Attack(unitNbr, target.Value);
                }
            }
        }
    }

    // ==================================================================
    // Tower / Lancer agents
    // ==================================================================

    internal class TrainFromTowerAgent : IPlanningAgent
    {
        private readonly UnitType trainType;
        private bool trained;

        public TrainFromTowerAgent(UnitType trainType) { this.trainType = trainType; }
        public void InitializeMatch() { trained = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (trained) return;
            var towers = state.GetMyUnits(UnitType.TOWER);
            if (towers.Count > 0)
            {
                var info = state.GetUnit(towers[0]);
                if (info.HasValue && info.Value.IsBuilt && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[trainType])
                {
                    actions.Train(towers[0], trainType);
                    trained = true;
                }
            }
        }
    }

    // ==================================================================
    // Monastery / Monk / Heal agents
    // ==================================================================

    internal class TrainFromMonasteryAgent : IPlanningAgent
    {
        private readonly UnitType trainType;
        private bool trained;

        public TrainFromMonasteryAgent(UnitType trainType) { this.trainType = trainType; }
        public void InitializeMatch() { trained = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (trained) return;
            var monasteries = state.GetMyUnits(UnitType.MONASTERY);
            if (monasteries.Count > 0)
            {
                var info = state.GetUnit(monasteries[0]);
                if (info.HasValue && info.Value.IsBuilt && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[trainType])
                {
                    actions.Train(monasteries[0], trainType);
                    trained = true;
                }
            }
        }
    }

    internal class HealWoundedAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var monks = state.GetMyUnits(UnitType.MONK);
            foreach (int monkNbr in monks)
            {
                var monkInfo = state.GetUnit(monkNbr);
                if (!monkInfo.HasValue || monkInfo.Value.CurrentAction == UnitAction.HEAL) continue;
                if (monkInfo.Value.Mana < GameConstants.MANA_COST) continue;

                // Find most-wounded friendly mobile unit
                int bestTarget = -1;
                float lowestRatio = GameConstants.HEAL_THRESHOLD;

                foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER, UnitType.PAWN })
                {
                    foreach (int unitNbr in state.GetMyUnits(ut))
                    {
                        var info = state.GetUnit(unitNbr);
                        if (!info.HasValue) continue;
                        float ratio = info.Value.Health / GameConstants.HEALTH[ut];
                        if (ratio <= lowestRatio)
                        {
                            lowestRatio = ratio;
                            bestTarget = unitNbr;
                        }
                    }
                }

                if (bestTarget >= 0)
                    actions.Heal(monkNbr, bestTarget);
            }
        }
    }

    internal class LancerAttackAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            int? target = null;
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER, UnitType.MONK, UnitType.PAWN,
                                            UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER, UnitType.MONASTERY })
            {
                var enemies = state.GetEnemyUnits(ut);
                if (enemies.Count > 0) { target = enemies[0]; break; }
            }
            if (!target.HasValue) return;

            foreach (int lancerNbr in state.GetMyUnits(UnitType.LANCER))
            {
                var info = state.GetUnit(lancerNbr);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                    actions.Attack(lancerNbr, target.Value);
            }
        }
    }
}
