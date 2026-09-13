# Animation Packing

Animation Packing converts authored clips into `.animbin` through an Editor importer. The packed file is the interchange artifact; runtime code must not depend on importer APIs.

## Contract

- `AnimationBinaryFormat` owns the binary version and layout.
- `AnimationBinaryUtility` owns conversion and validation helpers.
- `AnimationBinaryImporter` turns `.animbin` into Unity assets.
- `AnimationBinaryMenu` exposes authoring commands.

Preserve curve identity, binding paths, property names, key order, tangents, wrap behavior, and clip metadata unless the requested format change explicitly revises them. Reject malformed or unsupported input with an actionable import error instead of producing a partial clip.

Use the existing menu/importer path for authoring validation. A format revision must define reader compatibility and representative round-trip fixtures; an importer UI or error-message fix does not require a new format version.
