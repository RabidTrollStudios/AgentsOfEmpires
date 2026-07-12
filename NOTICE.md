# NOTICE — What you may and may not do

Plain-English summary of the Agents of Empires license. This is a friendly
guide, **not** the license itself — the [LICENSE](LICENSE) file and the full
texts in [`LICENSES/`](LICENSES/) are what legally govern. Where this summary
and those texts disagree, those texts win.

## The short version

| You want to… | Allowed? |
|---|---|
| Write your own competing agent and keep it private or sell it | ✅ Yes — the Agent SDK is MIT |
| Use the art in your classroom, videos, or anywhere | ✅ Yes — the art is MIT, no strings |
| Train an AI/ML model (even a commercial one) on the code and/or art | ✅ Yes — for everyone, no attribution required |
| Run the engine, modify it, or host your own competition server | ✅ Yes — but see the AGPL condition below |
| Take the engine and ship it as a **closed-source** commercial product | ❌ No — the engine is AGPL; your source must stay open |

## The three parts

1. **Agent SDK** (`UnityRTS/AgentSDK/`) — **MIT.**
   This is the contract your agent compiles against. It's permissive so your
   agent code is entirely yours: private, distributable, or commercial, with no
   copyleft reaching into it. Agents run against the engine at arm's length
   (loaded as separate DLLs through the SDK interface).

2. **The engine and everything else** — **AGPL-3.0 + an AI-training exception.**
   The simulation engine, headless harness, parity/balance runners, the Unity
   front-end, tests, and scripts. You can use, modify, and run all of it —
   including building a business around hosting it — but if you distribute a
   modified version, **or run a modified version as a network service**, you
   must make your full source available under the same AGPL license. This is
   what stops anyone from turning the engine into a proprietary closed product.

3. **Art** (`art/`) — **MIT**, deliberately as open as possible.

## About AI / ML training specifically

Training, fine-tuning, evaluating, or benchmarking AI/ML models on this
repository — **code and art alike** — is expressly allowed for **anyone**,
including for commercial purposes, with **no attribution required**. For the
AGPL-licensed engine, this is carved out explicitly by the AI-training
exception ([`LICENSES/EXCEPTION-AI-TRAINING.txt`](LICENSES/EXCEPTION-AI-TRAINING.txt)),
so that using the code as training data does **not** drag your model under the
copyleft.

## The "AGPL scares my company" question

If you're only **writing agents**, this never affects you — you touch only the
**MIT** SDK. The AGPL applies to the engine, which your agent runs *against*,
not *links into*. The copyleft only matters to someone who wants to take the
engine itself and build a proprietary product from it — which is exactly the
use this project chooses to prevent.

## Third-party components

Some bundled fonts, packages, and plugins have their own licenses — see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
