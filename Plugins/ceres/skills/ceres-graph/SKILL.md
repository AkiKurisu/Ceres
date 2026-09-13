---
name: ceres-graph
description: Extend or diagnose Ceres graph models, shared variables, serialization, ports, nodes, search, and graph editor foundations without Flow-specific execution semantics.
---

# Ceres Graph

Use this skill for the reusable graph layer under `Runtime/Graph` and `Editor/Graph`. For executable control flow, Flow code generation, functions, or events, use `ceres-flow` as well.

## References

- Read [model-and-variables.md](references/model-and-variables.md) for graph data, shared variables, scopes, serialization, compilation, and IL2CPP boundaries.
- Read [editor-extension.md](references/editor-extension.md) for node and port views, factories, search, blackboards, inspectors, and manipulators.

Keep runtime graph types independent of GraphView and `UnityEditor`. Extend factories and metadata used by the current editor instead of bypassing discovery with feature-local registries.
