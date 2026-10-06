# Releases and storage

Use the [developer guide](https://github.com/AkiKurisu/Ceres/blob/main/Documentations/docs/core_content_pipeline.md#creating-a-content-release)
for release and maintenance API examples.

Source anchors under `Packages/com.kurisu.ceres/Editor/ContentPipeline` are
`ContentBuildArtifacts.cs`, `DynamicContentReleaseBuilder.cs`,
`DynamicContentReleaseOutputs.cs`, and `ContentBuildStorageMaintenance.cs`.

Validate changes with deterministic graph/report comparisons and a representative
backend build when authorized. A read-only diagnosis does not authorize rebuilding,
deleting storage or publishing a release. Review cleanup's resolved targets before
execution; never bypass writer exclusion to accelerate automation.
