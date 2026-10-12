# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.1] - 2026-10-12

### Added

- `[EditorOnly]` keeps a Data Table row member in the source and out of the runtime table.

### Changed

- Data Tables are authored as `.datatable` text files and imported as read-only binary tables.

### Removed

- Removed the earlier Data Table asset format and runtime editing of Data Tables.

## [1.1.0] - 2026-10-11

### Added

- Added animation binary packing, importing and extraction for lossless `.animbin` animation assets.
- Added an Editor-only UI Toolkit preview host with deterministic fixtures, viewport simulation, live asset reload and automation APIs.
- Added the Ceres Neutral runtime theme with shared buttons, toggles, sliders and a framework-owned component catalog.
- Added runtime settings: pages, sections and settings bound to configs, with edit states, presets, confirmed changes and sessions that save changed configs.
- Added a settings window framework that binds a settings registry to product-owned UXML templates.
- Added `DisplayConfig` for display mode, resolution, vertical sync, frame-rate limit and FPS display.
- Added ray traced GI (`r.rtgi`), sun shafts (`r.sunshafts`) and texture mipmap limit to `GraphicsConfig`.
- Added per-asset build dependency digests to the content pipeline, so cached bundles rebuild when inputs outside Unity's dependency hash change.

### Changed

- `GraphicsSettingsModule` and `DisplaySettingsModule` apply graphics and display configs for the whole process; `GraphicsController` only applies scene-side state.
- `GraphicsConfig.RenderScaleRatio` and `DisplayConfig.FrameRateLimit` replace the preset indices `RenderScale` and `FrameRate`.
- The `GraphicsController` inspector edits the frame-rate limit and render scale in Play Mode.

### Removed

- Removed `GraphicsConfig.Vignette` and `GraphicsSettingsAsset.frameRateOptions`.

### Fixed

- Resources and Data Table managers initialize reliably in Edit Mode, and concurrent initialization calls share one pass.

## [1.0.1] - 2026-09-05

### Added

- Content pipeline automates incremental package reuse and indexes releases without bundle duplication.
- `Ceres.Capture.RenderPipelineCaptureHooks` lets a render pipeline supply temporal readiness and camera data copying to screenshot capture.
- `[BindConfigVariable]` and `ConfigVariable.SetValue(value, save)` forward config members to config variables by name.

### Changed

- `GraphicsController` forwards pipeline feature toggles through `ConfigVariableRegistry`; `Ceres.Gameplay` has no assembly reference to IllusionRP.

### Fixed

- Screenshot capture preserves the per-object shadow light source on the isolated camera.

## [1.0.0] - 2026-08-15

Initial stable release.
