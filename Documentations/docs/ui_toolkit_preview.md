# UI Toolkit Preview

Ceres can preview runtime UI Toolkit surfaces in the Editor without entering Play Mode. Open **Tools > Ceres > UI Toolkit Preview** to select a surface, state, and viewport. Changes to the active UXML, USS, or PanelSettings rebuild the preview automatically.

The preview uses the production UXML, USS, PanelSettings, scale policy, and Player rendering context. This keeps layout and resolved styles comparable with the runtime UI.

## Provide a surface

Implement `IUIToolkitPreviewProvider` in an Editor assembly that references `Ceres.UIElements.Editor`. Give the provider a stable id, deterministic fixtures, supported viewports, and the assets used by production. Create a disposable session to bind the selected fixture:

```csharp
public sealed class SettingsPreviewProvider : UIToolkitPreviewProvider<SettingsFixture>
{
    // Id, assets, fixtures, viewports, and panel scale omitted.
    public override string Id => "settings";
    public override string DisplayName => "Settings";

    protected override IUIToolkitPreviewSession CreateSession(
        VisualElement root,
        UIToolkitPreviewContext context,
        SettingsFixture fixture)
    {
        var binding = new SettingsBinding(root, new PreviewActions(fixture));
        binding.Render(fixture.State);
        return new UIToolkitPreviewSession(
            binding,
            SettingsBinding.GeneratedElementDescriptors);
    }
}
```

Use `UIToolkitPreviewViewports.Standard` for common landscape layouts. Feature packages may provide additional viewports and safe areas when their product contract requires them.

## Bind a runtime view

The same binding can be constructed by the preview provider and the production `UIDocument`:

```csharp
[UIToolkitView("settings-")]
internal sealed partial class SettingsBinding : IDisposable
{
    [UIToolkitQuery] private Button _apply;
    [UIToolkitQuery("status-text")] private Label _status;
    private readonly UIToolkitBindingScope _scope = new();

    public SettingsBinding(VisualElement root)
    {
        BindGeneratedElements(root);
    }

    public void Dispose() => _scope.Dispose();
}
```

The generator resolves named elements and reports invalid declarations. Use `UIToolkitBindingScope` to own event subscriptions, and use `SetValueWithoutNotify` when rendering state into controls whose callbacks issue commands.

## Automation

The static API provides a stable entry point for Editor automation:

```csharp
UIToolkitPreviewAutomation.Open("settings", "default", "desktop-1920x1080");

// Query in a later bounded Editor call.
return UIToolkitPreviewAutomation.IsReady
    ? UIToolkitPreviewAutomation.GetSnapshot()
    : "not ready";
```

Use named elements with `GetSnapshot(...)` for a focused comparison. Match viewport, scale, state, and enabled status when comparing Preview with a production `UIDocument`.
