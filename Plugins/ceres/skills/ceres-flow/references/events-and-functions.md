# Executable events and functions

Choose the least specialized exposure mechanism.

- `[ExecutableFunction]` exposes a suitable C# method as an executable call.
- `ExecutableFunctionLibrary` groups stateless functions that do not belong to an instance.
- `[ImplementableEvent]` lets a host declaration receive a Flow implementation through generated or injected glue.
- `[ExecutableEvent]` defines a reusable event shape when an existing event type cannot represent the execution contract.

Keep displayed names, parameter direction, defaults, return handling, and self-target semantics explicit. Do not use reflection-only discovery in player code when the existing registry/generation path can produce stable metadata.

For a missing entry, inspect the declaration attribute, supported signature, assembly references, generated output/ILPP diagnostics, registry refresh, and preservation in that order. Do not add a second registry to work around a discovery failure.

Source anchors: `Runtime/Flow/Annotations`, `Runtime/Flow/Models/ExecutableReflection.cs`, `Editor/Flow/ExecutableFunctionRegistry.cs`, and `Editor/Flow/FlowGraphFunctionRegistry.cs`.
