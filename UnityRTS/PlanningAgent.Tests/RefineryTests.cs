using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the refinery system: build prerequisites, mining boost, and economy impact.
    /// </summary>
    public class RefineryTests
    {
        // ------------------------------------------------------------------
        // Happy path
        // ------------------------------------------------------------------

        [Fact]
        public void RefineryBoostsMining_GoldHigherWithRefinery()
        {
            // Game WITH refinery
            var withRefinery = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.REFINERY, new Position(10, 10), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithMine(new Position(12, 5), health: 50000)
                .WithAgent(0, new GatherAgent())
                .Build();

            withRefinery.InitializeMatch();
            withRefinery.InitializeRound();
            withRefinery.Run(1000);
            int goldWithRefinery = withRefinery.GetGold(0);

            // Game WITHOUT refinery
            var withoutRefinery = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithMine(new Position(12, 5), health: 50000)
                .WithAgent(0, new GatherAgent())
                .Build();

            withoutRefinery.InitializeMatch();
            withoutRefinery.InitializeRound();
            withoutRefinery.Run(1000);
            int goldWithout = withoutRefinery.GetGold(0);

            Assert.True(goldWithRefinery > goldWithout,
                $"Refinery should boost mining. With: {goldWithRefinery}, Without: {goldWithout}");
        }

        [Fact]
        public void UnbuiltRefinery_DoesNotBoostMining()
        {
            // Game with unbuilt refinery
            var withUnbuilt = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.REFINERY, new Position(10, 10), isBuilt: false)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithMine(new Position(12, 5), health: 50000)
                .WithAgent(0, new GatherAgent())
                .Build();

            withUnbuilt.InitializeMatch();
            withUnbuilt.InitializeRound();
            withUnbuilt.Run(1000);
            int goldUnbuilt = withUnbuilt.GetGold(0);

            // Game without any refinery
            var without = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithMine(new Position(12, 5), health: 50000)
                .WithAgent(0, new GatherAgent())
                .Build();

            without.InitializeMatch();
            without.InitializeRound();
            without.Run(1000);
            int goldWithout = without.GetGold(0);

            // Unbuilt refinery should not provide any boost
            Assert.True(goldUnbuilt <= goldWithout + 10,
                $"Unbuilt refinery should not boost mining. Unbuilt: {goldUnbuilt}, Without: {goldWithout}");
        }

        // ------------------------------------------------------------------
        // Boundary cases
        // ------------------------------------------------------------------

        [Fact]
        public void RefineryBuiltByWorker_CompletesSuccessfully()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.BARRACKS, new Position(10, 10), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithAgent(0, new BuildOnceAgent(UnitType.REFINERY, new Position(15, 15)))
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            bool built = game.RunUntil(g =>
            {
                var refineries = g.GetUnitsByType(0, UnitType.REFINERY);
                return refineries.Count > 0 && refineries[0].IsBuilt;
            }, 2000);

            Assert.True(built, "Refinery should complete construction");
        }

        [Fact]
        public void MultipleRefineries_StackBoost()
        {
            // Use mine adjacent to base to minimize travel time,
            // so mining speed difference is observable.

            // 1 refinery
            var oneRefinery = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.REFINERY, new Position(10, 10), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithMine(new Position(9, 5), health: 50000)
                .WithAgent(0, new GatherAgent())
                .Build();

            oneRefinery.InitializeMatch();
            oneRefinery.InitializeRound();
            oneRefinery.Run(3000);
            int goldOne = oneRefinery.GetGold(0);

            // 2 refineries
            var twoRefineries = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.REFINERY, new Position(10, 10), isBuilt: true)
                .WithUnit(0, UnitType.REFINERY, new Position(20, 20), isBuilt: true)
                .WithUnit(0, UnitType.WORKER, new Position(8, 5))
                .WithMine(new Position(9, 5), health: 50000)
                .WithAgent(0, new GatherAgent())
                .Build();

            twoRefineries.InitializeMatch();
            twoRefineries.InitializeRound();
            twoRefineries.Run(3000);
            int goldTwo = twoRefineries.GetGold(0);

            Assert.True(goldTwo >= goldOne,
                $"Two refineries should mine at least as fast as one. Two: {goldTwo}, One: {goldOne}");
        }
    }
}
