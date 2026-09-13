# Preview and automation

The preview host renders a Player-context `UIDocument` into an isolated texture. A provider must supply production assets and deterministic state; it must not load a Studio scene, user content, credentials, or another mutable application state.

## Provider contract

- Implement `IUIToolkitPreviewProvider` in an Editor assembly referencing `Ceres.UIElements.Editor`.
- Use a stable unique provider id, production `VisualTreeAsset`, stylesheets, `PanelSettings`, and scale policy.
- Define fixtures with `UIToolkitPreviewCase<TFixture>` when typed fixture data is useful.
- Return an `IUIToolkitPreviewSession`; the host disposes it before replacing the visual tree.
- Register stable snapshot element names, normally from generated descriptors.
- Use `UIToolkitPreviewViewports.Standard` unless the product needs a distinct viewport or safe-area contract.

## DotCraft workflow

1. Open with `UIToolkitPreviewAutomation.Open(providerId, fixtureId, viewportId)`.
2. In a new bounded Editor query, wait until `IsReady` is true.
3. Call parameterless `GetSnapshot()` for the session set or pass names for a focused snapshot.
4. Capture and crop using the returned Editor window geometry when visual evidence is required.
5. Store temporary output under `.craft/captures/ui-preview/`.

Use the available official Unity automation connection. If compilation or reload interrupts a dispatched call, do not replay it; recover the same connection, make a fresh read-only readiness query, and inspect the Console.

## Fidelity checks

Compare preview and production at the same physical viewport, panel scale, safe area, state, enabled status, and content. Compare element type, local geometry, classes, resolved styles, font/image identity, and missing elements. Window-space coordinates are not comparable across hosts.

If preview differs from runtime, first compare PanelSettings/TSS, style-sheet order, root classes, Player versus Editor context, scale policy, and fixture state. Do not introduce a preview-only style fix or silently fall back to an Editor visual tree.
