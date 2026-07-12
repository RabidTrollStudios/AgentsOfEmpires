# Art Generation Guide — Agents of Empires

How to generate all-new, legally-clean unit art for this project using local
(self-hosted) AI, and how to regenerate matching art when new units are added.

This guide targets **few-frame unit animations** (idle / walk / attack loops of a
handful of frames each) plus **static assets** (building icons, portraits, UI).
It's written for the project's hardware target: a Windows desktop with an NVIDIA
GPU (a single 8 GB card such as an RTX 4060 is sufficient; a second card lets you
generate on one while iterating on the other).

---

## 0. Why local / self-hosted (the licensing rationale)

The art must be **freely redistributable** — teachers can use it in class, and it
(and this repo) can be used as AI/ML training data. That rules out every hosted
tool:

- **Adobe Firefly** — its terms explicitly **ban** using output "to create, train,
  test, or otherwise improve any machine learning … systems." Disqualifying.
- **Midjourney** — free tier is non-commercial (CC BY-NC); paid retains a
  perpetual, sublicensable license over your output, so you can't grant a dataset
  user clean rights.
- **DALL·E / OpenAI** — assigns output to you but keeps you inside a corporate ToS
  with content-policy strings attached.

**Self-hosted open models put no company in the loop at all** — nobody can restrict
what you do with the output:

- **SDXL** (Stable Diffusion XL) under **CreativeML OpenRAIL-M**: the license states
  *"Licensor claims no rights in the Output You generate."* You own it. Caveat: the
  license carries **use-restrictions (Attachment A** — no illegal/harmful uses) that
  must be **carried forward** when you redistribute output. Use **SDXL or SD 1.5**
  (SD 3/3.5 use a different, more restrictive license).
- **Flux.1 Schnell** under **Apache 2.0**: the cleanest license — no output
  use-restrictions at all. ⚠️ Use **Schnell only.** Flux.1 **[dev]** and
  **Kontext-dev** are *non-commercial* — do not use them for this project.

### Copyright reality (important, and in your favor)
Purely prompt-generated images are **not copyrightable in the US** (US Copyright
Office, Jan 2025 Part 2 report; SCOTUS declined to revisit, Mar 2, 2026). That is
*fine* here: the art is deliberately released open, so it doesn't need copyright.
If you ever want a sprite to be copyrightable (e.g. to enforce a license on it),
the legal lever is **meaningful human editing** — compositing, retouching,
assembling sprite sheets — which can cross the "sufficient human authorship"
threshold. See the repo's licensing docs for the two-tier plan (open art +
attribution/non-commercial project license).

**Model choice for THIS project:** **SDXL is primary** because the animation-
consistency toolchain (AnimateDiff, ControlNet, IP-Adapter) is mature on the
Stable Diffusion architecture and immature on Flux. Keep **Flux Schnell** for
high-quality *static* assets where its cleaner license is a bonus.

---

## 1. Install ComfyUI (the host app)

ComfyUI is a free, local, node-graph UI that runs SDXL, Flux, AnimateDiff, and
ControlNet. Use the **portable Windows build** — it bundles its own Python so it
won't touch your system.

1. Download the **ComfyUI Windows portable** build from the official releases:
   <https://github.com/comfyanonymous/ComfyUI/releases> (the
   `ComfyUI_windows_portable_nvidia.7z` asset).
2. Extract it somewhere with space (models are large) — e.g.
   `D:\ComfyUI_windows_portable\`.
3. Launch `run_nvidia_gpu.bat`. A browser tab opens at `http://127.0.0.1:8188`.
4. Install **ComfyUI Manager** (handles every other install for you):
   <https://github.com/ltdrdata/ComfyUI-Manager> — follow its README (drop it in
   `ComfyUI\custom_nodes\` or use the one-line installer), then restart ComfyUI.
   A **Manager** button appears in the UI.

> Prefer the Manager over manual `git clone` for all custom nodes below — it
> resolves each node's Python dependencies and handles the restart trigger.

---

## 2. Install the consistency toolchain

All via **Manager → Install Custom Nodes** (search the name, Install, then restart):

| Node pack | What it does |
|---|---|
| **AnimateDiff Evolved** (Kosinkadink) | Motion module — the thing that keeps frames temporally consistent (no flicker). |
| **ComfyUI-Advanced-ControlNet** (Kosinkadink) | ControlNet that plays nicely with AnimateDiff. |
| **ComfyUI-VideoHelperSuite** (Kosinkadink) | Load/save frame sequences & preview animations. |
| **ComfyUI's ControlNet Auxiliary Preprocessors** | Generates pose skeletons / depth maps from reference images (OpenPose etc.). |
| **ComfyUI Impact Pack** *(optional)* | Detailing/upscaling helpers useful for cleaning sprite frames. |

Node pack repos for reference:
- AnimateDiff-Evolved: <https://github.com/Kosinkadink/ComfyUI-AnimateDiff-Evolved>

---

## 3. Download the models

Place each file in the indicated folder under your ComfyUI install.

### 3a. Base checkpoint (required) → `models\checkpoints\`
- An **SDXL** checkpoint. Start with base **SDXL 1.0**
  (`sd_xl_base_1.0.safetensors`) from Stability's Hugging Face repo, or a
  community SDXL checkpoint tuned for your art style (many on Civitai — **check
  each model's license permits commercial + redistribution** before relying on it;
  prefer ones marked OpenRAIL-M or more permissive).

### 3b. AnimateDiff motion module (required for animation) → `custom_nodes\ComfyUI-AnimateDiff-Evolved\models\`
- **`mm_sdxl_v10_beta.ckpt`** — the SDXL motion module. (For SD 1.5 workflows the
  equivalents are `mm_sd_v15_v2.ckpt` etc.)
- SDXL sweet spot: **~8 frames** per context window — ideal for short unit loops.

### 3c. ControlNet (required for pose control) → `models\controlnet\`
- An **SDXL ControlNet** model — **OpenPose** (for driving a pose skeleton per
  frame) is the key one for walk/attack cycles. A "ControlNet Union" SDXL model
  bundles several types (pose, depth, canny) in one file.

### 3d. (Optional) Flux Schnell for static assets → `models\checkpoints\` / `models\unet\`
- **FLUX.1-schnell** (`black-forest-labs/FLUX.1-schnell` on Hugging Face,
  Apache 2.0). On an 8 GB card use a **quantized GGUF** build + the ComfyUI-GGUF
  node so it fits in VRAM. Use this for building icons, unit portraits, UI art —
  anything static where you want top quality and the cleanest license.

---

## 4. The workflow for a consistent unit

The goal: define a unit **once**, then regenerate matching frames — and, months
later, generate a **new** unit that visually matches the existing roster.

### Step 1 — Lock the STYLE (do this once for the whole game)
Write a **style prompt fragment** you paste into *every* generation. Example:

```
<style> flat 2D game sprite, top-down 3/4 view, clean cel shading, bold
readable silhouette, limited palette, single character centered on transparent
background, no ground shadow, consistent line weight
```

Pick **one seed** as your "house seed" for exploration and record it. Consistency
comes from holding **style fragment + checkpoint + sampler + seed** fixed and only
changing the unit description.

### Step 2 — Lock the CHARACTER (per unit)
Two levels, cheapest first:

- **Cheap / immediate — reference image + IP-Adapter:** generate one good "hero"
  image of the unit, then feed it via IP-Adapter so its appearance carries into
  every subsequent frame. Good enough for a handful of units.
- **Strongest — train a character LoRA:** once you have ~15–30 images of a unit
  (or a consistent style), train a LoRA (via `kohya_ss` or a ComfyUI training
  node). The LoRA *bakes in* that unit/style so every future generation matches.
  This is the tool that makes **"add a new unit next year that still matches"**
  actually work — train a **style LoRA** on your finished roster and apply it to
  every new unit.

### Step 3 — Pose the FRAMES (per animation)
1. Make or grab a **pose sequence** — a few OpenPose skeletons for the frames of
   the loop (idle: 2–4, walk: 6–8, attack: 4–6). You can pose a free rig, use
   existing reference frames, or use the Auxiliary Preprocessors to extract poses
   from any reference clip.
2. Feed the skeletons into **ControlNet (OpenPose)** so each frame is pinned to
   its pose.
3. Run it through **AnimateDiff** (SDXL motion module, ~8-frame context) so the
   frames stay stylistically coherent frame-to-frame.
4. Output the frame sequence via VideoHelperSuite.

### Step 4 — Assemble & clean
- Diffusion animation is "good enough for prototype" — expect to **hand-clean** the
  occasional bad frame (this hand-editing also strengthens the human-authorship
  argument if you ever want the assembled sheet copyrightable).
- Pack frames into a **sprite sheet**. There's a ready-made **Sprite Sheet
  Generator** template in ComfyUI's workflow library that turns one sprite into
  idle/attack/walk/jump sequences — a good starting point:
  <https://comfy.org/workflows/templates-sprite_sheet-fe5600667e2c/>

---

## 5. Reproducibility checklist (so art stays consistent over time)

Record these per unit in a small manifest (e.g. `art/units/<unit>.json`) so any
frame can be regenerated identically later:

- Checkpoint filename + hash
- Style prompt fragment (the shared one) + unit-specific prompt
- Negative prompt
- Seed(s)
- Sampler + steps + CFG
- ControlNet model + the pose skeletons used
- LoRA(s) + weights
- AnimateDiff motion module + frame/context count

Commit the **prompts, seeds, and pose skeletons** to the repo alongside the art.
That is what makes the pipeline reproducible — and doubles as documentation for
anyone training on the dataset.

---

## 6. Practical notes for the dual-4060 rig

- Image generation uses **one GPU per job** (a single image is not split across
  cards). The second 4060 is still useful: generate a batch on card 0 while
  iterating/previewing on card 1, or run a text-to-image job and an upscale job in
  parallel. Set the target card with `CUDA_VISIBLE_DEVICES` per ComfyUI instance.
- 8 GB VRAM handles **SDXL** and **quantized Flux Schnell** fine. Full-precision
  Flux dev-class models are the ones that want 24 GB — you're not using those.
- 128 GB system RAM is ample headroom for loading/swapping models.

---

## Sources
- ComfyUI Windows install (2026): <https://runaihome.com/blog/comfyui-windows-setup-guide/>
- AnimateDiff-Evolved (motion modules, SDXL support): <https://github.com/Kosinkadink/ComfyUI-AnimateDiff-Evolved>
- Indie game asset pipeline (keyframe + OpenPose + LoRA → walk cycle): <https://www.strayspark.studio/blog/comfyui-game-asset-pipeline-indie-2026>
- Sprite Sheet Generator workflow template: <https://comfy.org/workflows/templates-sprite_sheet-fe5600667e2c/>
- ControlNet for SDXL (ComfyUI): <https://comfyui-wiki.com/en/resource/controlnet-models/controlnet-sdxl>
- FLUX.1-schnell (Apache 2.0): <https://huggingface.co/black-forest-labs/FLUX.1-schnell>
- SDXL OpenRAIL-M license: <https://huggingface.co/spaces/CompVis/stable-diffusion-license/raw/main/license.txt>
- US Copyright Office, AI & Copyright: <https://www.copyright.gov/ai/>
