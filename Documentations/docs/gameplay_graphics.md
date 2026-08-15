# Graphics

The Ceres graphics module applies persistent, reactive graphics settings to URP cameras, render scale, frame rate, and Volume profiles. `GraphicsController`, `GraphicsSettingsAsset`, and the dynamic Volume APIs are compiled only when `com.unity.render-pipelines.universal` is installed and the `URP_INSTALL` version define is active. `GraphicsConfig` can exist without URP, but no Ceres controller applies it in that configuration.

## Runtime setup

1. Create a `GraphicsSettingsAsset` from `Assets > Create > Ceres > Graphics > GraphicsSettingsAsset`.
2. Add one `GraphicsController` to the gameplay scene and assign that asset.
3. Create the `DynamicVolumeProfileTable` DataTable with `DynamicVolumeProfileRow` rows.
4. Give each row a stable Volume type ID and assign Windows, Mobile, and Console profiles as needed.

The controller registers itself in the world `ContainerSubsystem` during `Start`, so gameplay code can then retrieve the active instance with `GraphicsController.Get()`. The method returns `null` before registration or when no controller is active.

```csharp
using Ceres.Gameplay.Graphics;

GraphicsController graphics = GraphicsController.Get();
graphics.ApplyCameraSettings();
graphics.ApplyDynamicVolumeProfiles();
```

`ApplyCameraSettings` writes the configured field of view and clipping planes to `Camera.main`. It has no effect when the settings asset or main camera is missing.

## Live graphics config

`GraphicsConfig` is stored at `Ceres.Graphics`. Its R3 properties are subscribed by `GraphicsController` during initialization, so changing a supported value applies it without recreating the controller.

```csharp
using Ceres.Gameplay.Graphics;

GraphicsConfig config = GraphicsConfig.Get();

config.Bloom.Value = false;
config.Vignette.Value = true;
config.RenderScale.Value = 2;
config.FrameRate.Value = 1;
```

`RenderScale` is an index into `GraphicsConfig.RenderScalePresets` (`0.7`, `0.8`, `0.9`, `1.0`); unsupported indices are ignored. `FrameRate` is an index into `GraphicsSettingsAsset.frameRateOptions`, not a frame-rate value, and must remain between zero and the final option index.

The standard URP bindings cover Bloom, Depth of Field, Motion Blur, Vignette, render scale, and target frame rate. `GraphicsSettingsAsset` can disable selected features for a project. Depth of Field and Motion Blur are disabled outside Play Mode.

The config is saved when the runtime controller is destroyed.

## Platform Volume profiles

Each `DynamicVolumeProfileRow` defines one logical Volume type with:

- Windows, Mobile, and Console `SoftAssetReference<VolumeProfile>` values;
- a Volume priority;
- a DataTable row ID used as the runtime Volume ID.

The table manager selects Mobile on Android and iOS, Console on Xbox One and PlayStation 5, and Windows otherwise. A missing Mobile or Console reference falls back to the Windows profile. Store the profiles as Addressables in the `Volumes` group. `ApplyVolumeProfiles` accepts an optional `DynamicVolumePlatform` override for Editor preview.

At runtime, `GraphicsController` creates hidden child `Volume` objects for the table rows, assigns the selected profiles and priorities, and exposes them through `GetVolume(id)`. The requested ID must exist in the table.

Built-in reactive toggles are matched by row ID. Use `Bloom`, `DepthOfField`, `MotionBlur`, or `Vignette` when the profile should respond to the corresponding `GraphicsConfig` property.

Other row IDs still produce runtime Volumes and can be retrieved or weighted by project code.

## Look Dev

The custom GraphicsController Inspector provides:

- a Look Dev mode for material and lighting inspection;
- platform-profile preview;
- project quality-level selection;
- direct inspection of generated Volume weights.

Look Dev is an Editor preference. Leaving the mode or pressing refresh reapplies the configured dynamic profiles. It is not a runtime quality preset.

## Optional IllusionRP integration

When `com.kurisu.illusion-render-pipelines` is installed, the `ILLUSION_RP_INSTALL` define adds bindings for pipeline-specific features such as convolution bloom, contact shadows, soft shadows, SSAO, SSR, SSGI, and volumetric fog.

These properties remain part of `GraphicsConfig`, but Ceres only applies the IllusionRP-specific runtime switches when that package is present. Projects using standard URP should treat them as inactive configuration fields.

## Flow integration

`ApplyCameraSettings`, `ApplyDynamicVolumeProfiles`, and `GetVolume` are executable functions. Flow can therefore refresh the active graphics state or retrieve a named Volume without a separate wrapper library.

Keep platform selection, profile authoring, and project quality policy in C# or project data. Flow operates on the controller's resolved runtime state.
