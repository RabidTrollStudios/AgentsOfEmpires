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
        public void Move_PawnReachesTarget()
        {
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(2, 5))
                .WithAgent(0, new MoveToAgent(new Position(10, 5)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            bool arrived = game.RunUntil(g =>
            {
                var pawns = g.GetUnitsByType(0, UnitType.PAWN);
                return pawns.Count > 0 && pawns[0].GridPosition.Equals(new Position(10, 5));
            }, 500);

            Assert.True(arrived, "Pawn should reach target position");
        }

        [Fact]
        public void Move_WarriorReachesTarget()
        {
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.WARRIOR, new Position(2, 5))
                .WithAgent(0, new MoveToAgent(new Position(10, 5)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            bool arrived = game.RunUntil(g =>
            {
                var warriors = g.GetUnitsByType(0, UnitType.WARRIOR);
                return warriors.Count > 0 && warriors[0].GridPosition.Equals(new Position(10, 5));
            }, 500);

            Assert.True(arrived, "Warrior should reach target position");
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
        public void Move_WarriorFasterThanPawn()
        {
            // Pawn speed = 1.0x, Warrior speed = 2.1x
            // Over the same distance, warrior should take fewer ticks
            int distance = 10;
            var target = new Position(2 + distance, 5);

            // Pawn
            var pawnGame = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(2, 5))
                .WithAgent(0, new MoveToAgent(target))
                .Build();

            pawnGame.InitializeMatch();
            pawnGame.InitializeRound();
            pawnGame.RunUntil(g =>
            {
                var pawns = g.GetUnitsByType(0, UnitType.PAWN);
                return pawns.Count > 0 && pawns[0].GridPosition.Equals(target);
            }, 500);
            int pawnTicks = pawnGame.CurrentTick;

            // Warrior
            var warriorGame = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.WARRIOR, new Position(2, 5))
                .WithAgent(0, new MoveToAgent(target))
                .Build();

            warriorGame.InitializeMatch();
            warriorGame.InitializeRound();
            warriorGame.RunUntil(g =>
            {
                var warriors = g.GetUnitsByType(0, UnitType.WARRIOR);
                return warriors.Count > 0 && warriors[0].GridPosition.Equals(target);
            }, 500);
            int warriorTicks = warriorGame.CurrentTick;

            Assert.True(warriorTicks < pawnTicks,
                $"Warrior ({warriorTicks} ticks) should be faster than pawn ({pawnTicks} ticks)");
        }

        [Fact]
        public void Move_ArcherFasterThanPawn()
        {
            // Pawn speed = 1.0x, Archer speed = 3.0x
            int distance = 10;
            var target = new Position(2 + distance, 5);

            // Pawn
            var pawnGame = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(2, 5))
                .WithAgent(0, new MoveToAgent(target))
                .Build();

            pawnGame.InitializeMatch();
            pawnGame.InitializeRound();
            pawnGame.RunUntil(g =>
            {
                var pawns = g.GetUnitsByType(0, UnitType.PAWN);
                return pawns.Count > 0 && pawns[0].GridPosition.Equals(target);
            }, 500);
            int pawnTicks = pawnGame.CurrentTick;

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

            Assert.True(archerTicks < pawnTicks,
                $"Archer ({archerTicks} ticks) should be faster than pawn ({pawnTicks} ticks)");
        }

        // ------------------------------------------------------------------
        // Pathfinding around obstacles
        // ------------------------------------------------------------------

        [Fact]
        public void Move_AroundWall_ReachesTarget()
        {
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(5, 3))
                .WithWall(new Position(0, 5), new Position(7, 5))
                .WithAgent(0, new MoveToAgent(new Position(5, 7)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            bool arrived = game.RunUntil(g =>
            {
                var pawns = g.GetUnitsByType(0, UnitType.PAWN);
                return pawns.Count > 0 && pawns[0].GridPosition.Equals(new Position(5, 7));
            }, 500);

            Assert.True(arrived, "Pawn should pathfind around wall to reach target");
        }

        [Fact]
        public void Move_BlockedTarget_UnitStaysIdle()
        {
            var game = new SimGameBuilder()
                .WithMapSize(10, 10)
                .WithUnit(0, UnitType.PAWN, new Position(2, 2))
                .WithAgent(0, new MoveToBlockedAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Block the target cell
            game.Map.SetCellBlocked(new Position(5, 5));
            game.Run(50);

            // Pawn should still be alive, just unable to reach destination
            Assert.Single(game.GetUnitsByType(0, UnitType.PAWN));
        }

        // ------------------------------------------------------------------
        // Move completion
        // ------------------------------------------------------------------

        [Fact]
        public void Move_UnitGoesIdleAfterArrival()
        {
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(2, 5))
                .WithAgent(0, new MoveOnceAgent(new Position(5, 5)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(200);

            var pawns = game.GetUnitsByType(0, UnitType.PAWN);
            Assert.Single(pawns);
            Assert.Equal(new Position(5, 5), pawns[0].GridPosition);
            Assert.Equal(UnitAction.IDLE, pawns[0].CurrentAction);
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
                foreach (UnitType ut in new[] { UnitType.PAWN, UnitType.WARRIOR, UnitType.ARCHER })
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
                var pawns = state.GetMyUnits(UnitType.PAWN);
                if (pawns.Count > 0)
                {
                    actions.Move(pawns[0], _target);
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
                var pawns = state.GetMyUnits(UnitType.PAWN);
                if (pawns.Count > 0)
                {
                    actions.Move(pawns[0], new Position(5, 5));
                    _tried = true;
                }
            }
        }
    }
}
