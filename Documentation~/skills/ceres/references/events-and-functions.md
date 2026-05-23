# Ceres Events And Executable Functions

Use this reference when exposing C# behavior to Ceres Flow or adding Flow event entry points.

## ImplementableEvent

Use `[ImplementableEvent]` on methods of a type that implements `IFlowGraphRuntime`, usually a `FlowGraphObject` or compatible generated runtime container.

```csharp
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Annotations;

public class DoorFlow : FlowGraphObject
{
    [ImplementableEvent]
    public void Opened(int doorId)
    {
    }
}
```

Behavior:

- ILPP injects `this.ProcessEvent(...)` at the start of the method.
- If the method already calls `ProcessEvent` or `ProcessEventUber`, ILPP skips automatic injection. Use this to control event timing manually.
- Up to 6 parameters use strongly typed `ProcessEvent<T...>` overloads. More parameters use `ProcessEventUber(object[])` and allocate/box.
- Implementable events only work when the containing type implements `IFlowGraphRuntime`.

## ExecutableFunction On Instance Methods

Use `[ExecutableFunction]` directly for instance methods that should be callable from Flow.

```csharp
using Ceres.Graph.Flow.Annotations;
using UnityEngine;

public class DoorFlow : FlowGraphObject
{
    [ExecutableFunction]
    public void LockDoor()
    {
        Debug.Log("Locked");
    }
}
```

Rules:

- Avoid generic methods; write a generic node instead.
- Avoid duplicate method name plus parameter count in the same class hierarchy.
- Use `[CeresLabel]` when overloads need clear display names.
- Keep parameter count at 6 or fewer when possible; larger signatures use Uber nodes with higher overhead.

## ExecutableFunctionLibrary

Use a `partial` class inheriting `ExecutableFunctionLibrary` for static APIs. The source generator injects registration code using function pointers.

```csharp
using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Ceres.Graph.Flow.Utilities;
using UnityEngine;

[CeresGroup("Gameplay")]
public partial class GameplayFlowLibrary : ExecutableFunctionLibrary
{
    [ExecutableFunction, CeresLabel("Damage Actor")]
    public static void Flow_DamageActor(GameObject actor, int amount)
    {
        // Apply damage.
    }

    [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Get Health")]
    public static int Flow_GetHealth(GameObject actor)
    {
        return actor ? 100 : 0;
    }
}
```

Best practices:

- The class must be `partial`; missing it produces a `Ceres001` diagnostic.
- Prefix methods with `Flow_` to reduce collisions. Ceres strips `Flow_` from the display name when no label is provided.
- Use `[CeresGroup("Group/Subgroup")]` on the class or method for search organization.
- Use `[CeresLabel("Readable Name")]` for display names, especially overloads.
- Use `ExecuteInDependency = true` only for pure-ish data functions that do not depend on forward execution order. Prefer forward path for side effects and reference-type mutation.

## Script Method And Self Target

Use `IsScriptMethod` when a static function should appear as a method of its first parameter type.

```csharp
[ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("Get Component")]
public static Component Flow_GetComponent(GameObject target, [ResolveReturn] SerializedType<Component> type)
{
    return target ? target.GetComponent(type) : null;
}
```

Options:

- `IsScriptMethod = true`: first parameter is treated as the target type.
- `IsSelfTarget = true`: if the target port is unconnected and compatible, Flow passes the graph context object as the default target.
- `DisplayTarget = false`: hide the target subtitle for operator-like nodes.
- `SearchName = "..."`: override the search entry text.
- `[ResolveReturn] SerializedType<T>`: lets the editor resolve a dynamic return port type from a type parameter.

## ExecutableEvent For Custom Events

Use `[ExecutableEvent]` on a non-generic `EventBase<T>` subclass to expose it as a custom Flow event.

```csharp
using Ceres.Graph.Flow.Annotations;
using Chris.Events;
using UnityEngine;

[ExecutableEvent]
public class DamageEvent : EventBase<DamageEvent>
{
    public GameObject Target { get; private set; }
    public int Amount { get; private set; }

    [ExecutableEvent]
    public static DamageEvent Create(GameObject target, int amount)
    {
        var evt = GetPooled();
        evt.Target = target;
        evt.Amount = amount;
        return evt;
    }
}
```

Generated behavior:

- `ExecutableEvent_DamageEvent` implements the custom event entry node and exposes public getters as output ports.
- `FlowNode_CreateDamageEvent` is generated when a static `[ExecutableEvent]` creation method exists.
- Send events from runtime code with `this.SendEvent(evt)` on an `IFlowGraphRuntime`.
- Override graph handling in code with `OverrideEventImplementation<TEvent>()` when a script-side implementation should prevent or replace graph execution.

## Useful Package Files

- `Documentation~/docs/flow_executable_event.md`
- `Documentation~/docs/flow_executable_function.md`
- `Documentation~/docs/flow_function_library.md`
- `Runtime/Flow/Annotations/*.cs`
- `Runtime/Flow/Models/ExecutableReflection.cs`
- `Runtime/SourceGenerators/Source~/Ceres.SourceGenerator/Generators/ExecutableLibraryGenerator.cs`
- `Runtime/SourceGenerators/Source~/Ceres.SourceGenerator/Generators/CustomEventGenerator.cs`
- `Runtime/CodeGen/ExecutableReflectionILPP.cs`
