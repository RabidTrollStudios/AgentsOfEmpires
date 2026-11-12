# Per-Unit Prompts — Agents of Empires

For each unit: **style fragment** (from [`house-style.md`](house-style.md)) **+**
the **unit line** below, then the shared **negative prompt**. Generate the master
in the neutral key (blue) team color; recolor per team at load.

Roster stats (from `UnityRTS/AgentSDK/GameConstants.cs`) are noted so the silhouette
matches the role. The rock-paper-scissors triangle is **Warrior → Archer → Lancer →
Warrior**; design the three combat units so their silhouettes read as a clear
triangle (bulky / lithe-ranged / long-reach-mounted).

---

## PAWN — worker (200 hp, gathers gold, builds everything, cheapest)

The humble everyman. Smallest and softest of the roster; no armor, just work
clothes. Reads as "harmless little laborer."

```
a small round chibi peasant worker, simple tunic and soft cloth cap, a big round
head, carrying a wooden-handled pickaxe or builder's hammer, rolled sleeves,
little work gloves, blue cloth apron or tunic as the team color, friendly simple
face, no armor, humble and soft, the smallest unit
```

- **Team zone:** blue tunic / apron.
- **Signature action:** mining swing (down-chop into a mine) and a build hammer-tap.
- **Notes:** keep it clearly weaker/rounder than the warriors so a watcher never
  mistakes a pawn for a fighter.

---

## WARRIOR — heavy melee (1500 hp, dmg 50, range 1, beats Archer)

The tank. Bulkiest silhouette on the field — wide rounded pauldrons, a big helmet,
a stout heavy body. Toughness comes from **mass and rounded armor volume**, not
from an angry face. A sturdy, dependable bruiser.

```
a stout heavy chibi knight, big rounded dome helmet with a small plume, broad
round pauldrons, a short wide armored body, soft steel plate with gentle top-left
highlights, a blue tabard over the chest plate as the team color, holding a
short broadsword and a small round shield, planted wide stance, a calm simple
face under the helmet brim, the bulkiest tankiest unit
```

- **Team zone:** blue tabard (and the shield face — double it).
- **Signature action:** a big overhead sword chop.
- **Notes:** widest, heaviest silhouette of the three fighters. Soft rounded plate,
  NOT hard angular slabs, NOT bare barbarian muscle.

---

## ARCHER — ranged (600 hp, dmg 38, range 9, fragile, beats Lancer)

The lithe sharpshooter. Slender and light next to the warrior — leather, not plate.
A hood or soft cap, a longbow, a quiver. Reads as "fast and fragile, hits from far."

```
a slim light chibi archer, soft hood or feathered cap, light leather jerkin, a
blue cloth hood or scarf as the team color, holding a tall longbow, a quiver of
arrows on the back, lithe and quick, lightly built, calm focused simple face,
clearly lighter and more agile than the armored knight
```

- **Team zone:** blue hood / scarf.
- **Signature action:** draw the bow and loose (arrow leaves the hand).
- **Notes:** the thinnest, least-armored combat silhouette. Tall bow helps it read
  from above. Fragile look telegraphs its low HP.

---

## LANCER — reach / cavalry (900 hp, dmg 42, range 2.5, mobile, beats Warrior)

The reach fighter. The engine has the **Tower** train the Lancer, and its 2.5 range
+ mobility read as a mounted or long-reach spearman. Give it the longest weapon
silhouette and a sense of speed. (Mount optional — a small sturdy pony reads great
from above and sells "cavalry"; a foot pikeman with a very long lance also works.
Pick one and keep it consistent.)

```
a chibi lancer with a very long spear or lance held forward, a light open helmet
with a small blue pennant as the team color, a medium build between the heavy
knight and the light archer, a blue tunic, poised and mobile, long reach, braced
forward stance, calm determined simple face; OPTION: riding a small round sturdy
pony seen from a high 3/4 top-down angle
```

- **Team zone:** blue pennant on the lance + blue tunic.
- **Signature action:** a braced forward lance thrust.
- **Notes:** the **long lance** is the read — give it the longest weapon of the
  roster so the triangle (bulky warrior / lithe archer / long-reach lancer) is
  obvious from above. Decide mounted-vs-foot once, then lock it.

---

## MONK — healer / caster (400 hp, no attack, 100 mana, heals allies, heal range 4)

The support mystic. Robed, hooded, peaceful — no weapon, a staff or holy symbol
instead. A soft **mint-green heal glow** is its signature (the only magic FX in the
palette). Reads as "fragile, precious, keep it safe."

```
a gentle chibi monk in a long hooded robe, a deep soft cowl, a wooden staff topped
with a small glowing orb, a blue sash or robe trim as the team color, hands raised
in a soft mint-green healing glow, no weapon, peaceful serene simple face partly
shadowed by the hood, soft and fragile, a supportive mystic
```

- **Team zone:** blue sash / robe trim.
- **Signature action:** raise hands, emit a soft mint-green heal aura toward an ally.
- **Notes:** the only unit with magic FX. Keep the robe silhouette distinct from the
  fighters (flowing hem, no armor). Soft glow = additive mint green, used sparingly.

---

## Triangle silhouette check

Before finalizing, line the three fighters up and squint:

- **Warrior** = widest, roundest, bulkiest (heavy helmet + pauldrons).
- **Archer** = thinnest, tallest-weapon (longbow), least armor.
- **Lancer** = longest horizontal reach (lance), medium build, (optional pony).

If you can't tell them apart from above at tiny scale, push the silhouettes further
apart — the spectator read depends on it.
