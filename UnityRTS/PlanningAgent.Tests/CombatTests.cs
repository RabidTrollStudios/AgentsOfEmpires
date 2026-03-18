using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the combat system: attacking, damage, death, validation.
    /// </summary>
    public class CombatTests
    {
        // ------------------------------------------------------------------
        // Happy path
        // ------------------------------------------------------------------

        [Fact]
        public void WarriorAttacksEnemy_EnemyHealthDecreases()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(10, 10))
                .WithUnit(1, UnitType.WARRIOR, new Position(11, 10))
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            float healthBefore = game.GetUnitsByType(1, UnitType.WARRIOR)[0].Health;
            game.Run(20);

            var enemyWarriors = game.GetUnitsByType(1, UnitType.WARRIOR);
            if (enemyWarriors.Count > 0)
            {
                Assert.True(enemyWarriors[0].Health < healthBefore,
                    $"Enemy health should decrease. Before: {healthBefore}, After: {enemyWarriors[0].Health}");
            }
            // If enemy is dead, that's also a valid outcome
        }

        [Fact]
        public void WarriorKillsEnemy_EnemyRemoved()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(10, 10))
                .WithUnit(1, UnitType.PAWN, new Position(11, 10)) // Low health target
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            // Pawn (50 HP) should be dead
            Assert.Empty(game.GetUnitsByType(1, UnitType.PAWN));
        }

        [Fact]
        public void AttackerGoesIdle_WhenTargetDies()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(10, 10))
                .WithUnit(1, UnitType.PAWN, new Position(11, 10))
                .WithAgent(0, new AttackOnceAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            var warriors = game.GetUnitsByType(0, UnitType.WARRIOR);
            Assert.Single(warriors);
            Assert.Equal(UnitAction.IDLE, warriors[0].CurrentAction);
        }

        [Fact]
        public void ArcherAttacks_DealsLessDamage()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.ARCHER, new Position(10, 10))
                .WithUnit(1, UnitType.BASE, new Position(12, 10), isBuilt: true)
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            float baseBefore = game.GetUnitsByType(1, UnitType.BASE)[0].Health;
            game.Run(20);

            var bases = game.GetUnitsByType(1, UnitType.BASE);
            Assert.Single(bases);
            // Archer base damage 40, scaled by game speed. BASE has 1000 HP,
            // so a single archer shouldn't kill it in 20 ticks (40*20=800 damage).
            Assert.True(bases[0].Health < baseBefore);
            Assert.True(bases[0].Health > 0, "BASE should still be alive — archer DPS is moderate");
        }

        [Fact]
        public void WarriorWalksToTarget_ThenAttacks()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(5, 5))
                .WithUnit(1, UnitType.PAWN, new Position(15, 5))
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(200);

            // Warrior should have walked over and killed the pawn
            Assert.Empty(game.GetUnitsByType(1, UnitType.PAWN));
        }

        // ------------------------------------------------------------------
        // Error cases
        // ------------------------------------------------------------------

        [Fact]
        public void AttackOwnUnit_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(10, 10))
                .WithUnit(0, UnitType.PAWN, new Position(11, 10))
                .WithAgent(0, new AttackOwnPawnAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            // Own pawn should still be alive
            var pawns = game.GetUnitsByType(0, UnitType.PAWN);
            Assert.Single(pawns);
            Assert.Equal(GameConstants.HEALTH[UnitType.PAWN], pawns[0].Health);
        }

        [Fact]
        public void PawnCannotAttack()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.PAWN, new Position(10, 10))
                .WithUnit(1, UnitType.PAWN, new Position(11, 10))
                .WithAgent(0, new PawnAttackAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            // Enemy pawn should be unharmed
            var enemies = game.GetUnitsByType(1, UnitType.PAWN);
            Assert.Single(enemies);
            Assert.Equal(GameConstants.HEALTH[UnitType.PAWN], enemies[0].Health);
        }

        [Fact]
        public void AttackMine_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(10, 10))
                .WithMine(new Position(12, 10), health: 10000)
                .WithAgent(0, new AttackMineAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            var mines = game.GetUnitsByType(-1, UnitType.MINE);
            Assert.Single(mines);
            Assert.Equal(10000, mines[0].Health);
        }

        // ------------------------------------------------------------------
        // Boundary: destroyed unit frees cells
        // ------------------------------------------------------------------

        [Fact]
        public void DestroyedBuilding_FreesCells()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(10, 10))
                .WithUnit(0, UnitType.WARRIOR, new Position(10, 11))
                .WithUnit(0, UnitType.WARRIOR, new Position(10, 12))
                .WithUnit(1, UnitType.BARRACKS, new Position(12, 11), isBuilt: true)
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            // Barracks has 500 HP, warriors do 400 dps each
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(200);

            Assert.Empty(game.GetUnitsByType(1, UnitType.BARRACKS));

            // Cells under the barracks footprint should be free
            Assert.True(game.Map.IsPositionBuildable(new Position(12, 11)));
            Assert.True(game.Map.IsPositionBuildable(new Position(13, 11)));
        }

        [Fact]
        public void DestroyedPawn_FreesCell()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(10, 10))
                .WithUnit(1, UnitType.PAWN, new Position(11, 10))
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            var pawnPos = game.GetUnitsByType(1, UnitType.PAWN)[0].GridPosition;

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            Assert.Empty(game.GetUnitsByType(1, UnitType.PAWN));
            Assert.True(game.Map.IsPositionBuildable(pawnPos));
        }

        // ------------------------------------------------------------------
        // Stress test
        // ------------------------------------------------------------------

        [Fact]
        public void ManyUnitsInCombat_NoExceptions()
        {
            var builder = new SimGameBuilder()
                .WithMapSize(30, 30);

            // 10 warriors per side
            for (int i = 0; i < 10; i++)
            {
                builder.WithUnit(0, UnitType.WARRIOR, new Position(5, 2 + i));
                builder.WithUnit(1, UnitType.WARRIOR, new Position(25, 2 + i));
            }

            var game = builder
                .WithAgent(0, new AttackAllEnemiesAgent())
                .WithAgent(1, new AttackAllEnemiesAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Should run without exceptions
            game.Run(500);

            // One side should have fewer warriors (combat happened)
            int total = game.GetUnitsByType(0, UnitType.WARRIOR).Count
                      + game.GetUnitsByType(1, UnitType.WARRIOR).Count;
            Assert.True(total < 20, $"Some warriors should have died. Total remaining: {total}");
        }
    }
}
