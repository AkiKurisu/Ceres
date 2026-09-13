# Ceres Neutral theme and controls

`Specs/UIElement/DESIGN.md` is the design source of truth. Inspect `Runtime/UIElements/Themes/CeresNeutral.uss` and the control implementation before changing shared behavior.

## Integration

- Reference `CeresNeutral.uss` explicitly and add `ceres-ui` to the surface root.
- Add `ceres-touch` at the root or an ancestor for touch density.
- Use `UIToolkitButton`, `UIToolkitToggle`, and `UIToolkitSlider` before building feature-local equivalents.
- Feature USS owns composition, label widths, safe areas, and layout; it must not redefine shared control appearance.

## Decisions

- Variant expresses action hierarchy: Secondary is ordinary, Primary is the single direct decision, Ghost is quiet, Outline is explicitly bounded, and Danger is destructive data loss.
- Size expresses region density. Keep one density within an action row and use compact icon/text counterparts together.
- Compact visible controls may remain 28px on touch while their layout-owned target is at least 44px. Hit targets must not overlap.
- State changes must not alter size, padding, or neighboring layout. Reserve width for changing state labels.
- Do not add left accent strips, ordinary pill geometry, decorative gradients, or color-only state.
- Unity USS is not web CSS. Confirm property support in the project's minimum Unity version before using a web pattern.

Validate resolved runtime geometry, not just USS declarations. Inspect both the hit target and the painted child for composite controls.
