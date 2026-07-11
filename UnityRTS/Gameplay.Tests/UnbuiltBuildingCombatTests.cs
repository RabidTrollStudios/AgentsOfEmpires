using System.Linq;
using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace Gameplay.Tests
{
    /// <summary>
    /// Headless tests for attacking under-construction buildings (U2).
    /// Damage to an unbuilt building drains its BuildProgress instead of Health;
    /// the building dies when progress is depleted. Health stays full until then.
    /// </summary>
    public class UnbuiltBuildingCombatTests
    {
        /// <summary>WARRIOR that attacks the first enemy BARRACKS it can see, once.</summary>
        private sealed class AttackEnemyBarracksAgent : IPlanningAgent
        {
            private bool done;
            public void InitializeMatch() { done = false; }
            public void InitializeRound(IGameState state) { }
            public void Learn(IGameState state) { }
            public void Update(IGameState state, IAgentActions actions)
            {
                if (done) return;
                var warriors = state.GetMyUnits(UnitType.WARRIOR);
                var targets = state.GetEnemyUnits(UnitType.BARRACKS);
                if (warriors.Count > 0 && targets.Count > 0)
                {
                    actions.Attack(warriors[0], targets[0]);
                    done = true;
                }
            }
        }

        [Fact]
        public void AttackingUnbuiltBuilding_DrainsProgress_NotHealth()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(10, 10))
                // Enemy unbuilt BARRACKS (3x3) adjacent to the warrior
                .WithUnit(1, UnitType.BARRACKS, new Position(11, 11), isBuilt: false)
                .WithAgent(0, new AttackEnemyBarracksAgent())
                .WithAgent(1, new DoNothingAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            var barracks = game.GetUnitsByType(1, UnitType.BARRACKS).Single();
            // Seed plenty of progress so it survives several hits (absolute value
            // is irrelevant — we only assert the direction of change).
            barracks.BuildProgress = 100f;
            float fullHealth = barracks.Health;
            float progressBefore = barracks.BuildProgress;

            // Run a few ticks of attacking.
            for (int i = 0; i < 15 && game.GetUnitsByType(1, UnitType.BARRACKS).Count > 0; i++)
                game.Tick();

            var still = game.GetUnitsByType(1, UnitType.BARRACKS).FirstOrDefault();
            if (still != null)
            {
                Assert.True(still.BuildProgress < progressBefore,
                    $"Build progress should drain under attack (was {progressBefore:F2}, now {still.BuildProgress:F2})");
                Assert.Equal(fullHealth, still.Health); // health untouched while unbuilt
            }
            // else it already died — also valid (progress depleted)
        }

        [Fact]
        public void UnbuiltBuilding_Dies_WhenProgressDepleted()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(10, 10))
                .WithUnit(1, UnitType.BARRACKS, new Position(11, 11), isBuilt: false)
                .WithAgent(0, new AttackEnemyBarracksAgent())
                .WithAgent(1, new DoNothingAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Tiny remaining progress → should die quickly under attack.
            var barracks = game.GetUnitsByType(1, UnitType.BARRACKS).Single();
            barracks.BuildProgress = 0.01f;

            for (int i = 0; i < 100 && game.GetUnitsByType(1, UnitType.BARRACKS).Count > 0; i++)
                game.Tick();

            Assert.Empty(game.GetUnitsByType(1, UnitType.BARRACKS));
        }
    }
}
