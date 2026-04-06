using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace Gameplay.Tests
{
    // ==================================================================
    // Determinism — same seed + config must produce identical output
    // ==================================================================

    public class MapDeterminismTests
    {
        [Theory]
        [InlineData(42, MapTemplate.OpenField)]
        [InlineData(42, MapTemplate.Maze)]
        [InlineData(42, MapTemplate.Forest)]
        [InlineData(123, MapTemplate.OpenField)]
        [InlineData(999, MapTemplate.Maze)]
        public void SameSeed_ProducesIdenticalMap(int seed, MapTemplate template)
        {
            var config = new MapGeneratorConfig { Seed = seed, Template = template };
            var gen = new MapGenerator();

            var r1 = gen.Generate(config);
            var r2 = gen.Generate(config);

            Assert.Equal(r1.BlockedCells.Count, r2.BlockedCells.Count);
            Assert.True(r1.BlockedCells.SetEquals(r2.BlockedCells),
                "Blocked cells differ between identical-seed runs");
            Assert.Equal(r1.SpawnPositions, r2.SpawnPositions);
            Assert.Equal(r1.MinePositions, r2.MinePositions);
        }

        [Theory]
        [InlineData(42, 43, MapTemplate.OpenField)]
        [InlineData(100, 200, MapTemplate.Maze)]
        [InlineData(1, 2, MapTemplate.Forest)]
        public void DifferentSeeds_ProduceDifferentMaps(int seed1, int seed2, MapTemplate template)
        {
            var gen = new MapGenerator();
            var r1 = gen.Generate(new MapGeneratorConfig { Seed = seed1, Template = template });
            var r2 = gen.Generate(new MapGeneratorConfig { Seed = seed2, Template = template });

            // It's theoretically possible for two seeds to produce the same map, but
            // astronomically unlikely for non-trivial obstacle densities.
            Assert.False(r1.BlockedCells.SetEquals(r2.BlockedCells),
                "Different seeds produced identical maps");
        }

        [Fact]
        public void SameSeed_WalkabilityGrid_IsIdentical()
        {
            var config = new MapGeneratorConfig { Seed = 77, Template = MapTemplate.Maze };
            var r1 = new MapGenerator().Generate(config);
            var r2 = new MapGenerator().Generate(config);

            for (int x = 0; x < config.Width; x++)
                for (int y = 0; y < config.Height; y++)
                {
                    var pos = new Position(x, y);
                    Assert.Equal(r1.Map.IsPositionWalkable(pos), r2.Map.IsPositionWalkable(pos));
                    Assert.Equal(r1.Map.IsPositionBuildable(pos), r2.Map.IsPositionBuildable(pos));
                }
        }
    }

    // ==================================================================
    // Symmetry — obstacle layout, spawns, and mines must be symmetric
    // ==================================================================

    public class MapSymmetryTests
    {
        [Theory]
        [InlineData(MapTemplate.OpenField, 42)]
        [InlineData(MapTemplate.Maze, 42)]
        [InlineData(MapTemplate.Forest, 42)]
        [InlineData(MapTemplate.OpenField, 7)]
        [InlineData(MapTemplate.Maze, 7)]
        [InlineData(MapTemplate.Forest, 7)]
        public void MirrorSymmetry_AllBlockedCellsHaveMirror(MapTemplate template, int seed)
        {
            var config = new MapGeneratorConfig
            {
                Seed = seed,
                Template = template,
                Symmetry = SymmetryType.Mirror
            };
            var result = new MapGenerator().Generate(config);
            int w = config.Width, h = config.Height;

            foreach (var pos in result.BlockedCells)
            {
                var mirror = new Position(w - 1 - pos.X, h - 1 - pos.Y);
                Assert.True(result.BlockedCells.Contains(mirror),
                    $"Blocked cell {pos} has no mirror at {mirror}");
            }
        }

        [Theory]
        [InlineData(42)]
        [InlineData(99)]
        [InlineData(314)]
        public void SpawnPositions_AreMirrored(int seed)
        {
            var config = new MapGeneratorConfig { Seed = seed };
            var result = new MapGenerator().Generate(config);
            int w = config.Width, h = config.Height;

            var s0 = result.SpawnPositions[0];
            var s1 = result.SpawnPositions[1];
            Assert.Equal(w - 1 - s0.X, s1.X);
            Assert.Equal(h - 1 - s0.Y, s1.Y);
        }

        [Theory]
        [InlineData(42)]
        [InlineData(99)]
        public void MinePositions_AreMirrored(int seed)
        {
            var config = new MapGeneratorConfig { Seed = seed };
            var result = new MapGenerator().Generate(config);
            int w = config.Width, h = config.Height;

            var m0 = result.MinePositions[0];
            var m1 = result.MinePositions[1];
            // Mines are 3x3 — bottom-left anchor mirror: (w - sizeX - x, h - sizeY - y)
            var mineSize = GameConstants.UNIT_SIZE[UnitType.MINE];
            Assert.Equal(w - mineSize.X - m0.X, m1.X);
            Assert.Equal(h - mineSize.Y - m0.Y, m1.Y);
        }

        [Theory]
        [InlineData(MapTemplate.OpenField)]
        [InlineData(MapTemplate.Forest)]
        public void GroveBased_GrovesAppearInPairs(MapTemplate template)
        {
            var config = new MapGeneratorConfig
            {
                Seed = 42,
                Template = template,
                Symmetry = SymmetryType.Mirror,
                ObstacleDensity = 0.15f
            };
            var result = new MapGenerator().Generate(config);

            // Grove count should be even (each primary grove has a mirror)
            Assert.True(result.Groves.Count % 2 == 0,
                $"Expected even grove count, got {result.Groves.Count}");
        }
    }

    // ==================================================================
    // Mine Distance Equality
    // ==================================================================

    public class MapMineDistanceTests
    {
        /// <summary>Center of a mine's 3x3 footprint from its top-left anchor.</summary>
        private static Position MineCenter(Position anchor)
        {
            var size = GameConstants.UNIT_SIZE[UnitType.MINE];
            return Position.Center(anchor, size);
        }

        [Theory]
        [InlineData(MapTemplate.OpenField, 42)]
        [InlineData(MapTemplate.Maze, 42)]
        [InlineData(MapTemplate.Forest, 42)]
        [InlineData(MapTemplate.OpenField, 1)]
        [InlineData(MapTemplate.Maze, 1)]
        public void EuclideanDistance_SpawnToMineCenter_EqualForAllPlayers(MapTemplate template, int seed)
        {
            var config = new MapGeneratorConfig { Seed = seed, Template = template };
            var result = new MapGenerator().Generate(config);

            float dist0 = Position.Distance(result.SpawnPositions[0], MineCenter(result.MinePositions[0]));
            float dist1 = Position.Distance(result.SpawnPositions[1], MineCenter(result.MinePositions[1]));

            Assert.True(Math.Abs(dist0 - dist1) < 1.5f,
                $"Mine center distances differ: P0={dist0:F2}, P1={dist1:F2}");
        }

        [Theory]
        [InlineData(MapTemplate.OpenField, 42)]
        [InlineData(MapTemplate.Maze, 42)]
        [InlineData(MapTemplate.Forest, 42)]
        public void PathLength_SpawnToMine_CloseForAllPlayers(MapTemplate template, int seed)
        {
            var config = new MapGeneratorConfig { Seed = seed, Template = template };
            var result = new MapGenerator().Generate(config);

            var path0 = result.Map.FindPath(result.SpawnPositions[0], result.MinePositions[0]);
            var path1 = result.Map.FindPath(result.SpawnPositions[1], result.MinePositions[1]);

            Assert.True(path0.Count > 0, "Player 0 has no path to mine");
            Assert.True(path1.Count > 0, "Player 1 has no path to mine");
            // Anchor-based paths may differ by a couple steps due to 3x3 footprint offset
            Assert.True(Math.Abs(path0.Count - path1.Count) <= 3,
                $"Path lengths differ: P0={path0.Count}, P1={path1.Count}");
        }
    }

    // ==================================================================
    // Path Balance — path lengths to center should be equal or very close
    // ==================================================================

    public class MapPathBalanceTests
    {
        [Theory]
        [InlineData(MapTemplate.OpenField, 42)]
        [InlineData(MapTemplate.Maze, 42)]
        [InlineData(MapTemplate.Forest, 42)]
        [InlineData(MapTemplate.OpenField, 7)]
        [InlineData(MapTemplate.Maze, 7)]
        public void PathToCenter_BalancedForAllPlayers(MapTemplate template, int seed)
        {
            var config = new MapGeneratorConfig { Seed = seed, Template = template };
            var result = new MapGenerator().Generate(config);
            var center = new Position(config.Width / 2, config.Height / 2);

            var path0 = result.Map.FindPath(result.SpawnPositions[0], center);
            var path1 = result.Map.FindPath(result.SpawnPositions[1], center);

            Assert.True(path0.Count > 0, "Player 0 cannot reach center");
            Assert.True(path1.Count > 0, "Player 1 cannot reach center");

            // Center cell may not be exactly at the mirror midpoint for even-sized maps,
            // so allow a tolerance of 2 cells.
            Assert.True(Math.Abs(path0.Count - path1.Count) <= 2,
                $"Path-to-center lengths differ by {Math.Abs(path0.Count - path1.Count)}: " +
                $"P0={path0.Count}, P1={path1.Count}");
        }

        [Theory]
        [InlineData(MapTemplate.OpenField)]
        [InlineData(MapTemplate.Maze)]
        [InlineData(MapTemplate.Forest)]
        public void SpawnsCanReachEachOther(MapTemplate template)
        {
            for (int seed = 0; seed < 5; seed++)
            {
                var config = new MapGeneratorConfig { Seed = seed, Template = template };
                var result = new MapGenerator().Generate(config);
                var path = result.Map.FindPath(result.SpawnPositions[0], result.SpawnPositions[1]);
                Assert.True(path.Count > 0, $"Seed {seed}: spawns not connected");
            }
        }
    }

    // ==================================================================
    // Connectivity — key positions must always be reachable
    // ==================================================================

    public class MapConnectivityTests
    {
        [Theory]
        [InlineData(MapTemplate.OpenField, 42)]
        [InlineData(MapTemplate.Maze, 42)]
        [InlineData(MapTemplate.Forest, 42)]
        [InlineData(MapTemplate.OpenField, 1)]
        [InlineData(MapTemplate.Maze, 1)]
        [InlineData(MapTemplate.Forest, 1)]
        [InlineData(MapTemplate.OpenField, 999)]
        [InlineData(MapTemplate.Maze, 999)]
        [InlineData(MapTemplate.Forest, 999)]
        public void SpawnPositions_AreBuildable(MapTemplate template, int seed)
        {
            var config = new MapGeneratorConfig { Seed = seed, Template = template };
            var result = new MapGenerator().Generate(config);

            foreach (var spawn in result.SpawnPositions)
                Assert.True(result.Map.IsPositionBuildable(spawn),
                    $"Spawn {spawn} is not buildable");
        }

        [Theory]
        [InlineData(MapTemplate.OpenField, 42)]
        [InlineData(MapTemplate.Maze, 42)]
        [InlineData(MapTemplate.Forest, 42)]
        public void MinePositions_AreWalkable(MapTemplate template, int seed)
        {
            var config = new MapGeneratorConfig { Seed = seed, Template = template };
            var result = new MapGenerator().Generate(config);

            foreach (var mine in result.MinePositions)
                Assert.True(result.Map.IsPositionWalkable(mine),
                    $"Mine {mine} is not walkable");
        }

        [Theory]
        [InlineData(MapTemplate.OpenField)]
        [InlineData(MapTemplate.Maze)]
        [InlineData(MapTemplate.Forest)]
        public void SpawnToMine_AlwaysReachable(MapTemplate template)
        {
            for (int seed = 0; seed < 10; seed++)
            {
                var config = new MapGeneratorConfig { Seed = seed, Template = template };
                var result = new MapGenerator().Generate(config);

                for (int p = 0; p < 2; p++)
                {
                    var path = result.Map.FindPath(result.SpawnPositions[p], result.MinePositions[p]);
                    Assert.True(path.Count > 0,
                        $"Seed {seed}: Player {p} cannot reach mine");
                }
            }
        }

        [Theory]
        [InlineData(MapTemplate.OpenField)]
        [InlineData(MapTemplate.Maze)]
        [InlineData(MapTemplate.Forest)]
        public void SpawnToCenter_AlwaysReachable(MapTemplate template)
        {
            for (int seed = 0; seed < 10; seed++)
            {
                var config = new MapGeneratorConfig { Seed = seed, Template = template };
                var result = new MapGenerator().Generate(config);
                var center = new Position(config.Width / 2, config.Height / 2);

                for (int p = 0; p < 2; p++)
                {
                    var path = result.Map.FindPath(result.SpawnPositions[p], center);
                    Assert.True(path.Count > 0,
                        $"Seed {seed}: Player {p} cannot reach center");
                }
            }
        }
    }

    // ==================================================================
    // Template-specific properties
    // ==================================================================

    public class MapTemplateTests
    {
        [Theory]
        [InlineData(0.05f)]
        [InlineData(0.10f)]
        [InlineData(0.15f)]
        public void OpenField_DensityStaysReasonable(float targetDensity)
        {
            var config = new MapGeneratorConfig
            {
                Seed = 42,
                Template = MapTemplate.OpenField,
                ObstacleDensity = targetDensity
            };
            var result = new MapGenerator().Generate(config);

            float actual = (float)result.BlockedCells.Count / (config.Width * config.Height);
            // Allow some tolerance — actual density depends on exclusion zones
            Assert.True(actual <= targetDensity * 3f + 0.05f,
                $"OpenField density {actual:P1} exceeds reasonable bounds for target {targetDensity:P1}");
        }

        [Theory]
        [InlineData(0.25f)]
        [InlineData(0.35f)]
        [InlineData(0.45f)]
        public void Maze_ProducesSubstantialObstacles(float targetDensity)
        {
            var config = new MapGeneratorConfig
            {
                Seed = 42,
                Template = MapTemplate.Maze,
                ObstacleDensity = targetDensity
            };
            var result = new MapGenerator().Generate(config);

            Assert.True(result.BlockedCells.Count > 0,
                $"Maze with density {targetDensity:P1} produced no obstacles");
        }

        [Fact]
        public void Forest_HasGroves()
        {
            var config = new MapGeneratorConfig
            {
                Seed = 42,
                Template = MapTemplate.Forest,
                ObstacleDensity = 0.20f
            };
            var result = new MapGenerator().Generate(config);

            Assert.True(result.Groves.Count > 0, "Forest should produce groves");
            foreach (var grove in result.Groves)
            {
                Assert.True(grove.Cells.Count > 0, "Grove should have cells");
                Assert.InRange(grove.TreeType, 1, 4);
            }
        }

        [Theory]
        [InlineData(MapTemplate.OpenField)]
        [InlineData(MapTemplate.Forest)]
        public void GroveBased_EachGroveHasSingleTreeType(MapTemplate template)
        {
            var config = new MapGeneratorConfig
            {
                Seed = 42,
                Template = template,
                ObstacleDensity = 0.15f
            };
            var result = new MapGenerator().Generate(config);

            foreach (var grove in result.Groves)
                Assert.InRange(grove.TreeType, 1, 4);
        }

        [Fact]
        public void Maze_GrovesFromFloodFill()
        {
            var config = new MapGeneratorConfig
            {
                Seed = 42,
                Template = MapTemplate.Maze,
                ObstacleDensity = 0.30f
            };
            var result = new MapGenerator().Generate(config);

            // All blocked cells should appear in exactly one grove
            var groveCells = new HashSet<Position>();
            foreach (var grove in result.Groves)
                foreach (var cell in grove.Cells)
                    Assert.True(groveCells.Add(cell), $"Cell {cell} appears in multiple groves");

            Assert.True(groveCells.SetEquals(result.BlockedCells),
                "Grove cells don't match blocked cells");
        }
    }

    // ==================================================================
    // Edge Cases
    // ==================================================================

    public class MapEdgeCaseTests
    {
        [Fact]
        public void MinimalMapSize_15x15_Works()
        {
            var config = new MapGeneratorConfig
            {
                Seed = 42, Width = 15, Height = 15, ObstacleDensity = 0.1f
            };
            var result = new MapGenerator().Generate(config);

            Assert.Equal(15, result.Width);
            Assert.Equal(15, result.Height);
            Assert.Equal(2, result.SpawnPositions.Length);
            Assert.Equal(2, result.MinePositions.Length);
        }

        [Fact]
        public void LargeMap_72x42_Works()
        {
            var config = new MapGeneratorConfig
            {
                Seed = 42, Width = 72, Height = 42, ObstacleDensity = 0.15f
            };
            var result = new MapGenerator().Generate(config);

            Assert.Equal(72, result.Width);
            Assert.Equal(42, result.Height);
            var path = result.Map.FindPath(result.SpawnPositions[0], result.SpawnPositions[1]);
            Assert.True(path.Count > 0, "Spawns not connected on large map");
        }

        [Fact]
        public void ZeroDensity_ProducesEmptyMap()
        {
            var config = new MapGeneratorConfig
            {
                Seed = 42,
                Template = MapTemplate.OpenField,
                ObstacleDensity = 0.0f
            };
            var result = new MapGenerator().Generate(config);
            Assert.Empty(result.BlockedCells);
        }

        [Fact]
        public void InvalidPlayerCount_Throws()
        {
            var config = new MapGeneratorConfig { PlayerCount = 3 };
            Assert.Throws<ArgumentException>(() => new MapGenerator().Generate(config));
        }

        [Fact]
        public void MapTooSmall_Throws()
        {
            var config = new MapGeneratorConfig { Width = 10, Height = 10 };
            Assert.Throws<ArgumentException>(() => new MapGenerator().Generate(config));
        }

        [Fact]
        public void NonSquareMap_Works()
        {
            var config = new MapGeneratorConfig
            {
                Seed = 42, Width = 40, Height = 20, ObstacleDensity = 0.12f
            };
            var result = new MapGenerator().Generate(config);

            Assert.Equal(40, result.Width);
            Assert.Equal(20, result.Height);
            Assert.True(result.Map.FindPath(result.SpawnPositions[0], result.SpawnPositions[1]).Count > 0);
        }

        [Fact]
        public void NoSymmetry_DoesNotEnforceMirror()
        {
            var config = new MapGeneratorConfig
            {
                Seed = 42,
                Template = MapTemplate.OpenField,
                Symmetry = SymmetryType.None,
                ObstacleDensity = 0.15f
            };
            var result = new MapGenerator().Generate(config);

            // With no symmetry, blocked cells are only in the primary half.
            // Every blocked cell should have y < height/2.
            int halfH = config.Height / 2;
            foreach (var pos in result.BlockedCells)
                Assert.True(pos.Y < halfH,
                    $"No-symmetry map has blocked cell {pos} outside primary half");
        }
    }

    // ==================================================================
    // Building-tree overlap — buildable areas must not contain blocked cells
    // ==================================================================

    public class MapBuildOverlapTests
    {
        [Fact]
        public void Seed1914087774_NoTreeAtMonasteryPosition()
        {
            // Reproduce the visual overlap: monastery at anchor (53,26), 3x3 footprint
            var config = new MapGeneratorConfig
            {
                Seed = 1914087774, Width = 75, Height = 30,
                ObstacleDensity = 0.20f, Template = MapTemplate.OpenField
            };
            var result = new MapGenerator().Generate(config);

            // Dump all grove cells near the monastery
            var output = new System.Text.StringBuilder();
            output.AppendLine($"Total groves: {result.Groves.Count}, blocked cells: {result.BlockedCells.Count}");
            foreach (var grove in result.Groves)
            {
                foreach (var cell in grove.Cells)
                {
                    if (cell.X >= 50 && cell.X <= 58 && cell.Y >= 22 && cell.Y <= 30)
                        output.AppendLine($"  grove tree at ({cell.X},{cell.Y}) type={grove.TreeType}");
                }
            }
            // Also dump blocked cells near monastery
            foreach (var cell in result.BlockedCells)
            {
                if (cell.X >= 50 && cell.X <= 58 && cell.Y >= 22 && cell.Y <= 30)
                    output.AppendLine($"  blocked at ({cell.X},{cell.Y})");
            }
            output.AppendLine("Footprint (53-55, 26-28) buildability:");
            for (int x = 53; x <= 55; x++)
                for (int y = 26; y <= 28; y++)
                    output.AppendLine($"  ({x},{y}): buildable={result.Map.IsPositionBuildable(new Position(x, y))} walkable={result.Map.IsPositionWalkable(new Position(x, y))}");

            // Check if any grove cell is in the monastery footprint
            foreach (var grove in result.Groves)
            {
                foreach (var cell in grove.Cells)
                {
                    if (cell.X >= 53 && cell.X <= 55 && cell.Y >= 26 && cell.Y <= 28)
                        Assert.Fail($"Tree at ({cell.X},{cell.Y}) overlaps monastery footprint (53-55, 26-28)\n{output}");
                }
            }

            // Also verify all footprint cells are open
            for (int x = 53; x <= 55; x++)
                for (int y = 26; y <= 28; y++)
                    Assert.True(result.Map.IsPositionBuildable(new Position(x, y)),
                        $"Cell ({x},{y}) is not buildable but should be for monastery placement\n{output}");

            // All checks passed — no tree overlaps monastery footprint
        }

        [Theory]
        [InlineData(1914087774)]
        [InlineData(42)]
        [InlineData(99)]
        [InlineData(1050142638)]
        public void BuildableArea_NeverOverlapsBlockedCells(int seed)
        {
            var config = new MapGeneratorConfig
            {
                Seed = seed, Width = 75, Height = 30,
                ObstacleDensity = 0.20f, Template = MapTemplate.OpenField
            };
            var result = new MapGenerator().Generate(config);

            var buildingTypes = new[] { UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY,
                                       UnitType.TOWER, UnitType.MONASTERY };

            foreach (var bt in buildingTypes)
            {
                var size = GameConstants.UNIT_SIZE[bt];
                for (int x = 0; x < config.Width - size.X + 1; x++)
                {
                    for (int y = 0; y < config.Height - size.Y + 1; y++)
                    {
                        var anchor = new Position(x, y);
                        if (!result.Map.IsAreaBuildable(bt, anchor)) continue;

                        for (int dx = 0; dx < size.X; dx++)
                            for (int dy = 0; dy < size.Y; dy++)
                            {
                                var cell = new Position(x + dx, y + dy);
                                Assert.False(result.BlockedCells.Contains(cell),
                                    $"Seed {seed}: {bt} buildable at {anchor} but blocked at {cell}");
                            }
                    }
                }
            }
        }
    }

    // ==================================================================
    // SimGameBuilder integration
    // ==================================================================

    public class MapGeneratorBuilderTests
    {
        [Fact]
        public void WithGeneratedMap_PlacesSpawnsAndMines()
        {
            var game = new SimGameBuilder()
                .WithGeneratedMap(42, MapTemplate.OpenField)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .Build();

            var pawns0 = game.GetUnitsByType(0, UnitType.PAWN);
            var pawns1 = game.GetUnitsByType(1, UnitType.PAWN);
            var mines = game.GetUnitsByType(-1, UnitType.MINE);

            Assert.Single(pawns0);
            Assert.Single(pawns1);
            Assert.Equal(2, mines.Count);
        }

        [Fact]
        public void WithGeneratedMap_GameRunsWithoutCrash()
        {
            var game = new SimGameBuilder()
                .WithGeneratedMap(42, MapTemplate.Maze)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .Build();

            // Should survive 100 frames with DoNothingAgents
            game.Run(100);
            Assert.True(game.CurrentFrame == 100);
        }

        [Fact]
        public void WithGeneratedMap_IsDeterministic()
        {
            var game1 = new SimGameBuilder()
                .WithGeneratedMap(42, MapTemplate.Forest)
                .Build();
            var game2 = new SimGameBuilder()
                .WithGeneratedMap(42, MapTemplate.Forest)
                .Build();

            // Walkability grids must match
            for (int x = 0; x < 30; x++)
                for (int y = 0; y < 30; y++)
                {
                    var pos = new Position(x, y);
                    Assert.Equal(
                        game1.Map.IsPositionWalkable(pos),
                        game2.Map.IsPositionWalkable(pos));
                }
        }

        [Fact]
        public void WithGeneratedMap_WithFullConfig()
        {
            var mapConfig = new MapGeneratorConfig
            {
                Seed = 99,
                Width = 40,
                Height = 40,
                Template = MapTemplate.Maze,
                ObstacleDensity = 0.25f
            };

            var game = new SimGameBuilder()
                .WithGeneratedMap(mapConfig)
                .WithGold(0, 3000)
                .Build();

            Assert.Equal(40, game.Map.Width);
            Assert.Equal(40, game.Map.Height);
            Assert.Equal(3000, game.GetGold(0));
        }

        [Fact]
        public void WithGeneratedMap_AdditionalUnitsCanBePlaced()
        {
            var game = new SimGameBuilder()
                .WithGeneratedMap(42, MapTemplate.OpenField)
                .WithGold(0, 10000)
                .WithUnit(0, UnitType.BASE, new Position(5, 8), isBuilt: true)
                .Build();

            var bases = game.GetUnitsByType(0, UnitType.BASE);
            Assert.Single(bases);
        }
    }

    // ==================================================================
    // Regression — multiple seeds × templates produce valid, playable maps
    // ==================================================================

    public class MapRegressionTests
    {
        [Theory]
        [InlineData(MapTemplate.OpenField, 0)]
        [InlineData(MapTemplate.OpenField, 1)]
        [InlineData(MapTemplate.OpenField, 42)]
        [InlineData(MapTemplate.OpenField, 100)]
        [InlineData(MapTemplate.OpenField, 999)]
        [InlineData(MapTemplate.Maze, 0)]
        [InlineData(MapTemplate.Maze, 1)]
        [InlineData(MapTemplate.Maze, 42)]
        [InlineData(MapTemplate.Maze, 100)]
        [InlineData(MapTemplate.Maze, 999)]
        [InlineData(MapTemplate.Forest, 0)]
        [InlineData(MapTemplate.Forest, 1)]
        [InlineData(MapTemplate.Forest, 42)]
        [InlineData(MapTemplate.Forest, 100)]
        [InlineData(MapTemplate.Forest, 999)]
        public void AllSeedsProduceValidMaps(MapTemplate template, int seed)
        {
            var config = new MapGeneratorConfig { Seed = seed, Template = template };
            var result = new MapGenerator().Generate(config);

            // Spawns buildable
            foreach (var spawn in result.SpawnPositions)
                Assert.True(result.Map.IsPositionBuildable(spawn),
                    $"Spawn {spawn} not buildable");

            // Mines walkable
            foreach (var mine in result.MinePositions)
                Assert.True(result.Map.IsPositionWalkable(mine),
                    $"Mine {mine} not walkable");

            // Full connectivity
            Assert.True(
                result.Map.FindPath(result.SpawnPositions[0], result.SpawnPositions[1]).Count > 0,
                "Spawns not connected");
            Assert.True(
                result.Map.FindPath(result.SpawnPositions[0], result.MinePositions[0]).Count > 0,
                "Player 0 cannot reach mine");
            Assert.True(
                result.Map.FindPath(result.SpawnPositions[1], result.MinePositions[1]).Count > 0,
                "Player 1 cannot reach mine");
        }

        [Theory]
        [InlineData(MapTemplate.OpenField)]
        [InlineData(MapTemplate.Maze)]
        [InlineData(MapTemplate.Forest)]
        public void GeneratedMap_SupportsBasePlacement(MapTemplate template)
        {
            var config = new MapGeneratorConfig { Seed = 42, Template = template };
            var result = new MapGenerator().Generate(config);

            // Near each spawn, there should be room for a BASE (6x4)
            foreach (var spawn in result.SpawnPositions)
            {
                bool foundBuildSite = false;
                for (int dx = -2; dx <= 8 && !foundBuildSite; dx++)
                    for (int dy = -2; dy <= 8 && !foundBuildSite; dy++)
                    {
                        var anchor = new Position(spawn.X + dx, spawn.Y + dy);
                        if (result.Map.IsAreaBuildable(UnitType.BASE, anchor))
                            foundBuildSite = true;
                    }

                Assert.True(foundBuildSite,
                    $"No BASE build site near spawn {spawn} on {template} map");
            }
        }

        [Theory]
        [InlineData(MapTemplate.OpenField)]
        [InlineData(MapTemplate.Maze)]
        [InlineData(MapTemplate.Forest)]
        public void AgentSimulation_RunsOnGeneratedMap(MapTemplate template)
        {
            // Build a game with a simple gather agent on a generated map
            var game = new SimGameBuilder()
                .WithGeneratedMap(42, template)
                .WithGold(0, 5000)
                .WithGold(1, 5000)
                .Build();

            // Add bases near spawns for the gather agent to use
            // (spawns already have pawns from WithGeneratedMap)
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(200);

            // Game should still be running (no crash, units still exist)
            Assert.True(game.CurrentFrame == 200);
        }
    }
}
