# Generic Nodes Guide

Generic nodes allow you to create type-safe, reusable nodes that work with different types without code duplication. This guide explains how to create and use generic nodes in Flow.

## How Generic Nodes Work

Generic nodes use a two-part system:

1. **Generic Node Class** - The actual node implementation with generic type parameters
2. **Generic Node Template** - Tells the editor how to construct and display the node

## Example 1: Cast Node

Let's create a generic cast node that converts from one type to another.

### Step 1: Define the Generic Node

```csharp
using System;
using Ceres.Annotations;
using Ceres.Graph.Flow;
using Cysharp.Threading.Tasks;

[Serializable]
[CeresGroup("Utilities")]
[CeresLabel("Cast to {0}")]
[CeresMetadata("style = ConstNode")]
public class FlowNode_CastT<TFrom, TTo> : ForwardNode where TTo : TFrom
{
    [OutputPort(false), CeresLabel("")]
    public NodePort exec;
    
    // HideInGraphEditor prevents manual editing - user must connect a port
    [InputPort, HideInGraphEditor, CeresLabel("Source")]
    public CeresPort<TFrom> sourceValue;
    
    [OutputPort(false), CeresLabel("Cast Failed")]
    public NodePort castFailed;
    
    [OutputPort, CeresLabel("Result")]
    public CeresPort<TTo> resultValue;

    protected override UniTask Execute(ExecutionContext executionContext)
    {
        try
        {
            resultValue.Value = (TTo)sourceValue.Value;
            executionContext.SetNext(exec.GetT<ExecutableNode>());
        }
        catch (InvalidCastException)
        {
            executionContext.SetNext(castFailed.GetT<ExecutableNode>());
        }

        return UniTask.CompletedTask;
    }
}
```

**Key Points:**
- Generic type parameters: `TFrom` (source) and `TTo` (target)
- Constraint: `where TTo : TFrom` ensures safe casting
- Label uses `{0}` placeholder for type name
- `HideInGraphEditor` on input port forces connection-based type inference

### Step 2: Create the Template

Create a class in your editor assembly named `{NodeName}_Template` that inherits from `GenericNodeTemplate`:

```csharp
using System;
using System.Linq;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Utilities;

public class FlowNode_CastT_Template : GenericNodeTemplate
{
    // Tell editor this node requires a port to determine types
    public override bool RequirePort()
    {
        return true;
    }
    
    // Construct generic arguments from port type and user selection
    public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
    {
        // portValueType = TFrom (from connected port)
        // selectArgumentType = TTo (user selected)
        return new[] { portValueType, selectArgumentType };
    }

    // Provide available types user can select
    public override Type[] GetAvailableArguments(Type portValueType)
    {
        // Get all types that are assignable to portValueType
        return CeresPort.GetAssignedPortValueTypes()
                        .Where(x => x.IsAssignableTo(portValueType) && x != portValueType)
                        .ToArray();
    }
    
    // Customize node display name
    protected override string GetGenericNodeBaseName(string label, Type[] argumentTypes)
    {
        // label = "Cast to {0}"
        // argumentTypes[1] = TTo
        return string.Format(label, CeresLabel.GetTypeName(argumentTypes[1]));
    }
}
```

**Template Naming Convention:**
- Template class name must be `{GenericNodeClassName}_Template`
- Example: `FlowNode_CastT` → `FlowNode_CastT_Template`

## Example 2: GetComponent<T> Node

A more practical example - getting a component of a specific type.

### Generic Node

```csharp
using System;
using Ceres.Annotations;
using Ceres.Graph.Flow;
using Cysharp.Threading.Tasks;
using UnityEngine;

[Serializable]
[CeresGroup("GameObject")]
[CeresLabel("Get Component {0}")]
public class FlowNode_GetComponentT<T> : FlowNode where T : Component
{
    [InputPort, CeresLabel("Target")]
    public CeresPort<GameObject> target = new CeresPort<GameObject>();
    
    [OutputPort, CeresLabel("Component")]
    public CeresPort<T> component = new CeresPort<T>();
    
    [OutputPort(false), CeresLabel("Not Found")]
    public NodePort notFound;

    protected override UniTask Execute(ExecutionContext executionContext)
    {
        if (target.Value == null)
        {
            executionContext.SetNext(notFound.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }
        
        var comp = target.Value.GetComponent<T>();
        if (comp != null)
        {
            component.Value = comp;
            executionContext.SetNext(exec.GetT<ExecutableNode>());
        }
        else
        {
            executionContext.SetNext(notFound.GetT<ExecutableNode>());
        }
        
        return UniTask.CompletedTask;
    }
}
```

### Template

```csharp
using System;
using System.Linq;
using Ceres.Graph;
using UnityEngine;

public class FlowNode_GetComponentT_Template : GenericNodeTemplate
{
    public override bool RequirePort()
    {
        // Don't require port - user selects type directly
        return false;
    }
    
    public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
    {
        // Only one generic argument: T
        return new[] { selectArgumentType };
    }

    public override Type[] GetAvailableArguments(Type portValueType)
    {
        // Get all Component types
        return typeof(Component).Assembly
                                .GetTypes()
                                .Where(t => typeof(Component).IsAssignableFrom(t) 
                                         && !t.IsAbstract 
                                         && !t.IsInterface)
                                .ToArray();
    }
    
    protected override string GetGenericNodeBaseName(string label, Type[] argumentTypes)
    {
        return string.Format(label, argumentTypes[0].Name);
    }
}
```

## Template Methods Explained

### RequirePort()

Indicates whether the node needs a port connection to determine types.

```csharp
public override bool RequirePort()
{
    return true;  // User must drag a port to create this node
    // or
    return false; // User can create node directly, then select type
}
```

**When to return `true`:**
- Node type depends on input port type (like Cast)
- Type should be inferred from connection

**When to return `false`:**
- Type is independent (like GetComponent)
- User selects type from a list

### GetGenericArguments()

Constructs the generic type arguments array.

```csharp
public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
{
    // portValueType: Type from connected port (null if RequirePort() == false)
    // selectArgumentType: Type user selected in editor
    
    // Return array matching generic type parameters
    return new[] { portValueType, selectArgumentType };
}
```

**Parameters:**
- `portValueType`: Type from connected port (null if no port or `RequirePort() == false`)
- `selectArgumentType`: Type selected by user in editor

**Return:** Array of types matching your generic type parameters in order.

### GetAvailableArguments()

Provides the list of types users can select.

```csharp
public override Type[] GetAvailableArguments(Type portValueType)
{
    // Return all types that can be used as generic arguments
    return availableTypes;
}
```

**Common Patterns:**

```csharp
// All Component types
return typeof(Component).Assembly
        .GetTypes()
        .Where(t => typeof(Component).IsAssignableFrom(t) && !t.IsAbstract)
        .ToArray();

// All types assignable to port type
return CeresPort.GetAssignedPortValueTypes()
        .Where(x => x.IsAssignableTo(portValueType))
        .ToArray();

// Specific type list
return new[] { typeof(int), typeof(float), typeof(string) };
```

### GetGenericNodeBaseName()

Customizes how the node appears in the editor.

```csharp
protected override string GetGenericNodeBaseName(string label, Type[] argumentTypes)
{
    // label = "[CeresLabel("Cast to {0}")]"
    // argumentTypes = array of generic type arguments
    
    return string.Format(label, CeresLabel.GetTypeName(argumentTypes[1]));
}
```

## Example 3: FindObjectOfType<T>

A node that finds objects by type.

### Generic Node

```csharp
using System;
using Ceres.Annotations;
using Ceres.Graph.Flow;
using Cysharp.Threading.Tasks;
using UnityEngine;

[Serializable]
[CeresGroup("GameObject")]
[CeresLabel("Find Object of Type {0}")]
public class FlowNode_FindObjectOfTypeT<T> : FlowNode where T : Object
{
    [OutputPort, CeresLabel("Object")]
    public CeresPort<T> foundObject = new CeresPort<T>();
    
    [OutputPort(false), CeresLabel("Not Found")]
    public NodePort notFound;

    protected override UniTask Execute(ExecutionContext executionContext)
    {
        var obj = Object.FindObjectOfType<T>();
        if (obj != null)
        {
            foundObject.Value = obj;
            executionContext.SetNext(exec.GetT<ExecutableNode>());
        }
        else
        {
            executionContext.SetNext(notFound.GetT<ExecutableNode>());
        }
        
        return UniTask.CompletedTask;
    }
}
```

### Template

```csharp
using System;
using System.Linq;
using Ceres.Graph;
using UnityEngine;

public class FlowNode_FindObjectOfTypeT_Template : GenericNodeTemplate
{
    public override bool RequirePort()
    {
        return false;
    }
    
    public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
    {
        return new[] { selectArgumentType };
    }

    public override Type[] GetAvailableArguments(Type portValueType)
    {
        // Get all Unity Object types
        return typeof(Object).Assembly
                            .GetTypes()
                            .Where(t => typeof(Object).IsAssignableFrom(t) 
                                     && !t.IsAbstract 
                                     && !t.IsInterface
                                     && t != typeof(Object))
                            .ToArray();
    }
    
    protected override string GetGenericNodeBaseName(string label, Type[] argumentTypes)
    {
        return string.Format(label, argumentTypes[0].Name);
    }
}
```

## Advanced: Multiple Generic Parameters

You can create nodes with multiple generic parameters:

```csharp
// Node with two generic parameters
public class FlowNode_ConvertT<TFrom, TTo> : FlowNode
{
    [InputPort]
    public CeresPort<TFrom> input;
    
    [OutputPort]
    public CeresPort<TTo> output;
}

// Template
public class FlowNode_ConvertT_Template : GenericNodeTemplate
{
    public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
    {
        return new[] { portValueType, selectArgumentType };
    }
    
    public override Type[] GetAvailableArguments(Type portValueType)
    {
        // Return compatible target types
        return GetCompatibleTypes(portValueType);
    }
}
```

## Best Practices

### 1. Use Type Constraints

Always use appropriate type constraints for type safety:

```csharp
// Good: Constraint ensures TTo can be cast from TFrom
public class FlowNode_CastT<TFrom, TTo> : FlowNode where TTo : TFrom

// Good: Constraint ensures T is a Component
public class FlowNode_GetComponentT<T> : FlowNode where T : Component
```

### 2. Provide Sensible Defaults

Use `HideInGraphEditor` for ports that should be connection-only:

```csharp
[InputPort, HideInGraphEditor, CeresLabel("Source")]
public CeresPort<TFrom> sourceValue;
```

### 3. Handle Null Cases

Always check for null when working with Unity objects:

```csharp
if (target.Value == null)
{
    executionContext.SetNext(notFound.GetT<ExecutableNode>());
    return UniTask.CompletedTask;
}
```

### 4. Use Meaningful Labels

Labels should include type information:

```csharp
[CeresLabel("Get Component {0}")]  // Shows "Get Component Rigidbody"
[CeresLabel("Cast to {0}")]        // Shows "Cast to Transform"
```

### 5. Optimize Type Lookups

Cache type lists in templates for better performance:

```csharp
private static Type[] _cachedComponentTypes;

public override Type[] GetAvailableArguments(Type portValueType)
{
    _cachedComponentTypes ??= typeof(Component).Assembly
        .GetTypes()
        .Where(t => typeof(Component).IsAssignableFrom(t) && !t.IsAbstract)
        .ToArray();
    
    return _cachedComponentTypes;
}
```

## Next Steps

- Learn about [Custom Nodes](./flow_custom_node.md) for non-generic nodes
- Explore [ExecutableFunctionLibrary](./flow_function_library.md) for static functions
- Check [Advanced Features](./flow_advanced.md) for more patterns

