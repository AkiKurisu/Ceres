---
name: ceres-animationpacking
description: Import, author, extend, or diagnose Ceres Animation Packing and `.animbin` assets.
---

# Ceres Animation Packing

Use the existing animation packing importer and authoring pipeline rather than adding a second binary format or runtime loader. Read [animation-packing.md](references/animation-packing.md) before changing format, importer, compression, or asset ownership behavior.

Keep authoring and import helpers in `Ceres.AnimationPacking.Editor`. Runtime consumers should depend on produced assets, not Editor APIs or source animation files. Format changes require explicit compatibility and validation decisions; ordinary importer fixes do not.
