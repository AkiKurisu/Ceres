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

Projects can supply `ContentPipelineBuildRequest.AssetBuildDependencyHashes` for
asset build inputs that Unity's dependency hash cannot see. Keys are graph asset
IDs and values are stable, non-empty digests. Supply the complete current map for
baseline and update builds. The backend incorporates each digest into the asset
snapshot and the SBP write operations containing that asset's objects, preserving
the previous packing callback and restoring it after the build. Null or empty
maps do not change fingerprints or cache keys; Ceres does not interpret project
policy or change ownership and packing to apply these dependencies.

Enabling a non-empty map is a baseline configuration capability; adding/removing
the last entry requires a new baseline, while value changes remain incremental.
For enabled updates, compare snapshots against the original baseline before
Addressables reversion, expand changed assets through reverse dependencies and
old/current bundle peers, and exclude their cached entries from an in-memory
Content State copy. Never edit the baseline state file or compare its entries
against only the latest update manifest.
