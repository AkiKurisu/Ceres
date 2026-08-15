# Actor Flow

`Actor` derives from `FlowGraphObject`, so C# gameplay types can expose lifecycle events and typed functions to Flow without a separate proxy component. Each Actor can use an embedded graph, a shared `FlowGraphAsset`, or an address-selected graph managed by the Gameplay world.

## Expose Actor behavior

The base Actor lifecycle methods `Awake`, `OnEnable`, `Start`, `Update`, `FixedUpdate`, `LateUpdate`, `OnDisable`, and `OnDestroy` are implementable events. Add executable functions to a derived Actor for project-specific operations.

```csharp
using Ceres.Flow.Annotations;
using Ceres.Gameplay;
using UnityEngine;

public sealed class DoorActor : Actor
{
    [SerializeField]
    private Animator animator;

    [ExecutableFunction]
    public void SetOpen(bool open)
    {
        animator.SetBool("Open", open);
    }
}
```

Attach `DoorActor`, open its Flow Graph from the Inspector, implement a lifecycle event, and call `Set Open`. The embedded graph remains serialized with that component.

## Choose the graph source

At runtime, an Actor resolves its graph in this order:

1. `advancedSettings.actorAddress`, when remote update is enabled and the address resolves through `ActorFlowGraphSubsystem`;
2. `advancedSettings.graphAsset`, when a shared asset is assigned;
3. the Actor's embedded graph.

An unresolved address falls back to the assigned asset and then the embedded graph.

| Source | Use it when |
| --- | --- |
| Embedded graph | One Actor instance owns the behavior and should serialize it with the component |
| `graphAsset` | Multiple Actors share one authored graph or the graph needs an independent asset lifecycle |
| `actorAddress` | A project deliberately enables per-Actor graph replacement from packaged or deployed content |

The Actor Inspector edits its embedded graph. Edit a shared `FlowGraphAsset` from the asset itself.

## Configure an actor address

Address-based selection is opt-in:

1. Enable **Remote Update** under **Project Settings > Ceres > Gameplay Settings**.
2. Create a DataTable whose row type is `ActorFlowGraphRow`. Ceres registers that table with the `ActorFlowGraphDataTable` Addressables address.
3. Use the Actor address as the row ID and assign the same value to `advancedSettings.actorAddress`.
4. Enable the row's `remoteUpdate` option.
5. Assign `reference` and enable `preload` when a packaged `FlowGraphAsset` should be available for that address.
6. Set `remotePath` only when the deployed file name differs from the Actor address.

The export button beside **Actor Address** writes remote graph content under `SaveUtility.SavePath/Flow`. Depending on **Serialize Mode**, the Editor writes JSON when the graph can be represented as text or an AssetBundle when Unity object dependencies must travel with it.

This mechanism selects graph content; it does not change the Actor's C# type or add compatibility between different graph APIs.

## Gameplay Flow integration

`Ceres.Gameplay` registers executable functions for common gameplay operations, including:

- resolving world subsystems and Actors;
- loading levels by name or `LevelReference`;
- playing and stopping 2D or 3D audio;
- spawning pooled particle effects;
- capturing cameras or the screen.

Animation APIs expose their own Flow functions, and the Gameplay module registers conversions between animation `LayerHandle`, `int`, and `string` ports.

Use these nodes for orchestration. Keep domain rules in typed C# methods when they need independent testing or reuse outside a graph.

## Lifecycle and constraints

- Graph selection happens in Play Mode. The Actor owns the resulting runtime graph or generated program and releases it in `OnDestroy`.
- `ActorFlowGraphSubsystem` is scoped to `GameWorld`; remote graph objects and loaded AssetBundles are released with that world.
- A packaged row reference is loaded only when `preload` is enabled. Otherwise the address must resolve to deployed JSON or an AssetBundle, or the Actor uses its fallback source.
- Text export cannot preserve Unity object dependencies. Use AssetBundle mode when the graph references assets.
- Generated C# execution follows the same container data and is configured through the standard Flow code-generation workflow.

## Related documentation

- [World and Actors](./gameplay_world.md) for Actor and subsystem ownership
- [Flow Concept](./flow_concept.md) for nodes, events, and execution
- [Executable Functions](./flow_executable_function.md) for exposing typed C# APIs
- [Flow Code Generation](./flow_codegen.md) for generated runtime programs
- [Data Driven](./core_data_driven.md) for Actor graph table authoring
