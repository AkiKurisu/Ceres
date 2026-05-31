# Built-in Flow Nodes and Libraries

Ceres ships with built-in Flow nodes for control flow, arrays, timing, data conversion, event subscription, asset loading, and Unity API bridging. These nodes cover common visual scripting primitives without requiring custom C# nodes for everyday graph logic.

## Tooltip Sources

Flow node tooltips come from two sources:

- Custom runtime nodes use `[NodeInfo("...")]` on the node class.
- `ExecutableFunction` nodes use XML documentation comments on the C# method that exposes the function.

For `ExecutableFunction` methods, Ceres reads the `<summary>`, `<param>`, and `<returns>` XML comments from the source file when the node view is created. Add XML comments to every function exposed through an `ExecutableFunctionLibrary`; they become the node tooltip in the graph editor and improve generated API documentation.

```csharp
/// <summary>
/// Gets the active state stored directly on the GameObject.
/// </summary>
/// <param name="gameObject">The target GameObject.</param>
/// <returns>True when the GameObject is locally active.</returns>
[ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetActiveSelf")]
public static bool Flow_GameObjectGetActiveSelf(GameObject gameObject)
{
    return gameObject.activeSelf;
}
```

## Built-in Node Categories

### Flow Control

- `Branch`
- `Sequence`
- `Switch on String`
- `Switch on Int`
- `Switch on Enum`
- `For Loop`
- `For Loop With Break`
- `For Each Loop`
- `While Loop`
- `Do Once`
- `Do N`
- `Flip Flop`
- `Gate`
- `MultiGate`

### Timing

- `Delay`
- `Retriggerable Delay`
- `Delay Frames`
- `Next Frame`
- `Wait Until`
- `Wait While`
- `Timeout`

### Array

- `Make Array`
- `Get`
- `Length`
- `Is Valid Index`
- `Contains`
- `Index Of`
- `First`
- `Last`
- `Random Element`
- `Append`
- `Insert`
- `Set`
- `Remove At`
- `Remove Item`
- `Clear`
- `Reverse`
- `Shuffle`

### Data Utilities

- `Equals`
- `Not Equals`
- `Compare`
- `Is Null`
- `Is Not Null`
- `Cast`
- `Select`

### Runtime Helpers

- `Get Config`
- `Load Asset Async`
- `Instantiate`
- `Subscribe`
- `Subscribe Event`
- `Global Subscribe Event`
- `Debug Log`
- `Log String`

## Executable Function Libraries

Built-in libraries expose C# and Unity APIs as visual function nodes:

- `MathExecutableLibrary`: scalar math, booleans, Vector2, Vector3, and Quaternion helpers.
- `UnityExecutableLibrary`: UObject, GameObject, Transform, Component, Random, Time, Physics, and LayerMask helpers.
- `TextExecutableLibrary`: string concatenation, joining, formatting, replacement, casing, and hashing.
- `RxExecutableLibrary`: subscriptions and disposable lifetime helpers.
- `ResourceExecutableLibrary`: resource loading and instantiation helpers.
- `SchedulerExecutableLibrary`: timer and frame-counter scheduling helpers.
- `DataDrivenExecutableLibrary`: DataTable manager, table, and row access helpers.
- `ConfigsExecutableLibrary`: config persistence helpers.
- `CeresExecutableLibrary`: Ceres runtime settings helpers.

Prefer `ExecutableFunctionLibrary` for stateless API bridging. Use custom nodes when the behavior needs custom execution flow, dynamic ports, async continuation, persistent node state, or editor-specific node views.
