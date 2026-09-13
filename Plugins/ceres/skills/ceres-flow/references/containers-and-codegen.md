# Flow containers and code generation

## Choose a container

- `FlowGraphObject` for an owned runtime graph.
- `FlowGraphAsset` for authored reusable graph assets.
- `FlowGraphInstanceObject` for an instance backed by reusable graph data.
- `FlowGraphScriptableObject` when a ScriptableObject is the natural Unity owner.

Inspect the current base classes before adding another container. Ownership must make initialization, variable scope, disposal, and generated-runtime lookup unambiguous.

## Generated Flow

Use `[GenerateFlow]` only on supported partial host types. Generated code is a build artifact of declarations, not a second hand-maintained implementation. Keep runtime declarations in assemblies referenced by the generator and keep Editor generation orchestration in `Ceres.Flow.Editor`.

When generation changes, verify:

- the source generator or Editor generator discovers the intended assembly;
- generated member names remain stable for serialized callers;
- diagnostics point to the declaring type or member;
- runtime initialization and disposal match interpreted Flow;
- player code has no `UnityEditor` dependency;
- required generated types survive IL2CPP stripping.

Source anchors: `Runtime/Flow/Models`, `Runtime/Flow/Annotations/GenerateFlowAttribute.cs`, `Editor/Flow/CodeGen`, and `Runtime/SourceGenerators`.
