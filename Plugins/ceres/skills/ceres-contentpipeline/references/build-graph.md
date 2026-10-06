# Content build graph

Use the
[developer guide](https://github.com/AkiKurisu/Ceres/blob/main/Documentations/docs/core_content_pipeline.md)
for request fields and integration examples, then trace the relevant package
implementation before changing ownership, partitioning or cache behavior.

## Source navigation

Under `Packages/com.kurisu.ceres/Editor/ContentPipeline`:

- `ContentBuildGraphModel.cs` and `ContentBuildGraphBuilder.cs`: contribution,
  diagnostics and resolved graph.
- `UnityContentAssetDependencyResolver.cs`: Unity dependency discovery.
- `ContentBundlePartitionPlanner.cs`: physical partition planning.
- `AddressablesContentBuildBackend.cs`: build orchestration.
- `ContentBuildSnapshot.cs`, `ContentAssetBuildDependencies.cs` and
  `ContentPackedBuild.cs`: additional asset inputs and cache integration.

Trace producers and consumers before changing request or artifact serialization.
For cache changes, validate identical-input reuse, changed-input invalidation,
repeated updates and return-to-baseline behavior.
