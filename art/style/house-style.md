# House Style — Agents of Empires

The shared art direction and prompt kit for the whole roster. Paste the **style
fragment** into every generation; append the **per-asset prompt** for the thing
you're making. Holding the style fragment + checkpoint + sampler + seed fixed and
changing only the per-asset line is what keeps the roster coherent (see
[`../../docs/art-generation.md`](../../docs/art-generation.md) §4).

This is **original art direction.** It describes a *feel* and our *own* unit
designs. It is not derived from, and must not reproduce, any third-party asset
pack's specific sprites, proportions, color values, or designs. The look below was
developed as an original target; where it was inspired by the general idiom of
soft-shaded top-down strategy art, it takes only the idiom (camera angle, shading
approach, chibi proportion language), never specific assets.

---

## The look in one paragraph

Charming top-down strategy-game creatures, rendered in a **soft, rounded,
hand-painted 2.5D style**: a **high-oblique 3/4 top-down camera** (you see the tops
of helmets, shoulders, and roofs, tilted toward the viewer), **soft volumetric
shading with a single consistent top-left light** (gentle gradient form-shadows,
soft ambient occlusion pooling in the crevices, a crisp rim/top highlight),
**chibi proportions** (big rounded head/helmet, small stout body, short stubby
limbs), a **thin dark desaturated outline** (not a heavy black comic ink), and a
**cohesive muted palette** (teal, slate-blue, warm sand, soft green). Cute comes
from **roundness and proportion**, not from exaggerated facial mugging — faces are
simple and small (a nose, a dot of eyes, maybe a mustache). Every creature must
read **instantly from above at tiny scale**, because matches are watched from a
zoomed-out top-down spectator view.

---

## Style fragment (paste into EVERY generation, positive prompt, first)

```
soft hand-painted 2D game sprite, high-angle 3/4 top-down view, chibi proportions
with large rounded head and small stout body and short stubby limbs, soft
volumetric cel shading, single soft light from top-left, gentle gradient form
shadows, soft ambient occlusion, crisp rim highlight, thin dark desaturated
outline, cohesive muted palette of teal slate-blue warm-sand and soft-green,
bold readable silhouette, cute by proportion not by expression, single character
centered, transparent background, no cast ground shadow, consistent line weight,
game asset, clean edges
```

## Negative prompt (paste into EVERY generation)

```
side view, front orthographic view, flat orthographic, isometric grid, realistic
human proportions, tall lanky body, heavy black comic ink outline, cel-shaded
anime, harsh cast shadow on ground, busy background, scenery, multiple characters,
text, watermark, signature, logo, oversaturated neon colors, gradient background,
photorealistic, 3d render, blurry, low contrast, extra limbs, deformed hands,
exaggerated screaming face, bulging eyes
```

> The negatives are doing real work here: `side view` / `front orthographic` kill
> the paper-doll poses; `heavy black comic ink outline` and `exaggerated screaming
> face` / `bulging eyes` kill the previous wrong direction; `harsh cast shadow`
> keeps backgrounds clean for compositing; `isometric grid` keeps it the softer
> oblique look rather than a hard iso tile.

---

## Team color (the recolor system)

The game is **1v1, two AI teams, same sculpt per unit** — team identity must read
at a glance from above. Design each unit so **one cloth zone** carries the team
color: the **tunic / tabard / cloth wrap** on units, the **roof / banner** on
buildings. Everything else (skin, metal, wood, leather) stays neutral so the
single team-color zone recolors cleanly.

- Generate the **master** in a **neutral key color** (a mid desaturated blue works
  as the key, like the pygame projects' `#3a7bd5` approach), then recolor per team
  at load time (hue-shift / palette-swap the key zone only).
- Keep the team-color zone **large and high-contrast** against the neutral body so
  it reads at 1-tile scale. Double it where you can (tunic **and** a shield face,
  roof **and** a pennant).
- In prompts, name the team zone explicitly ("blue tunic", "blue-slate tiled
  roof") so it lands in a predictable, maskable region.

---

## State readability (spectator game)

No human plays this — two agents fight and people **watch**. Each unit needs
poses that telegraph its **state** from above:

- **idle** — relaxed, weapon lowered/shouldered, slight breathing bob (2–4 frames)
- **walk** — clear stride, weapon carried (6–8 frames)
- **attack / act** — the signature action reads big: warrior's overhead chop,
  archer's draw-and-loose, lancer's braced thrust, monk's raised-hands heal glow,
  pawn's mining swing / hammer tap (4–6 frames)

Pose the frames with **ControlNet OpenPose** skeletons, run through **AnimateDiff**
for frame-to-frame coherence (guide §3). The 3/4 camera means skeletons should be
posed as seen **from a high angle**, not flat side-on.

---

## Palette anchors (approximate, muted and cohesive)

Use these as targets, not exact values — the point is a **unified desaturated
range**, nothing neon. Light is always top-left.

| Role | Hue family | Note |
|---|---|---|
| Team key | desaturated blue `#3a7bd5`-ish | the recolorable zone; swap per team |
| Metal / armor | cool slate grey `#8793a0` → `#59636f` | soft steel, top highlight |
| Skin | warm tan `#d99b63` → shadow `#b2733f` | simple, 2 tones + highlight |
| Wood / hafts | mid brown `#6d4423` → `#4a3017` | |
| Leather / straps | warm dark brown | |
| Gold trim | muted gold `#e8b94a` → `#a8781f` | accents only, sparingly |
| Outline | very dark desaturated brown-black `#241c17` | thin, never pure black |
| Heal / magic FX | soft mint-green glow | monk only, additive |

---

## Proportion + camera rules (the chibi recipe)

- **Head/helmet ≈ 40–45% of total height.** Big and round. This is the main cute
  lever — not the face.
- **Body:** short and stout, egg-shaped torso, small. **Limbs:** short stubby
  arms and legs, round mitten hands, little oval feet.
- **Face:** small and simple — a soft nose, two small eyes (dots or short dashes),
  optional mustache/beard for older units. **No big expressions.** The charm is the
  silhouette and the top-down roundness.
- **Camera:** high 3/4 top-down. Show the **top plane** of the helmet/head and the
  **tops of the shoulders**; the face is foreshortened and tucked under the helmet
  brim. Feet sit close together under the body.
- **Shading:** soft top-left light. Form shadow is a gentle gradient, not a hard
  cel edge; add a soft occlusion darkening where parts meet (under the helmet, under
  the arms, between the legs) and a small crisp highlight on the top-most rounded
  surfaces. This soft-3D shading is the "realistic" half of the brief; the chibi
  proportion is the cute half.

---

See [`prompts-units.md`](prompts-units.md) and
[`prompts-buildings.md`](prompts-buildings.md) for the per-asset prompt lines, and
each unit's `manifest.json` for the committed seed/sampler/LoRA record.
