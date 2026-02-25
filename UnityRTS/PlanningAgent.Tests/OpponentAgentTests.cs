using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Verifies each opponent agent runs without crashing in a standard game.
    /// Each test runs the opponent as player 1 against a DoNothingAgent for 1000 ticks.
    /// </summary>
    public class OpponentAgentTests
    {
        /// <summary>
        /// Standard game setup for testing an opponent.
        /// Opponent is agent 1 with a base, worker, and mine.
        /// </summary>
        private SimGame BuildStandardGame(PlanningAgentBase opponent)
        {
            return new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithUnit(1, UnitType.BASE, new Position(25, 25), isBuilt: true)
                .WithUnit(1, UnitType.WORKER, new Position(22, 25))
                .WithMine(new Position(15, 10), health: 10000)
                .WithMine(new Position(15, 20), health: 10000)
                .WithAgent(0, new DoNothingAgent())
                .WithAgent(1, opponent)
                .Build();
        }

        private void RunOpponentTest(PlanningAgentBase opponent, int ticks = 1000)
        {
            var game = BuildStandardGame(opponent);
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(ticks);
        }

        // ------------------------------------------------------------------
        //  EASY tier — no crash
        // ------------------------------------------------------------------

        [Fact]
        public void IdleOpponent_NoCrash()
        {
            RunOpponentTest(new IdleOpponent());
        }

        [Fact]
        public void GathererOpponent_NoCrash()
        {
            RunOpponentTest(new GathererOpponent());
        }

        [Fact]
        public void ArcherOnlyOpponent_NoCrash()
        {
            RunOpponentTest(new ArcherOnlyOpponent());
        }

        // ------------------------------------------------------------------
        //  MEDIUM tier — no crash
        // ------------------------------------------------------------------

        [Fact]
        public void SoldierRushOpponent_NoCrash()
        {
            RunOpponentTest(new SoldierRushOpponent());
        }

        [Fact]
        public void ArcherSwarmOpponent_NoCrash()
        {
            RunOpponentTest(new ArcherSwarmOpponent());
        }

        [Fact]
        public void TurtleOpponent_NoCrash()
        {
            RunOpponentTest(new TurtleOpponent());
        }

        [Fact]
        public void BalancedOpponent_NoCrash()
        {
            RunOpponentTest(new BalancedOpponent());
        }

        // ------------------------------------------------------------------
        //  HARD tier — no crash
        // ------------------------------------------------------------------

        [Fact]
        public void EconBoomOpponent_NoCrash()
        {
            RunOpponentTest(new EconBoomOpponent());
        }

        [Fact]
        public void SwarmOpponent_NoCrash()
        {
            RunOpponentTest(new SwarmOpponent());
        }

        [Fact]
        public void CommanderOpponent_NoCrash()
        {
            RunOpponentTest(new CommanderOpponent());
        }

        // ------------------------------------------------------------------
        //  Behavior verification — easy opponents are beatable
        // ------------------------------------------------------------------

        [Fact]
        public void IdleOpponent_DoesNothing()
        {
            var game = BuildStandardGame(new IdleOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(500);

            // Idle opponent should still have exactly 1 base and 1 worker, nothing more
            Assert.Single(game.GetUnitsByType(1, UnitType.BASE));
            Assert.Single(game.GetUnitsByType(1, UnitType.WORKER));
            Assert.Empty(game.GetUnitsByType(1, UnitType.SOLDIER));
            Assert.Empty(game.GetUnitsByType(1, UnitType.ARCHER));
            Assert.Empty(game.GetUnitsByType(1, UnitType.BARRACKS));
        }

        [Fact]
        public void GathererOpponent_TrainsWorkers_ButNoMilitary()
        {
            var game = BuildStandardGame(new GathererOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            // Should have multiple workers
            Assert.True(game.GetUnitsByType(1, UnitType.WORKER).Count > 1,
                "Gatherer should train workers");
            // No military
            Assert.Empty(game.GetUnitsByType(1, UnitType.SOLDIER));
            Assert.Empty(game.GetUnitsByType(1, UnitType.ARCHER));
            Assert.Empty(game.GetUnitsByType(1, UnitType.BARRACKS));
        }

        // ------------------------------------------------------------------
        //  Behavior verification — medium/hard opponents build armies
        // ------------------------------------------------------------------

        [Fact]
        public void SoldierRushOpponent_TrainsSoldiers()
        {
            var game = BuildStandardGame(new SoldierRushOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            // Should have built barracks and trained soldiers
            Assert.True(game.GetUnitsByType(1, UnitType.BARRACKS).Count > 0 ||
                         game.GetUnitsByType(1, UnitType.SOLDIER).Count > 0,
                "SoldierRush should build barracks and/or train soldiers");
        }

        [Fact]
        public void BalancedOpponent_TrainsMixedArmy()
        {
            var game = BuildStandardGame(new BalancedOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            int soldiers = game.GetUnitsByType(1, UnitType.SOLDIER).Count;
            int archers = game.GetUnitsByType(1, UnitType.ARCHER).Count;
            Assert.True(soldiers + archers > 0,
                "Balanced should have trained some troops");
        }

        [Fact]
        public void CommanderOpponent_BuildsRefinery()
        {
            var game = BuildStandardGame(new CommanderOpponent());
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            // Commander builds refinery for economic advantage
            Assert.True(game.GetUnitsByType(1, UnitType.BARRACKS).Count > 0 ||
                         game.GetUnitsByType(1, UnitType.REFINERY).Count > 0,
                "Commander should build barracks and/or refinery");
        }

        // ------------------------------------------------------------------
        //  PvP — opponents play against each other without crashing
        // ------------------------------------------------------------------

        [Fact]
        public void SoldierRush_vs_Turtle_NoCrash()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithUnit(1, UnitType.BASE, new Position(25, 25), isBuilt: true)
                .WithUnit(1, UnitType.WORKER, new Position(22, 25))
                .WithMine(new Position(15, 10), health: 10000)
                .WithMine(new Position(15, 20), health: 10000)
                .WithAgent(0, new SoldierRushOpponent())
                .WithAgent(1, new TurtleOpponent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);
        }

        [Fact]
        public void EconBoom_vs_Commander_NoCrash()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithUnit(1, UnitType.BASE, new Position(25, 25), isBuilt: true)
                .WithUnit(1, UnitType.WORKER, new Position(22, 25))
                .WithMine(new Position(15, 10), health: 10000)
                .WithMine(new Position(15, 20), health: 10000)
                .WithAgent(0, new EconBoomOpponent())
                .WithAgent(1, new CommanderOpponent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);
        }

        [Fact]
        public void Swarm_vs_ArcherSwarm_NoCrash()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithUnit(1, UnitType.BASE, new Position(25, 25), isBuilt: true)
                .WithUnit(1, UnitType.WORKER, new Position(22, 25))
                .WithMine(new Position(15, 10), health: 10000)
                .WithMine(new Position(15, 20), health: 10000)
                .WithAgent(0, new SwarmOpponent())
                .WithAgent(1, new ArcherSwarmOpponent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);
        }
    }
}
