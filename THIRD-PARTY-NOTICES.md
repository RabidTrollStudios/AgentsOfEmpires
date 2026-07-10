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

### Tiny Swords

- **Location:** `UnityRTS/RTS/Assets/Tiny Swords/`
- **What it is:** 2D sprite art used for the game's visual front-end (buildings, pawns
  and resources, terrain, UI elements, particle FX).
- **Author / source:** **[VERIFY]** — record the creator and where it was obtained
  (e.g. asset store, itch.io, direct license).
- **License:** **[VERIFY]** — confirm the asset's license and whether it permits
  redistribution in a public repository. If the license does **not** permit
  redistribution, the art should be removed from the repo and documented as a
  separately-obtained dependency instead.

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
