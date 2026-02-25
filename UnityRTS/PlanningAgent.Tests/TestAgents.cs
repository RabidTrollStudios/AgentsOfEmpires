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
                actions.Train(baseNbr, UnitType.WORKER);
        }
    }

    internal class TrainNWorkersAgent : IPlanningAgent
    {
        private readonly int max;
        private int trained;

        public TrainNWorkersAgent(int max) { this.max = max; }
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
                    && state.MyGold >= GameConstants.COST[UnitType.WORKER])
                {
                    actions.Train(bases[0], UnitType.WORKER);
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
            var workers = state.GetMyUnits(UnitType.WORKER);
            if (workers.Count > 0)
            {
                var info = state.GetUnit(workers[0]);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                {
                    actions.Build(workers[0], buildPos, buildType);
                    built = true;
                }
            }
        }
    }

    internal class BuildWithSoldierAgent : IPlanningAgent
    {
        private bool tried;
        public void InitializeMatch() { tried = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (tried) return;
            var soldiers = state.GetMyUnits(UnitType.SOLDIER);
            if (soldiers.Count > 0)
            {
                actions.Build(soldiers[0], new Position(15, 15), UnitType.BARRACKS);
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

            var workers = state.GetMyUnits(UnitType.WORKER);
            foreach (int wNbr in workers)
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
            var workers = state.GetMyUnits(UnitType.WORKER);
            var mines = state.GetAllUnits(UnitType.MINE);
            var bases = state.GetMyUnits(UnitType.BASE);

            if (mines.Count == 0 || bases.Count == 0) return;

            foreach (int wNbr in workers)
            {
                var info = state.GetUnit(wNbr);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                {
                    actions.Gather(wNbr, mines[0], bases[0]);
                }
            }
        }
    }

    internal class GatherWithSoldierAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var soldiers = state.GetMyUnits(UnitType.SOLDIER);
            var mines = state.GetAllUnits(UnitType.MINE);
            var bases = state.GetMyUnits(UnitType.BASE);
            if (soldiers.Count > 0 && mines.Count > 0 && bases.Count > 0)
                actions.Gather(soldiers[0], mines[0], bases[0]);
        }
    }

    internal class GatherFromBarracksAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var workers = state.GetMyUnits(UnitType.WORKER);
            var barracks = state.GetMyUnits(UnitType.BARRACKS);
            var bases = state.GetMyUnits(UnitType.BASE);
            if (workers.Count > 0 && barracks.Count > 0 && bases.Count > 0)
                actions.Gather(workers[0], barracks[0], bases[0]);
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
            foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                            UnitType.BASE, UnitType.BARRACKS, UnitType.REFINERY })
            {
                var enemies = state.GetEnemyUnits(ut);
                if (enemies.Count > 0) { targetNbr = enemies[0]; break; }
            }

            if (!targetNbr.HasValue) return;

            // All my attack-capable units attack that target
            foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER })
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
            var soldiers = state.GetMyUnits(UnitType.SOLDIER);
            var enemies = state.GetEnemyUnits(UnitType.WORKER);
            if (soldiers.Count > 0 && enemies.Count > 0)
            {
                actions.Attack(soldiers[0], enemies[0]);
                attacked = true;
            }
        }
    }

    internal class AttackOwnWorkerAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var soldiers = state.GetMyUnits(UnitType.SOLDIER);
            var workers = state.GetMyUnits(UnitType.WORKER);
            if (soldiers.Count > 0 && workers.Count > 0)
                actions.Attack(soldiers[0], workers[0]);
        }
    }

    internal class WorkerAttackAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var workers = state.GetMyUnits(UnitType.WORKER);
            var enemies = state.GetEnemyUnits(UnitType.WORKER);
            if (workers.Count > 0 && enemies.Count > 0)
                actions.Attack(workers[0], enemies[0]);
        }
    }

    internal class AttackMineAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var soldiers = state.GetMyUnits(UnitType.SOLDIER);
            var mines = state.GetAllUnits(UnitType.MINE);
            if (soldiers.Count > 0 && mines.Count > 0)
                actions.Attack(soldiers[0], mines[0]);
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
            foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                            UnitType.BASE, UnitType.BARRACKS, UnitType.REFINERY })
            {
                var enemies = state.GetEnemyUnits(ut);
                if (enemies.Count > 0) { target = enemies[0]; break; }
            }

            if (!target.HasValue) return;

            foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER })
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
}
