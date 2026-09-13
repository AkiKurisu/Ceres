# Content build graph

The build graph describes content before a backend materializes it. Keep graph construction deterministic and backend-independent.

## Data flow

1. `IContentBuildGraphContributor` adds scopes and asset contributions.
2. `IContentAssetDependencyResolver` resolves Unity dependencies.
3. `ContentBuildGraphBuilder` produces nodes, ownership, dependency edges, and diagnostics.
4. Partition planning groups resolved content according to `ContentBundlePackingOptions`.
5. A backend such as `AddressablesContentBuildBackend` materializes the plan and reports artifacts.

Treat stable content identity, physical asset path, ownership scope, and bundle assignment as different concepts. Do not repair ambiguous ownership inside a backend; emit graph diagnostics at the stage that has enough context.

## Source anchors

- `Editor/ContentPipeline/ContentBuildGraphModel.cs`
- `Editor/ContentPipeline/ContentBuildGraphBuilder.cs`
- `Editor/ContentPipeline/ContentBundlePartitionPlanner.cs`
- `Editor/ContentPipeline/AddressablesContentBuildBackend.cs`
- `Editor/ContentPipeline/UnityContentAssetDependencyResolver.cs`

When changing an artifact or request type, trace every producer and consumer before changing serialization. Preserve deterministic ordering and fingerprints so identical inputs produce identical reports.
