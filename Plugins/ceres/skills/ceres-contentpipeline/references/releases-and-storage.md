# Releases and storage

Build outputs are evidence and inputs to later release steps; do not treat generated directories as the source of content truth.

- `ContentArtifactManifest` and related snapshots describe a completed build.
- `DynamicContentReleaseBuilder` assembles a release from explicit bundle sources and emits a release manifest.
- Catalog projection and package materialization derive distribution outputs from that release.
- `ContentBuildStorageMaintenance` previews cleanup before deleting build storage. Keep preview and execution based on the same resolved targets.
- `ContentBuildProcessLock` prevents overlapping writers; never bypass it to make an automation appear faster.

Source anchors are `ContentBuildArtifacts.cs`, `DynamicContentReleaseBuilder.cs`, `DynamicContentReleaseOutputs.cs`, and `ContentBuildStorageMaintenance.cs` under `Editor/ContentPipeline`.

Validate changes with deterministic graph/report comparison and a representative backend build when the task authorizes it. A read-only diagnosis does not authorize rebuilding, deleting storage, or publishing a release.
