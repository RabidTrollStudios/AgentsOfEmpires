using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using UnityEngine;

namespace GameManager
{
    /// <summary>
    /// Result of procedural map generation — blocked cells, spawn/mine positions, and grove data.
    /// Uses Vector3Int for Unity tilemap/rendering consumption.
    /// Consumed by <see cref="GameManager"/> to create tilemaps at runtime.
    /// </summary>
    internal class ProceduralMapResult
    {
        public int Width;
        public int Height;
        public HashSet<Vector3Int> BlockedCells;
        public Vector3Int[] SpawnPositions;
        public Vector3Int[] MinePositions;
        public List<ProceduralGrove> Groves;
        /// <summary>Deterministic agent-slot flip from the shared seed (see MapGenResult.BlueIsAgent0).</summary>
        public bool BlueIsAgent0;
    }

    /// <summary>A cluster of blocked cells sharing one visual tree type (1–4).</summary>
    internal class ProceduralGrove
    {
        public int TreeType;
        public List<Vector3Int> Cells;
        public ProceduralGrove(int treeType, List<Vector3Int> cells)
        { TreeType = treeType; Cells = cells; }
    }

    /// <summary>
    /// Unity-side procedural map generator. Delegates all game-logic algorithms
    /// to <see cref="MapGenCore"/> in AgentSDK, then converts the output to
    /// Vector3Int for tilemap/rendering consumption.
    /// </summary>
    internal static class ProceduralMapGenerator
    {
        /// <summary>
        /// Generate a procedural map with the given parameters.
        /// Converts Unity-side enums to AgentSDK equivalents, delegates to
        /// MapGenCore, and converts the result back to Vector3Int.
        /// </summary>
        public static ProceduralMapResult Generate(int width, int height, float density,
            int seed, MapTemplate template, MapSymmetryMode symmetry)
        {
            var config = new MapGenConfig
            {
                Seed = seed,
                Width = width,
                Height = height,
                ObstacleDensity = density,
                Template = ToSdkTemplate(template),
                Symmetry = ToSdkSymmetry(symmetry)
            };

            var core = MapGenCore.Generate(config);
            return ToUnityResult(core);
        }

        #region Enum Conversion

        private static AgentSDK.MapTemplate ToSdkTemplate(MapTemplate t)
        {
            switch (t)
            {
                case MapTemplate.OPEN_FIELD: return AgentSDK.MapTemplate.OPEN_FIELD;
                case MapTemplate.MAZE:      return AgentSDK.MapTemplate.MAZE;
                case MapTemplate.FOREST:    return AgentSDK.MapTemplate.FOREST;
                default:                    return AgentSDK.MapTemplate.OPEN_FIELD;
            }
        }

        private static SymmetryType ToSdkSymmetry(MapSymmetryMode s)
        {
            switch (s)
            {
                case MapSymmetryMode.NONE:       return SymmetryType.NONE;
                case MapSymmetryMode.MIRROR:     return SymmetryType.MIRROR;
                case MapSymmetryMode.ROTATIONAL: return SymmetryType.ROTATIONAL;
                default:                         return SymmetryType.MIRROR;
            }
        }

        #endregion

        #region Result Conversion

        private static ProceduralMapResult ToUnityResult(MapGenResult core)
        {
            return new ProceduralMapResult
            {
                Width = core.Width,
                Height = core.Height,
                BlockedCells = new HashSet<Vector3Int>(
                    core.BlockedCells.Select(p => new Vector3Int(p.X, p.Y, 0))),
                SpawnPositions = core.SpawnPositions
                    .Select(p => new Vector3Int(p.X, p.Y, 0)).ToArray(),
                MinePositions = core.MinePositions
                    .Select(p => new Vector3Int(p.X, p.Y, 0)).ToArray(),
                Groves = core.Groves.Select(g => new ProceduralGrove(
                    g.TreeType,
                    g.Cells.Select(c => new Vector3Int(c.X, c.Y, 0)).ToList()
                )).ToList(),
                BlueIsAgent0 = core.BlueIsAgent0
            };
        }

        #endregion
    }
}
