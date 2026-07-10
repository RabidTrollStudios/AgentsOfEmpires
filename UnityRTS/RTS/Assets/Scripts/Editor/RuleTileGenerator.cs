using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameManager.EditorTools
{
    /// <summary>
    /// Editor tool to generate FlatGround RuleTile assets using Unity's RuleTile API.
    /// Uses the API directly to guarantee correct neighbor encoding.
    ///
    /// Sprite layout in Tilemap_color{N}.png (64x64 grid, left side = flat ground):
    ///   Row 0: sp0=TL_corner  sp1=Top_edge   sp2=TR_corner  sp3=notch_BR  (piece 13)
    ///   Row 1: sp8=Left_edge  sp9=Center     sp10=Right_edge sp11=notch_BL (piece 14)
    ///   Row 2: sp16=BL_corner sp17=Bot_edge  sp18=BR_corner  sp19=notch_TR (piece 15)
    ///   Row 3: sp24=L_endcap  sp25=H_thin    sp26=R_endcap   sp27=notch_TL (piece 16)
    /// </summary>
    public static class RuleTileGenerator
    {
        private static readonly string[] ATLAS_PATHS = new string[] {
            null, // index 0 unused
            "Assets/PlaceholderArt/Terrain/Tileset/Tilemap_color1.png",
            "Assets/PlaceholderArt/Terrain/Tileset/Tilemap_color2.png",
            "Assets/PlaceholderArt/Terrain/Tileset/Tilemap_color3.png",
            "Assets/PlaceholderArt/Terrain/Tileset/Tilemap_color4.png",
            "Assets/PlaceholderArt/Terrain/Tileset/Tilemap_color5.png",
        };

        private const string OUTPUT_DIR = "Assets/PlaceholderArt/Terrain/Tileset/Rule Tiles";

        // Neighbor constraint values (from RuleTile.TilingRuleOutput.Neighbor)
        private const int DC = 0;   // Don't care
        private const int TH = 1;   // This (same tile)
        private const int NT = 2;   // NotThis (different/empty)

        [MenuItem("Tools/Terrain/Generate FlatGround RuleTiles (API)")]
        public static void GenerateAllFlatGroundRuleTiles()
        {
            if (!AssetDatabase.IsValidFolder(OUTPUT_DIR))
            {
                AssetDatabase.CreateFolder("Assets/PlaceholderArt/Terrain/Tileset", "Rule Tiles");
            }

            for (int color = 1; color <= 1; color++)
            {
                GenerateFlatGroundRuleTile(color);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("RuleTileGenerator: Done! Check the Rule Tiles folder.");
        }

        private static void GenerateFlatGroundRuleTile(int colorNum)
        {
            string atlasPath = ATLAS_PATHS[colorNum];
            string assetName = "FlatGround_color" + colorNum;
            string assetPath = OUTPUT_DIR + "/" + assetName + ".asset";

            // Load all sprites from the atlas, indexed by their sprite number
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(atlasPath);
            Dictionary<int, Sprite> sp = new Dictionary<int, Sprite>();

            foreach (Object asset in allAssets)
            {
                if (asset is Sprite sprite)
                {
                    string spName = sprite.name;
                    int lastUnderscore = spName.LastIndexOf('_');
                    if (lastUnderscore >= 0)
                    {
                        string indexStr = spName.Substring(lastUnderscore + 1);
                        int idx;
                        if (int.TryParse(indexStr, out idx))
                        {
                            sp[idx] = sprite;
                        }
                    }
                }
            }

            if (sp.Count < 28)
            {
                Debug.LogError("RuleTileGenerator: Only found " + sp.Count + " sprites in " + atlasPath);
                return;
            }

            Debug.Log("RuleTileGenerator: Loaded " + sp.Count + " sprites from " + atlasPath);

            // Load existing asset to preserve GUID, or create new
            RuleTile ruleTile = AssetDatabase.LoadAssetAtPath<RuleTile>(assetPath);
            if (ruleTile == null)
            {
                ruleTile = ScriptableObject.CreateInstance<RuleTile>();
                AssetDatabase.CreateAsset(ruleTile, assetPath);
            }

            ruleTile.m_DefaultSprite = sp[9]; // Center
            ruleTile.m_DefaultColliderType = Tile.ColliderType.None;
            ruleTile.m_TilingRules = new List<RuleTile.TilingRule>();

            // ============================================================
            // Rules in priority order (first match wins).
            // Each rule specifies all 8 neighbors: TL, T, TR, L, R, BL, B, BR
            // DC=don't care, TH=This, NT=NotThis
            // ============================================================

            // --- ISLAND: all 4 cardinals = NotThis ---
            //                              TL  T   TR  L   R   BL  B   BR
            AddRule(ruleTile, sp[25],       DC, NT, DC, NT, NT, DC, NT, DC);

            // --- ENDCAPS: 3 cardinals NotThis, 1 This ---
            AddRule(ruleTile, sp[24],       DC, NT, DC, NT, TH, DC, NT, DC); // L_endcap
            AddRule(ruleTile, sp[26],       DC, NT, DC, TH, NT, DC, NT, DC); // R_endcap
            AddRule(ruleTile, sp[0],        DC, NT, DC, NT, NT, DC, TH, DC); // Top endcap
            AddRule(ruleTile, sp[16],       DC, TH, DC, NT, NT, DC, NT, DC); // Bot endcap

            // --- OUTER CORNERS: 2 adjacent cardinals NotThis ---
            AddRule(ruleTile, sp[0],        DC, NT, DC, NT, TH, DC, TH, DC); // TL corner
            AddRule(ruleTile, sp[2],        DC, NT, DC, TH, NT, DC, TH, DC); // TR corner
            AddRule(ruleTile, sp[16],       DC, TH, DC, NT, TH, DC, NT, DC); // BL corner
            AddRule(ruleTile, sp[18],       DC, TH, DC, TH, NT, DC, NT, DC); // BR corner

            // --- THIN STRIPS: 2 opposing cardinals NotThis ---
            AddRule(ruleTile, sp[25],       DC, NT, DC, TH, TH, DC, NT, DC); // H_thin
            AddRule(ruleTile, sp[9],        DC, TH, DC, NT, NT, DC, TH, DC); // V_thin (center approx)

            // --- EDGES: 1 cardinal NotThis ---
            AddRule(ruleTile, sp[1],        DC, NT, DC, TH, TH, DC, TH, DC); // Top edge
            AddRule(ruleTile, sp[17],       DC, TH, DC, TH, TH, DC, NT, DC); // Bot edge
            AddRule(ruleTile, sp[8],        DC, TH, DC, NT, TH, DC, TH, DC); // Left edge
            AddRule(ruleTile, sp[10],       DC, TH, DC, TH, NT, DC, TH, DC); // Right edge

            // --- INNER CORNERS (notches): all 4 cardinals This, 1 diagonal NotThis ---
            AddRule(ruleTile, sp[3],        DC, TH, DC, TH, TH, DC, TH, NT); // notch_BR
            AddRule(ruleTile, sp[11],       DC, TH, DC, TH, TH, NT, TH, DC); // notch_BL
            AddRule(ruleTile, sp[19],       DC, TH, NT, TH, TH, DC, TH, DC); // notch_TR
            AddRule(ruleTile, sp[27],       NT, TH, DC, TH, TH, DC, TH, DC); // notch_TL

            // --- CENTER: all 4 cardinals This (catch-all) ---
            AddRule(ruleTile, sp[9],        DC, TH, DC, TH, TH, DC, TH, DC); // Center

            EditorUtility.SetDirty(ruleTile);
            Debug.Log("RuleTileGenerator: Created " + assetName + " with " + ruleTile.m_TilingRules.Count + " rules");
        }

        /// <summary>
        /// Add a rule with explicit 8-neighbor values in order: TL, T, TR, L, R, BL, B, BR.
        /// Uses RuleTile's ApplyNeighbors API with the standard 3x3 positions.
        /// </summary>
        private static void AddRule(RuleTile tile, Sprite sprite,
            int tl, int t, int tr, int l, int r, int bl, int b, int br)
        {
            RuleTile.TilingRule rule = new RuleTile.TilingRule();
            rule.m_Sprites = new Sprite[] { sprite };
            rule.m_Output = RuleTile.TilingRuleOutput.OutputSprite.Single;
            rule.m_ColliderType = Tile.ColliderType.None;
            rule.m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed;

            // Build dictionary with only non-zero (non-don't-care) entries.
            // ApplyNeighbors sets m_NeighborPositions and m_Neighbors from the dict.
            Dictionary<Vector3Int, int> neighbors = new Dictionary<Vector3Int, int>();
            if (tl != 0) neighbors.Add(new Vector3Int(-1, 1, 0), tl);
            if (t  != 0) neighbors.Add(new Vector3Int(0, 1, 0), t);
            if (tr != 0) neighbors.Add(new Vector3Int(1, 1, 0), tr);
            if (l  != 0) neighbors.Add(new Vector3Int(-1, 0, 0), l);
            if (r  != 0) neighbors.Add(new Vector3Int(1, 0, 0), r);
            if (bl != 0) neighbors.Add(new Vector3Int(-1, -1, 0), bl);
            if (b  != 0) neighbors.Add(new Vector3Int(0, -1, 0), b);
            if (br != 0) neighbors.Add(new Vector3Int(1, -1, 0), br);

            rule.ApplyNeighbors(neighbors);
            tile.m_TilingRules.Add(rule);
        }
    }
}
