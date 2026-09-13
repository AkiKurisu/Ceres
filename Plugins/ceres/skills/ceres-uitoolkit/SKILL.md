---
name: ceres-uitoolkit
description: Design, implement, preview, automate, or diagnose Ceres runtime UI Toolkit surfaces, Ceres Neutral controls, typed bindings, and preview sessions.
---

# Ceres UI Toolkit

Use production UI assets and bindings as the source of truth. Preview must mount the same UXML, USS, PanelSettings, scale policy, and binding path as runtime; an Editor-styled substitute is not an acceptable fidelity check.

## References

- Read [theme-and-controls.md](references/theme-and-controls.md) when styling Ceres Neutral or choosing button, toggle, slider, field, scrollbar, density, and touch behavior.
- Read [typed-binding.md](references/typed-binding.md) when using the query generator, binding events, synchronizing values, or managing view lifetime.
- Read [preview-and-automation.md](references/preview-and-automation.md) when adding a provider or fixture, automating previews, comparing runtime snapshots, or diagnosing preview/runtime differences.

Keep runtime controls in `Ceres.UIElements` and preview infrastructure in `Ceres.UIElements.Editor`. Feature packages own composition, business state, safe areas, and product layout. Map business actions explicitly with typed C#; never infer commands from element names.
