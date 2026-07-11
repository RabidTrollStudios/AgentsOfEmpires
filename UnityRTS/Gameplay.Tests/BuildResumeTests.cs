using System.Linq;
using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace Gameplay.Tests
{
    /// <summary>
    /// Headless tests for pausable/resumable construction (U1).
    /// Build progress lives on the BUILDING (SimUnit.BuildProgress), so it survives
    /// the original builder's death and any pawn can resume construction of the same
    /// unbuilt building without re-paying gold or placing a duplicate.
    /// Mirrors the Unity PlayMode BuildResumeTests against the shared engine.
    /// </summary>
    public class BuildResumeTests
    {
        private static SimUnit FindUnbuilt(SimGame game, UnitType type)
        {
            return game.GetUnitsByType(0, type).FirstOrDefault(u => !u.IsBuilt);
        }

        [Fact]
        public void Pawn_KilledDuringBuild_BuildingRetainsProgress()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(14, 15)) // adjacent to build site
                .WithAgent(0, new BuildOnceAgent(UnitType.BARRACKS, new Position(15, 15)))
                .WithAgent(1, new DoNothingAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Run until the building exists and has accumulated some progress.
            SimUnit building = null;
            for (int i = 0; i < 500 && (building == null || building.BuildProgress <= 0f); i++)
            {
                game.Tick();
                building = FindUnbuilt(game, UnitType.BARRACKS);
            }

            Assert.NotNull(building);
            Assert.True(building.BuildProgress > 0f, "Building should have positive progress before death");
            Assert.False(building.IsBuilt, "Building should not be complete yet");
            float progressBeforeDeath = building.BuildProgress;

            // Kill the pawn; the next tick reaps it (TickEngine Phase 4).
            var pawn = game.GetUnitsByType(0, UnitType.PAWN).Single();
            pawn.Health = 0;
            game.Tick();

            Assert.Empty(game.GetUnitsByType(0, UnitType.PAWN)); // pawn gone
            Assert.False(building.IsBuilt, "Building should still be incomplete after pawn death");
            Assert.True(building.BuildProgress >= progressBeforeDeath,
                $"Building should retain progress after pawn death (was {progressBeforeDeath:F2}s, now {building.BuildProgress:F2}s)");
        }

        [Fact]
        public void AnotherPawn_ResumesInterruptedBuild_ToCompletion()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 5000)
                .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
                .WithUnit(0, UnitType.PAWN, new Position(14, 15))   // first builder
                .WithUnit(0, UnitType.PAWN, new Position(15, 12))   // resumer, also adjacent
                .WithAgent(0, new ResumeBuildAgent(UnitType.BARRACKS, new Position(15, 15)))
                .WithAgent(1, new DoNothingAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            int startingGold = game.GetGold(0);
            int barracksCost = (int)GameConstants.COST[UnitType.BARRACKS];

            // Run until the building exists with progress.
            SimUnit building = null;
            for (int i = 0; i < 500 && (building == null || building.BuildProgress <= 0f); i++)
            {
                game.Tick();
                building = FindUnbuilt(game, UnitType.BARRACKS);
            }
            Assert.NotNull(building);
            Assert.True(building.BuildProgress > 0f);

            // Gold was charged exactly once at initial placement.
            Assert.Equal(startingGold - barracksCost, game.GetGold(0));
            int buildingNbr = building.UnitNbr;

            // Kill whichever pawn is currently building; the other pawn resumes.
            var builder = game.GetUnitsByType(0, UnitType.PAWN)
                .First(p => p.CurrentAction == UnitAction.BUILD);
            builder.Health = 0;
            game.Tick();

            // Run to completion.
            for (int i = 0; i < 2000 && !building.IsBuilt; i++)
                game.Tick();

            Assert.True(building.IsBuilt, "Building should complete after another pawn resumes");
            // Still the SAME building unit — resume did not place a duplicate.
            Assert.Equal(buildingNbr, building.UnitNbr);
            Assert.Single(game.GetUnitsByType(0, UnitType.BARRACKS));
            // Gold charged only once across the whole resume flow.
            Assert.Equal(startingGold - barracksCost, game.GetGold(0));
        }

        /// <summary>
        /// Multiple pawns can co-build one structure: each builder's tick adds to the
        /// SAME building.BuildProgress, so N pawns complete it faster than 1. This is
        /// the real "co-building" behavior (previously only observed via the now-removed
        /// ActiveBuilders bookkeeping set).
        /// </summary>
        [Fact]
        public void TwoPawns_CoBuildOneStructure_FasterThanOne()
        {
            int TicksToBuildWith(int pawnCount)
            {
                var b = new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithGold(0, 5000)
                    .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true);
                // Pawns clustered next to the build site so all reach it quickly.
                for (int i = 0; i < pawnCount; i++)
                    b.WithUnit(0, UnitType.PAWN, new Position(14 + i, 12));
                var g = b.WithAgent(0, new ResumeBuildAgent(UnitType.BARRACKS, new Position(15, 15)))
                         .WithAgent(1, new DoNothingAgent())
                         .Build();
                g.InitializeMatch();
                g.InitializeRound();

                for (int t = 1; t <= 3000; t++)
                {
                    g.Tick();
                    var built = g.GetUnitsByType(0, UnitType.BARRACKS).FirstOrDefault(u => u.IsBuilt);
                    if (built != null) return t;
                }
                return int.MaxValue; // never completed
            }

            int oneTicks = TicksToBuildWith(1);
            int twoTicks = TicksToBuildWith(2);

            Assert.True(oneTicks < int.MaxValue, "Single pawn should complete the build");
            Assert.True(twoTicks < int.MaxValue, "Two pawns should complete the build");
            // Two builders accumulate BuildProgress ~2x per tick, so the build finishes
            // meaningfully sooner. Allow slack for travel/pathing before both attach.
            Assert.True(twoTicks < oneTicks,
                $"Two pawns should co-build faster than one (1 pawn: {oneTicks} ticks, 2 pawns: {twoTicks} ticks)");
        }
    }
}
