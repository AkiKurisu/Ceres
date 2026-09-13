---
name: ceres-core
description: Build or extend Ceres runtime services, including pooling, events, configs, resources, data-driven utilities, schedulers, serialization, collections, R3, tasks, and modules.
---

# Ceres Core

Inspect `Packages/com.kurisu.ceres/Runtime/Core` and the nearest existing consumer before choosing an abstraction. Keep reusable runtime APIs in `Ceres`; do not introduce dependencies on Graph, Flow, Gameplay, or `UnityEditor`.

## References

- Read [runtime-services.md](references/runtime-services.md) for pooling, events, configs, resources, tasks, and modules.
- Read [data-and-reactive.md](references/data-and-reactive.md) for data-driven types, schedulers, serialization, collections, and R3 integration.

Use public developer documentation for conceptual usage, then confirm signatures and lifecycle behavior in current source. Avoid copying API inventories into new documentation.
