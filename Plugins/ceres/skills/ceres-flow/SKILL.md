---
name: ceres-flow
description: Build or diagnose Ceres Flow containers, executable nodes, functions, events, code generation, IL post-processing, and hot reload.
---

# Ceres Flow

Inspect the current runtime and Editor Flow assemblies before extending execution behavior. Use Graph guidance as well when a change modifies shared graph serialization, variables, ports, or editor foundations.

## References

- Read [containers-and-codegen.md](references/containers-and-codegen.md) for container selection, `[GenerateFlow]`, generated runtime ownership, and lifecycle.
- Read [events-and-functions.md](references/events-and-functions.md) for implementable events, executable functions, libraries, and custom executable events.
- Read [custom-nodes.md](references/custom-nodes.md) for node base classes, ports, arrays, generics, metadata, and editor views.
- Read [troubleshooting.md](references/troubleshooting.md) when discovery, generation, ILPP, preservation, validation, or hot reload fails.

Prefer executable functions for stateless API exposure. Create a custom node only when behavior needs state, execution control, dynamic ports, async flow, or a specialized editor shape. Do not promise a stable programmatic GraphBuilder API without inspecting the current source.
