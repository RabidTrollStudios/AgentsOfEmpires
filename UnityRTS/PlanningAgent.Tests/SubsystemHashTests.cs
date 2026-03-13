using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for per-subsystem hashing and state snapshot diffs.
    /// </summary>
    public class SubsystemHashTests
    {
        [Fact]
        public void SubsystemHash_IdenticalGames_AllMatch()
        {
            var game1 = BuildIdleGame();
            var game2 = BuildIdleGame();

            game1.Tick();
            game2.Tick();

            var h1 = game1.GetSubsystemHash();
            var h2 = game2.GetSubsystemHash();

            Assert.Equal(h1.Global, h2.Global);
            Assert.Equal(h1.UnitPositions, h2.UnitPositions);
            Assert.Equal(h1.UnitHealth, h2.UnitHealth);
            Assert.Equal(h1.UnitActions, h2.UnitActions);
            Assert.Equal(h1.UnitTimers, h2.UnitTimers);
            Assert.Equal(h1.Combined, h2.Combined);
        }

        [Fact]
        public void SubsystemHash_DifferentGold_OnlyGlobalDiffers()
        {
            var game1 = new SimGameBuilder()
                .WithMapSize(10, 10)
                .WithGold(0, 100)
                .WithUnit(0, UnitType.PAWN, new Position(5, 5))
                .WithAgent(0, new DoNothingAgent())
                .Build();

            var game2 = new SimGameBuilder()
                .WithMapSize(10, 10)
                .WithGold(0, 999)
                .WithUnit(0, UnitType.PAWN, new Position(5, 5))
                .WithAgent(0, new DoNothingAgent())
                .Build();

            game1.InitializeMatch(); game1.InitializeRound(); game1.Tick();
            game2.InitializeMatch(); game2.InitializeRound(); game2.Tick();

            var h1 = game1.GetSubsystemHash();
            var h2 = game2.GetSubsystemHash();

            Assert.NotEqual(h1.Global, h2.Global);
            Assert.Equal(h1.UnitPositions, h2.UnitPositions);
            Assert.Equal(h1.UnitHealth, h2.UnitHealth);
            Assert.Equal(h1.UnitActions, h2.UnitActions);
            Assert.Equal(h1.UnitTimers, h2.UnitTimers);
        }

        [Fact]
        public void SubsystemHash_DifferentPositions_PositionsDiffer()
        {
            var game1 = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(5, 5))
                .WithAgent(0, new DoNothingAgent())
                .Build();

            var game2 = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(10, 10))
                .WithAgent(0, new DoNothingAgent())
                .Build();

            game1.InitializeMatch(); game1.InitializeRound(); game1.Tick();
            game2.InitializeMatch(); game2.InitializeRound(); game2.Tick();

            var h1 = game1.GetSubsystemHash();
            var h2 = game2.GetSubsystemHash();

            Assert.NotEqual(h1.UnitPositions, h2.UnitPositions);
            Assert.Equal(h1.Global, h2.Global);
        }

        [Fact]
        public void SubsystemHash_DiffString_ShowsDivergentSubsystems()
        {
            var h1 = new SubsystemHash { Global = 100, UnitPositions = 200, UnitHealth = 300, UnitActions = 400, UnitTimers = 500 };
            var h2 = new SubsystemHash { Global = 100, UnitPositions = 999, UnitHealth = 300, UnitActions = 400, UnitTimers = 500 };

            var diff = SubsystemHash.Diff(h1, h2);
            Assert.Contains("UnitPositions", diff);
            Assert.DoesNotContain("Global", diff);
            Assert.DoesNotContain("UnitHealth", diff);
        }

        [Fact]
        public void SubsystemHash_DiffString_NoDifferences()
        {
            var h = new SubsystemHash { Global = 1, UnitPositions = 2, UnitHealth = 3, UnitActions = 4, UnitTimers = 5 };
            Assert.Equal("no differences", SubsystemHash.Diff(h, h));
        }

        [Fact]
        public void Snapshot_Capture_MatchesGameState()
        {
            var game = BuildIdleGame();
            game.Tick();

            var snap = StateSnapshot.Capture(game);

            Assert.Equal(game.CurrentTick, snap.CurrentTick);
            Assert.Equal(game.GetGold(0), snap.Gold0);
            Assert.Equal(game.GetGold(1), snap.Gold1);
            // Verify snapshot captured the unit
            Assert.True(snap.Units.Count > 0);
        }

        [Fact]
        public void Snapshot_Diff_IdenticalSnapshots_EmptyDiff()
        {
            var game = BuildIdleGame();
            game.Tick();

            var snap1 = StateSnapshot.Capture(game);
            var snap2 = StateSnapshot.Capture(game);

            var diff = StateSnapshot.Diff(snap1, snap2);
            Assert.Equal("", diff);
        }

        [Fact]
        public void Snapshot_Diff_DifferentTicks_ShowsChange()
        {
            var game = BuildIdleGame();
            game.Tick();
            var snap1 = StateSnapshot.Capture(game);

            game.Tick();
            var snap2 = StateSnapshot.Capture(game);

            var diff = StateSnapshot.Diff(snap1, snap2);
            Assert.Contains("CurrentTick", diff);
        }

        [Fact]
        public void Snapshot_Diff_MovedUnit_ShowsPositionChange()
        {
            var game = new SimGameBuilder()
                .WithMapSize(20, 20)
                .WithUnit(0, UnitType.PAWN, new Position(2, 10))
                .WithAgent(0, new MoveOnceAgent(new Position(18, 10)))
                .Build();
            game.InitializeMatch();
            game.InitializeRound();

            game.Tick();
            var snap1 = StateSnapshot.Capture(game);

            // Run enough ticks for the pawn to move
            for (int i = 0; i < 50; i++) game.Tick();
            var snap2 = StateSnapshot.Capture(game);

            var diff = StateSnapshot.Diff(snap1, snap2);
            Assert.Contains("CurrentTick", diff);
            Assert.Contains("GridPosition", diff);
        }

        [Fact]
        public void Snapshot_Diff_UnitDestroyed_ShowsPresenceChange()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(14, 15))
                .WithUnit(1, UnitType.WARRIOR, new Position(16, 15))
                .WithAgent(0, new AttackFirstEnemyAgent())
                .WithAgent(1, new DoNothingAgent())
                .Build();
            game.InitializeMatch();
            game.InitializeRound();

            game.Tick();
            var snap1 = StateSnapshot.Capture(game);

            // Run until one warrior dies
            for (int i = 0; i < 300; i++) game.Tick();
            var snap2 = StateSnapshot.Capture(game);

            var diff = StateSnapshot.Diff(snap1, snap2);
            // Should show a unit present in one snapshot but not the other
            Assert.Contains("present in A only", diff);
        }

        private SimGame BuildIdleGame()
        {
            var game = new SimGameBuilder()
                .WithMapSize(10, 10)
                .WithUnit(0, UnitType.PAWN, new Position(5, 5))
                .WithAgent(0, new DoNothingAgent())
                .Build();
            game.InitializeMatch();
            game.InitializeRound();
            return game;
        }
    }
}
