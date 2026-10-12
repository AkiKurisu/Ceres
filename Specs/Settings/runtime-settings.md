# Runtime Settings

This specification owns the runtime settings contract: the setting model, its
ownership, apply and persistence semantics, and the settings window. Graphics
and display configs it edits are described by the
[Graphics guide](../../Documentations/docs/gameplay_graphics.md); usage and
extension examples belong to the
[Settings guide](../../Documentations/docs/gameplay_settings.md).

## Purpose and Boundary

Settings are the user-facing layer over persistent configs. A setting describes
one choice a player can make — its label, description, value domain, default,
edit state and apply behavior — and binds to the config value that stores it.
The model is product-agnostic: Ceres ships no player-visible text, and the host
product composes pages, sections and wording.

Settings edit the user's limits and preferences only. In the rendering layer
chain `capability → user setting → content request`, a renderer capability
decides whether a feature can run on this device, a setting decides whether the
player allows it, and scene content such as a Volume decides whether the scene
uses it. A feature runs only when all three agree. Settings never write scene
content, and scene content never writes settings.

The model has no UI Toolkit dependency so automation, consoles and tests can
drive it. The settings window framework is a separate UI Toolkit layer that
binds the model to product-owned assets.

## Model

A registry holds ordered pages; a page holds ordered sections; a section holds
ordered settings. Every setting has a stable identifier that is unique in the
registry and is never shown to players.

Setting kinds:

- **Toggle**: a boolean.
- **Slider**: a bounded number with a step and display format. A slider may
  carry a companion toggle shown in its row; the toggle takes part in sessions
  like any other setting but has no row of its own.
- **Choice**: one of a fixed list of options. Each option has its own edit
  state, so an option can be unavailable while the setting remains editable.
- **Action**: a command, such as opening another tool. It has no value.
- **Preset**: a choice whose options assign values to other settings. Its value
  is derived: the first preset whose assignments all match the current values,
  otherwise Custom. Choosing a preset writes its assignments; editing any
  assigned setting therefore turns the preset into Custom without extra state.

A value setting reads, writes and observes its value through a binding. Binding
to a config's reactive property is the normal case; a binding may also map one
setting onto several config values, such as a method selector that drives two
feature flags. Bindings are typed delegates, never reflection paths, so they
survive IL2CPP stripping. A binding to a config also identifies the config that
must be saved after a change.

A setting and each of its options may carry a preview key naming an image that
illustrates it. Keys are opaque to the model; the product resolves them to
images, so the model holds no assets.

Every value setting has a default. Resetting restores defaults; defaults come
from the setting definition or the config type's constructed state, never from
persisted user data.

### Edit state

A setting is Enabled, Disabled with a player-facing reason, or Hidden. Hidden is
for settings that do not exist on the current platform or configuration;
Disabled is for settings the player could use under other conditions, and the
reason must say which. Edit state is evaluated from conditions supplied by the
setting definition and re-evaluated after every value change in the registry,
so dependencies between settings need no separate declaration.

Platform rules use traits — Desktop, Mobile, Touch and product-defined traits —
rather than compile-time platform checks. The Editor can emulate a trait set so
previews and tests can show another platform's settings.

### Apply behavior

Most settings apply immediately: writing the value updates the config's
reactive property, and the systems observing that property react in the same
frame. Settings whose change can leave the player unable to see or operate the
window, such as display mode and resolution, are confirmed: the new value is
staged, applied explicitly, and reverted automatically unless the player keeps
it before a timeout. Settings that take effect only after restart say so.

## Session and Persistence

The window opens a session that records the value of every setting. Within a
session the player can revert all changes to that record or reset one page to
defaults. Closing the session saves each config that changed once. Values are
never saved while a confirmation is pending; an unconfirmed change reverts.

## Ownership and Lifetime

Contributors fill the registry. Any package can contribute pages, sections or
settings by registering a contributor during module initialization; the
registry is built from all contributors on first use and lasts for the process.
Registration is explicit, so the set of contributors does not depend on
assembly scanning. A contributor owns the wording, conditions and bindings of
what it adds; it does not depend on another contributor's objects except by
setting identifier.

Configs that settings bind to apply themselves without the window. Display
values apply at startup. Graphics feature values are forwarded to the runtime
rendering variables they control by a process-wide module, independent of any
scene object, so a value set before or between scene loads is in force when
rendering starts. Scene objects only apply scene-specific state.

## Settings Window

Ceres provides the window framework, not the window. The product owns every
visual asset: the window UXML, the row, section, tab and impact templates, the
style sheets, fonts and static wording. The framework binds a registry to those
assets through a fixed contract of element names and control types, so one
implementation of navigation, editing, confirmation and session handling serves
products with different visual languages. A missing or mistyped contract element
fails when the window is built.

The window presents one registry. Pages become tabs; sections become headers;
each setting instantiates the row template of its kind and binds its value
control. A choice uses the short-choice template when its options are few and
short, and the list template otherwise. The detail area shows the focused
setting's description, its performance impact when declared, the reasons it or
any of its options is disabled and whether it needs a restart. The window shows a
pending confirmation with a countdown and focuses the action that keeps the new
value.

When the product's UXML has a preview element and the host has a preview source,
the detail area also shows the focused setting's preview image. A choice
previews the option under the pointer, then its selected option, then the
setting's own key; with no image the element is hidden, and while an image loads
it stays empty rather than showing the previous one. Images load on first use,
stay cached for the session and are released when the window closes. The touch
layout requests no previews. Wording the framework composes at runtime — the countdown prompt, impact
names and levels, the restart note — is supplied by the product.

The window exists outside scene content: one host per process owns its panel,
survives scene loads and opens or closes it on request. Opening never pauses or
hides the scene, so rendering changes are visible behind the window while the
player edits them. While open, the window owns pointer and keyboard input for
its area and reports that state so the product can block conflicting input.
Touch layouts mark the window root so product styles can switch to touch-sized
rows and inline descriptions. Previews belong to the product and render its
production assets with fixtures.
