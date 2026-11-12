# Per-Building Prompts — Agents of Empires

Same recipe: **style fragment** + the **building line**, then the shared
**negative prompt**. Buildings are **static** assets — Flux Schnell is a good
choice here for top quality + the cleanest license (guide §3d). Generate the master
with a **blue team-color roof/banner**; recolor per team at load.

Footprints (grid tiles, from `GameConstants.cs`) are noted — the art's **relative
mass** should match (the Base is the biggest; the Tower is small). All buildings
share the chibi softness: rounded forms, soft top-left light, thin dark outline,
blue-slate tiled roofs over warm stone/plaster walls.

The team color lives in the **roof tiles + any banner/pennant**, so one zone
recolors.

---

## BASE — 6×4, trains Pawns, 8000 hp (the biggest, the heart of the town)

```
a cozy chibi town hall keep, the largest friendliest building, warm plaster and
stone walls, a big blue-slate tiled roof as the team color, a small flag or banner
on top, a round arched wooden door, a few glowing windows, rounded soft forms,
high 3/4 top-down view, the central hub building
```

- **Team zone:** big blue roof + rooftop banner.
- **Mass:** biggest building on the field. Make it unmistakably the home base.

---

## BARRACKS — 3×3, trains Warriors, 4000 hp

```
a sturdy chibi barracks, a square timber-and-stone building with a blue-slate
roof as the team color, crossed-swords emblem or a weapon rack by the door, a
training-yard feel, solid and martial, rounded soft forms, high 3/4 top-down view
```

- **Team zone:** blue roof. Add a crossed-swords sign to read "makes warriors."

---

## ARCHERY — 3×3, trains Archers, 4000 hp

```
a chibi archery range building, open-fronted wooden lodge with a blue-slate roof
as the team color, a round straw target and a rack of bows out front, lighter and
more open than the barracks, rounded soft forms, high 3/4 top-down view
```

- **Team zone:** blue roof. Target + bows read "makes archers"; keep it visibly
  lighter/airier than the solid barracks.

---

## TOWER — 2×2, trains Lancers + defensive, 3000 hp (small footprint, tall)

The Tower both **trains Lancers** and reads as defense. Small footprint but tall —
let height be its silhouette. (If mounted lancers are chosen, a small stable door
or a pennant hints at the cavalry role.)

```
a small round chibi defensive tower, tall and narrow with a small blue conical
tiled roof as the team color, stone battlements at the top, a small pennant, an
arched doorway at the base, compact footprint but tall, rounded soft forms, high
3/4 top-down view
```

- **Team zone:** blue conical roof + pennant.
- **Mass:** smallest footprint, but tall — the one vertical silhouette.

---

## MONASTERY — 3×3, trains Monks, 3500 hp

```
a serene chibi monastery, a small temple or chapel with a blue-slate domed or
peaked roof as the team color, a glowing round stained-glass window, a soft
mint-green magical shimmer, a quiet holy feel, warm stone walls, rounded soft
forms, high 3/4 top-down view
```

- **Team zone:** blue roof/dome. The faint mint-green shimmer ties it to the Monk's
  heal FX and reads "makes the healer."

---

## MINE — 3×3, gold resource, not team-owned

The Mine is a **neutral resource**, not a team building — so **no team color** on
it. Pawns gather gold from it. Give it a clearly "resource node" look (ore, cart,
rocky mound) so a watcher never confuses it with a team structure.

```
a chibi gold mine, a rocky mound or hillside with a timber-framed mine entrance,
a wooden minecart with glinting gold ore, scattered golden nuggets and a pickaxe,
neutral stone and brown palette with NO team color, clearly a resource node not a
building, rounded soft forms, high 3/4 top-down view
```

- **Team zone:** none — keep it palette-neutral (stone/brown/gold) so it reads as
  shared terrain, not a team's structure.
- **Notes:** the gold glint is the read. Should look obviously different from the
  built structures.

---

## Building set check

Line them up: Base (biggest) → Barracks/Archery/Monastery (mid 3×3, each with a
role-telling emblem) → Tower (small/tall) → Mine (neutral resource). Rooftop blue
should be the first thing the eye catches for team ID; the Mine should be the one
that is obviously *not* blue.
