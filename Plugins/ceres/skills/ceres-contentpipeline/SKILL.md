---
name: ceres-contentpipeline
description: Design, implement, or diagnose Ceres content builds, Addressables backends, bundle partitioning, dynamic releases, and build storage maintenance.
---

# Ceres Content Pipeline

Treat content identity, dependency analysis, partitioning, build execution, release output, and storage cleanup as separate stages. Inspect `Editor/ContentPipeline` and the application's content policy before changing a stage.

## References

- Read [build-graph.md](references/build-graph.md) for graph construction, dependency resolution, mounts, partitioning, and build backends.
- Read [releases-and-storage.md](references/releases-and-storage.md) for release outputs, artifact ownership, cleanup, and validation.

Content Pipeline APIs are Editor-only. Do not move build orchestration into runtime assemblies or make Ceres depend on a downstream product's catalog conventions.
