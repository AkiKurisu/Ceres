# Gameplay

Ceres Gameplay is a lightweight Actor-based scaffold for projects that need shared world lifecycles, scene composition, and gameplay services without adopting a large application framework. It is implemented by the `Ceres.Gameplay` assembly and builds on Ceres Core, Graph, and Flow.

Gameplay provides reusable contracts and runtime services. Projects still own their game-specific state, spawning rules, save model, networking, and presentation architecture.

## Module map

| Area | Purpose |
| --- | --- |
| [World and Actors](./gameplay_world.md) | `GameWorld`, Actors, Components, Controllers, versioned handles, world subsystems, and actor snapshots |
| [Actor Flow](./gameplay_flow.md) | Actor lifecycle events, embedded or shared graphs, remote graph selection, and Gameplay nodes |
| [Level](./gameplay_level.md) | DataTable-authored levels, Addressables scene loading, progress, events, and additive scene ownership |
| [AI and Spatial Queries](./gameplay_ai.md) | Job-backed spatial queries built on world actor snapshots |
| [Animation](./gameplay_animation.md) | Script-driven PlayableGraph layers, blending, sequences, and notifications |
| [Audio and FX](./gameplay_audio_fx.md) | Pooled playback from direct assets or Addressables addresses |
| [Graphics](./gameplay_graphics.md) | Reactive render settings and data-driven volume profiles |
| [Capture](./gameplay_capture.md) | Camera and screen capture with platform gallery integration |
| [Mod](./gameplay_mod.md) | Mod discovery, validation, configuration, and Addressables content mounting |

These modules are independent entry points over the same world and Core services. Use only the parts required by the project.

## Dependency direction

Gameplay is the highest framework layer: `Ceres` → `Ceres.Graph` → `Ceres.Flow` → `Ceres.Gameplay`.

Core services do not depend on Gameplay. Gameplay can expose its C# APIs to Flow, use Core resources and DataTables, and share Graph runtime infrastructure.

## Start here

- Start with [World and Actors](./gameplay_world.md) when defining the runtime ownership model.
- Add [Actor Flow](./gameplay_flow.md) only to actors that need visually authored behavior.
- Use [Level](./gameplay_level.md) when a gameplay level spans one or more Addressables scenes.
- Read [Ceres Architecture and Concepts](./ceres_concept.md) for the boundary between Core, Graph, Flow, and Gameplay.
