"""
Generate an editable RuleTile .asset for Tiny Swords terrain.

Produces ~20 clean rules using don't-care for irrelevant neighbors,
so the Unity RuleTile inspector shows a small, easy-to-edit list.
Rules are ordered most-specific first (first match wins).

Spritesheet layout (left half of Tilemap_color{N}.png):
  Row 0:  sp0=TL_corner   sp1=Top_edge    sp2=TR_corner   sp3=notch_BR
  Row 1:  sp8=Left_edge   sp9=Center      sp10=Right_edge  sp11=notch_BL
  Row 2:  sp16=BL_corner  sp17=Bot_edge   sp18=BR_corner   sp19=notch_TR
  Row 3:  sp24=L_endcap   sp25=H_thin     sp26=R_endcap    sp27=notch_TL

Each sprite's neighbors are labeled in comments for easy cross-reference.
"""

import hashlib
import os


# NOTE: Sprite fileIDs come from nameFileIdTable at the bottom of each .png.meta.
# If sprites are re-sliced in Unity, nameFileIdTable gets new IDs while
# internalIDToNameTable at the top may keep stale ones. Always use nameFileIdTable.
COLOR_DATA = {
    1: {
        "guid": "027fd9e06196b4e62a8e40527bfbf3a9",
        "sprites": {
            0: 4684625920699962428, 1: -2801873246907429078,
            2: -1844109271, 3: -1483446518,
            8: -529755566, 9: -985832654, 10: 1273371556, 11: -438687076,
            16: 1611736068, 17: -160155248, 18: 659350005, 19: 196836861,
            24: 1599712627, 25: 1292135869, 26: -55024715, 27: 157780901,
        }
    },
}

RULETILE_SCRIPT_GUID = "9d1514134bc4fbd41bb739b1b9a49231"

# Neighbor values
THIS = 1
NOT = 2


def encode_hex(values):
    return "".join(f"{v:02x}000000" for v in values)


def sprite_ref(file_id, guid):
    return f"{{fileID: {file_id}, guid: {guid}, type: 3}}"


def make_rule(rule_id, positions, values, sprite_str):
    """positions: list of (x,y) tuples; values: list of THIS/NOT matching positions."""
    hex_str = encode_hex(values)
    lines = [
        f"  - m_Id: {rule_id}",
        "    m_Sprites:",
        f"    - {sprite_str}",
        "    m_GameObject: {fileID: 0}",
        "    m_MinAnimationSpeed: 1",
        "    m_MaxAnimationSpeed: 1",
        "    m_PerlinScale: 0.5",
        "    m_Output: 0",
        "    m_ColliderType: 0",
        "    m_RandomTransform: 0",
        f"    m_Neighbors: {hex_str}",
        "    m_NeighborPositions:",
    ]
    for (x, y) in positions:
        lines.append(f"    - {{x: {x}, y: {y}, z: 0}}")
    lines.append("    m_RuleTransform: 0")
    return "\n".join(lines)


# Cardinal positions
P_T  = (0, 1)
P_L  = (-1, 0)
P_R  = (1, 0)
P_B  = (0, -1)

# Diagonal positions
P_TL = (-1, 1)
P_TR = (1, 1)
P_BL = (-1, -1)
P_BR = (1, -1)

CARDINALS = [P_T, P_L, P_R, P_B]


def generate_flat_ground_ruletile(color_num):
    data = COLOR_DATA[color_num]
    guid = data["guid"]
    sprites = data["sprites"]

    def sp(idx):
        return sprite_ref(sprites[idx], guid)

    rules = []
    rid = [0]

    def add(positions, values, sprite_idx, comment=""):
        rules.append(make_rule(rid[0], positions, values, sp(sprite_idx)))
        rid[0] += 1

    # =====================================================================
    # PRIORITY 1: Inner corners (notches)
    # All 4 cardinals = This, one specific diagonal = NotThis
    # Must come before Center rule (which also has all 4 cardinals = This)
    # =====================================================================

    # notch_TL: TL diagonal missing (sp27)
    add([P_TL, P_T, P_L, P_R, P_B],
        [NOT,  THIS, THIS, THIS, THIS], 27)

    # notch_TR: TR diagonal missing (sp19)
    add([P_T, P_TR, P_L, P_R, P_B],
        [THIS, NOT,  THIS, THIS, THIS], 19)

    # notch_BL: BL diagonal missing (sp11)
    add([P_T, P_L, P_R, P_BL, P_B],
        [THIS, THIS, THIS, NOT,  THIS], 11)

    # notch_BR: BR diagonal missing (sp3)
    add([P_T, P_L, P_R, P_B, P_BR],
        [THIS, THIS, THIS, THIS, NOT], 3)

    # =====================================================================
    # PRIORITY 2: Center (all 4 cardinals = This, diags don't care)
    # =====================================================================

    # Center (sp9)
    add(CARDINALS, [THIS, THIS, THIS, THIS], 9)

    # =====================================================================
    # PRIORITY 3: Edges (exactly 1 cardinal = NotThis, rest = This)
    # =====================================================================

    # Top edge: T=Not (sp1)
    add(CARDINALS, [NOT, THIS, THIS, THIS], 1)

    # Bottom edge: B=Not (sp17)
    add(CARDINALS, [THIS, THIS, THIS, NOT], 17)

    # Left edge: L=Not (sp8)
    add(CARDINALS, [THIS, NOT, THIS, THIS], 8)

    # Right edge: R=Not (sp10)
    add(CARDINALS, [THIS, THIS, NOT, THIS], 10)

    # =====================================================================
    # PRIORITY 4: Outer corners (2 adjacent cardinals = NotThis)
    # =====================================================================

    # TL corner: T=Not, L=Not (sp0)
    add(CARDINALS, [NOT, NOT, THIS, THIS], 0)

    # TR corner: T=Not, R=Not (sp2)
    add(CARDINALS, [NOT, THIS, NOT, THIS], 2)

    # BL corner: B=Not, L=Not (sp16)
    add(CARDINALS, [THIS, NOT, THIS, NOT], 16)

    # BR corner: B=Not, R=Not (sp18)
    add(CARDINALS, [THIS, THIS, NOT, NOT], 18)

    # =====================================================================
    # PRIORITY 5: Thin strips (2 opposing cardinals = NotThis)
    # =====================================================================

    # H thin: T=Not, B=Not, L=This, R=This (sp25)
    add(CARDINALS, [NOT, THIS, THIS, NOT], 25)

    # V thin: L=Not, R=Not, T=This, B=This (sp9 center, no dedicated sprite)
    add(CARDINALS, [THIS, NOT, NOT, THIS], 9)

    # =====================================================================
    # PRIORITY 6: Endcaps (3 cardinals = NotThis)
    # =====================================================================

    # L endcap: only R=This (sp24)
    add(CARDINALS, [NOT, NOT, THIS, NOT], 24)

    # R endcap: only L=This (sp26)
    add(CARDINALS, [NOT, THIS, NOT, NOT], 26)

    # T endcap: only B=This (sp0 TL_corner approx)
    add(CARDINALS, [NOT, NOT, NOT, THIS], 0)

    # B endcap: only T=This (sp16 BL_corner approx)
    add(CARDINALS, [THIS, NOT, NOT, NOT], 16)

    # =====================================================================
    # PRIORITY 7: Island (all 4 cardinals = NotThis)
    # =====================================================================

    # Island (sp25 H_thin approx)
    add(CARDINALS, [NOT, NOT, NOT, NOT], 25)

    # Build YAML
    default_sprite = sp(9)
    rules_yaml = "\n".join(rules)

    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {RULETILE_SCRIPT_GUID}, type: 3}}
  m_Name: FlatGround_color{color_num}
  m_EditorClassIdentifier:
  m_DefaultSprite: {default_sprite}
  m_DefaultGameObject: {{fileID: 0}}
  m_DefaultColliderType: 0
  m_TilingRules:
{rules_yaml}
"""


def main():
    output_dir = os.path.join(
        os.path.dirname(__file__),
        "Assets", "Tiny Swords", "Terrain", "Tileset", "Rule Tiles"
    )
    os.makedirs(output_dir, exist_ok=True)

    name = "FlatGround_color1"
    asset_path = os.path.join(output_dir, f"{name}.asset")
    meta_path = os.path.join(output_dir, f"{name}.asset.meta")

    content = generate_flat_ground_ruletile(1)
    with open(asset_path, "w", newline="\n") as f:
        f.write(content)
    print(f"Created: {asset_path}")

    if not os.path.exists(meta_path):
        meta_guid = hashlib.md5(f"ruletile_{name}".encode()).hexdigest()
        with open(meta_path, "w", newline="\n") as f:
            f.write(f"fileFormatVersion: 2\nguid: {meta_guid}\n"
                    "NativeFormatImporter:\n  externalObjects: {}\n"
                    "  mainObjectFileID: 11400000\n  userData:\n"
                    "  assetBundleName:\n  assetBundleVariant:\n")
        print(f"Created: {meta_path}")
    else:
        print(f"Kept existing: {meta_path}")

    print(f"\n21 rules generated. Open in Unity RuleTile inspector to edit.")
    print("Rule order (first match wins):")
    print("  1-4:   Inner corners (notch_TL, notch_TR, notch_BL, notch_BR)")
    print("  5:     Center")
    print("  6-9:   Edges (Top, Bottom, Left, Right)")
    print("  10-13: Outer corners (TL, TR, BL, BR)")
    print("  14-15: Thin strips (Horizontal, Vertical)")
    print("  16-19: Endcaps (Left, Right, Top, Bottom)")
    print("  20:    Island")


if __name__ == "__main__":
    main()
