# Typed UI Toolkit binding

Use one binding implementation for preview and runtime.

```csharp
[UIToolkitView("settings-")]
internal sealed partial class SettingsBinding
{
    [UIToolkitQuery] private Button _apply;
    [UIToolkitQuery("status-text")] private Label _status;

    private void Initialize(VisualElement root) => BindGeneratedElements(root);
}
```

The generator strips a leading underscore, converts the field name to kebab-case, and prepends the view prefix. An explicit query replaces the inferred suffix. It emits `BindGeneratedElements` and `GeneratedElementDescriptors`.

Only declare elements the binding actually uses. Fields must be mutable instance fields derived from `VisualElement`. Compile diagnostics cover invalid declarations and duplicate names; missing UXML elements and type mismatches fail when binding because Unity does not expose UXML to the generator.

Use `UIToolkitBindingScope` for clicks, value changes, pointer/UI events, and custom cleanup. Dispose it with the view. Keep business mapping as explicit typed C# calls rather than method attributes or string commands.

During rendering, use `SetValueWithoutNotify` for controls whose change callbacks issue commands. Preserve active text editing and pointer capture when periodic state refreshes update the view.
