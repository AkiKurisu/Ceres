# Gameplay systems

Select the subsystem that owns the behavior and follow its existing lifecycle.

- **World and Actor**: composition, registration, ticking, and actor-owned capabilities.
- **Level**: level state and transitions; keep project scene policy outside the framework.
- **Audio and FX**: playback/effect orchestration and resource lifetime.
- **Animation**: gameplay animation control; use `ceres-animationpacking` only for `.animbin` authoring/import.
- **Graphics**: gameplay-facing graphics coordination, not render-pipeline implementation.
- **AI/EQS**: query contexts, generators, tests, scoring, and execution ownership.
- **Mod**: framework extension and mod resource boundaries.
- **Capture**: gameplay capture coordination and render-pipeline hooks.
- **Gameplay Flow**: nodes and functions that expose gameplay behavior to Flow; load `ceres-flow` for execution mechanics.

Source is under `Runtime/Gameplay` with Editor support under `Editor/Gameplay`. Public overviews live in `Documentations/docs/gameplay*.md`.

Keep reusable primitives in Core and application rules downstream. Do not make a lower-level Ceres assembly depend on Gameplay merely to reuse one helper.
