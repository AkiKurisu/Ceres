---
version: "1.0.2"
name: "Ceres Neutral"
description: "Neutral-first runtime UI Toolkit design system for Ceres applications."
sourceTokens: "Packages/com.kurisu.ceres/Runtime/UIElements/Themes/CeresNeutral.uss"
colors:
  bg-primary: "var(--ceres-bg-primary)"
  bg-secondary: "var(--ceres-bg-secondary)"
  bg-tertiary: "var(--ceres-bg-tertiary)"
  bg-active: "var(--ceres-bg-active)"
  text-primary: "var(--ceres-text-primary)"
  text-secondary: "var(--ceres-text-secondary)"
  text-dimmed: "var(--ceres-text-dimmed)"
  text-disabled: "var(--ceres-text-disabled)"
  border-default: "var(--ceres-border-default)"
  focus: "var(--ceres-focus)"
  success: "var(--ceres-success)"
  warning: "var(--ceres-warning)"
  danger: "var(--ceres-danger)"
  danger-surface: "var(--ceres-danger-surface)"
  danger-hover: "var(--ceres-danger-hover)"
typography:
  ui:
    fontSize: "var(--ceres-type-ui)"
    desktopSize: "13px"
    touchSize: "15px"
  secondary:
    fontSize: "var(--ceres-type-secondary)"
    desktopSize: "12px"
    touchSize: "13px"
  heading:
    fontSize: "var(--ceres-type-heading)"
    desktopSize: "15px"
    touchSize: "16px"
  title:
    fontSize: "var(--ceres-type-title)"
    desktopSize: "18px"
    touchSize: "18px"
spacing:
  xs: "var(--ceres-space-xs)"
  sm: "var(--ceres-space-sm)"
  md: "var(--ceres-space-md)"
  lg: "var(--ceres-space-lg)"
  xl: "var(--ceres-space-xl)"
  2xl: "var(--ceres-space-2xl)"
rounded:
  sm: "var(--ceres-radius-sm)"
  control: "var(--ceres-radius-control)"
  panel: "var(--ceres-radius-panel)"
  compact-action: "var(--ceres-button-compact-radius)"
  prominent-action: "var(--ceres-button-prominent-radius)"
components:
  action:
    variants: [Secondary, Primary, Ghost, Outline, Danger]
    sizes: [Standard, Compact, Prominent, Icon, IconCompact]
    defaultVariant: "Secondary"
    defaultSize: "Standard"
  toggle:
    control: "Ceres.UIElements.UIToolkitToggle"
    checkmarkSize: "17px"
  slider:
    control: "Ceres.UIElements.UIToolkitSlider"
    trackHeight: "4px"
    draggerSize: "14px"
    numberFieldWidth: "52px"
---

# Ceres Neutral Design

This document is the source of truth for the Ceres Neutral runtime UI Toolkit
design system. The canonical token and control styling implementation is
`Packages/com.kurisu.ceres/Runtime/UIElements/Themes/CeresNeutral.uss`. Runtime
controls live in the independent `Ceres.UIElements` assembly, while the Editor
assembly only previews those production controls.

## Overview

Ceres Neutral is the default runtime theme supplied by Ceres. It currently
provides a dark appearance and does not reskin the Unity Editor, existing
EditorWindow or Inspector surfaces, or uGUI.

The design posture is neutral-first:

- Use neutral surfaces, text hierarchy, and spacing to establish structure.
- Use emphasis only to clarify priority, state, focus, or destructive risk.
- Allow at most one Primary action in a direct decision area.
- Express state with text, checkmarks, or a full neutral selected surface rather
  than color alone.
- Do not add accent strips to buttons, tabs, selected rows, or group edges.
- Do not use decorative gradients, glow, or default blue fills for ordinary
  actions.

## Ownership and Integration

- `Runtime/UIElements/Ceres.UIElements.asmdef` owns the runtime controls and has
  no dependency on Ceres business modules, downstream products, or
  `UnityEditor`.
- `Runtime/UIElements/Themes/CeresNeutral.uss` owns reusable tokens and base
  control styles.
- `Editor/UIElements/Ceres.UIElements.Editor.asmdef` owns the preview host,
  automation API, and component catalog. It depends on `Ceres.UIElements`, not
  on downstream product assemblies.
- Runtime surfaces opt in by referencing `CeresNeutral.uss` and adding the
  `ceres-ui` class to their root. Touch layouts add `ceres-touch` to that root or
  an ancestor.
- Feature packages own composition, business state, safe-area policy, field
  labels, and layout. They do not redefine the base appearance of actions,
  toggles, or sliders.
- Runtime bindings may use `UIToolkitView` and `UIToolkitQuery` to generate
  strongly typed element references. `UIToolkitBindingScope` owns explicit
  event registrations; element names never become implicit business commands.
- Unity 6 uses `UxmlElement` and `UxmlAttribute`; supported older Unity versions
  use `UxmlFactory` and `UxmlTraits` without raising the package's Unity 2022.3
  minimum.

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:ceres="Ceres.UIElements">
    <Style src="/Packages/com.kurisu.ceres/Runtime/UIElements/Themes/CeresNeutral.uss" />
    <ui:VisualElement class="ceres-ui">
        <ceres:UIToolkitButton text="Refresh" variant="Secondary" size="Compact" />
    </ui:VisualElement>
</ui:UXML>
```

## Colors

Neutral tokens carry the visual hierarchy:

| Token | Value | Role |
| --- | --- | --- |
| `--ceres-bg-primary` | `#171717` | Deep background and input surface |
| `--ceres-bg-secondary` | `#202020` | Panels and grouped surfaces |
| `--ceres-bg-tertiary` | `#2b2b2b` | Ordinary controls and nested surfaces |
| `--ceres-bg-active` | `#353535` | Hover, pressed, selected, or active neutral state |
| `--ceres-text-primary` | `#eeeeee` | Primary copy and inverse action surface |
| `--ceres-text-secondary` | `#b5b5b5` | Labels, secondary copy, and quiet icons |
| `--ceres-text-dimmed` | `#858585` | Low-priority supporting information |
| `--ceres-text-disabled` | `#696969` | Disabled content |
| `--ceres-border-default` | `#3b3b3b` | Ordinary input and outline boundaries |
| `--ceres-focus` | `#9ab9e6` | Keyboard focus and active input boundary |
| `--ceres-success` | `#7fc8a5` | Successful state |
| `--ceres-warning` | `#dab477` | Caution or recoverable risk |
| `--ceres-danger` | `#e89b9b` | Destructive action foreground |
| `--ceres-danger-surface` | `#3b2727` | Destructive action surface |
| `--ceres-danger-hover` | `#4c2e2e` | Destructive action hover surface |

Primary actions use neutral inversion: a primary text-colored surface with the
primary background color as text. Semantic colors communicate state or actual
risk and are not decorative accents. A verb such as Stop or Clear does not make
an action dangerous unless it causes destructive data loss.

## Typography and Spacing

All dimensions are logical pixels. The standard desktop type scale is 13px for
ordinary UI, 12px for secondary text, 15px for group headings, and 18px for
panel titles. Touch layouts raise ordinary UI to 15px, secondary text to 13px,
and group headings to 16px. Compact actions remain 12px.

The spacing scale is 4, 8, 12, 16, 24, and 32. Use 8px between actions, 12px
between a field and its group heading, 16px for content padding, and 24px
between groups unless a feature specification defines a denser composition.
Letter spacing remains zero.

## Shapes

The shared radii are 4px for small geometry, 8px for standard controls, and
10px for panels. Compact actions use a deliberate 10px radius, and Prominent
actions use 12px. These are semantic tiers rather than ratios derived from the
element height.

Do not use half-height or 999px pill geometry for ordinary actions. Hover,
pressed, focused, selected, and disabled states must not change control size,
padding, or position. Controls do not scale down while pressed.

## Components

### Actions

`UIToolkitButton.Variant` defines action hierarchy and
`UIToolkitButton.Size` defines density. The UXML attributes are `variant` and
`size`. Do not create controls named after business verbs.

| Variant | Use |
| --- | --- |
| Secondary | Default management and repeated inline action with a low-contrast neutral fill |
| Primary | The single primary submit or continue action in a direct decision area |
| Ghost | Quiet text or icon action with a transparent resting surface |
| Outline | Special action that genuinely requires an explicit boundary |
| Danger | Action that causes destructive data loss and uses explicit risk copy |

| Size | Desktop visible surface / target | Touch visible surface / target | Radius | Type / horizontal padding |
| --- | --- | --- | --- | --- |
| Standard | 32px high / 32px | 44px high / 44px | 8px | 13px, touch 15px / 12px |
| Compact | 28px high / 28px | 28px high / 44px | 10px | 12px / 10px |
| Prominent | 38px high / 38px | 38px high / 44px | 12px | 13px / 16px |
| Icon | 32x32px / 32x32px | 44x44px / 44x44px | 8px | 15px, touch 16px / 0 |
| IconCompact | 28x28px / 28x28px | 28x28px / 44x44px | 10px | 15px / 0 |

Text actions size to their content and padding instead of stretching equally by
default. Touch targets are at least 44x44px and must not overlap; the visible
surface remains centered within the target and ignores picking. State actions
reserve enough width for their longest label so transitions such as On and Off
do not shift neighboring content.

Every visible action surface reserves a transparent 1px border. Focus and the
Outline variant make that boundary visible without changing geometry.

Use one density throughout an action row and pair text and icon actions at the
same level. A low-contrast Secondary action is not a disabled action. The
control preserves native `Button.text`, `clicked`, keyboard activation, and
`SetEnabled` behavior. The outer `ceres-action` owns the hit target, while
`ceres-action__surface` paints the visible face. Navigation compositions such
as tabs may use the theme's native `ceres-button` style family.

Custom UXML children of `UIToolkitButton` enter its visible surface through
`contentContainer`. Icon actions place a 14x14 `ceres-icon` inside that slot;
the slot uses flex centering, not absolute coordinates or sibling overlays.
Set the icon's picking mode to Ignore and describe the action with a tooltip.
`ceres-icon--close` supplies the shared font-independent close glyph. Both the
glyph and its visible surface stay inside the button's layout-owned hit target.

### Text Fields

Apply `ceres-text-field` to standard text fields. The visible field is 32px high
on desktop and 44px in touch layouts, with an 8px radius, a 1px neutral border,
and 12px horizontal padding. Long content may shrink and clip; the owning
feature supplies a tooltip when the full value must remain discoverable.

Focused fields use `--ceres-focus`. Disabled fields retain their geometry and
change only their interaction and visual state.

### Toggles

`UIToolkitToggle` makes the complete row interactive. Its checkmark is 17x17px
with a 4px radius and an 8px gap before the text. Use the toggle's internal text
rather than a separate label. The geometric checkmark ignores picking, and the
default Editor background image is disabled.

Focus remains visible. Disabled toggles retain the same geometry and avoid
stacking default skin opacity on their children.

### Sliders

`UIToolkitSlider` uses a 32px desktop target and a 44px touch target. Its track
is 4px high and its dragger is 14x14px, both explicitly centered within the
target. The number field is 52px wide, with a 28px desktop visible height, a
32px touch visible height, 6px horizontal padding, and centered text.

The owning feature defines label width, row spacing, and the value range.
Programmatic synchronization uses `SetValueWithoutNotify` so UI refreshes do
not trigger runtime commands. Pointer input in the number-field target is
forwarded to the inner input so the field receives focus reliably.

### Native scrollbars

Scoped native ScrollView scrollbars use a 13px rail and a 10px neutral rounded
thumb. Arrow buttons use the shipped serialized VectorImage glyph and transparent neutral
surfaces. The runtime theme explicitly defines these styles rather than
inheriting Editor skin colors or runtime-default light geometry. Track length
and thumb sizing, wheel, repeat buttons, pointer capture, and touch content
scrolling remain native; feature packages own margins around the rail.
The native vector asset does not require an SVG importer or Editor-only icons
at runtime or when the package is imported into supported Unity versions.

## Preview and Automation

The component catalog appears under **Tools > Ceres > UI Toolkit Preview** as
**Ceres Neutral · Buttons**. Business-specific action groups stay in their
owning product surface.

Preview and runtime use the same UXML, USS, PanelSettings, scale policy, and
binding. Providers bind deterministic fixtures through disposable sessions.
Visual comparison must use matching viewport, state, enabled status, and scale;
both layout and resolved style are part of acceptance.

## Do's and Don'ts

Do:

- Choose action hierarchy before selecting a Variant.
- Choose density from the owning region and keep each action row consistent.
- Use shared tokens and runtime controls before adding feature-local styles.
- Keep ordinary surfaces neutral and reserve semantic colors for meaning.
- Validate both visible geometry and touch hit targets in production-mounted
  previews.
- Keep focus visible and disabled geometry stable.

Don't:

- Use Primary more than once in a direct decision area.
- Add a left-side highlight strip to an action, tab, selection, or group.
- Turn ordinary text actions into pills or stretch them equally by default.
- Use color as the only expression of state.
- Reimplement shared actions, toggles, or sliders in a feature package.
- Add unsupported web CSS such as `gap`, `color-mix`, or text-box properties to
  Unity USS.
- Claim visual equivalence from class names or declared numbers without
  inspecting the rendered result.
