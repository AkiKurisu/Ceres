# Animation Packing

For authoring commands and public APIs, see the
[Animation Packing guide](../../Documentations/docs/animation_packing.md).

## Purpose and Boundary

Animation Packing reduces the stored size of authored animation without changing
animation quality. It preserves complete clip data, including curve identity,
bindings, key order, tangents, wrap behavior and metadata. It does not perform
key reduction or quantization.

Conversion and import belong to the Editor. Runtime consumers receive ordinary
Unity AnimationClip assets and require neither a custom loader nor the source
animation file. Packed assets participate in controllers and bundle builds through
Unity's normal asset pipeline.

## Authoring and Import Flow

The authoring flow is text-serialized clip → packed interchange asset → Unity
imported clip. Editing reverses the storage step into an ordinary source clip;
imported packed clips are not a persistent editing surface.

Each packed asset represents exactly one clip. Invalid or unsupported input must
fail with an actionable error rather than emit a partial asset. Conversion must
not overwrite an existing destination or expose incomplete output as a completed
asset; temporary conversion resources are released after success or failure.

Changes to the storage contract require explicit compatibility decisions and
representative round-trip validation. Importer UI or diagnostics changes do not
justify a format revision. Preserve the existing importer and authoring path
rather than introduce a parallel runtime format.
