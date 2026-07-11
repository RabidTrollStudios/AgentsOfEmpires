using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using GridPalette = UnityEditor.GridPalette;

namespace GameManager.EditorTools
{
    /// <summary>
    /// Editor tool to set up placeholder terrain tilemap layers.
    /// Adds a Water Background layer behind everything and fills the
    /// Flat Ground layer with the FlatGround_color1 rule tile,
    /// leaving a water border around the edges.
    ///
    /// IMPORTANT: MapManager.GenerateGraph uses child index 0 as the walkable
    /// ground tilemap. The "Grass" layer MUST remain child index 0.
    /// Water Background is placed at child index 1 but uses sortingOrder -10
    /// so it renders behind Grass despite being later in the hierarchy.
    ///
    /// Layer stacking (render order, back to front):
    ///   sortingOrder -10: Water Background (solid blue fill)
    ///   sortingOrder   0: Grass / Flat Ground (rule tile with auto-edges)
    ///   sortingOrder   1: Walls (obstacles)
    ///   sortingOrder   2: Trees (obstacles)
    ///   sortingOrder  10: Influence (debug overlay)
    /// </summary>
    public static class TerrainSetup
    {
        private const int WATER_BORDER = 2; // cells of water around the playable area

        // Sorting order constants
        private const int SORT_WATER_BG = -10;
        private const int SORT_GROUND = 0;
        private const int SORT_WALLS = 1;
        private const int SORT_TREES = 2;
        private const int SORT_INFLUENCE = 10;

        [MenuItem("Tools/Terrain/Setup Water + Flat Ground Layers")]
        public static void SetupTerrainLayers()
        {
            var grid = Object.FindFirstObjectByType<Grid>();
            if (grid == null)
            {
                Debug.LogError("TerrainSetup: No Grid found in the scene.");
                return;
            }

            // Load tile assets
            var waterBgTile = AssetDatabase.LoadAssetAtPath<TileBase>(
                "Assets/PlaceholderArt/Terrain/Tileset/Tilemap Settings/Water Background color.asset");
            var flatGroundTile = AssetDatabase.LoadAssetAtPath<TileBase>(
                "Assets/PlaceholderArt/Terrain/Tileset/Rule Tiles/FlatGround_color1.asset");

            if (waterBgTile == null)
            {
                Debug.LogError("TerrainSetup: Could not load Water Background color tile asset.");
                return;
            }
            if (flatGroundTile == null)
            {
                Debug.LogError("TerrainSetup: Could not load FlatGround_color1 rule tile asset. " +
                    "Make sure you ran generate_rule_tiles.py and Unity has imported the asset.");
                return;
            }

            // Find existing tilemaps by name
            Tilemap grassTilemap = null, wallsTilemap = null, treesTilemap = null, influenceTilemap = null;
            foreach (var tm in grid.GetComponentsInChildren<Tilemap>())
            {
                switch (tm.gameObject.name)
                {
                    case "Grass": grassTilemap = tm; break;
                    case "Walls": wallsTilemap = tm; break;
                    case "Trees": treesTilemap = tm; break;
                    case "Influence": influenceTilemap = tm; break;
                }
            }

            if (grassTilemap == null)
            {
                Debug.LogError("TerrainSetup: Could not find 'Grass' tilemap under Grid.");
                return;
            }

            // Determine map size from existing Grass tilemap
            grassTilemap.CompressBounds();
            var mapSize = grassTilemap.size;
            var origin = grassTilemap.origin;
            Debug.Log($"TerrainSetup: Map size = {mapSize}, origin = {origin}");

            // Create or find the Water Background tilemap
            Tilemap waterTilemap = FindOrCreateTilemap(grid, "Water Background", SORT_WATER_BG);

            // Register undo for all modified objects
            Undo.RecordObject(waterTilemap, "Setup Water Background");
            Undo.RecordObject(grassTilemap, "Setup Flat Ground");

            // --- Set sorting orders on ALL tilemap layers ---
            SetSortingOrder(waterTilemap, SORT_WATER_BG);
            SetSortingOrder(grassTilemap, SORT_GROUND);
            if (wallsTilemap != null) SetSortingOrder(wallsTilemap, SORT_WALLS);
            if (treesTilemap != null) SetSortingOrder(treesTilemap, SORT_TREES);
            if (influenceTilemap != null) SetSortingOrder(influenceTilemap, SORT_INFLUENCE);

            // --- Fill water background (entire map + border) ---
            int totalWidth = mapSize.x + WATER_BORDER * 2;
            int totalHeight = mapSize.y + WATER_BORDER * 2;
            int waterOriginX = origin.x - WATER_BORDER;
            int waterOriginY = origin.y - WATER_BORDER;

            waterTilemap.ClearAllTiles();
            for (int x = 0; x < totalWidth; x++)
            {
                for (int y = 0; y < totalHeight; y++)
                {
                    var pos = new Vector3Int(waterOriginX + x, waterOriginY + y, 0);
                    waterTilemap.SetTile(pos, waterBgTile);
                }
            }
            Debug.Log($"TerrainSetup: Filled water background ({totalWidth}x{totalHeight})");

            // --- Replace Grass tilemap with FlatGround rule tile ---
            grassTilemap.ClearAllTiles();
            for (int x = 0; x < mapSize.x; x++)
            {
                for (int y = 0; y < mapSize.y; y++)
                {
                    var pos = new Vector3Int(origin.x + x, origin.y + y, 0);
                    grassTilemap.SetTile(pos, flatGroundTile);
                }
            }
            Debug.Log($"TerrainSetup: Filled flat ground ({mapSize.x}x{mapSize.y}) with FlatGround_color1 rule tile");

            // --- Ensure Grass stays at child index 0 (MapManager requirement) ---
            // Water Background goes to index 1, rendered behind via sortingOrder
            grassTilemap.transform.SetAsFirstSibling();
            waterTilemap.transform.SetSiblingIndex(1);

            // Mark all modified objects dirty
            EditorUtility.SetDirty(waterTilemap.gameObject);
            EditorUtility.SetDirty(grassTilemap.gameObject);
            if (wallsTilemap != null) EditorUtility.SetDirty(wallsTilemap.gameObject);
            if (treesTilemap != null) EditorUtility.SetDirty(treesTilemap.gameObject);
            if (influenceTilemap != null) EditorUtility.SetDirty(influenceTilemap.gameObject);

            Debug.Log("TerrainSetup: Done! Layer sorting orders set:");
            Debug.Log("  Water Background: sortingOrder=-10 (child 1, renders behind)");
            Debug.Log("  Grass:            sortingOrder=0   (child 0, MapManager ground layer)");
            Debug.Log("  Walls:            sortingOrder=1");
            Debug.Log("  Trees:            sortingOrder=2");
            Debug.Log("  Influence:        sortingOrder=10");
            Debug.Log("Save the scene (Ctrl+S) to persist changes.");
        }

        [MenuItem("Tools/Terrain/Clear Water Layer")]
        public static void ClearWaterLayer()
        {
            var grid = Object.FindFirstObjectByType<Grid>();
            if (grid == null) return;

            foreach (var tm in grid.GetComponentsInChildren<Tilemap>())
            {
                if (tm.gameObject.name == "Water Background")
                {
                    Undo.RecordObject(tm, "Clear Water Layer");
                    tm.ClearAllTiles();
                    Debug.Log("TerrainSetup: Cleared Water Background layer.");
                    return;
                }
            }
            Debug.LogWarning("TerrainSetup: No 'Water Background' tilemap found.");
        }

        [MenuItem("Tools/Terrain/Restore Original Grass Tiles")]
        public static void RestoreOriginalGrass()
        {
            var grid = Object.FindFirstObjectByType<Grid>();
            if (grid == null) return;

            var grassTile = AssetDatabase.LoadAssetAtPath<TileBase>(
                "Assets/Tiles/Rule Tiles/Grass Rule Tile.asset");
            if (grassTile == null)
            {
                Debug.LogError("TerrainSetup: Could not load original Grass Rule Tile.");
                return;
            }

            foreach (var tm in grid.GetComponentsInChildren<Tilemap>())
            {
                if (tm.gameObject.name == "Grass")
                {
                    Undo.RecordObject(tm, "Restore Original Grass");
                    tm.CompressBounds();
                    var size = tm.size;
                    var origin = tm.origin;
                    tm.ClearAllTiles();
                    for (int x = 0; x < size.x; x++)
                    {
                        for (int y = 0; y < size.y; y++)
                        {
                            tm.SetTile(new Vector3Int(origin.x + x, origin.y + y, 0), grassTile);
                        }
                    }
                    Debug.Log("TerrainSetup: Restored original Grass Rule Tile.");
                    EditorUtility.SetDirty(tm.gameObject);
                    return;
                }
            }
        }

        [MenuItem("Tools/Terrain/Create Terrain Tile Palette")]
        public static void CreateTerrainPalette()
        {
            string paletteDir = "Assets/PlaceholderArt/Terrain/Tileset/Palette";
            if (!AssetDatabase.IsValidFolder(paletteDir))
            {
                AssetDatabase.CreateFolder("Assets/PlaceholderArt/Terrain/Tileset", "Palette");
            }

            string palettePath = paletteDir + "/Terrain Palette.prefab";

            // If palette already exists, just select it
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(palettePath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("TerrainSetup: Terrain Palette already exists. Selected it.");
                return;
            }

            // Load all the tiles we want in the palette
            var tiles = new (string name, string path, Vector3Int pos)[]
            {
                ("FlatGround", "Assets/PlaceholderArt/Terrain/Tileset/Rule Tiles/FlatGround_color1.asset",
                    new Vector3Int(0, 0, 0)),
                ("Water BG", "Assets/PlaceholderArt/Terrain/Tileset/Tilemap Settings/Water Background color.asset",
                    new Vector3Int(1, 0, 0)),
                ("Water Foam", "Assets/PlaceholderArt/Terrain/Tileset/Tilemap Settings/Water Tile animated.asset",
                    new Vector3Int(2, 0, 0)),
                ("Shadow", "Assets/PlaceholderArt/Terrain/Tileset/Tilemap Settings/Shadow.asset",
                    new Vector3Int(3, 0, 0)),
            };

            // Also add all 44 individual color1 tiles for manual placement (stairs, cliffs, etc.)
            // These go on row 1 (y=1) for flat ground pieces and row 2 (y=2) for elevated/cliff pieces

            // Create the palette GameObject
            var paletteGo = new GameObject("Terrain Palette");
            var grid = paletteGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1, 1, 0);

            var child = new GameObject("Layer1");
            child.transform.SetParent(paletteGo.transform);
            child.transform.localPosition = Vector3.zero;
            var tilemap = child.AddComponent<Tilemap>();
            child.AddComponent<TilemapRenderer>();

            // Place the main tiles (rule tiles + special tiles)
            foreach (var (name, path, pos) in tiles)
            {
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                if (tile != null)
                {
                    tilemap.SetTile(pos, tile);
                    Debug.Log($"TerrainSetup: Added '{name}' to palette at {pos}");
                }
                else
                {
                    Debug.LogWarning($"TerrainSetup: Could not load tile '{name}' from {path}");
                }
            }

            // Add individual sliced tiles for manual placement (stairs, cliffs)
            // Row 2: Left side flat ground pieces (sp0-sp27 that aren't covered by the rule tile)
            // Row 3: Right side elevated/cliff pieces
            string tileSettingsDir = "Assets/PlaceholderArt/Terrain/Tileset/Tilemap Settings";
            for (int i = 0; i <= 43; i++)
            {
                string tilePath = $"{tileSettingsDir}/Tilemap_color1_{i}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);
                if (tile != null)
                {
                    // Arrange in rows of 8: sprites 0-7 on row 2, 8-15 on row 3, etc.
                    int col = i % 8;
                    int row = 2 + (i / 8);
                    tilemap.SetTile(new Vector3Int(col, row, 0), tile);
                }
            }

            tilemap.CompressBounds();

            // Save as prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(paletteGo, palettePath);
            Object.DestroyImmediate(paletteGo);

            // Create the GridPalette asset (tells Unity's Tile Palette window this is a palette)
            var gridPalette = ScriptableObject.CreateInstance<GridPalette>();
            gridPalette.name = "Terrain Palette";
            gridPalette.cellSizing = GridPalette.CellSizing.Automatic;
            AssetDatabase.AddObjectToAsset(gridPalette, palettePath);
            AssetDatabase.SaveAssets();

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"TerrainSetup: Created Terrain Palette at {palettePath}");
            Debug.Log("Open Window > 2D > Tile Palette, then select 'Terrain Palette' from the dropdown.");
            Debug.Log("Row 0: Rule tiles (FlatGround, Water BG, Water Foam, Shadow)");
            Debug.Log("Rows 2+: Individual tiles for manual placement (stairs, cliffs, etc.)");
        }

        private static void SetSortingOrder(Tilemap tilemap, int order)
        {
            var renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer != null && renderer.sortingOrder != order)
            {
                Undo.RecordObject(renderer, $"Set sorting order on {tilemap.gameObject.name}");
                renderer.sortingOrder = order;
            }
        }

        private static Tilemap FindOrCreateTilemap(Grid grid, string name, int sortingOrder)
        {
            foreach (var tm in grid.GetComponentsInChildren<Tilemap>())
            {
                if (tm.gameObject.name == name)
                    return tm;
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name} Tilemap");
            go.transform.SetParent(grid.transform);
            go.transform.localPosition = Vector3.zero;

            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;

            Debug.Log($"TerrainSetup: Created '{name}' tilemap layer (sortingOrder={sortingOrder}).");
            return tilemap;
        }
    }
}
