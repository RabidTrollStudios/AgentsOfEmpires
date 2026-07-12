using AgentSDK;

namespace AgentTestHarness
{
    // ==================================================================
    // Minimal single-purpose agents for automated testing.
    //
    // Each agent implements exactly one behavior (train, build, gather,
    // attack, heal) so tests can isolate individual game mechanics without
    // complex AI logic interfering. They are intentionally simple — most
    // issue a single command and then stop, making test outcomes deterministic.
    // ==================================================================

    // ==================================================================
    // Training agents
    // ==================================================================

    /// <summary>Trains one unit of the specified type from the first available BASE, then stops.</summary>
    public class TrainOnceAgent : IPlanningAgent
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

    /// <summary>Trains one unit of the specified type from the first BARRACKS, then stops.</summary>
    public class TrainFromBarracksAgent : IPlanningAgent
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

    /// <summary>Trains one unit of the specified type from the first ARCHERY, then stops.</summary>
    public class TrainFromArcheryAgent : IPlanningAgent
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

    /// <summary>Trains one unit from the first BASE without checking IsBuilt or IDLE state.</summary>
    public class TrainFromBaseAgent : IPlanningAgent
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

    /// <summary>Continuously trains PAWNs from all bases every tick (stress test).</summary>
    public class SpamTrainAgent : IPlanningAgent
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

    /// <summary>Trains exactly N pawns from BASE, one at a time, then stops.</summary>
    public class TrainNPawnsAgent : IPlanningAgent
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

    /// <summary>Orders the first idle PAWN to build a structure at a fixed position, then stops.</summary>
    public class BuildOnceAgent : IPlanningAgent
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

    /// <summary>
    /// Orders any idle PAWN to build a structure at a fixed position — every time one is
    /// idle and the building is not yet complete. Because it re-issues the build against the
    /// same position, a second pawn will RESUME an already-placed unbuilt building rather than
    /// try to place a new one. Used to exercise the build-resume path.
    /// </summary>
    public class ResumeBuildAgent : IPlanningAgent
    {
        private readonly UnitType buildType;
        private readonly Position buildPos;

        public ResumeBuildAgent(UnitType buildType, Position buildPos)
        {
            this.buildType = buildType;
            this.buildPos = buildPos;
        }

        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            foreach (int pawnNbr in state.GetMyUnits(UnitType.PAWN))
            {
                var info = state.GetUnit(pawnNbr);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                {
                    actions.Build(pawnNbr, buildPos, buildType);
                    return; // one command per tick
                }
            }
        }
    }

    /// <summary>Attempts to build with a WARRIOR (should fail — warriors can't build).</summary>
    public class BuildWithWarriorAgent : IPlanningAgent
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

    /// <summary>Orders idle PAWNs to build BARRACKS at multiple predetermined sites.</summary>
    public class BuildMultipleAgent : IPlanningAgent
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

    /// <summary>Sends all idle PAWNs to gather from the first mine to the first base.</summary>
    public class GatherAgent : IPlanningAgent
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

    /// <summary>Attempts to gather with a WARRIOR (should fail — warriors can't gather).</summary>
    public class GatherWithWarriorAgent : IPlanningAgent
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

    /// <summary>Attempts to gather from a BARRACKS (should fail — barracks isn't a mine).</summary>
    public class GatherFromBarracksAgent : IPlanningAgent
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

    /// <summary>All idle WARRIOR/ARCHER units attack the first visible enemy unit.</summary>
    public class AttackFirstEnemyAgent : IPlanningAgent
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

    /// <summary>First WARRIOR attacks the first enemy PAWN once, then stops.</summary>
    public class AttackOnceAgent : IPlanningAgent
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

    /// <summary>Attempts friendly fire — WARRIOR attacks own PAWN (should fail).</summary>
    public class AttackOwnPawnAgent : IPlanningAgent
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

    /// <summary>Attempts to attack with a PAWN (should fail — pawns can't attack).</summary>
    public class PawnAttackAgent : IPlanningAgent
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

    /// <summary>Attempts to attack a MINE (should fail — mines are non-targetable).</summary>
    public class AttackMineAgent : IPlanningAgent
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

    /// <summary>All idle combat units (WARRIOR/ARCHER/LANCER) attack the first visible enemy.</summary>
    public class AttackAllEnemiesAgent : IPlanningAgent
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

    /// <summary>Trains one unit of the specified type from the first TOWER, then stops.</summary>
    public class TrainFromTowerAgent : IPlanningAgent
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

    /// <summary>Trains one unit of the specified type from the first MONASTERY, then stops.</summary>
    public class TrainFromMonasteryAgent : IPlanningAgent
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

    /// <summary>Each monk heals the most-wounded friendly mobile unit that is missing at least HEAL_AMOUNT HP.</summary>
    public class HealWoundedAgent : IPlanningAgent
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
                float lowestHealth = float.MaxValue;

                foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER, UnitType.PAWN })
                {
                    foreach (int unitNbr in state.GetMyUnits(ut))
                    {
                        var info = state.GetUnit(unitNbr);
                        if (!info.HasValue) continue;
                        float maxHp = GameConstants.HEALTH[ut];
                        if (info.Value.Health > maxHp - GameConstants.HEAL_AMOUNT) continue;
                        if (info.Value.Health < lowestHealth)
                        {
                            lowestHealth = info.Value.Health;
                            bestTarget = unitNbr;
                        }
                    }
                }

                if (bestTarget >= 0)
                    actions.Heal(monkNbr, bestTarget);
            }
        }
    }

    /// <summary>All idle LANCERs attack the first visible enemy unit.</summary>
    public class LancerAttackAgent : IPlanningAgent
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
