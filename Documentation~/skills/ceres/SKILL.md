---
name: ceres
description: Practical handbook for extending Ceres Flow in Unity projects. Use when Codex needs to add or review Ceres Flow containers, ImplementableEvent or ExecutableEvent events, ExecutableFunction APIs, ExecutableFunctionLibrary classes, custom Flow nodes, generic nodes, port-array nodes, or diagnose Ceres source generator, ILPP, linker, or hot reload issues.
---

# Ceres

Use this skill when working with Ceres Flow extension code in a Unity project.
Prefer existing Ceres package patterns over inventing new graph infrastructure.

## First Steps

1. Locate the Ceres package before making decisions. Common locations:
   - `Packages/Ceres`
   - `Packages/com.kurisu.ceres`
   - a Unity package listed as `com.kurisu.ceres`
2. Read the relevant Ceres source or docs near the user's task. Useful package docs usually live in `Documentation~/docs`.
3. Choose one path below and load only the matching reference file.

## Extension Paths

- **Containers and code generation**: Read `references/containers-and-codegen.md` when creating or modifying Flow containers, graph assets, runtime objects, ScriptableObject containers, or `[GenerateFlow]` classes.
- **Events and executable functions**: Read `references/events-and-functions.md` when exposing C# APIs to Flow, adding `[ImplementableEvent]`, creating custom `[ExecutableEvent]` event types, or writing `ExecutableFunctionLibrary` classes.
- **Custom nodes**: Read `references/custom-nodes.md` when implementing a custom node, generic node, port-array node, port metadata, or custom node behavior.
- **Troubleshooting**: Read `references/troubleshooting.md` when generated code, ILPP event injection, function discovery, type preservation, hot reload, or Ceres editor search behavior is wrong.

## Working Rules

- Do not change Ceres package internals for ordinary user extension work. Add extension code in the user's Unity project unless the user explicitly asks to modify Ceres itself.
- Keep Ceres Flow APIs in runtime assemblies and editor-only helpers in editor assemblies.
- Prefer `FlowGraphObject`, `FlowGraphAsset`, `FlowGraphInstanceObject`, and `FlowGraphScriptableObject` before designing custom containers.
- Prefer `[ExecutableFunction]` and `ExecutableFunctionLibrary` for exposing C# methods. Use custom nodes only when the behavior needs state, custom execution flow, async control, dynamic ports, or editor-specific node shape.
- Do not teach or implement programmatic graph/blueprint creation from this skill unless the user explicitly asks for it; if asked, inspect the current Ceres source first because there is no single stable public GraphBuilder facade.
- Validate with Unity compilation or the most local available compile/test signal, and inspect generated warnings when source generation or ILPP is involved.
