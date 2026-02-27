using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
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
                .WithUnit(0, UnitType.SOLDIER, new Position(5, 5))
                .WithAgent(0, new AttackUnitNbrAgent(9999))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            // Should not crash, soldier should still be idle
            var soldiers = game.GetUnitsByType(0, UnitType.SOLDIER);
            Assert.Single(soldiers);
            Assert.Equal(UnitAction.IDLE, soldiers[0].CurrentAction);
        }

        [Fact]
        public void GatherFromDestroyedMine_NoCrash()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithMine(new Position(12, 5), health: 1) // Will deplete almost immediately
                .WithAgent(0, new GatherAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(2000);

            // Worker should survive even after mine is destroyed
            Assert.Single(game.GetUnitsByType(0, UnitType.WORKER));
        }

        // ------------------------------------------------------------------
        // Wrong unit type for command
        // ------------------------------------------------------------------

        [Fact]
        public void TrainFromBarracks_WorkerType_Rejected()
        {
            // Barracks can't train workers — only BASE can
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BARRACKS, new Position(5, 5), isBuilt: true)
                .WithAgent(0, new TrainFromBarracksAgent(UnitType.WORKER))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(500);

            // Should not have trained any workers from barracks
            Assert.Empty(game.GetUnitsByType(0, UnitType.WORKER));
        }

        [Fact]
        public void AttackWithWorker_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WORKER, new Position(5, 5))
                .WithUnit(1, UnitType.WORKER, new Position(7, 5))
                .WithAgent(0, new WorkerAttackAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(200);

            // Enemy worker should be unharmed (workers can't attack)
            var enemyWorkers = game.GetUnitsByType(1, UnitType.WORKER);
            Assert.Single(enemyWorkers);
            Assert.Equal(GameConstants.HEALTH[UnitType.WORKER], enemyWorkers[0].Health);
        }

        // ------------------------------------------------------------------
        // Friendly fire
        // ------------------------------------------------------------------

        [Fact]
        public void FriendlyFire_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.SOLDIER, new Position(5, 5))
                .WithUnit(0, UnitType.WORKER, new Position(7, 5))
                .WithAgent(0, new AttackOwnWorkerAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(200);

            // Own worker should be unharmed
            var workers = game.GetUnitsByType(0, UnitType.WORKER);
            Assert.Single(workers);
            Assert.Equal(GameConstants.HEALTH[UnitType.WORKER], workers[0].Health);
        }

        // ------------------------------------------------------------------
        // Duplicate commands
        // ------------------------------------------------------------------

        [Fact]
        public void DoubleTrainSameTick_OnlyOneSucceeds()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 200) // Enough for 4 workers (50 each)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithAgent(0, new SpamTrainAgent()) // Sends train every tick
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Run just 1 tick — spam agent sends train, but base can only process one
            game.Run(1);

            // Base should now be training (not idle)
            var bases = game.GetUnitsByType(0, UnitType.BASE);
            Assert.Single(bases);
            Assert.Equal(UnitAction.TRAIN, bases[0].CurrentAction);

            // Gold should have been deducted exactly once
            int workerCost = (int)GameConstants.COST[UnitType.WORKER];
            Assert.Equal(200 - workerCost, game.GetGold(0));
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
            var soldiers = state.GetMyUnits(UnitType.SOLDIER);
            if (soldiers.Count > 0)
            {
                actions.Attack(soldiers[0], targetNbr);
                tried = true;
            }
        }
    }
}
