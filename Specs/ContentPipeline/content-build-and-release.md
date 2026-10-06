# Content Build and Release

This specification owns the content build and release lifecycle contract.
For setup, public APIs and examples, use the
[Content Pipeline guide](../../Documentations/docs/core_content_pipeline.md).

## Responsibility and Flow

Content Pipeline is an Editor-only boundary between a project's source model
and Unity's Addressables/SBP build machinery. The project owns source discovery,
generated metadata, inclusion policy, workflow UI, Player integration and
publication. Ceres owns dependency planning, build artifacts, release indexing
and the Editor content mount. It does not depend on a downstream product's
catalog conventions.

The flow is source discovery → temporary metadata → complete dependency graph
→ validation → baseline or update → release index → project-owned deployment.
The same graph supplies Editor AssetDatabase access. Generated sources remain
alive until every build or packaging step that refers to them has finished.

Persistent Addressable groups are not the source of truth. A build's temporary
Addressables model must not populate or replace the project's maintained groups.

## Identity, Ownership and Delivery

Content identity, physical asset paths, ownership and bundle assignment are
separate decisions. Identity and graph ordering must be deterministic; ambiguous
addresses, paths or ownership are graph errors, not backend repair opportunities.
The graph contains the complete current content and its transitive dependencies.
An explicit entry represents one standalone Unity asset; sub-assets must be
promoted before contribution.

Shared dependencies have one planned owner. A dependency used by Local and
Remote content belongs to the immutable Local baseline. BuiltIn content is
provided by Unity or the Player; Excluded content is analysis-only. Neither is
emitted as an explicit dynamic content entry or exposed by the Editor mount.

Logical scopes express project identity, not physical bundle identity. Packing
preserves delivery location and scene/semantic boundaries. Size targets are soft:
an indivisible entry may exceed them. A baseline establishes the partition plan;
updates retain existing assignments rather than globally rebalancing unrelated
content. Deleted entries leave capacity, and additions use compatible capacity
or deterministic overflow partitions. A new baseline may rebalance the layout.

## Baselines, Updates and Cache Correctness

A baseline binds content to its toolchain, target, channel, load path and packing
configuration. Local content changes or incompatible configuration require a new
baseline. Remote changes, including metadata and shared dependencies, expand to
all affected scopes automatically. Updates always analyze the complete graph.

The latest successful content version is the change-detection head. An unchanged
request produces no new update. The original baseline remains the reference for
Addressables content-state reuse; it must not be confused with that latest head.

Project-owned build inputs invisible to Unity's asset hashes participate in both
asset change detection and the cache identity of output containing those assets.
Ceres treats their digests as opaque input. They do not change ownership or
packing policy. Enabling or disabling this capability requires a baseline;
changing its values is incremental, and an absent map preserves ordinary builds.

Cache reuse must account for dependents and assets sharing old or current bundles,
including implicit copies. Addressables must not revert a newly written bundle
merely because Unity's asset hashes are unchanged. Repeated updates with the same
non-baseline input remain valid; returning all relevant inputs to baseline values
may reuse baseline output. Baseline content-state files remain immutable, and
build-owned temporary resources are released and borrowed state is restored on every exit.

## Artifacts, Releases and Storage

A successful build commits immutable artifacts with identity, dependency and
integrity information. A release is a catalog plus a direct map to those artifact
payloads, not a second copy of the bundles. Updated releases contain the complete
current source map, including unchanged bundles, without requiring traversal of
older release chains.

Deployment installs a release's catalog and referenced payloads. Editor catalog
projection and explicit standalone package materialization are separate consumers
of that release; indexing alone does not create a runtime-loadable installation.

Builds stage and validate output before committing it. Publication pointers change
atomically after success; a failed build cannot advance them, and an update does
not replace the baseline pointer. Identical content may reuse a validated artifact
identity. Concurrent writers to one platform output are excluded, and content
builds do not overlap other Unity build workflows in the same Editor process.

Storage cleanup preserves current content and all artifacts retained by active
releases, deployments or rollback policy. Preview and execution resolve the same
ownership boundaries; invalid references or paths outside managed storage stop
pruning. Cleanup failure does not invalidate an already committed build. File
handling, including long paths, preserves artifact integrity checks.

## Editor Content Lifetime

The Editor mount and built content expose the same explicit addresses and asset
set. Dependencies load through Unity naturally. Labels can merge catalog results;
conflicting identities cannot silently choose a different asset.

A project selects either the AssetDatabase mount or the runtime catalog for a
content source during a session. The mount lives for that session and is disposed
on exit, reload or shutdown. Shared providers remain available to other mounts;
the last owner releases its own support and restores borrowed Editor state.
