using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for win/loss conditions: base destruction, unit elimination,
    /// and victory tracking.
    /// </summary>
    public class WinLossConditionTests
    {
        // ------------------------------------------------------------------
        // Base destruction
        // ------------------------------------------------------------------

        [Fact]
        public void BaseDestroyed_RemovedFromGame()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(5, 5))
                .WithUnit(0, UnitType.WARRIOR, new Position(6, 5))
                .WithUnit(0, UnitType.WARRIOR, new Position(7, 5))
                .WithUnit(1, UnitType.BASE, new Position(10, 5), isBuilt: true)
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            bool baseDestroyed = game.RunUntil(g =>
                g.GetUnitsByType(1, UnitType.BASE).Count == 0,
                5000);

            Assert.True(baseDestroyed, "3 warriors should destroy enemy base");
        }

        [Fact]
        public void AllUnitsDestroyed_NoneRemain()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(5, 5))
                .WithUnit(0, UnitType.WARRIOR, new Position(6, 5))
                .WithUnit(0, UnitType.WARRIOR, new Position(7, 5))
                .WithUnit(1, UnitType.PAWN, new Position(10, 5))
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            bool pawnKilled = game.RunUntil(g =>
                g.GetUnitsByType(1, UnitType.PAWN).Count == 0,
                3000);

            Assert.True(pawnKilled, "Warriors should eliminate enemy pawn");
        }

        // ------------------------------------------------------------------
        // Mutual combat
        // ------------------------------------------------------------------

        [Fact]
        public void MutualCombat_BothSidesTakeDamage()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(5, 5))
                .WithUnit(1, UnitType.WARRIOR, new Position(7, 5))
                .WithAgent(0, new AttackFirstEnemyAgent())
                .WithAgent(1, new AttackFirstEnemyAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Run until one dies
            game.RunUntil(g =>
                g.GetUnitsByType(0, UnitType.WARRIOR).Count == 0 ||
                g.GetUnitsByType(1, UnitType.WARRIOR).Count == 0,
                5000);

            // At least one side should have lost their warrior
            int p0Warriors = game.GetUnitsByType(0, UnitType.WARRIOR).Count;
            int p1Warriors = game.GetUnitsByType(1, UnitType.WARRIOR).Count;
            Assert.True(p0Warriors == 0 || p1Warriors == 0,
                "One side should have lost their warrior in mutual combat");
        }

        [Fact]
        public void SuperiorForce_WinsDecisively()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(5, 5))
                .WithUnit(0, UnitType.WARRIOR, new Position(6, 5))
                .WithUnit(0, UnitType.WARRIOR, new Position(7, 5))
                .WithUnit(1, UnitType.WARRIOR, new Position(10, 5))
                .WithAgent(0, new AttackFirstEnemyAgent())
                .WithAgent(1, new AttackFirstEnemyAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            bool enemyDead = game.RunUntil(g =>
                g.GetUnitsByType(1, UnitType.WARRIOR).Count == 0,
                5000);

            Assert.True(enemyDead, "3v1 should win decisively");
            Assert.True(game.GetUnitsByType(0, UnitType.WARRIOR).Count > 0,
                "Superior force should have survivors");
        }

        // ------------------------------------------------------------------
        // Building destruction frees cells
        // ------------------------------------------------------------------

        [Fact]
        public void DestroyedBuilding_FreesMapCells()
        {
            var buildPos = new Position(15, 15);

            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(5, 5))
                .WithUnit(0, UnitType.WARRIOR, new Position(6, 5))
                .WithUnit(0, UnitType.WARRIOR, new Position(7, 5))
                .WithUnit(1, UnitType.BARRACKS, buildPos, isBuilt: true)
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Barracks should occupy cells, making them unbuildable
            Assert.False(game.Map.IsPositionBuildable(buildPos));

            bool destroyed = game.RunUntil(g =>
                g.GetUnitsByType(1, UnitType.BARRACKS).Count == 0,
                5000);

            Assert.True(destroyed, "Warriors should destroy barracks");
            // After destruction, cells should be free again
            Assert.True(game.Map.IsPositionBuildable(buildPos),
                "Destroyed building should free map cells");
        }
    }
}
