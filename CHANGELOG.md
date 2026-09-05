# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
