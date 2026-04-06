using System.Collections.Generic;
using AgentSDK;
using AgentTestHarness;
using NUnit.Framework;

namespace GameManager.Tests
{
    /// <summary>
    /// SimGame record-replay parity tests running inside the Unity Editor.
    /// Mirrors the standalone Parity.Tests scenarios to verify determinism
    /// from within Unity's test runner.
    ///
    /// Pattern: run a scenario with real agents + command recording, then
    /// replay the recorded commands on a fresh game and assert identical
    /// state hashes at every frame.
    /// </summary>
    [TestFixture]
    public class SimParityTests
    {
        private DivergenceReport RunScenario(ParityScenario scenario)
        {
            var builder = scenario.BuilderFactory();
            var agent0 = scenario.Agent0Factory();
            var agent1 = scenario.Agent1Factory();
            int steps = scenario.Frames;

            builder.WithAgent(0, agent0).WithAgent(1, agent1);
            var game1 = builder.Build();
            game1.EnableRecording();
            game1.InitializeMatch();
            game1.InitializeRound();

            var hashes1 = new long[steps];
            for (int t = 0; t < steps; t++)
            {
                game1.Step();
                hashes1[t] = game1.GetStateHash();
            }

            var recorded0 = game1.GetRecordedCommands(0);
            var recorded1 = game1.GetRecordedCommands(1);

            var replayBuilder = scenario.BuilderFactory();
            replayBuilder.WithAgent(0, new CommandPlayer(recorded0))
                         .WithAgent(1, new CommandPlayer(recorded1));
            var game2 = replayBuilder.Build();
            game2.InitializeMatch();
            game2.InitializeRound();

            for (int t = 0; t < steps; t++)
            {
                game2.Step();
                long hash2 = game2.GetStateHash();
                if (hashes1[t] != hash2)
                {
                    return new DivergenceReport
                    {
                        ScenarioName = scenario.Name,
                        DivergenceFrame = t + 1,
                        ExpectedHash = hashes1[t],
                        ActualHash = hash2,
                        TotalFrames = steps
                    };
                }
            }

            return new DivergenceReport
            {
                ScenarioName = scenario.Name,
                TotalFrames = steps
            };
        }

        private void AssertDeterministic(ParityScenario scenario)
        {
            var report = RunScenario(scenario);
            Assert.IsTrue(report.Passed, report.ToString());
        }

        #region Movement scenarios

        [Test]
        public void Parity_IdleUnits()
        {
            AssertDeterministic(new ParityScenario(
                "IdleUnits",
                () => new SimGameBuilder()
                    .WithMapSize(20, 20)
                    .WithUnit(0, UnitType.PAWN, new Position(5, 5))
                    .WithUnit(1, UnitType.PAWN, new Position(15, 15)),
                () => new DoNothingAgent(),
                () => new DoNothingAgent(),
                100));
        }

        [Test]
        public void Parity_PawnMovesToTarget()
        {
            AssertDeterministic(new ParityScenario(
                "PawnMovesToTarget",
                () => new SimGameBuilder()
                    .WithMapSize(20, 20)
                    .WithUnit(0, UnitType.PAWN, new Position(2, 10)),
                () => new MoveOnceAgent(new Position(18, 10)),
                () => new DoNothingAgent(),
                200));
        }

        [Test]
        public void Parity_MultiUnitMovement()
        {
            AssertDeterministic(new ParityScenario(
                "MultiUnitMovement",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithUnit(0, UnitType.PAWN, new Position(2, 5))
                    .WithUnit(0, UnitType.PAWN, new Position(2, 15))
                    .WithUnit(0, UnitType.PAWN, new Position(2, 25)),
                () => new MoveAllPawnsAgent(new Position(28, 15)),
                () => new DoNothingAgent(),
                300));
        }

        [Test]
        public void Parity_MovementAroundWall()
        {
            AssertDeterministic(new ParityScenario(
                "MovementAroundWall",
                () => new SimGameBuilder()
                    .WithMapSize(20, 20)
                    .WithUnit(0, UnitType.PAWN, new Position(5, 10))
                    .WithWall(new Position(10, 5), new Position(10, 15)),
                () => new MoveOnceAgent(new Position(15, 10)),
                () => new DoNothingAgent(),
                300));
        }

        #endregion

        #region Combat scenarios

        [Test]
        public void Parity_WarriorVsWarrior()
        {
            AssertDeterministic(new ParityScenario(
                "WarriorVsWarrior",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithUnit(0, UnitType.WARRIOR, new Position(5, 15))
                    .WithUnit(1, UnitType.WARRIOR, new Position(25, 15)),
                () => new AttackFirstEnemyAgent(),
                () => new AttackFirstEnemyAgent(),
                300));
        }

        [Test]
        public void Parity_ArcherVsArcher()
        {
            AssertDeterministic(new ParityScenario(
                "ArcherVsArcher",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithUnit(0, UnitType.ARCHER, new Position(5, 15))
                    .WithUnit(1, UnitType.ARCHER, new Position(25, 15)),
                () => new AttackFirstEnemyAgent(),
                () => new AttackFirstEnemyAgent(),
                300));
        }

        [Test]
        public void Parity_ArcherVsWarrior()
        {
            AssertDeterministic(new ParityScenario(
                "ArcherVsWarrior",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithUnit(0, UnitType.ARCHER, new Position(5, 15))
                    .WithUnit(1, UnitType.WARRIOR, new Position(25, 15)),
                () => new AttackFirstEnemyAgent(),
                () => new AttackFirstEnemyAgent(),
                300));
        }

        [Test]
        public void Parity_MultiUnitCombat()
        {
            AssertDeterministic(new ParityScenario(
                "MultiUnitCombat",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithUnit(0, UnitType.WARRIOR, new Position(5, 12))
                    .WithUnit(0, UnitType.WARRIOR, new Position(5, 18))
                    .WithUnit(0, UnitType.ARCHER, new Position(3, 15))
                    .WithUnit(1, UnitType.WARRIOR, new Position(25, 15))
                    .WithUnit(1, UnitType.ARCHER, new Position(27, 15)),
                () => new AttackFirstEnemyAgent(),
                () => new AttackFirstEnemyAgent(),
                400));
        }

        [Test]
        public void Parity_MutualCombat()
        {
            AssertDeterministic(new ParityScenario(
                "MutualCombat",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithUnit(0, UnitType.WARRIOR, new Position(10, 15))
                    .WithUnit(0, UnitType.ARCHER, new Position(8, 15))
                    .WithUnit(1, UnitType.WARRIOR, new Position(20, 15))
                    .WithUnit(1, UnitType.ARCHER, new Position(22, 15)),
                () => new AttackFirstEnemyAgent(),
                () => new AttackFirstEnemyAgent(),
                400));
        }

        [Test]
        public void Parity_LancerVsWarrior()
        {
            AssertDeterministic(new ParityScenario(
                "LancerVsWarrior",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithUnit(0, UnitType.LANCER, new Position(5, 15))
                    .WithUnit(1, UnitType.WARRIOR, new Position(25, 15)),
                () => new LancerAttackAgent(),
                () => new AttackFirstEnemyAgent(),
                300));
        }

        [Test]
        public void Parity_LancerVsArcher()
        {
            AssertDeterministic(new ParityScenario(
                "LancerVsArcher",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithUnit(0, UnitType.LANCER, new Position(5, 15))
                    .WithUnit(1, UnitType.ARCHER, new Position(25, 15)),
                () => new LancerAttackAgent(),
                () => new AttackFirstEnemyAgent(),
                300));
        }

        [Test]
        public void Parity_MixedArmyCombat()
        {
            AssertDeterministic(new ParityScenario(
                "MixedArmyCombat",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithUnit(0, UnitType.WARRIOR, new Position(5, 12))
                    .WithUnit(0, UnitType.ARCHER, new Position(3, 15))
                    .WithUnit(0, UnitType.LANCER, new Position(5, 18))
                    .WithUnit(0, UnitType.MONK, new Position(2, 15))
                    .WithUnit(1, UnitType.WARRIOR, new Position(25, 12))
                    .WithUnit(1, UnitType.ARCHER, new Position(27, 15))
                    .WithUnit(1, UnitType.LANCER, new Position(25, 18)),
                () => new HealAndFightAgent(),
                () => new AttackAllEnemiesAgent(),
                500));
        }

        #endregion

        #region Economy scenarios

        [Test]
        public void Parity_GatheringFullCycle()
        {
            AssertDeterministic(new ParityScenario(
                "GatheringFullCycle",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithGold(0, 500)
                    .WithUnit(0, UnitType.BASE, new Position(3, 8), isBuilt: true)
                    .WithUnit(0, UnitType.PAWN, new Position(6, 5))
                    .WithMine(new Position(20, 8)),
                () => new GatherAgent(),
                () => new DoNothingAgent(),
                500));
        }

        [Test]
        public void Parity_MultiPawnGathering()
        {
            AssertDeterministic(new ParityScenario(
                "MultiPawnGathering",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithGold(0, 500)
                    .WithUnit(0, UnitType.BASE, new Position(3, 8), isBuilt: true)
                    .WithUnit(0, UnitType.PAWN, new Position(6, 5))
                    .WithUnit(0, UnitType.PAWN, new Position(6, 10))
                    .WithUnit(0, UnitType.PAWN, new Position(6, 15))
                    .WithMine(new Position(20, 8)),
                () => new GatherAgent(),
                () => new DoNothingAgent(),
                500));
        }

        [Test]
        public void Parity_FullEconomy()
        {
            AssertDeterministic(new ParityScenario(
                "FullEconomy",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithGold(0, 5000)
                    .WithUnit(0, UnitType.BASE, new Position(3, 8), isBuilt: true)
                    .WithUnit(0, UnitType.PAWN, new Position(6, 5))
                    .WithUnit(0, UnitType.PAWN, new Position(6, 10))
                    .WithMine(new Position(20, 8)),
                () => new EconomyAgent(),
                () => new DoNothingAgent(),
                800));
        }

        #endregion

        #region Training scenarios

        [Test]
        public void Parity_TrainPawnFromBase()
        {
            AssertDeterministic(new ParityScenario(
                "TrainPawnFromBase",
                () => new SimGameBuilder()
                    .WithMapSize(20, 20)
                    .WithGold(0, 500)
                    .WithUnit(0, UnitType.BASE, new Position(5, 10), isBuilt: true),
                () => new TrainOnceAgent(UnitType.PAWN),
                () => new DoNothingAgent(),
                100));
        }

        [Test]
        public void Parity_TrainWarriorFromBarracks()
        {
            AssertDeterministic(new ParityScenario(
                "TrainWarriorFromBarracks",
                () => new SimGameBuilder()
                    .WithMapSize(20, 20)
                    .WithGold(0, 500)
                    .WithUnit(0, UnitType.BASE, new Position(3, 10), isBuilt: true)
                    .WithUnit(0, UnitType.BARRACKS, new Position(10, 10), isBuilt: true),
                () => new TrainFromBarracksAgent(UnitType.WARRIOR),
                () => new DoNothingAgent(),
                100));
        }

        [Test]
        public void Parity_TrainLancerFromTower()
        {
            AssertDeterministic(new ParityScenario(
                "TrainLancerFromTower",
                () => new SimGameBuilder()
                    .WithMapSize(20, 20)
                    .WithGold(0, 500)
                    .WithUnit(0, UnitType.BASE, new Position(3, 10), isBuilt: true)
                    .WithUnit(0, UnitType.TOWER, new Position(10, 10), isBuilt: true),
                () => new TrainFromTowerAgent(UnitType.LANCER),
                () => new DoNothingAgent(),
                100));
        }

        [Test]
        public void Parity_TrainMonkFromMonastery()
        {
            AssertDeterministic(new ParityScenario(
                "TrainMonkFromMonastery",
                () => new SimGameBuilder()
                    .WithMapSize(20, 20)
                    .WithGold(0, 500)
                    .WithUnit(0, UnitType.BASE, new Position(3, 10), isBuilt: true)
                    .WithUnit(0, UnitType.MONASTERY, new Position(10, 10), isBuilt: true),
                () => new TrainFromMonasteryAgent(UnitType.MONK),
                () => new DoNothingAgent(),
                100));
        }

        #endregion

        #region Building scenarios

        [Test]
        public void Parity_BuildBarracks()
        {
            AssertDeterministic(new ParityScenario(
                "BuildBarracks",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithGold(0, 5000)
                    .WithUnit(0, UnitType.BASE, new Position(3, 8), isBuilt: true)
                    .WithUnit(0, UnitType.PAWN, new Position(6, 5)),
                () => new BuildOnceAgent(UnitType.BARRACKS, new Position(15, 15)),
                () => new DoNothingAgent(),
                500));
        }

        [Test]
        public void Parity_BuildTower()
        {
            AssertDeterministic(new ParityScenario(
                "BuildTower",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithGold(0, 5000)
                    .WithUnit(0, UnitType.BASE, new Position(3, 8), isBuilt: true)
                    .WithUnit(0, UnitType.PAWN, new Position(6, 5)),
                () => new BuildOnceAgent(UnitType.TOWER, new Position(15, 15)),
                () => new DoNothingAgent(),
                500));
        }

        [Test]
        public void Parity_BuildMonastery()
        {
            AssertDeterministic(new ParityScenario(
                "BuildMonastery",
                () => new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithGold(0, 5000)
                    .WithUnit(0, UnitType.BASE, new Position(3, 8), isBuilt: true)
                    .WithUnit(0, UnitType.PAWN, new Position(6, 5)),
                () => new BuildOnceAgent(UnitType.MONASTERY, new Position(15, 15)),
                () => new DoNothingAgent(),
                500));
        }

        [Test]
        public void Parity_RepairBuilding()
        {
            AssertDeterministic(new ParityScenario(
                "RepairBuilding",
                () => new SimGameBuilder()
                    .WithMapSize(20, 20)
                    .WithGold(0, 500)
                    .WithUnit(0, UnitType.BASE, new Position(5, 10), isBuilt: true)
                    .WithUnit(0, UnitType.PAWN, new Position(8, 10)),
                () => new RepairDamagedBaseAgent(),
                () => new DoNothingAgent(),
                200));
        }

        #endregion

        #region Healing scenarios

        [Test]
        public void Parity_MonkHealsWarrior()
        {
            AssertDeterministic(new ParityScenario(
                "MonkHealsWarrior",
                () => new SimGameBuilder()
                    .WithMapSize(20, 20)
                    .WithUnit(0, UnitType.WARRIOR, new Position(10, 10))
                    .WithUnit(0, UnitType.MONK, new Position(8, 10))
                    .WithUnit(1, UnitType.ARCHER, new Position(18, 10)),
                () => new HealAndFightAgent(),
                () => new AttackFirstEnemyAgent(),
                400));
        }

        #endregion

        #region Collision avoidance scenarios

        [Test]
        public void Parity_HeadOnCollision()
        {
            AssertDeterministic(new ParityScenario(
                "HeadOnCollision",
                () => new SimGameBuilder()
                    .WithMapSize(20, 20)
                    .WithUnit(0, UnitType.PAWN, new Position(2, 10))
                    .WithUnit(1, UnitType.PAWN, new Position(18, 10)),
                () => new MoveOnceAgent(new Position(18, 10)),
                () => new MoveOnceAgent(new Position(2, 10)),
                300));
        }

        [Test]
        public void Parity_CorridorCongestion()
        {
            AssertDeterministic(new ParityScenario(
                "CorridorCongestion",
                () =>
                {
                    var b = new SimGameBuilder()
                        .WithMapSize(20, 20)
                        .WithUnit(0, UnitType.PAWN, new Position(2, 10))
                        .WithUnit(0, UnitType.PAWN, new Position(2, 11));
                    for (int x = 8; x <= 12; x++)
                    {
                        b.WithWall(new Position(x, 11), new Position(x, 11));
                        b.WithWall(new Position(x, 9), new Position(x, 9));
                    }
                    return b;
                },
                () => new MoveAllPawnsAgent(new Position(18, 10)),
                () => new DoNothingAgent(),
                600));
        }

        [Test]
        public void Parity_AttackerBlockedByAlly()
        {
            AssertDeterministic(new ParityScenario(
                "AttackerBlockedByAlly",
                () => new SimGameBuilder()
                    .WithMapSize(20, 20)
                    .WithUnit(0, UnitType.WARRIOR, new Position(2, 10))
                    .WithUnit(0, UnitType.PAWN, new Position(5, 10))
                    .WithUnit(1, UnitType.WARRIOR, new Position(18, 10)),
                () => new AttackFirstEnemyAgent(),
                () => new DoNothingAgent(),
                300));
        }

        #endregion

        #region Scenario-specific test agents

        private class MoveOnceAgent : IPlanningAgent
        {
            private readonly Position _target;
            private bool _moved;

            public MoveOnceAgent(Position target) { _target = target; }
            public void InitializeMatch() { _moved = false; }
            public void InitializeRound(IGameState state) { }
            public void Learn(IGameState state) { }

            public void Update(IGameState state, IAgentActions actions)
            {
                if (_moved) return;
                var pawns = state.GetMyUnits(UnitType.PAWN);
                if (pawns.Count > 0)
                {
                    actions.Move(pawns[0], _target);
                    _moved = true;
                }
            }
        }

        private class MoveAllPawnsAgent : IPlanningAgent
        {
            private readonly Position _target;
            private bool _moved;

            public MoveAllPawnsAgent(Position target) { _target = target; }
            public void InitializeMatch() { _moved = false; }
            public void InitializeRound(IGameState state) { }
            public void Learn(IGameState state) { }

            public void Update(IGameState state, IAgentActions actions)
            {
                if (_moved) return;
                var pawns = state.GetMyUnits(UnitType.PAWN);
                foreach (int pNbr in pawns)
                    actions.Move(pNbr, _target);
                _moved = true;
            }
        }

        private class EconomyAgent : IPlanningAgent
        {
            private bool _gathering;
            private bool _building;
            private bool _training;

            public void InitializeMatch() { _gathering = false; _building = false; _training = false; }
            public void InitializeRound(IGameState state) { }
            public void Learn(IGameState state) { }

            public void Update(IGameState state, IAgentActions actions)
            {
                var pawns = state.GetMyUnits(UnitType.PAWN);
                var bases = state.GetMyUnits(UnitType.BASE);
                var mines = state.GetAllUnits(UnitType.MINE);
                var barracks = state.GetMyUnits(UnitType.BARRACKS);

                if (!_gathering && pawns.Count > 0 && mines.Count > 0 && bases.Count > 0)
                {
                    var info = state.GetUnit(pawns[0]);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                    {
                        actions.Gather(pawns[0], mines[0], bases[0]);
                        _gathering = true;
                    }
                }

                if (!_building && pawns.Count > 1 && state.MyGold >= GameConstants.COST[UnitType.BARRACKS])
                {
                    var info = state.GetUnit(pawns[1]);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                    {
                        actions.Build(pawns[1], new Position(15, 15), UnitType.BARRACKS);
                        _building = true;
                    }
                }

                if (!_training && barracks.Count > 0)
                {
                    var bInfo = state.GetUnit(barracks[0]);
                    if (bInfo.HasValue && bInfo.Value.IsBuilt && bInfo.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
                    {
                        actions.Train(barracks[0], UnitType.WARRIOR);
                        _training = true;
                    }
                }
            }
        }

        private class RepairDamagedBaseAgent : IPlanningAgent
        {
            private bool _repairing;
            public void InitializeMatch() { _repairing = false; }
            public void InitializeRound(IGameState state) { }
            public void Learn(IGameState state) { }

            public void Update(IGameState state, IAgentActions actions)
            {
                if (_repairing) return;
                var pawns = state.GetMyUnits(UnitType.PAWN);
                var bases = state.GetMyUnits(UnitType.BASE);
                if (pawns.Count > 0 && bases.Count > 0)
                {
                    actions.Repair(pawns[0], bases[0]);
                    _repairing = true;
                }
            }
        }

        private class HealAndFightAgent : IPlanningAgent
        {
            public void InitializeMatch() { }
            public void InitializeRound(IGameState state) { }
            public void Learn(IGameState state) { }

            public void Update(IGameState state, IAgentActions actions)
            {
                foreach (int monkNbr in state.GetMyUnits(UnitType.MONK))
                {
                    var monkInfo = state.GetUnit(monkNbr);
                    if (!monkInfo.HasValue || monkInfo.Value.CurrentAction == UnitAction.HEAL) continue;
                    if (monkInfo.Value.Mana < GameConstants.MANA_COST) continue;

                    int bestTarget = -1;
                    float lowestHealth = float.MaxValue;
                    foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER })
                    {
                        foreach (int unitNbr in state.GetMyUnits(ut))
                        {
                            var info = state.GetUnit(unitNbr);
                            if (!info.HasValue) continue;
                            float maxHp = GameConstants.HEALTH[ut];
                            if (info.Value.Health > maxHp - GameConstants.HEAL_AMOUNT) continue;
                            if (info.Value.Health < lowestHealth) { lowestHealth = info.Value.Health; bestTarget = unitNbr; }
                        }
                    }
                    if (bestTarget >= 0)
                        actions.Heal(monkNbr, bestTarget);
                }

                int? target = null;
                foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER, UnitType.MONK, UnitType.PAWN,
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

        #endregion
    }
}
