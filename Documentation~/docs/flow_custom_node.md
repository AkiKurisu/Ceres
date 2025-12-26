# Creating Custom Flow Nodes

Creating custom nodes in Flow allows you to encapsulate reusable logic and extend Flow's functionality. This guide will teach you how to create your own Flow nodes with practical examples.

## Understanding Flow Node Base Classes

Flow provides three base classes for creating custom nodes, each serving different purposes:

### ExecutableNode
The base class for all executable nodes. Use this when you need full control over execution flow.

- Must implement `Execute(ExecutionContext)` method
- Can handle both synchronous and asynchronous operations
- Full control over execution flow continuation

### ForwardNode
Inherits from `ExecutableNode` and adds an input port for forward execution path.

- Automatically includes `input` port for connecting previous nodes
- Use when your node is part of a sequential execution chain
- Most common base class for custom nodes

### FlowNode
Inherits from `ForwardNode` and provides the simplest interface for synchronous nodes.

- Automatically handles execution flow continuation via `exec` output port
- Override `LocalExecute(ExecutionContext)` for your logic
- Best for simple, synchronous operations

## Basic Node Structure

Every Flow node must:

1. Inherit from one of the base classes
2. Be marked with `[Serializable]`
3. Define input/output ports using attributes
4. Implement execution logic

### Port Attributes

- `[InputPort]` - Marks a field as an input port
- `[OutputPort(false)]` - Marks a field as an execution output port (white port)
- `[OutputPort]` - Marks a field as a data output port
- `[CeresLabel("Label")]` - Custom label for the port
- `[HideInGraphEditor]` - Hides field from graph editor (for internal data)

## Example 1: Simple Delay Node

Let's create a delay node that waits for a specified duration before continuing execution.

```csharp
using System;
using Ceres.Annotations;
using Ceres.Graph.Flow;
using Cysharp.Threading.Tasks;
using UnityEngine;

[Serializable]
[CeresGroup("Utilities")]
[CeresLabel("Delay")]
public class FlowNode_Delay : ForwardNode
{
    [InputPort, CeresLabel("Duration")]
    public CeresPort<float> duration = new CeresPort<float>(1.0f);
    
    [OutputPort(false), CeresLabel("")]
    public NodePort exec;
    
    protected override async UniTask Execute(ExecutionContext executionContext)
    {
        // Wait for the specified duration
        await UniTask.Delay(TimeSpan.FromSeconds(duration.Value), 
            cancellationToken: executionContext.Context.GetCancellationTokenOnDestroy());
        
        // Continue execution to next node
        executionContext.SetNext(exec.GetT<ExecutableNode>());
    }
}
```

**Key Points:**
- Inherits from `ForwardNode` for async support
- Uses `UniTask.Delay` for async waiting
- Uses `SetNext()` to continue execution flow
- Handles cancellation token for proper cleanup

## Example 2: Conditional Branch Node

Let's create a branch node that routes execution based on a boolean condition.

```csharp
using System;
using Ceres.Annotations;
using Ceres.Graph.Flow;
using Cysharp.Threading.Tasks;

[Serializable]
[CeresGroup("Flow Control")]
[CeresLabel("Branch")]
public class FlowNode_Branch : FlowNode
{
    [InputPort, CeresLabel("Condition")]
    public CeresPort<bool> condition = new CeresPort<bool>();
    
    [OutputPort(false), CeresLabel("True")]
    public NodePort trueExec;
    
    [OutputPort(false), CeresLabel("False")]
    public NodePort falseExec;
    
    protected override void LocalExecute(ExecutionContext executionContext)
    {
        // Get the next node based on condition
        var nextNode = condition.Value 
            ? trueExec.GetT<ExecutableNode>() 
            : falseExec.GetT<ExecutableNode>();
        
        // FlowNode automatically continues execution via exec port
        // But we need custom logic, so we override Execute instead
    }
    
    protected override UniTask Execute(ExecutionContext executionContext)
    {
        var nextNode = condition.Value 
            ? trueExec.GetT<ExecutableNode>() 
            : falseExec.GetT<ExecutableNode>();
        
        executionContext.SetNext(nextNode);
        return UniTask.CompletedTask;
    }
}
```

**Note:** Since we need custom execution flow (choosing between two outputs), we override `Execute()` instead of `LocalExecute()`.

## Example 3: Calculate Node

A simple synchronous node that performs calculations.

```csharp
using System;
using Ceres.Annotations;
using Ceres.Graph.Flow;

[Serializable]
[CeresGroup("Math")]
[CeresLabel("Add")]
public class FlowNode_Add : FlowNode
{
    [InputPort, CeresLabel("A")]
    public CeresPort<float> a = new CeresPort<float>();
    
    [InputPort, CeresLabel("B")]
    public CeresPort<float> b = new CeresPort<float>();
    
    [OutputPort, CeresLabel("Result")]
    public CeresPort<float> result = new CeresPort<float>();
    
    protected override void LocalExecute(ExecutionContext executionContext)
    {
        // Simple synchronous calculation
        result.Value = a.Value + b.Value;
        
        // FlowNode automatically continues execution via exec port
    }
}
```

**Key Points:**
- Inherits from `FlowNode` for simple synchronous operations
- Overrides `LocalExecute()` for the calculation logic
- Execution flow continues automatically through `exec` port

## Example 4: Sequence Node with Port Array

A node that executes multiple outputs sequentially. This demonstrates using `IPortArrayNode` for dynamic port arrays.

```csharp
using System;
using Ceres.Annotations;
using Ceres.Graph.Flow;
using Cysharp.Threading.Tasks;
using UnityEngine;

[Serializable]
[CeresGroup("Utilities")]
[CeresLabel("Sequence")]
[CeresMetadata("style = ForwardNode")]
public class FlowNode_Sequence : ForwardNode, ISerializationCallbackReceiver, IPortArrayNode
{
    [OutputPort(false), CeresLabel("Then"), CeresMetadata("DefaultLength = 2")]
    public NodePort[] outputs;

    [HideInGraphEditor]
    public int outputCount;

    protected override async UniTask Execute(ExecutionContext executionContext)
    {
        // Execute each output sequentially
        foreach (var output in outputs)
        {
            var next = output.GetT<ExecutableNode>();
            if (next == null) continue;
            await executionContext.Forward(next);
        }
    }

    // ISerializationCallbackReceiver implementation
    public void OnBeforeSerialize()
    {
        // Called before serialization
    }

    public void OnAfterDeserialize()
    {
        // Reconstruct port array after deserialization
        outputs = new NodePort[outputCount];
        for (int i = 0; i < outputCount; i++)
        {
            outputs[i] = new NodePort();
        }
    }

    // IPortArrayNode implementation
    public int GetPortArrayLength()
    {
        return outputCount;
    }

    public string GetPortArrayFieldName()
    {
        return nameof(outputs);
    }

    public void SetPortArrayLength(int newLength)
    {
        outputCount = newLength;
    }
}
```

**Key Points:**
- Implements `IPortArrayNode` for dynamic port arrays
- Uses `CeresMetadata("DefaultLength = 2")` to set default array size
- Implements `ISerializationCallbackReceiver` to handle serialization
- Uses `executionContext.Forward()` to execute nodes sequentially

## Node Metadata

Use attributes to customize node appearance and behavior:

- `[CeresGroup("Group Name")]` - Groups nodes in the search window
- `[CeresLabel("Display Name")]` - Custom display name
- `[CeresMetadata("key = value")]` - Additional metadata
- `[NodeInfo("Description")]` - Tooltip description

## Execution Context

The `ExecutionContext` provides important information and methods:

- `executionContext.Graph` - Access to the FlowGraph instance
- `executionContext.Context` - The Unity Object that owns this graph
- `executionContext.SetNext(node)` - Set the next node to execute
- `executionContext.Forward(node)` - Execute a node in forward path
- `executionContext.GetEvent<T>()` - Get the event that triggered execution

## Synchronous vs Asynchronous

### Synchronous Nodes (FlowNode)
- Override `LocalExecute(ExecutionContext)`
- Execution completes immediately
- Automatically continues via `exec` port
- Use for calculations, data transformations, simple logic

### Asynchronous Nodes (ForwardNode/ExecutableNode)
- Override `Execute(ExecutionContext)` returning `UniTask`
- Can await async operations
- Must manually call `SetNext()` or `Forward()`
- Use for delays, coroutines, async operations

## Best Practices

1. **Choose the right base class** - Use `FlowNode` when possible, only use `ForwardNode`/`ExecutableNode` when needed

2. **Handle null ports** - Always check if ports are connected:
   ```csharp
   var next = exec.GetT<ExecutableNode>();
   if (next == null) return;
   ```

3. **Use cancellation tokens** - For async operations, respect cancellation:
   ```csharp
   await UniTask.Delay(duration, cancellationToken: 
       executionContext.Context.GetCancellationTokenOnDestroy());
   ```

4. **Provide default values** - Initialize ports with sensible defaults:
   ```csharp
   public CeresPort<float> duration = new CeresPort<float>(1.0f);
   ```

5. **Use meaningful labels** - Help users understand your node:
   ```csharp
   [CeresLabel("Calculate Distance")]
   [CeresGroup("Math")]
   ```

6. **Document your nodes** - Add `[NodeInfo]` for tooltips:
   ```csharp
   [NodeInfo("Waits for the specified duration before continuing execution.")]
   ```

## Common Patterns

### Pattern 1: Data Transformation
```csharp
protected override void LocalExecute(ExecutionContext executionContext)
{
    output.Value = Transform(input.Value);
}
```

### Pattern 2: Conditional Execution
```csharp
protected override UniTask Execute(ExecutionContext executionContext)
{
    var next = condition.Value ? trueExec : falseExec;
    executionContext.SetNext(next.GetT<ExecutableNode>());
    return UniTask.CompletedTask;
}
```

### Pattern 3: Async Operation
```csharp
protected override async UniTask Execute(ExecutionContext executionContext)
{
    await SomeAsyncOperation();
    executionContext.SetNext(exec.GetT<ExecutableNode>());
}
```

## Next Steps

- Learn about [Generic Nodes](./flow_generic_node.md) for type-safe generic nodes
- Explore [ExecutableFunctionLibrary](./flow_function_library.md) for exposing C# methods
- Check [Advanced Features](./flow_advanced.md) for port arrays and more
- See [Graph Tracker](./flow_graph_tracker.md) for debugging custom nodes

