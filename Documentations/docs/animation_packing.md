# Animation Packing

## Purpose

`Ceres.AnimationPacking.Editor` stores text-serialized Unity `AnimationClip` assets in a compact
source form without changing their editor or runtime semantics. It is an Editor-only module in the
`com.kurisu.ceres` package and has no runtime assembly.

The module owns the `.animbin` format and its Unity importer. It does not own game extraction,
source-object identity, controller generation, content registration, or project-specific migration.

## Assembly boundary

```text
com.kurisu.ceres
    Editor/AnimationPacking
        Ceres.AnimationPacking.Editor
```

The assembly uses the `Ceres.AnimationPacking` namespace and does not depend on another Ceres
assembly. Installing Ceres registers an importer only for `.animbin`; ordinary `.anim` files, model
animation importers, and other binary files are unaffected.

## Animation Binary v1

An Animation Binary v1 file is a deterministic gzip stream whose decompressed payload is one
complete Unity text-serialized `AnimationClip`. It has the `.animbin` extension.

The format does not reduce keys, quantize values, or reinterpret animation data. Curves, tangents,
weights, events, clip settings, bounds, bindings, and external object references remain part of the
serialized payload.

The imported asset has these stable identifiers:

| Property | Value |
|---|---|
| Sub-asset identifier | `clip` |
| AnimationClip local file ID | `7400000` |
| Compression codec | `gzip` |
| Format version | `1` |

The source asset's GUID belongs to the creating project or source pipeline; the importer does not
derive or replace it.

## Import contract

The importer decompresses the source to a temporary serialized file, requires exactly one
`AnimationClip`, materializes the curve binding cache, and copies the complete serialized clip into
the imported object. External GUID references in the payload are registered as source dependencies.

Invalid gzip data, an empty payload, multiple objects, or a non-`AnimationClip` payload fails the
import. Temporary files and temporary Unity objects are removed on both success and failure.

The imported object is an ordinary `AnimationClip`: inspectors, `AnimatorController`,
`AnimatorOverrideController`, AssetBundles, and runtime animation APIs consume it without a custom
runtime loader. `Library` contains the normal expanded import artifact and is not part of the source
compression contract.

## Authoring utilities

The Editor API can pack one text-serialized `.anim` into `.animbin` and extract `.animbin` back to an
editable `.anim`. Both operations reject an existing destination instead of overwriting it. Packing
the same bytes must produce the same output bytes.

Select a `.anim` or `.animbin` asset and use `Tools/Ceres/Animation/Pack` or
`Tools/Ceres/Animation/Extract`.

Editing the imported `AnimationClip` does not modify the `.animbin` source and is lost on reimport.
Persistent manual edits therefore use an extracted `.anim` copy.
