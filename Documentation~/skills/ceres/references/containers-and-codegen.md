# Ceres Containers And Code Generation

Use this reference when adding or modifying Ceres Flow containers or generated container implementations.

## Container Decision Guide

- Use `FlowGraphObject` for normal scene `MonoBehaviour` gameplay logic with graph data stored on the component.
- Use `FlowGraphAsset` when one graph asset should be reused by multiple runtime instances. Set `runtimeType` so the editor knows what `Self`, properties, and implementable events should target.
- Use `FlowGraphInstanceObject` for a scene object that executes a referenced `FlowGraphAsset`.
- Use `FlowGraphScriptableObject` when the logic asset should own and execute its own graph, such as skills, states, buffs, dialogue, or other asset-driven behavior.
- Create a custom `[GenerateFlow] partial` class only when the built-in containers do not fit the runtime ownership model.

## Core Interfaces

- `IFlowGraphContainer` owns persistent graph data:
  - `FlowGraph GetFlowGraph()`
  - `FlowGraphData GetFlowGraphData()`
  - `void SetGraphData(CeresGraphData graphData)`
  - `UObject Object`
- `IFlowGraphRuntime` owns the runtime graph instance:
  - `UObject Object`
  - `FlowGraph Graph`
- `FlowGraphObjectBase` caches a runtime `FlowGraph`, compiles it on first `Graph` access, and disposes it through `ReleaseGraph()`.

## GenerateFlow

`[GenerateFlow]` tells `Ceres.SourceGenerator` to generate container/runtime boilerplate for a `partial` class.

```csharp
using Ceres.Graph.Flow.Annotations;
using UnityEngine;

[GenerateFlow(GenerateImplementation = true, GenerateRuntime = true)]
public partial class MyFlowRuntime : MonoBehaviour
{
}
```

Generation flags:

- `GenerateImplementation = true`: generate serialized `FlowGraphData graphData`, `Object`, `GetFlowGraph()`, `GetFlowGraphData()`, `SetGraphData()`, and protected `GetGraphData()`.
- `GenerateRuntime = true`: generate runtime `Graph` cache, compile-on-first-access, and `ReleaseGraph()`.

Built-in patterns:

- `FlowGraphScriptableObjectBase`: generated container implementation only.
- `FlowGraphScriptableObject`: generated runtime only, inheriting the container implementation from the base class.
- `FlowGraphObject`: generated container implementation; runtime caching is handled by `FlowGraphObjectBase`.

## Source Generator Constraints

- The class must be `partial`.
- The class must have a base type in the declaration for the generator receiver to pick it up.
- Prefer normal namespace declarations. If behavior seems missing, inspect generated output and generator logs before assuming the C# code is wrong.
- The project assembly must reference Ceres; generators skip assemblies that do not reference the Ceres assembly.

## Runtime Lifecycle

Use this pattern when manually compiling a runtime graph:

```csharp
using var context = FlowGraphCompilationContext.GetPooled();
using var compiler = CeresGraphCompiler.GetPooled(graph, context);
graph.Compile(compiler);
```

Do not mutate persistent `FlowGraphData` during play mode unless the workflow explicitly supports hot reload. In editor mode, clone graph data before creating instances when persistent data must stay untouched.

## Useful Package Files

- `Runtime/Flow/FlowGraphObject.cs`
- `Runtime/Flow/FlowGraphAsset.cs`
- `Runtime/Flow/FlowGraphInstanceObject.cs`
- `Runtime/Flow/FlowGraphScriptableObject.cs`
- `Runtime/Flow/Annotations/GenerateFlowAttribute.cs`
- `Runtime/SourceGenerators/Source~/Ceres.SourceGenerator/Generators/FlowGraphGenerator.cs`
