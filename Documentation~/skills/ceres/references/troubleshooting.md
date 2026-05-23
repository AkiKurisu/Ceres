# Ceres Troubleshooting

Use this reference when Ceres Flow extension code compiles but does not appear, generated code is missing, events do not fire, or runtime graph behavior differs from the editor.

## Source Generator Checks

For `[GenerateFlow]` containers:

- Confirm the class is `partial`.
- Confirm the declaration has a base type; the generator receiver filters for classes with a `BaseList`.
- Confirm the assembly references Ceres.
- Confirm the class has a namespace that the generator can process.

For `ExecutableFunctionLibrary`:

- The class must inherit `ExecutableFunctionLibrary`.
- The class must be `partial`; missing it should report diagnostic `Ceres001`.
- Methods must be `public static` and marked `[ExecutableFunction]`.

For custom `[ExecutableEvent]` events:

- The event type must be non-generic.
- The type should inherit `EventBase<TEvent>`.
- Public getters become output ports on the generated executable event node.
- A static method marked `[ExecutableEvent]` generates a create-event node.

Generated files and logs may be written under `Temp/CeresGenerated` when generator file output is enabled.
Use `Assets/Create/Ceres/SourceGenerator AnalyzerConfig` to create `Assets/Default.globalconfig` with generator debug options.

## ImplementableEvent Does Not Fire

Check these in order:

1. The method is marked `[ImplementableEvent]`.
2. The containing type implements `IFlowGraphRuntime`. Built-in runtime containers do; custom classes must generate or implement it.
3. The method name matches the event node name in the graph.
4. If the method manually calls `ProcessEvent` or `ProcessEventUber`, ILPP will skip auto-injection. That is expected.
5. If the method overrides a base method that already has `[ImplementableEvent]` and calls the base method, ILPP may skip injection to avoid duplicate calls.
6. For more than 6 parameters, inspect `ProcessEventUber` behavior and boxing/allocation issues.

## Executable Function Not In Search

- For instance methods, confirm `[ExecutableFunction]` is on the method unless the declaring assembly is included by Flow Settings.
- For static methods, prefer `partial class MyLibrary : ExecutableFunctionLibrary`.
- Avoid generic methods; Ceres does not support `[ExecutableFunction]` generic methods.
- Avoid duplicate method name plus parameter count in the same class hierarchy.
- Use `[CeresLabel]` and `SearchName` when overloads or operator-like names are hard to find.
- If the method is a script method, confirm the first parameter is the intended target type and `IsScriptMethod = true`.
- If `IsSelfTarget = true`, confirm the graph context object is assignable to the target port type when left unconnected.

## Custom Event Not In Search

- Confirm `[ExecutableEvent]` is on the `EventBase<T>` subclass.
- Confirm the generated `ExecutableEvent_{EventType}` class exists or generator output/logs explain why it does not.
- The event implementation appears under custom event implementation search entries, not as a normal `ExecutionEvent`.
- Send runtime events with `this.SendEvent(evt)` from an `IFlowGraphRuntime`.
- Use `OverrideEventImplementation<TEvent>()` only when script-side behavior should intercept graph handling.

## Node Or Port Problems

- Custom nodes must be `[Serializable]`.
- Use `CeresPort<T>` for data ports and `NodePort` for execution flow.
- Use `[OutputPort(false)]` for execution ports.
- For dynamic port arrays, implement `IReadOnlyPortArrayNode`; implement `IPortArrayNode` only if the editor should resize the array.
- Only one port array is supported per node type.
- For generic nodes, verify the template class name exactly matches `{NodeClassName}_Template`.
- If a node requires a dragged port, implement `RequirePort()` in the template or use `[RequirePort]`.

## Type Preservation And Linker

Ceres uses `CeresLinker` to preserve types used by visual scripts for IL2CPP builds.

When types are missing only in builds:

- Check the preserved types section in Project Settings > Ceres.
- Register extra types in editor tooling with `CeresLinker.LinkType(typeof(MyType))` or `CeresLinker.LinkTypes(...)`.
- Call `CeresLinker.Save()` after registering types.
- Generic node argument types should be linked when graph data is serialized through the editor path; inspect custom tooling if bypassing it.

## Hot Reload

Flow graph hot reload is editor play-mode only.

- It tracks `FlowGraphData.saveTimestamp`.
- It replaces runtime graph instances for matching containers.
- If execution is already in progress, active execution may continue on the old graph while new events use the new graph.
- Hot reload depends on active `FlowGraphObjectBase` runtime instance tracking.

## Useful Package Files

- `Editor/Flow/SourceGeneratorSettings.cs`
- `Editor/Flow/HotReload/FlowGraphHotReloadManager.cs`
- `Editor/Core/CeresLinker.cs`
- `Runtime/CodeGen/ExecutableReflectionILPP.cs`
- `Runtime/SourceGenerators/Source~/Ceres.SourceGenerator/Generators/*.cs`
- `Runtime/Flow/Models/ExecutableReflection.cs`
