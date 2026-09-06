# Animation Packing

Use `Ceres.AnimationPacking.Editor` for compact source storage of Unity `AnimationClip` assets.

## Boundaries

- The module lives in `Packages/com.kurisu.ceres/Editor/AnimationPacking` and uses the `Ceres.AnimationPacking` namespace.
- It is Editor-only and has no dependency on another Ceres assembly.
- `.animbin` imports as a standard `AnimationClip`; it does not introduce a runtime asset type or runtime decompression.
- Do not add AssetRipper, game-specific extraction, Distribution, DataTable, ContentGroup, or migration rules to Ceres.
- Do not intercept `.anim`, FBX, or other animation sources.

## Format contract

- Format version: `1`.
- Extension: `.animbin`.
- Codec: deterministic gzip.
- Payload: exactly one complete text-serialized Unity `AnimationClip`.
- Main-object identifier: `clip`.
- Local file ID: `7400000`.
- Preserve all serialized animation data. Do not reduce keys, quantize values, or reinterpret curves.
- Preserve external object references and register their GUIDs as importer dependencies.

## Authoring

- `AnimationBinaryUtility.Pack` converts one text `.anim` file to `.animbin`.
- `AnimationBinaryUtility.Extract` creates an editable `.anim` copy.
- Both operations fail when the destination exists.
- The utility owns only the data file. Unity or the calling source pipeline owns its `.meta` and GUID.
- The menu actions are `Tools/Ceres/Animation/Pack` and `Tools/Ceres/Animation/Extract` and operate on the selected compatible asset.

## Validation

For importer changes, verify Unity compilation and a representative import/reimport. Check that the imported object is an `AnimationClip`, its GUID and local ID remain stable, controller references resolve, and temporary files are removed after both success and failure. Do not add a Unity test assembly to the project.
