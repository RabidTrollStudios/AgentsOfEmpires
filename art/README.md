# Art — Agents of Empires

This is the **source of truth** for the game's art assets: original, openly
licensed art (MIT — see [LICENSE](LICENSE)) generated with self-hosted
open-source AI models and/or hand-authored. Nothing here is derived from
proprietary asset-store content.

The generation pipeline (ComfyUI + SDXL/Flux, ControlNet, AnimateDiff, LoRAs)
and the workflow for keeping art consistent across units is documented in
[`../docs/art-generation.md`](../docs/art-generation.md).

## Art direction

Flat-vector, smooth, cartoonish — cute, readable characters that make matches
fun to watch. Bold silhouettes, limited palette, clean cel shading, top-down
3/4 view. Consistency across the roster is maintained with a shared style
prompt fragment and a style LoRA (see the generation guide).

## Layout

```
art/
├── LICENSE              MIT + AI-training clause (this folder is maximally open)
├── README.md            this file
├── style/               shared style assets — the house style fragment, palette,
│                        reference sheets, and any trained style LoRA metadata
├── units/               per-unit art + a manifest.json per unit (see _template.json)
│   ├── _template.json   copy this when adding a new unit
│   ├── pawn/
│   ├── warrior/
│   ├── archer/
│   ├── lancer/
│   ├── monk/
│   └── ...
└── buildings/           base, barracks, archery, tower, monastery, mine
```

Each unit/building folder holds its **source frames** (the raw generated/edited
PNGs), its assembled **sprite sheet**, and a **`manifest.json`** capturing
exactly how it was generated so any frame can be reproduced later.

## Source vs. deployed (important)

- **This `art/` folder is the committed, tool-agnostic SOURCE.** Master frames,
  sprite sheets, and manifests live here.
- The Unity project consumes **deployed copies** under
  `UnityRTS/RTS/Assets/` (the import target). Treat those as build output of the
  art pipeline, not as the place to edit art.
- When you finalize a sprite here, deploy it into the Unity Assets folder (a
  copy step — see the generation guide). Keeping source and imported copies
  separate mirrors how the code side keeps `AgentSDK` source vs. the Unity
  `Plugins/` copy.

## Adding a new unit (checklist)

1. `cp art/units/_template.json art/units/<newunit>/manifest.json` and fill it in.
2. Generate frames using the shared style fragment + style LoRA so it matches
   the existing roster (see [`../docs/art-generation.md`](../docs/art-generation.md) §4).
3. Commit the source frames, the sprite sheet, and the manifest here.
4. Deploy the finished sprite sheet into the Unity Assets folder.
5. If you added third-party inputs of any kind, update
   [`../THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md) (normally you won't —
   this pipeline is all self-hosted/open).
