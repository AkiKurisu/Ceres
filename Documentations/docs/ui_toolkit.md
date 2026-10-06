# UI Toolkit

Ceres Neutral supplies runtime UI Toolkit controls and a shared dark theme.
Concrete tokens and dimensions live in
`Packages/com.kurisu.ceres/Runtime/UIElements/Themes/CeresNeutral.uss`.

## Mount the theme

Reference `Ceres.UIElements` from the runtime assembly. Include the theme and
add `ceres-ui` to the root; add `ceres-touch` to that root or an ancestor for touch
layouts. The theme does not change EditorWindow, Inspector, or uGUI surfaces.

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:ceres="Ceres.UIElements">
    <Style src="/Packages/com.kurisu.ceres/Runtime/UIElements/Themes/CeresNeutral.uss" />
    <ui:VisualElement class="ceres-ui">
        <ceres:UIToolkitButton text="Refresh" variant="Secondary" size="Compact" />
    </ui:VisualElement>
</ui:UXML>
```

Use shared theme tokens for feature styles. Feature USS supplies composition,
label widths, and spacing around controls rather than replacing their base
appearance. Confirm property support in Unity USS before adopting web CSS.

## Choose actions

`UIToolkitButton` exposes `variant` and `size` in UXML. Its defaults are
`Secondary` and `Standard`.

| Variant | Use |
| --- | --- |
| Secondary | Ordinary management or repeated inline action |
| Primary | Main submit or continue action |
| Ghost | Quiet text or icon action |
| Outline | Action requiring an explicit visible boundary |
| Danger | Destructive action with explicit risk copy |

| Size | Use |
| --- | --- |
| Standard | Normal action row |
| Compact | Dense action row |
| Prominent | Emphasized large action |
| Icon | Icon counterpart to Standard |
| IconCompact | Icon counterpart to Compact |

Use native `Button.text`, `clicked`, and `SetEnabled` APIs. Custom UXML children
enter the visible surface through `contentContainer`. For an icon action, place
a `ceres-icon` child inside the button, set its picking mode to Ignore, and add
a tooltip. `ceres-icon--close` supplies a font-independent close glyph.
Navigation compositions such as tabs can use the native `ceres-button` styles.

## Fields and value controls

Apply `ceres-text-field` to native text fields. Provide a tooltip when a clipped
value must remain discoverable.

Use `UIToolkitToggle` with its own text rather than a separate label. Use
`UIToolkitSlider` for a slider with a synchronized numeric field; the feature
supplies its range, label width, and row layout. Update controls through
`SetValueWithoutNotify` when callbacks issue business commands.

Scoped native ScrollViews receive the theme's scrollbar appearance while
retaining native scrolling behavior. Feature layouts provide margins around
the scrollbar; runtime icons do not require Editor-only resources.

## Bind and preview

Use the same binding in the production `UIDocument` and the preview provider.
See [UI Toolkit Preview](./ui_toolkit_preview.md) for generated element queries,
subscription ownership, deterministic fixtures, and automation. The preview
window also contains the **Ceres Neutral · Buttons** component catalog.
