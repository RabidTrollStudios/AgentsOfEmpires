using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace Gameplay.Tests
{
    /// <summary>
    /// Tests for command validation: invalid targets, wrong unit types,
    /// and agent error resilience.
    /// </summary>
    public class CommandValidationTests
    {
        // ------------------------------------------------------------------
        // Invalid target
        // ------------------------------------------------------------------

        [Fact]
        public void AttackNonExistentUnit_NoCrash()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(5, 5))
                .WithAgent(0, new AttackUnitNbrAgent(9999))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            // Should not crash, warrior should still be idle
            var warriors = game.GetUnitsByType(0, UnitType.WARRIOR);
            Assert.Single(warriors);
            Assert.Equal(UnitAction.IDLE, warriors[0].CurrentAction);
        }

        [Fact]
        public void GatherFromDestroyedMine_NoCrash()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(8, 5))
                .WithMine(new Position(12, 5), health: 1) // Will deplete almost immediately
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            // Pawn should survive even after mine is destroyed
            Assert.Single(game.GetUnitsByType(0, UnitType.PAWN));
        }

        // ------------------------------------------------------------------
        // Wrong unit type for command
        // ------------------------------------------------------------------

        [Fact]
        public void TrainFromBarracks_PawnType_Rejected()
        {
            // Barracks can't train pawns — only BASE can
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BARRACKS, new Position(5, 5), isBuilt: true)
                .WithAgent(0, new TrainFromBarracksAgent(UnitType.PAWN))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(500);

            // Should not have trained any pawns from barracks
            Assert.Empty(game.GetUnitsByType(0, UnitType.PAWN));
        }

        [Fact]
        public void AttackWithPawn_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.PAWN, new Position(5, 5))
                .WithUnit(1, UnitType.PAWN, new Position(7, 5))
                .WithAgent(0, new PawnAttackAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(200);

            // Enemy pawn should be unharmed (pawns can't attack)
            var enemyPawns = game.GetUnitsByType(1, UnitType.PAWN);
            Assert.Single(enemyPawns);
            Assert.Equal(GameConstants.HEALTH[UnitType.PAWN], enemyPawns[0].Health);
        }

        // ------------------------------------------------------------------
        // Friendly fire
        // ------------------------------------------------------------------

        [Fact]
        public void FriendlyFire_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(5, 5))
                .WithUnit(0, UnitType.PAWN, new Position(7, 5))
                .WithAgent(0, new AttackOwnPawnAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(200);

            // Own pawn should be unharmed
            var pawns = game.GetUnitsByType(0, UnitType.PAWN);
            Assert.Single(pawns);
            Assert.Equal(GameConstants.HEALTH[UnitType.PAWN], pawns[0].Health);
        }

        // ------------------------------------------------------------------
        // Duplicate commands
        // ------------------------------------------------------------------

        [Fact]
        public void DoubleTrainSameTick_OnlyOneSucceeds()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 200) // Enough for 4 pawns (50 each)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithAgent(0, new SpamTrainAgent()) // Sends train every tick
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Run 2 ticks: tick 1 = agent queues TRAIN, tick 2 = command dispatched
            game.Run(2);

            // Base should now be training (not idle)
            var bases = game.GetUnitsByType(0, UnitType.BASE);
            Assert.Single(bases);
            Assert.Equal(UnitAction.TRAIN, bases[0].CurrentAction);

            // Gold should have been deducted exactly once
            int pawnCost = (int)GameConstants.COST[UnitType.PAWN];
            Assert.Equal(200 - pawnCost, game.GetGold(0));
        }

        // ------------------------------------------------------------------
        // Agent exception resilience
        // ------------------------------------------------------------------

        [Fact]
        public void NullAgent_NoCrash()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                // No agents set — defaults to DoNothingAgent
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            // Should complete without crashing
            Assert.Equal(100, game.CurrentTick);
        }
    }

    /// <summary>
    /// Test agent that attacks a specific unit number (may not exist).
    /// </summary>
    internal class AttackUnitNbrAgent : IPlanningAgent
    {
        private readonly int targetNbr;
        private bool tried;

        public AttackUnitNbrAgent(int targetNbr) { this.targetNbr = targetNbr; }
        public void InitializeMatch() { tried = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (tried) return;
            var warriors = state.GetMyUnits(UnitType.WARRIOR);
            if (warriors.Count > 0)
            {
                actions.Attack(warriors[0], targetNbr);
                tried = true;
            }
        }
    }
}
