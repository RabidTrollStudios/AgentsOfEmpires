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
- **Source:** Unity Asset Store —
  <https://assetstore.unity.com/packages/2d/environments/tiny-swords-352566>
- **License:** [Unity Asset Store EULA](https://unity.com/legal/as-terms).

> ### ⚠️ Redistribution is NOT permitted — this art must be removed before public release
>
> The Unity Asset Store EULA (Appendix 1, **§2.2.1.1(d)**) prohibits the END-USER from
> distributing an Asset except as **embedded within a compiled "Licensed Product"** —
> i.e. baked into a shipped application where the Asset "does not comprise a substantial
> portion" of it (§2.2.1(a)–(b)). All rights not expressly granted are reserved
> (§9.2).
>
> Committing the **raw sprite files to a public repository** is *not* an embedded
> Licensed Product — it distributes the original, extractable Asset files, which the
> EULA does not permit. **Hosting this art in a public repo is therefore a license
> violation.**
>
> **Required action before this repository is made (or kept) public:**
> 1. Remove the `UnityRTS/RTS/Assets/Tiny Swords/` files from the repository, and
>    purge them from git **history** (not just the latest commit — a deletion commit
>    still leaves the files downloadable from earlier history).
> 2. Add the path to `.gitignore`.
> 3. Document Tiny Swords as a **separately-obtained dependency**: contributors who
>    want the visuals must purchase/download it themselves from the Asset Store link
>    above and place it under `Assets/Tiny Swords/`.
>
> The engine, SDK, tests, and headless simulation do **not** depend on this art — only
> the Unity visualization front-end does — so removing it does not affect building or
> running matches headlessly.
>
> *This note reflects a reading of the EULA text, not formal legal advice; confirm with
> counsel if in doubt.*

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
