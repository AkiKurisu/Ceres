---
name: ceres-gameplay
description: Build or extend Ceres gameplay systems such as GameWorld, Actor, Level, audio, animation, graphics, AI/EQS, mods, FX, capture, and Gameplay Flow.
---

# Ceres Gameplay

Start from the owning subsystem under `Runtime/Gameplay` and preserve the lifecycle established by GameWorld and Actor. Read [gameplay-systems.md](references/gameplay-systems.md) for subsystem selection, source anchors, and cross-system boundaries.

Keep application-specific rules outside the framework. Use `ceres-flow` when exposing gameplay behavior to executable graphs, and `ceres-core` when the change belongs to a reusable service rather than gameplay orchestration.
