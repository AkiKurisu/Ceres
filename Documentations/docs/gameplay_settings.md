# Settings

Settings describe options a player can change and bind them to configs such as
[Graphics](./gameplay_graphics.md). The model has no UI dependency; the
[settings window](#settings-window) binds it to UXML you provide.

## Contribute settings

Implement `ISettingsContributor` and register it while modules initialize. The
process-wide registry is built from every contributor on first use.

```csharp
using Ceres.Gameplay.Graphics;
using Ceres.Modules;
using Ceres.Settings;
using UnityEngine.Scripting;

public sealed class GraphicsSettings : ISettingsContributor
{
    public void Contribute(SettingsRegistry registry)
    {
        var page = registry.Page("graphics", "Graphics", order: 100);
        var effects = page.Section("effects", "Effects");

        var bloom = effects.Add(new ToggleSetting("graphics.bloom", "Bloom")
            .Describe("Glow around bright light sources.")
            .Cost(SettingImpactLevel.Low));
        bloom.Bind<GraphicsConfig>(config => config.Bloom);

        var scale = effects.Add(new SliderSetting("graphics.render-scale", "Render scale", 0.7f, 1f, 0.1f)
            .Formatted(value => $"{value * 100:0}%"));
        scale.Bind<GraphicsConfig>(config => config.RenderScaleRatio);
    }
}

[Preserve]
public sealed class GameSettingsModule : RuntimeModule
{
    public override void Initialize()
    {
        SettingsRegistry.AddContributor(new GraphicsSettings());
    }
}
```

Identifiers must be unique in the registry. `Page` and `Section` return the
existing entry when the identifier is already present, so several contributors
can add to the same page.

## Setting kinds

| Kind | Value | Use |
| --- | --- | --- |
| `ToggleSetting` | `bool` | On or off |
| `SliderSetting` | `float` | Bounded number; values snap to `Step` |
| `ChoiceSetting<T>` | `T` | One of a fixed list of options |
| `PresetSetting` | derived | Options that assign values to other settings |
| `ActionSetting` | none | A command such as opening another tool |

`Bind<TConfig>(selector)` binds a value setting to a `ReactiveProperty<T>` of a
config. The default is read from a newly constructed config, and the config is
saved when a session that changed it closes. Use the delegate overload of `Bind`
when one setting maps to several config values:

```csharp
var gi = lighting.Add(new ChoiceSetting<int>("graphics.gi", "Global illumination")
    .Option(0, "Off").Option(1, "Screen space").Option(2, "Ray traced"));
var config = GraphicsConfig.Get();
gi.Bind(
    () => config.RayTracedGlobalIllumination.Value ? 2 : config.ScreenSpaceGlobalIllumination.Value ? 1 : 0,
    value =>
    {
        config.ScreenSpaceGlobalIllumination.Value = value == 1;
        config.RayTracedGlobalIllumination.Value = value == 2;
    },
    Observable.Merge(config.ScreenSpaceGlobalIllumination.AsUnitObservable(),
        config.RayTracedGlobalIllumination.AsUnitObservable()),
    defaultValue: 1,
    owner: config);
```

A preset is Custom whenever no option matches the current values. Settings the
player cannot edit are ignored when a preset is applied or matched.

```csharp
section.Add(new PresetSetting("graphics.preset", "Preset", "Custom")
    .Option("Low", preset => preset.Set(bloom, false).Set(scale, 0.8f))
    .Option("High", preset => preset.Set(bloom, true).Set(scale, 1f)));
```

## Edit state

Conditions return `SettingEditState.Enabled`, `Disabled(reason)` or `Hidden`.
A disabled setting stays visible and its reason, when given, appears in the
detail area; a hidden setting has no row. Conditions are evaluated whenever a
view refreshes, so a condition that reads another setting needs no dependency
declaration.

```csharp
frameRate.When(() => SettingEditState.When(!vSync.Value, "Vertical sync limits the frame rate."));
dlss.When(() => SettingEditState.VisibleWhen(feature.IsDLSSNeuralRenderingAvailable));
orientation.WhenTrait(SettingsTraits.Mobile);
```

Choice options take their own condition, so one option can be unavailable while
the others remain selectable. `SettingsTraits` provides `Desktop`, `Mobile` and
`Touch`; `SettingsTraits.Add` adds custom traits, and `SettingsTraits.Emulate`
replaces the trait set in Editor previews outside Play Mode.

A slider can show a toggle in its own row:

```csharp
var mute = new ToggleSetting("audio.music.mute", "Mute");
mute.Bind<AudioConfig>(config => config.MusicMute);
var music = audio.Add(new SliderSetting("audio.music", "Music", 0, 100, 1).WithToggle(mute));
music.Bind<AudioConfig>(config => config.MusicVolume);
```

## Sessions

A `SettingsSession` records every value when it opens.

| Call | Effect |
| --- | --- |
| `RevertAll()` | Restore the recorded values |
| `ResetPage(page)` | Restore defaults of the page's editable, resettable, immediate settings |
| `Stage(setting, value)` / `ApplyStaged()` | Hold a confirmed setting's value, then apply it and start the countdown |
| `Keep()` / `RevertConfirmation()` | End the countdown with or without the new value |
| `Update()` | Revert the confirmation when its countdown has expired |
| `Close()` | Drop staged values, revert an unconfirmed change and save every config that changed |

Settings marked with `Confirmed()` are staged instead of applied, revert unless
kept within the countdown, and are excluded from page resets.

## Settings window

`Ceres.Settings.UI` binds a registry to a window built from your UXML. Add a
`SettingsWindowHost` next to a `UIDocument` whose source asset is the window
UXML, and assign the row templates to the host.

The window UXML names these elements:

| Name | Type | Role |
| --- | --- | --- |
| `settings-root` | VisualElement | Shown while open; start it with `display: none`. Receives `ceres-touch` in touch layouts |
| `settings-nav` | VisualElement or ScrollView | Receives one tab per page |
| `settings-content` | VisualElement or ScrollView | Receives the pages |
| `settings-detail` | VisualElement | Detail area of the focused setting |
| `settings-detail-title`, `settings-detail-description`, `settings-detail-reason`, `settings-detail-restart` | Label | Detail text |
| `settings-detail-impact` | VisualElement | Receives one impact template per declared cost |
| `settings-detail-preview` | VisualElement | Optional; shows the focused setting's preview image as its background |
| `settings-close`, `settings-reset-page`, `settings-revert`, `settings-apply` | Button | Window actions; Apply shows while values are staged |
| `settings-confirm` | VisualElement | Shown during a confirmation countdown |
| `settings-confirm-message` | Label | Countdown prompt |
| `settings-confirm-keep`, `settings-confirm-revert` | Button | Confirmation actions |

Elements with the class `settings-safe-area` keep their content inside the
screen safe area: each receives, as padding, the part of it that lies outside the
safe area, so its background still reaches the screen edge. Left and right use
the larger of the two side insets, keeping content centered when only one side has
a cutout. The framework owns their padding, so put their own spacing on their
children.

`SettingsWindowTemplates` holds one template per element the framework creates:

| Template | Required elements |
| --- | --- |
| `tab` | Button `settings-tab` |
| `section` | Label `settings-section` |
| `impact` | Label `settings-impact-name`, Label `settings-impact-level` |
| `toggle` | `settings-row`, Label `settings-row-label`, Toggle `settings-row-toggle` |
| `slider` | `settings-row`, Label `settings-row-label`, Slider `settings-row-slider`, optional Label `settings-row-value`, optional Toggle `settings-row-toggle` for a companion toggle (`WithToggle`) |
| `choice`, `choiceList` | `settings-row`, Label `settings-row-label`, an `ISettingsChoiceControl` named `settings-row-choice` |
| `action` | `settings-row`, Label `settings-row-label`, Button `settings-row-button` |

Every row template may add a Label `settings-row-detail` for an inline
description. The framework toggles `settings-row--focused`,
`settings-row--disabled`, `settings-tab--selected` and
`settings-section--first`.

```csharp
host.Text = new SettingsWindowText { ConfirmMessage = "Reverting in {0} seconds." };
host.Open("graphics");
```

The host survives scene loads. `Open` without a page shows the page that was
open last, `Cancel()` reverts a pending confirmation before it closes the window,
and `IsOpen` and `OpenChanged` report the window state. `Touch` defaults to the
`Touch` trait of the device.

### Preview images

A setting or an option can name a preview image by key. Keys are plain strings;
your project decides which image each key stands for.

```csharp
lighting.Add(new ToggleSetting("graphics.ssao", "Ambient occlusion").WithPreview("graphics.ssao"));
lighting.Add(new ChoiceSetting<int>("graphics.gi", "Global illumination")
    .Option(0, "Off", preview: "graphics.gi.off")
    .Option(1, "Screen space", preview: "graphics.gi.ssgi"));
```

Assign an `ISettingsPreviewSource` to `host.PreviewSource` to supply the images:

```csharp
public sealed class PreviewImages : ISettingsPreviewSource
{
    public async UniTask<Texture2D> LoadAsync(string key, CancellationToken cancellationToken)
    {
        // Return null when there is no image for the key.
        return await LoadFromYourContent(key, cancellationToken);
    }

    public void Release(string key) => ReleaseFromYourContent(key);
}
```

The window shows the image in `settings-detail-preview`. For a choice, it uses
the option under the pointer, then the selected option, then the setting's own
key; the element is hidden when no image applies. Images load on first use, stay
cached while the window is open and are released when it closes. A choice
control reports the option under the pointer by implementing
`ISettingsChoiceHighlight`; without it the image follows the selected option.
The touch layout shows no previews.
