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

`GraphicsConfig` and `DisplayConfig` are stored at `Ceres.Graphics` and `Ceres.Display`. Their R3 properties apply as soon as they change:

```csharp
using Ceres.Gameplay.Graphics;

GraphicsConfig graphics = GraphicsConfig.Get();
graphics.Bloom.Value = false;
graphics.RenderScaleRatio.Value = 0.9f;

DisplayConfig display = DisplayConfig.Get();
display.FrameRateLimit.Value = 60;
display.VSync.Value = true;
```

Process-wide modules apply the values that do not depend on scene content: render scale on the active URP asset, the global texture mipmap limit, the pipeline toggles described below, and display mode, resolution, vertical sync and frame-rate limit. Borderless fullscreen always uses the display's native resolution. Add `Ceres.Gameplay.Graphics.GraphicsSettingsModule` and `Ceres.Gameplay.Graphics.DisplaySettingsModule` to the module list when the project uses an explicit list. A frame-rate limit of zero or below is unlimited; vertical sync ignores the limit. On a desktop player's first run, `DisplayConfig` adopts the window the player opened with.

`GraphicsController` applies the scene-side state: Bloom and Motion Blur weights of the dynamic Volumes, camera settings and the FPS overlay (`r.fps`). `GraphicsSettingsAsset` can disable selected features for a project. Motion Blur is disabled outside Play Mode.

Save the configs after changing them, or edit them through [Settings](./gameplay_settings.md), which saves changed configs when its session closes.

## Platform Volume profiles

Each `DynamicVolumeProfileRow` defines one logical Volume type with:

- Windows, Mobile, and Console `SoftAssetReference<VolumeProfile>` values;
- a Volume priority;
- a DataTable row ID used as the runtime Volume ID.

The table manager selects Mobile on Android and iOS, Console on Xbox One and PlayStation 5, and Windows otherwise. A missing Mobile or Console reference falls back to the Windows profile. Store the profiles as Addressables in the `Volumes` group. `ApplyVolumeProfiles` accepts an optional `DynamicVolumePlatform` override for Editor preview.

At runtime, `GraphicsController` creates hidden child `Volume` objects for the table rows, assigns the selected profiles and priorities, and exposes them through `GetVolume(id)`. The requested ID must exist in the table.

Built-in reactive toggles are matched by row ID. Use `Bloom` or `MotionBlur` when the profile should respond to the corresponding `GraphicsConfig` property.

Other row IDs still produce runtime Volumes and can be retrieved or weighted by project code.

## Look Dev

The custom GraphicsController Inspector provides:

- a Look Dev mode for material and lighting inspection;
- platform-profile preview;
- project quality-level selection;
- direct inspection of generated Volume weights.

Look Dev is an Editor preference. Leaving the mode or pressing refresh reapplies the configured dynamic profiles. It is not a runtime quality preset.

## Optional IllusionRP integration

Pipeline-specific toggles in `GraphicsConfig` (convolution bloom, contact shadows, soft shadows, SSAO, SSR, SSGI, ray traced GI, volumetric fog, area lights, DLSS Neural Rendering) carry `[BindConfigVariable("r.xxx")]`. `GraphicsSettingsModule` forwards their values by name through `ConfigVariableRegistry`, so Ceres never references the render pipeline assembly; a pipeline that registers matching config variables (IllusionRP does through `IllusionRuntimeRenderingConfig`) receives the switches, otherwise the binding is skipped. Screenshot capture reaches the pipeline through `Ceres.Capture.RenderPipelineCaptureHooks`, which IllusionRP registers when installed.

These properties are part of `GraphicsConfig` on every pipeline. Projects using standard URP should treat them as inactive configuration fields.

## Flow integration

`ApplyCameraSettings`, `ApplyDynamicVolumeProfiles`, and `GetVolume` are executable functions. Flow can therefore refresh the active graphics state or retrieve a named Volume without a separate wrapper library.

Keep platform selection, profile authoring, and project quality policy in C# or project data. Flow operates on the controller's resolved runtime state.
