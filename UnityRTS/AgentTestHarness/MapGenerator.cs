using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace AgentTestHarness
{
    // =========================================================================
    // Backward-compatible aliases — these types used to live here but the
    // algorithms now live in AgentSDK.MapGenCore.  Existing test and builder
    // code that references AgentTestHarness.{SymmetryType, MapTemplate, ...}
    // keeps compiling without changes.
    // =========================================================================

    // Enums re-exported from AgentSDK so callers using `AgentTestHarness.SymmetryType`
    // still resolve.  C# treats a `using` alias inside a namespace as a type alias
    // only within this file, so we define thin wrapper types instead.

    // NOTE: SymmetryType and MapTemplate now live in AgentSDK directly.
    //       Because this namespace previously defined them, callers that
    //       `using AgentTestHarness;` will see them via AgentSDK since
    //       AgentTestHarness references AgentSDK.  If any caller explicitly
    //       qualifies `AgentTestHarness.SymmetryType`, the using statements
    //       below handle it.

    /// <summary>
    /// Backward-compatible configuration class.  Wraps <see cref="MapGenConfig"/>.
    /// </summary>
    public class MapGeneratorConfig
    {
        /// <summary>Random seed for deterministic generation.</summary>
        public int Seed { get; set; } = 42;

        /// <summary>Map width in cells.</summary>
        public int Width { get; set; } = 30;

        /// <summary>Map height in cells.</summary>
        public int Height { get; set; } = 30;

        /// <summary>Number of players (currently only 2 supported).</summary>
        public int PlayerCount { get; set; } = 2;

        /// <summary>
        /// Target fraction of cells that are obstacles (0.0–1.0).
        /// </summary>
        public float ObstacleDensity { get; set; } = 0.15f;

        /// <summary>Number of gold mines per player.</summary>
        public int MinesPerPlayer { get; set; } = 1;

        /// <summary>Symmetry enforcement mode.</summary>
        public SymmetryType Symmetry { get; set; } = SymmetryType.MIRROR;

        /// <summary>Map layout template.</summary>
        public MapTemplate Template { get; set; } = MapTemplate.OPEN_FIELD;

        internal MapGenConfig ToCore() => new MapGenConfig
        {
            Seed = Seed,
            Width = Width,
            Height = Height,
            PlayerCount = PlayerCount,
            ObstacleDensity = ObstacleDensity,
            MinesPerPlayer = MinesPerPlayer,
            Symmetry = Symmetry,
            Template = Template
        };
    }

    /// <summary>
    /// A cluster of blocked cells sharing a single visual tree type.
    /// Wraps <see cref="GroveData"/>.
    /// </summary>
    public class Grove
    {
        /// <summary>Visual tree type identifier (1–4).</summary>
        public int TreeType { get; }

        /// <summary>Grid positions belonging to this grove.</summary>
        public List<Position> Cells { get; }

        public Grove(int treeType, List<Position> cells)
        {
            TreeType = treeType;
            Cells = cells;
        }

        internal Grove(GroveData core) : this(core.TreeType, core.Cells) { }
    }

    /// <summary>
    /// Output of map generation.
    /// Wraps <see cref="MapGenResult"/> and adds a built <see cref="SimMap"/>.
    /// </summary>
    public class MapGeneratorResult
    {
        /// <summary>Seed used to generate this map.</summary>
        public int Seed { get; }

        /// <summary>Map width in cells.</summary>
        public int Width { get; }

        /// <summary>Map height in cells.</summary>
        public int Height { get; }

        /// <summary>The generated grid with blocked cells applied.</summary>
        public SimMap Map { get; }

        /// <summary>Starting pawn positions, one per player.</summary>
        public Position[] SpawnPositions { get; }

        /// <summary>Mine positions (player 0's mines first, then player 1's).</summary>
        public Position[] MinePositions { get; }

        /// <summary>Tree groves for visual rendering.</summary>
        public List<Grove> Groves { get; }

        /// <summary>All blocked cell positions on the map.</summary>
        public HashSet<Position> BlockedCells { get; }

        /// <summary>Deterministic agent-slot flip from the shared seed (see MapGenResult.BlueIsAgent0).</summary>
        public bool BlueIsAgent0 { get; }

        internal MapGeneratorResult(int seed, MapGenResult core, SimMap map)
        {
            Seed = seed;
            Width = core.Width;
            Height = core.Height;
            Map = map;
            SpawnPositions = core.SpawnPositions;
            MinePositions = core.MinePositions;
            BlockedCells = core.BlockedCells;
            Groves = core.Groves.Select(g => new Grove(g)).ToList();
            BlueIsAgent0 = core.BlueIsAgent0;
        }
    }

    /// <summary>
    /// Procedural map generator for the SimGame harness.
    /// Delegates all game-logic algorithms to <see cref="MapGenCore"/> in AgentSDK,
    /// then builds a <see cref="SimMap"/> for pathfinding/connectivity validation.
    /// </summary>
    public class MapGenerator
    {
        /// <summary>
        /// Generate a map from the given configuration.
        /// </summary>
        public MapGeneratorResult Generate(MapGeneratorConfig config)
        {
            var core = MapGenCore.Generate(config.ToCore());
            var map = BuildMap(core);
            return new MapGeneratorResult(config.Seed, core, map);
        }

        /// <summary>Point reflection for 1x1 cells.</summary>
        internal static Position MirrorPos(Position pos, int width, int height)
            => MapGenCore.MirrorPos(pos, width, height);

        private static SimMap BuildMap(MapGenResult core)
        {
            var map = new SimMap(core.Width, core.Height);
            foreach (var pos in core.BlockedCells)
                map.SetCellBlocked(pos);
            return map;
        }
    }
}
