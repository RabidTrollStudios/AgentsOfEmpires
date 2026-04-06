using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace Opponent.Tests
{
    /// <summary>
    /// Stress tests: full game simulations, many units, long runs.
    /// Verifies the harness doesn't crash or produce inconsistent state.
    /// </summary>
    public class StressTests
    {
        [Fact]
        public void FullGameSimulation_500Frames_NoCrash()
        {
            var agent = new global::PlanningAgent.PlanningAgent();
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithUnit(1, UnitType.BASE, new Position(25, 25), isBuilt: true)
                .WithUnit(1, UnitType.PAWN, new Position(22, 25))
                .WithMine(new Position(15, 10), health: 10000)
                .WithMine(new Position(15, 20), health: 10000)
                .WithAgent(0, agent)
                .WithAgent(1, new DoNothingAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Should complete without exceptions
            game.Run(500);

            // Basic sanity: agent 0 should still have units
            Assert.True(game.GetUnitsByType(0, UnitType.BASE).Count > 0 ||
                         game.GetUnitsByType(0, UnitType.PAWN).Count > 0,
                "Agent 0 should have some units after 500 frames");
        }

        [Fact]
        public void FullGameSimulation_2000Frames_NoCrash()
        {
            var agent = new global::PlanningAgent.PlanningAgent();
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithUnit(1, UnitType.BASE, new Position(25, 25), isBuilt: true)
                .WithUnit(1, UnitType.PAWN, new Position(22, 25))
                .WithMine(new Position(15, 10), health: 10000)
                .WithMine(new Position(15, 20), health: 10000)
                .WithAgent(0, agent)
                .WithAgent(1, new DoNothingAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            // After 2000 frames the agent should have built an army
            int totalUnits = 0;
            foreach (UnitType ut in new[] { UnitType.PAWN, UnitType.WARRIOR, UnitType.ARCHER,
                                            UnitType.BASE, UnitType.BARRACKS })
            {
                totalUnits += game.GetUnitsByType(0, ut).Count;
            }
            Assert.True(totalUnits > 2, $"Agent should have more than 2 units after 2000 frames, got {totalUnits}");
        }

        [Fact]
        public void BothAgentsPlaying_NoCrash()
        {
            var agent0 = new global::PlanningAgent.PlanningAgent();
            var agent1 = new global::PlanningAgent.PlanningAgent();

            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithUnit(1, UnitType.BASE, new Position(25, 25), isBuilt: true)
                .WithUnit(1, UnitType.PAWN, new Position(22, 25))
                .WithMine(new Position(15, 10), health: 10000)
                .WithMine(new Position(15, 20), health: 10000)
                .WithAgent(0, agent0)
                .WithAgent(1, agent1)
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Two real agents playing against each other
            game.Run(1000);

            // Should not crash — that's the main assertion
            Assert.True(true);
        }

        [Fact]
        public void RunUntil_StopsWhenPredicateMet()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainOnceAgent(UnitType.PAWN))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            bool satisfied = game.RunUntil(g => g.GetUnitsByType(0, UnitType.PAWN).Count > 0, maxFrames: 100);

            Assert.True(satisfied, "RunUntil should find the pawn was trained");
            Assert.True(game.CurrentFrame < 100, $"Should stop early, but ran {game.CurrentFrame} frames");
        }

        [Fact]
        public void RunUntil_ReturnsFlase_WhenMaxFramesExceeded()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0) // No gold to train
                .WithUnit(0, UnitType.BASE, new Position(10, 10), isBuilt: true)
                .WithAgent(0, new TrainOnceAgent(UnitType.PAWN))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            bool satisfied = game.RunUntil(g => g.GetUnitsByType(0, UnitType.PAWN).Count > 0, maxFrames: 50);

            Assert.False(satisfied, "Should not satisfy predicate with no gold");
        }

        [Fact]
        public void ManyUnitsOnMap_NoPerformanceIssue()
        {
            var builder = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 50000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true);

            // Place 20 pawns
            for (int i = 0; i < 20; i++)
            {
                int x = 8 + (i % 10);
                int y = 2 + (i / 10) * 2;
                builder.WithUnit(0, UnitType.PAWN, new Position(x, y));
            }

            builder.WithMine(new Position(20, 10), health: 100000);

            var game = builder
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // 20 pawns gathering for frames — should complete quickly
            game.Run(500);

            Assert.True(game.GetGold(0) > 50000,
                $"20 pawns should gather significant gold, got {game.GetGold(0)}");
        }

        [Fact]
        public void RapidUnitDeath_AllCleanedUp()
        {
            var builder = new SimGameBuilder()
                .WithMapSize(30, 30);

            // 15 enemy pawns in a line
            for (int i = 0; i < 15; i++)
                builder.WithUnit(1, UnitType.PAWN, new Position(15, 2 + i));

            // 5 warriors to kill them
            for (int i = 0; i < 5; i++)
                builder.WithUnit(0, UnitType.WARRIOR, new Position(14, 5 + i));

            var game = builder
                .WithAgent(0, new AttackAllEnemiesAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(500);

            // All enemy pawns should be dead
            Assert.Empty(game.GetUnitsByType(1, UnitType.PAWN));

            // All warriors should be idle
            foreach (var s in game.GetUnitsByType(0, UnitType.WARRIOR))
                Assert.Equal(UnitAction.IDLE, s.CurrentAction);
        }
    }
}
