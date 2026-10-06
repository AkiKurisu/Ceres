# Animation Packing

Use the
[developer guide](https://github.com/AkiKurisu/Ceres/blob/main/Documentations/docs/animation_packing.md)
for authoring commands and public APIs. Inspect the existing converter and
importer before changing preservation, ownership or import behavior.

## Source navigation

Under `Packages/com.kurisu.ceres/Editor/AnimationPacking`:

- `AnimationBinaryFormat.cs`: packed file conventions.
- `AnimationBinaryUtility.cs`: conversion and validation.
- `AnimationBinaryImporter.cs`: Unity asset import.
- `AnimationBinaryMenu.cs`: authoring commands.

Validate through the existing menu/importer path and representative clip
round trips. Check malformed input and destination collisions as well as
successful conversion. Compare complete imported clip data, not only playback
of one visible motion.
