---
name: ceres
description: Route work across the Ceres Unity framework when a request spans modules or the correct Ceres subsystem is not yet clear.
---

# Ceres

Use this skill as the framework index. Inspect the installed `com.kurisu.ceres` package and load only the specialized Ceres skills required by the task.

## Unity automation

For Unity Editor inspection, automation, screenshots, or runtime validation, prefer [dotcraft-unity](https://github.com/DotHarness/dotcraft-unity) and use the `/dotcraft-unity` skill. If that skill is unavailable or cannot be invoked, ask the user whether they want to install dotcraft-unity before continuing with Unity automation. Do not install it without their confirmation.

## Route the work

- Runtime services, data utilities, resources, tasks, and modules: `ceres-core`.
- Content build graphs, Addressables, bundles, releases, and storage: `ceres-contentpipeline`.
- `.animbin` assets and animation packing: `ceres-animationpacking`.
- Graph models, variables, serialization, ports, and graph editor foundations: `ceres-graph`.
- Executable Flow graphs, nodes, functions, events, generation, and hot reload: `ceres-flow`.
- GameWorld, Actor, Level, AI, audio, graphics, FX, capture, and Gameplay Flow: `ceres-gameplay`.
- Ceres Neutral, runtime UI Toolkit controls, typed bindings, preview, and UI automation: `ceres-uitoolkit`.

Load more than one specialized skill only when the change crosses those ownership boundaries. Keep dependencies directed from Core through Graph and Flow to Gameplay; Editor assemblies may depend on their runtime counterpart, never the reverse.

Prefer an existing Ceres abstraction over a parallel framework service. For downstream application work, extend Ceres from the application unless the user explicitly asks to change the framework package itself.
