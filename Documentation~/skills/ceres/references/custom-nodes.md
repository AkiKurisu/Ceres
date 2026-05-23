# Ceres Custom Nodes

Use this reference when writing custom Flow nodes, generic nodes, dynamic port nodes, or port behavior.

## Choose A Base Class

- Use `FlowNode` for simple synchronous nodes. Override `LocalExecute(ExecutionContext)`. Flow continues through the default `exec` port.
- Use `ForwardNode` when the node is in a forward chain but needs async work or custom continuation.
- Use `ExecutableNode` when the node needs full execution control or should run only in dependency path.

Minimal synchronous node:

```csharp
using System;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Graph.Flow;

[Serializable]
[CeresGroup("Gameplay")]
[CeresLabel("Add Score")]
public class FlowNode_AddScore : FlowNode
{
    [InputPort, CeresLabel("Amount")]
    public CeresPort<int> amount = new(1);

    protected override void LocalExecute(ExecutionContext executionContext)
    {
        // Apply score.
    }
}
```

Async/custom continuation node:

```csharp
using System;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Cysharp.Threading.Tasks;

[Serializable]
[CeresGroup("Utilities")]
[CeresLabel("Delay")]
public class FlowNode_Delay : ForwardNode
{
    [InputPort]
    public CeresPort<float> seconds = new(1f);

    [OutputPort(false), CeresLabel("")]
    public NodePort exec;

    protected override async UniTask Execute(ExecutionContext executionContext)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(seconds.Value),
            cancellationToken: executionContext.Context.GetCancellationTokenOnDestroy());
        executionContext.SetNext(exec.GetT<ExecutableNode>());
    }
}
```

## Ports

- Use `[InputPort] public CeresPort<T> name` for data inputs.
- Use `[OutputPort] public CeresPort<T> name` for data outputs.
- Use `[OutputPort(false)] public NodePort exec` for execution outputs.
- Use `NodePort.GetT<ExecutableNode>()` to resolve the connected next node at runtime.
- Give default values by constructing ports, such as `new CeresPort<float>(1f)`.
- Use `[HideInGraphEditor]` for internal fields or connection-only ports.
- Use `[CeresLabel]` to make port and node labels readable.
- Use `[NodeInfo]` for tooltips.

## Execution Path Metadata

Use dependency path only for nodes that produce values without relying on forward order.

```csharp
[CeresMetadata("style = ConstNode", "path = Dependency")]
public class FlowNode_ReadValue : ExecutableNode
{
}
```

Forward path is the default and should be used for side effects, control flow, async work, and Unity object mutation.

## Port Arrays

Use `IReadOnlyPortArrayNode` for fixed dynamic arrays and `IPortArrayNode` when the editor can resize them.
Only one port array is supported per node type.

```csharp
using System;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Cysharp.Threading.Tasks;
using UnityEngine;

[Serializable]
[CeresGroup("Flow Control")]
[CeresLabel("Sequence")]
[CeresMetadata("style = ForwardNode")]
public class FlowNode_SequenceLike : ForwardNode, ISerializationCallbackReceiver, IPortArrayNode
{
    [OutputPort(false), CeresLabel("Then"), CeresMetadata("DefaultLength = 2")]
    public NodePort[] outputs;

    [HideInGraphEditor]
    public int outputCount;

    protected override async UniTask Execute(ExecutionContext executionContext)
    {
        foreach (var output in outputs)
        {
            var next = output.GetT<ExecutableNode>();
            if (next != null)
                await executionContext.Forward(next);
        }
    }

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        outputs = new NodePort[outputCount];
        for (var i = 0; i < outputCount; i++)
            outputs[i] = new NodePort();
    }

    public int GetPortArrayLength() => outputCount;
    public string GetPortArrayFieldName() => nameof(outputs);
    public void SetPortArrayLength(int newLength) => outputCount = newLength;
}
```

## Generic Nodes

Generic nodes need two classes:

- Runtime node: `FlowNode_NameT<T...>`
- Template class: `FlowNode_NameT_Template : GenericNodeTemplate`

The template name must match `{node type name without arity}_Template`.

```csharp
using System;
using System.Linq;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Utilities;
using Cysharp.Threading.Tasks;

[Serializable]
[CeresGroup("Utilities")]
[CeresLabel("Cast to {0}")]
public class FlowNode_CastLikeT<TFrom, TTo> : ForwardNode where TTo : TFrom
{
    [InputPort, HideInGraphEditor]
    public CeresPort<TFrom> source;

    [OutputPort]
    public CeresPort<TTo> result;

    [OutputPort(false), CeresLabel("")]
    public NodePort exec;

    protected override UniTask Execute(ExecutionContext executionContext)
    {
        result.Value = (TTo)source.Value;
        executionContext.SetNext(exec.GetT<ExecutableNode>());
        return UniTask.CompletedTask;
    }
}

public class FlowNode_CastLikeT_Template : GenericNodeTemplate
{
    public override bool RequirePort() => true;

    public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
    {
        return new[] { portValueType, selectArgumentType };
    }

    public override Type[] GetAvailableArguments(Type portValueType)
    {
        return CeresPort.GetAssignedPortValueTypes()
            .Where(type => type.IsAssignableTo(portValueType) && type != portValueType)
            .ToArray();
    }
}
```

Template guidance:

- Return `true` from `RequirePort()` when type arguments depend on a dragged port.
- Return selectable types from `GetAvailableArguments`.
- Return generic arguments in the same order as the node type parameters.
- Cache expensive type lists in static fields.

## Metadata And Search

- `[CeresGroup("Group/Subgroup")]` controls search grouping.
- `[CeresLabel("Display")]` controls node, function, and port display text.
- `[CeresMetadata("style = ConstNode")]` controls editor style classes.
- `[RequirePort(typeof(SomeType))]` hides a node unless it is created from a compatible dragged port.
- `[HideInGraphEditor]` hides serialized fields from property UI.

## Useful Package Files

- `Documentation~/docs/flow_custom_node.md`
- `Documentation~/docs/flow_generic_node.md`
- `Documentation~/docs/flow_advanced.md`
- `Runtime/Core/Models/Graph/Nodes/CeresNode.cs`
- `Runtime/Core/Models/Graph/Nodes/GenericNodeTemplate.cs`
- `Runtime/Core/Models/Graph/Nodes/PortArrayNodeReflection.cs`
- `Runtime/Core/Models/Graph/Ports/CeresPort.cs`
- `Runtime/Flow/Models/Nodes/Core/ExecutableNode.cs`
