# Third-Party Notices

Agents of Empires (the code) is licensed under the [MIT License](LICENSE). However,
this repository also **bundles third-party assets and packages** that are covered by
their **own** licenses. Those licenses remain in effect for the respective files —
the MIT license on this project's code does **not** relicense them.

This file inventories the bundled third-party components. Where a component ships its
own license file, that file is authoritative.

> **⚠️ Maintainer action required.** Some entries below are marked **[VERIFY]** — the
> exact license and permitted redistribution terms need to be confirmed and filled in
> by a maintainer who knows where each asset was obtained. Do not treat a **[VERIFY]**
> entry as legal clearance to redistribute.

---

## Art assets

### Placeholder art (formerly Tiny Swords)

- **Location:** `UnityRTS/RTS/Assets/PlaceholderArt/`
- **What it is:** simple, self-generated placeholder sprites (circles for units,
  squares for buildings, triangles for trees, flat UI panels, etc.) used by the Unity
  visual front-end.
- **License:** authored for this project — covered by the repository's [MIT License](LICENSE).
  No third-party license applies.

The Unity front-end originally used **Tiny Swords** art from the Unity Asset Store, whose
EULA does not permit redistributing the raw asset files in a public repository. That art
has been **replaced** in the working tree with the placeholder sprites above, so the
current checkout contains no Asset Store content.

> **⚠️ History note.** The original Tiny Swords files still exist in earlier git
> **history** (they were only replaced going forward). A history rewrite to purge them
> from all commits is tracked separately and is required before the repository can be
> considered fully clean of the licensed asset. The engine, SDK, tests, and headless
> simulation never depended on this art — only the Unity visualization did.

---

## Fonts

### Kingthings Foundation

- **Location:** `UnityRTS/RTS/Assets/Fonts/KingsThings/Kingthings Foundation.ttf`
- **Author / source:** Kevin King, <https://www.kingthingsfonts.co.uk>
- **License:** free for any use, per the Kingthings EULA — the only condition is that the
  original `.TTF` file must not be modified. This permits redistribution (including in a
  public repository) and imposes no restriction on use as ML-training input. The bundled
  file is unmodified. *(The paid CheapProFonts versions of Kingthings fonts carry a
  separate EULA and are not used here.)*

### LiberationSans (TextMesh Pro)

- **Location:** `UnityRTS/RTS/Assets/TextMesh Pro/Fonts/LiberationSans.ttf`
- **License:** [SIL Open Font License](https://scripts.sil.org/OFL) — see the bundled
  `LiberationSans - OFL.txt`. Redistributable.

---

## Unity packages & plugins

### MobileDependencyResolver

- **Location:** `UnityRTS/RTS/Assets/MobileDependencyResolver/`
- **License:** ships its own license file at
  `UnityRTS/RTS/Assets/MobileDependencyResolver/Editor/LICENSE` — that file is
  authoritative for this component.

### Unity Engine packages

- **Location:** managed by the Unity Package Manager (`UnityRTS/RTS/Packages/`,
  `Library/PackageCache/`).
- **License:** governed by the
  [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license)
  and the individual package licenses, as applicable. These are not redistributed by
  this project beyond what Unity's own terms allow.

---

## .NET / NuGet dependencies

Build- and test-time dependencies (e.g. the test framework and .NET runtime
libraries) are restored via NuGet and are governed by their respective licenses
(commonly MIT or the .NET Foundation licenses). They are not vendored into this
repository.

---

## How to update this file

When you add a third-party asset or package to the repository:

1. Add an entry here with its location, source, and license.
2. Include or reference the component's own license file where one exists.
3. Confirm the license actually permits redistribution in a public repo before
   committing the asset.
