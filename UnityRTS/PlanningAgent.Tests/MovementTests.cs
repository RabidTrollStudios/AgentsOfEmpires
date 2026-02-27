using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for unit movement system: Move command, speed differences,
    /// pathfinding integration, and edge cases.
    /// </summary>
    public class MovementTests
    {
        // ------------------------------------------------------------------
        // Basic movement
        // ------------------------------------------------------------------

        [Fact]
        public void Move_WorkerReachesTarget()
        {
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.WORKER, new Position(2, 5))
                .WithAgent(0, new MoveToAgent(new Position(10, 5)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            bool arrived = game.RunUntil(g =>
            {
                var workers = g.GetUnitsByType(0, UnitType.WORKER);
                return workers.Count > 0 && workers[0].GridPosition.Equals(new Position(10, 5));
            }, 500);

            Assert.True(arrived, "Worker should reach target position");
        }

        [Fact]
        public void Move_SoldierReachesTarget()
        {
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.SOLDIER, new Position(2, 5))
                .WithAgent(0, new MoveToAgent(new Position(10, 5)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            bool arrived = game.RunUntil(g =>
            {
                var soldiers = g.GetUnitsByType(0, UnitType.SOLDIER);
                return soldiers.Count > 0 && soldiers[0].GridPosition.Equals(new Position(10, 5));
            }, 500);

            Assert.True(arrived, "Soldier should reach target position");
        }

        [Fact]
        public void Move_ArcherReachesTarget()
        {
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.ARCHER, new Position(2, 5))
                .WithAgent(0, new MoveToAgent(new Position(10, 5)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            bool arrived = game.RunUntil(g =>
            {
                var archers = g.GetUnitsByType(0, UnitType.ARCHER);
                return archers.Count > 0 && archers[0].GridPosition.Equals(new Position(10, 5));
            }, 500);

            Assert.True(arrived, "Archer should reach target position");
        }

        // ------------------------------------------------------------------
        // Speed differences
        // ------------------------------------------------------------------

        [Fact]
        public void Move_SoldierSlowerThanWorker()
        {
            // Worker speed = 1.0, Soldier speed = 0.75
            // Over the same distance, soldier should take more ticks
            int distance = 10;
            var target = new Position(2 + distance, 5);

            // Worker
            var workerGame = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.WORKER, new Position(2, 5))
                .WithAgent(0, new MoveToAgent(target))
                .Build();

            workerGame.InitializeMatch();
            workerGame.InitializeRound();
            workerGame.RunUntil(g =>
            {
                var workers = g.GetUnitsByType(0, UnitType.WORKER);
                return workers.Count > 0 && workers[0].GridPosition.Equals(target);
            }, 500);
            int workerTicks = workerGame.CurrentTick;

            // Soldier
            var soldierGame = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.SOLDIER, new Position(2, 5))
                .WithAgent(0, new MoveToAgent(target))
                .Build();

            soldierGame.InitializeMatch();
            soldierGame.InitializeRound();
            soldierGame.RunUntil(g =>
            {
                var soldiers = g.GetUnitsByType(0, UnitType.SOLDIER);
                return soldiers.Count > 0 && soldiers[0].GridPosition.Equals(target);
            }, 500);
            int soldierTicks = soldierGame.CurrentTick;

            Assert.True(soldierTicks > workerTicks,
                $"Soldier ({soldierTicks} ticks) should be slower than worker ({workerTicks} ticks)");
        }

        [Fact]
        public void Move_WorkerAndArcherSameSpeed()
        {
            // Worker speed = 1.0, Archer speed = 1.0
            int distance = 10;
            var target = new Position(2 + distance, 5);

            // Worker
            var workerGame = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.WORKER, new Position(2, 5))
                .WithAgent(0, new MoveToAgent(target))
                .Build();

            workerGame.InitializeMatch();
            workerGame.InitializeRound();
            workerGame.RunUntil(g =>
            {
                var workers = g.GetUnitsByType(0, UnitType.WORKER);
                return workers.Count > 0 && workers[0].GridPosition.Equals(target);
            }, 500);
            int workerTicks = workerGame.CurrentTick;

            // Archer
            var archerGame = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.ARCHER, new Position(2, 5))
                .WithAgent(0, new MoveToAgent(target))
                .Build();

            archerGame.InitializeMatch();
            archerGame.InitializeRound();
            archerGame.RunUntil(g =>
            {
                var archers = g.GetUnitsByType(0, UnitType.ARCHER);
                return archers.Count > 0 && archers[0].GridPosition.Equals(target);
            }, 500);
            int archerTicks = archerGame.CurrentTick;

            Assert.Equal(workerTicks, archerTicks);
        }

        // ------------------------------------------------------------------
        // Pathfinding around obstacles
        // ------------------------------------------------------------------

        [Fact]
        public void Move_AroundWall_ReachesTarget()
        {
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.WORKER, new Position(5, 3))
                .WithWall(new Position(0, 5), new Position(7, 5))
                .WithAgent(0, new MoveToAgent(new Position(5, 7)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            bool arrived = game.RunUntil(g =>
            {
                var workers = g.GetUnitsByType(0, UnitType.WORKER);
                return workers.Count > 0 && workers[0].GridPosition.Equals(new Position(5, 7));
            }, 500);

            Assert.True(arrived, "Worker should pathfind around wall to reach target");
        }

        [Fact]
        public void Move_BlockedTarget_UnitStaysIdle()
        {
            var game = new SimGameBuilder()
                .WithMapSize(10, 10)
                .WithUnit(0, UnitType.WORKER, new Position(2, 2))
                .WithAgent(0, new MoveToBlockedAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Block the target cell
            game.Map.SetCellBlocked(new Position(5, 5));
            game.Run(50);

            // Worker should still be alive, just unable to reach destination
            Assert.Single(game.GetUnitsByType(0, UnitType.WORKER));
        }

        // ------------------------------------------------------------------
        // Move completion
        // ------------------------------------------------------------------

        [Fact]
        public void Move_UnitGoesIdleAfterArrival()
        {
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.WORKER, new Position(2, 5))
                .WithAgent(0, new MoveOnceAgent(new Position(5, 5)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(200);

            var workers = game.GetUnitsByType(0, UnitType.WORKER);
            Assert.Single(workers);
            Assert.Equal(new Position(5, 5), workers[0].GridPosition);
            Assert.Equal(UnitAction.IDLE, workers[0].CurrentAction);
        }

        // ------------------------------------------------------------------
        // Test agents for movement
        // ------------------------------------------------------------------

        private class MoveToAgent : IPlanningAgent
        {
            private readonly Position _target;
            public MoveToAgent(Position target) { _target = target; }
            public void InitializeMatch() { }
            public void InitializeRound(IGameState state) { }
            public void Learn(IGameState state) { }

            public void Update(IGameState state, IAgentActions actions)
            {
                foreach (UnitType ut in new[] { UnitType.WORKER, UnitType.SOLDIER, UnitType.ARCHER })
                {
                    foreach (int unitNbr in state.GetMyUnits(ut))
                    {
                        var info = state.GetUnit(unitNbr);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                            && !info.Value.GridPosition.Equals(_target))
                        {
                            actions.Move(unitNbr, _target);
                        }
                    }
                }
            }
        }

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
                var workers = state.GetMyUnits(UnitType.WORKER);
                if (workers.Count > 0)
                {
                    actions.Move(workers[0], _target);
                    _moved = true;
                }
            }
        }

        private class MoveToBlockedAgent : IPlanningAgent
        {
            private bool _tried;
            public void InitializeMatch() { _tried = false; }
            public void InitializeRound(IGameState state) { }
            public void Learn(IGameState state) { }

            public void Update(IGameState state, IAgentActions actions)
            {
                if (_tried) return;
                var workers = state.GetMyUnits(UnitType.WORKER);
                if (workers.Count > 0)
                {
                    actions.Move(workers[0], new Position(5, 5));
                    _tried = true;
                }
            }
        }
    }
}
