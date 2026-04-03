using System.Collections.Generic;
using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace Parity.Tests
{
    /// <summary>
    /// Verifies SimGame determinism: recording commands from a live agent and replaying
    /// them through a CommandPlayer produces identical game state at every tick.
    ///
    /// Test pattern:
    /// 1. Run game with real agents + recording enabled, collect per-tick state hashes
    /// 2. Replay recorded commands through CommandPlayer agents on a fresh game
    /// 3. Assert hashes match at every tick
    /// </summary>
    public class ParityTests
    {
        /// <summary>
        /// Run a scenario with recording, then replay and compare per-tick state hashes.
        /// Returns a DivergenceReport with the result.
        /// </summary>
        private DivergenceReport RunScenario(ParityScenario scenario)
        {
            var builder = scenario.BuilderFactory();
            var agent0 = scenario.Agent0Factory();
            var agent1 = scenario.Agent1Factory();
            int ticks = scenario.Ticks;

            // --- Recording run ---
            builder.WithAgent(0, agent0).WithAgent(1, agent1);
            var game1 = builder.Build();
            game1.EnableRecording();
            game1.InitializeMatch();
            game1.InitializeRound();

            var hashes1 = new long[ticks];
            for (int t = 0; t < ticks; t++)
            {
                game1.Tick();
                hashes1[t] = game1.GetStateHash();
            }

            var recorded0 = game1.GetRecordedCommands(0);
            var recorded1 = game1.GetRecordedCommands(1);

            // --- Replay run ---
            var replayBuilder = scenario.BuilderFactory();
            replayBuilder.WithAgent(0, new CommandPlayer(recorded0))
                         .WithAgent(1, new CommandPlayer(recorded1));
            var game2 = replayBuilder.Build();
            game2.InitializeMatch();
            game2.InitializeRound();

            for (int t = 0; t < ticks; t++)
            {
                game2.Tick();
                long hash2 = game2.GetStateHash();
                if (hashes1[t] != hash2)
                {
                    return new DivergenceReport
                    {
                        ScenarioName = scenario.Name,
                        DivergenceTick = t + 1,
                        ExpectedHash = hashes1[t],
                        ActualHash = hash2,
                        TotalTicks = ticks
                    };
                }
            }

            return new DivergenceReport
            {
                ScenarioName = scenario.Name,
                TotalTicks = ticks
            };
        }

        /// <summary>
        /// Run a scenario and assert deterministic parity (no divergence).
        /// </summary>
        private void AssertDeterministic(ParityScenario scenario)
        {
            var report = RunScenario(scenario);
            Assert.True(report.Passed, report.ToString());
        }

        #region Scenario definitions

        public static IEnumerable<object[]> AllScenarios()
        {
            yield return new object[] { Scenarios.IdleUnits() };
            yield return new object[] { Scenarios.PawnMovesToTarget() };
            yield return new object[] { Scenarios.MultiUnitMovement() };
            yield return new object[] { Scenarios.MovementAroundWall() };
            yield return new object[] { Scenarios.WarriorVsWarrior() };
            yield return new object[] { Scenarios.ArcherVsArcher() };
            yield return new object[] { Scenarios.ArcherVsWarrior() };
            yield return new object[] { Scenarios.MultiUnitCombat() };
            yield return new object[] { Scenarios.MutualCombat() };
            yield return new object[] { Scenarios.GatheringFullCycle() };
            yield return new object[] { Scenarios.MultiPawnGathering() };
            yield return new object[] { Scenarios.TrainPawnFromBase() };
            yield return new object[] { Scenarios.TrainWarriorFromBarracks() };
            yield return new object[] { Scenarios.BuildBarracks() };
            yield return new object[] { Scenarios.FullEconomy() };
            yield return new object[] { Scenarios.RepairBuilding() };
            yield return new object[] { Scenarios.HeadOnCollision() };
            yield return new object[] { Scenarios.CorridorCongestion() };
            yield return new object[] { Scenarios.AttackerBlockedByAlly() };
            // New unit types
            yield return new object[] { Scenarios.TrainLancerFromTower() };
            yield return new object[] { Scenarios.TrainMonkFromMonastery() };
            yield return new object[] { Scenarios.LancerVsWarrior() };
            yield return new object[] { Scenarios.LancerVsArcher() };
            yield return new object[] { Scenarios.MonkHealsWarrior() };
            yield return new object[] { Scenarios.MixedArmyCombat() };
            yield return new object[] { Scenarios.BuildTower() };
            yield return new object[] { Scenarios.BuildMonastery() };
        }

        #endregion

        [Theory]
        [MemberData(nameof(AllScenarios))]
        public void Parity_Scenario_IsDeterministic(ParityScenario scenario)
        {
            AssertDeterministic(scenario);
        }

        #region Standalone tests (non-scenario)

        [Fact]
        public void Parity_HashSensitivity_DifferentStatesProduceDifferentHashes()
        {
            var game1 = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(5, 5))
                .WithAgent(0, new DoNothingAgent())
                .Build();
            game1.InitializeMatch();
            game1.InitializeRound();
            game1.Tick();

            var game2 = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(10, 10))
                .WithAgent(0, new DoNothingAgent())
                .Build();
            game2.InitializeMatch();
            game2.InitializeRound();
            game2.Tick();

            Assert.NotEqual(game1.GetStateHash(), game2.GetStateHash());
        }

        [Fact]
        public void Parity_IdenticalRuns_WithoutRecording_MatchHashes()
        {
            var game1 = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(5, 5))
                .WithAgent(0, new DoNothingAgent())
                .Build();

            var game2 = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(5, 5))
                .WithAgent(0, new DoNothingAgent())
                .Build();

            game1.InitializeMatch(); game1.InitializeRound();
            game2.InitializeMatch(); game2.InitializeRound();

            for (int t = 0; t < 50; t++)
            {
                game1.Tick();
                game2.Tick();
                Assert.Equal(game1.GetStateHash(), game2.GetStateHash());
            }
        }

        #endregion

        #region Test agents

        /// <summary>
        /// Agent that issues a single Move command for its first idle pawn.
        /// </summary>
        private class MoveToTargetAgent : IPlanningAgent
        {
            private readonly Position target;
            private bool moved;

            public MoveToTargetAgent(Position target) { this.target = target; }
            public void InitializeMatch() { moved = false; }
            public void InitializeRound(IGameState state) { }
            public void Learn(IGameState state) { }

            public void Update(IGameState state, IAgentActions actions)
            {
                if (moved) return;
                var pawns = state.GetMyUnits(UnitType.PAWN);
                if (pawns.Count > 0)
                {
                    actions.Move(pawns[0], target);
                    moved = true;
                }
            }
        }

        /// <summary>
        /// Agent that moves multiple pawns to different targets.
        /// </summary>
        private class MoveMultipleAgent : IPlanningAgent
        {
            private readonly Position[] targets;
            private bool moved;

            public MoveMultipleAgent(params Position[] targets) { this.targets = targets; }
            public void InitializeMatch() { moved = false; }
            public void InitializeRound(IGameState state) { }
            public void Learn(IGameState state) { }

            public void Update(IGameState state, IAgentActions actions)
            {
                if (moved) return;
                var pawns = state.GetMyUnits(UnitType.PAWN);
                for (int i = 0; i < pawns.Count && i < targets.Length; i++)
                {
                    actions.Move(pawns[i], targets[i]);
                }
                moved = true;
            }
        }

        /// <summary>
        /// Agent that attacks all enemies using all combat-capable units (including archers).
        /// </summary>
        private class AttackAllAgent : IPlanningAgent
        {
            public void InitializeMatch() { }
            public void InitializeRound(IGameState state) { }
            public void Learn(IGameState state) { }

            public void Update(IGameState state, IAgentActions actions)
            {
                int? targetNbr = null;
                foreach (var ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.PAWN,
                                           UnitType.BASE, UnitType.BARRACKS })
                {
                    var enemies = state.GetEnemyUnits(ut);
                    if (enemies.Count > 0) { targetNbr = enemies[0]; break; }
                }
                if (!targetNbr.HasValue) return;

                foreach (var ut in new[] { UnitType.WARRIOR, UnitType.ARCHER })
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

        /// <summary>
        /// Agent that repairs the first damaged friendly building.
        /// </summary>
        private class RepairAgent : IPlanningAgent
        {
            private bool repairing;
            public void InitializeMatch() { repairing = false; }
            public void InitializeRound(IGameState state) { }
            public void Learn(IGameState state) { }

            public void Update(IGameState state, IAgentActions actions)
            {
                if (repairing) return;
                var pawns = state.GetMyUnits(UnitType.PAWN);
                if (pawns.Count == 0) return;

                // Find a damaged building
                foreach (var buildingType in new[] { UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY })
                {
                    foreach (int bNbr in state.GetMyUnits(buildingType))
                    {
                        var info = state.GetUnit(bNbr);
                        if (info.HasValue && info.Value.IsBuilt &&
                            info.Value.Health < GameConstants.HEALTH[buildingType])
                        {
                            actions.Repair(pawns[0], bNbr);
                            repairing = true;
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Agent that builds barracks, then trains warriors — full economy test.
        /// </summary>
        private class EconomyAgent : IPlanningAgent
        {
            private bool gathering;
            private bool building;
            private bool training;

            public void InitializeMatch()
            {
                gathering = false;
                building = false;
                training = false;
            }

            public void InitializeRound(IGameState state) { }
            public void Learn(IGameState state) { }

            public void Update(IGameState state, IAgentActions actions)
            {
                var pawns = state.GetMyUnits(UnitType.PAWN);
                var bases = state.GetMyUnits(UnitType.BASE);
                var mines = state.GetAllUnits(UnitType.MINE);
                var barracks = state.GetMyUnits(UnitType.BARRACKS);

                // Step 1: Start gathering with first pawn
                if (!gathering && pawns.Count > 0 && mines.Count > 0 && bases.Count > 0)
                {
                    var info = state.GetUnit(pawns[0]);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                    {
                        actions.Gather(pawns[0], mines[0], bases[0]);
                        gathering = true;
                    }
                }

                // Step 2: Build barracks with second pawn when we have enough gold
                if (!building && pawns.Count > 1 && state.MyGold >= GameConstants.COST[UnitType.BARRACKS])
                {
                    var info = state.GetUnit(pawns[1]);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                    {
                        actions.Build(pawns[1], new Position(15, 15), UnitType.BARRACKS);
                        building = true;
                    }
                }

                // Step 3: Train warrior from barracks
                if (!training && barracks.Count > 0)
                {
                    var bInfo = state.GetUnit(barracks[0]);
                    if (bInfo.HasValue && bInfo.Value.IsBuilt && bInfo.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
                    {
                        actions.Train(barracks[0], UnitType.WARRIOR);
                        training = true;
                    }
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Central registry of parity test scenarios.
    /// Each method returns a self-contained ParityScenario with factories
    /// that create fresh builders and agents per invocation.
    /// </summary>
    internal static class Scenarios
    {
        /// <summary>
        /// Returns all registered parity scenarios as a flat list.
        /// Used by ParityRunner CLI.
        /// </summary>
        public static List<ParityScenario> AllScenarios()
        {
            return new List<ParityScenario>
            {
                IdleUnits(),
                PawnMovesToTarget(),
                MultiUnitMovement(),
                MovementAroundWall(),
                WarriorVsWarrior(),
                ArcherVsArcher(),
                ArcherVsWarrior(),
                MultiUnitCombat(),
                MutualCombat(),
                GatheringFullCycle(),
                MultiPawnGathering(),
                TrainPawnFromBase(),
                TrainWarriorFromBarracks(),
                BuildBarracks(),
                FullEconomy(),
                RepairBuilding(),
                HeadOnCollision(),
                CorridorCongestion(),
                AttackerBlockedByAlly(),
                TrainLancerFromTower(),
                TrainMonkFromMonastery(),
                LancerVsWarrior(),
                LancerVsArcher(),
                MonkHealsWarrior(),
                MixedArmyCombat(),
                BuildTower(),
                BuildMonastery(),
            };
        }

        public static ParityScenario IdleUnits() => ParityScenario.FromDuration(
            "IdleUnits",
            () => new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(5, 5))
                .WithUnit(1, UnitType.PAWN, new Position(15, 15)),
            () => new DoNothingAgent(),
            () => new DoNothingAgent(),
            5f);

        public static ParityScenario PawnMovesToTarget() => ParityScenario.FromDuration(
            "PawnMovesToTarget",
            () => new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(2, 10)),
            () => new MoveOnceAgent(new Position(18, 10)),
            () => new DoNothingAgent(),
            10f);

        public static ParityScenario MultiUnitMovement() => ParityScenario.FromDuration(
            "MultiUnitMovement",
            () => new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.PAWN, new Position(2, 5))
                .WithUnit(0, UnitType.PAWN, new Position(2, 15))
                .WithUnit(0, UnitType.PAWN, new Position(2, 25)),
            () => new MoveAllPawnsAgent(new Position(28, 15)),
            () => new DoNothingAgent(),
            15f);

        public static ParityScenario MovementAroundWall() => ParityScenario.FromDuration(
            "MovementAroundWall",
            () => new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(5, 10))
                .WithWall(new Position(10, 5), new Position(10, 15)),
            () => new MoveOnceAgent(new Position(15, 10)),
            () => new DoNothingAgent(),
            15f);

        public static ParityScenario WarriorVsWarrior() => ParityScenario.FromDuration(
            "WarriorVsWarrior",
            () => new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(5, 15))
                .WithUnit(1, UnitType.WARRIOR, new Position(25, 15)),
            () => new AttackFirstEnemyAgent(),
            () => new AttackFirstEnemyAgent(),
            15f);

        public static ParityScenario ArcherVsArcher() => ParityScenario.FromDuration(
            "ArcherVsArcher",
            () => new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.ARCHER, new Position(5, 15))
                .WithUnit(1, UnitType.ARCHER, new Position(25, 15)),
            () => new AttackFirstEnemyAgent(),
            () => new AttackFirstEnemyAgent(),
            15f);

        public static ParityScenario ArcherVsWarrior() => ParityScenario.FromDuration(
            "ArcherVsWarrior",
            () => new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.ARCHER, new Position(5, 15))
                .WithUnit(1, UnitType.WARRIOR, new Position(25, 15)),
            () => new AttackFirstEnemyAgent(),
            () => new AttackFirstEnemyAgent(),
            15f);

        public static ParityScenario MultiUnitCombat() => ParityScenario.FromDuration(
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
            20f);

        public static ParityScenario MutualCombat() => ParityScenario.FromDuration(
            "MutualCombat",
            () => new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(10, 15))
                .WithUnit(0, UnitType.ARCHER, new Position(8, 15))
                .WithUnit(1, UnitType.WARRIOR, new Position(20, 15))
                .WithUnit(1, UnitType.ARCHER, new Position(22, 15)),
            () => new AttackFirstEnemyAgent(),
            () => new AttackFirstEnemyAgent(),
            20f);

        public static ParityScenario GatheringFullCycle() => ParityScenario.FromDuration(
            "GatheringFullCycle",
            () => new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 500)
                .WithUnit(0, UnitType.BASE, new Position(3, 8), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(6, 5))
                .WithMine(new Position(20, 8)),
            () => new GatherAgent(),
            () => new DoNothingAgent(),
            25f);

        public static ParityScenario MultiPawnGathering() => ParityScenario.FromDuration(
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
            25f);

        public static ParityScenario TrainPawnFromBase() => ParityScenario.FromDuration(
            "TrainPawnFromBase",
            () => new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithGold(0, 500)
                .WithUnit(0, UnitType.BASE, new Position(5, 10), isBuilt: true),
            () => new TrainOnceAgent(UnitType.PAWN),
            () => new DoNothingAgent(),
            5f);

        public static ParityScenario TrainWarriorFromBarracks() => ParityScenario.FromDuration(
            "TrainWarriorFromBarracks",
            () => new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithGold(0, 500)
                .WithUnit(0, UnitType.BASE, new Position(3, 10), isBuilt: true)
                .WithUnit(0, UnitType.BARRACKS, new Position(10, 10), isBuilt: true),
            () => new TrainFromBarracksAgent(UnitType.WARRIOR),
            () => new DoNothingAgent(),
            5f);

        public static ParityScenario BuildBarracks() => ParityScenario.FromDuration(
            "BuildBarracks",
            () => new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(3, 8), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(6, 5)),
            () => new BuildOnceAgent(UnitType.BARRACKS, new Position(15, 15)),
            () => new DoNothingAgent(),
            25f);

        public static ParityScenario FullEconomy() => ParityScenario.FromDuration(
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
            40f);

        public static ParityScenario RepairBuilding() => ParityScenario.FromDuration(
            "RepairBuilding",
            () => new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithGold(0, 500)
                .WithUnit(0, UnitType.BASE, new Position(5, 10), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 10)),
            () => new RepairDamagedBaseAgent(),
            () => new DoNothingAgent(),
            10f);

        #region Collision avoidance scenarios

        public static ParityScenario HeadOnCollision() => ParityScenario.FromDuration(
            "HeadOnCollision",
            () => new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(2, 10))
                .WithUnit(1, UnitType.PAWN, new Position(18, 10)),
            () => new MoveOnceAgent(new Position(18, 10)),
            () => new MoveOnceAgent(new Position(2, 10)),
            15f);

        public static ParityScenario CorridorCongestion()
        {
            return ParityScenario.FromDuration(
                "CorridorCongestion",
                () =>
                {
                    var b = new SimGameBuilder()
                        .WithMapSize(20, 20)
                        .WithUnit(0, UnitType.PAWN, new Position(2, 10))
                        .WithUnit(0, UnitType.PAWN, new Position(2, 11));
                    // Build walls leaving only row 10 open between x=8..12
                    for (int x = 8; x <= 12; x++)
                    {
                        b.WithWall(new Position(x, 11), new Position(x, 11));
                        b.WithWall(new Position(x, 9), new Position(x, 9));
                    }
                    return b;
                },
                () => new MoveAllPawnsAgent(new Position(18, 10)),
                () => new DoNothingAgent(),
                30f);
        }

        public static ParityScenario AttackerBlockedByAlly() => ParityScenario.FromDuration(
            "AttackerBlockedByAlly",
            () => new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.WARRIOR, new Position(2, 10))
                .WithUnit(0, UnitType.PAWN, new Position(5, 10))
                .WithUnit(1, UnitType.WARRIOR, new Position(18, 10)),
            () => new AttackFirstEnemyAgent(),
            () => new DoNothingAgent(),
            15f);

        #endregion

        #region New unit type scenarios (LANCER, TOWER, MONK, MONASTERY)

        public static ParityScenario TrainLancerFromTower() => ParityScenario.FromDuration(
            "TrainLancerFromTower",
            () => new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithGold(0, 500)
                .WithUnit(0, UnitType.BASE, new Position(3, 10), isBuilt: true)
                .WithUnit(0, UnitType.TOWER, new Position(10, 10), isBuilt: true),
            () => new TrainFromTowerAgent(UnitType.LANCER),
            () => new DoNothingAgent(),
            5f);

        public static ParityScenario TrainMonkFromMonastery() => ParityScenario.FromDuration(
            "TrainMonkFromMonastery",
            () => new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithGold(0, 500)
                .WithUnit(0, UnitType.BASE, new Position(3, 10), isBuilt: true)
                .WithUnit(0, UnitType.MONASTERY, new Position(10, 10), isBuilt: true),
            () => new TrainFromMonasteryAgent(UnitType.MONK),
            () => new DoNothingAgent(),
            5f);

        public static ParityScenario LancerVsWarrior() => ParityScenario.FromDuration(
            "LancerVsWarrior",
            () => new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.LANCER, new Position(5, 15))
                .WithUnit(1, UnitType.WARRIOR, new Position(25, 15)),
            () => new LancerAttackAgent(),
            () => new AttackFirstEnemyAgent(),
            15f);

        public static ParityScenario LancerVsArcher() => ParityScenario.FromDuration(
            "LancerVsArcher",
            () => new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.LANCER, new Position(5, 15))
                .WithUnit(1, UnitType.ARCHER, new Position(25, 15)),
            () => new LancerAttackAgent(),
            () => new AttackFirstEnemyAgent(),
            15f);

        public static ParityScenario MonkHealsWarrior() => ParityScenario.FromDuration(
            "MonkHealsWarrior",
            () => new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.WARRIOR, new Position(10, 10))
                .WithUnit(0, UnitType.MONK, new Position(8, 10))
                .WithUnit(1, UnitType.ARCHER, new Position(18, 10)),
            () => new HealAndFightAgent(),
            () => new AttackFirstEnemyAgent(),
            20f);

        public static ParityScenario MixedArmyCombat() => ParityScenario.FromDuration(
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
            25f);

        public static ParityScenario BuildTower() => ParityScenario.FromDuration(
            "BuildTower",
            () => new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(3, 8), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(6, 5)),
            () => new BuildOnceAgent(UnitType.TOWER, new Position(15, 15)),
            () => new DoNothingAgent(),
            25f);

        public static ParityScenario BuildMonastery() => ParityScenario.FromDuration(
            "BuildMonastery",
            () => new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(3, 8), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(6, 5)),
            () => new BuildOnceAgent(UnitType.MONASTERY, new Position(15, 15)),
            () => new DoNothingAgent(),
            25f);

        #endregion
    }

    #region Scenario-specific test agents

    /// <summary>
    /// Moves a single pawn to a target position once.
    /// </summary>
    internal class MoveOnceAgent : IPlanningAgent
    {
        private readonly Position target;
        private bool moved;

        public MoveOnceAgent(Position target) { this.target = target; }
        public void InitializeMatch() { moved = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (moved) return;
            var pawns = state.GetMyUnits(UnitType.PAWN);
            if (pawns.Count > 0)
            {
                actions.Move(pawns[0], target);
                moved = true;
            }
        }
    }

    /// <summary>
    /// Moves all idle pawns toward the same target position.
    /// </summary>
    internal class MoveAllPawnsAgent : IPlanningAgent
    {
        private readonly Position target;
        private bool moved;

        public MoveAllPawnsAgent(Position target) { this.target = target; }
        public void InitializeMatch() { moved = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (moved) return;
            var pawns = state.GetMyUnits(UnitType.PAWN);
            foreach (int pNbr in pawns)
                actions.Move(pNbr, target);
            moved = true;
        }
    }

    /// <summary>
    /// Economy agent: gathers, builds barracks, trains warrior.
    /// </summary>
    internal class EconomyAgent : IPlanningAgent
    {
        private bool gathering;
        private bool building;
        private bool training;

        public void InitializeMatch()
        {
            gathering = false;
            building = false;
            training = false;
        }

        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var pawns = state.GetMyUnits(UnitType.PAWN);
            var bases = state.GetMyUnits(UnitType.BASE);
            var mines = state.GetAllUnits(UnitType.MINE);
            var barracks = state.GetMyUnits(UnitType.BARRACKS);

            // Step 1: Start gathering with first pawn
            if (!gathering && pawns.Count > 0 && mines.Count > 0 && bases.Count > 0)
            {
                var info = state.GetUnit(pawns[0]);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                {
                    actions.Gather(pawns[0], mines[0], bases[0]);
                    gathering = true;
                }
            }

            // Step 2: Build barracks with second pawn
            if (!building && pawns.Count > 1 && state.MyGold >= GameConstants.COST[UnitType.BARRACKS])
            {
                var info = state.GetUnit(pawns[1]);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                {
                    actions.Build(pawns[1], new Position(15, 15), UnitType.BARRACKS);
                    building = true;
                }
            }

            // Step 3: Train warrior from barracks
            if (!training && barracks.Count > 0)
            {
                var bInfo = state.GetUnit(barracks[0]);
                if (bInfo.HasValue && bInfo.Value.IsBuilt && bInfo.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
                {
                    actions.Train(barracks[0], UnitType.WARRIOR);
                    training = true;
                }
            }
        }
    }

    /// <summary>
    /// Agent that damages a base (via an enemy warrior attack in the scenario setup),
    /// then repairs it. For parity testing, we use a simpler approach: the base starts
    /// at full health and the agent just issues a repair command (which becomes a no-op
    /// if already at full health, testing the repair path).
    /// </summary>
    internal class RepairDamagedBaseAgent : IPlanningAgent
    {
        private bool repairing;
        public void InitializeMatch() { repairing = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (repairing) return;
            var pawns = state.GetMyUnits(UnitType.PAWN);
            var bases = state.GetMyUnits(UnitType.BASE);
            if (pawns.Count > 0 && bases.Count > 0)
            {
                actions.Repair(pawns[0], bases[0]);
                repairing = true;
            }
        }
    }

    /// <summary>
    /// Combined agent: monks heal wounded allies, combat units attack enemies.
    /// </summary>
    internal class HealAndFightAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            // Monks heal the most-wounded friendly unit
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

            // Combat units attack first visible enemy
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
