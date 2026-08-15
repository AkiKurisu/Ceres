# Executable Function

You can define `ExecutableFunction` in three ways.

## Instance Function

For an instance method, add `[ExecutableFunction]` directly.

```csharp
using Ceres.Flow.Annotations;
using UnityEngine;

public class MyComponent: Component
{
    /// <summary>
    /// Runs gameplay work exposed to Flow.
    /// </summary>
    /// <param name="arg1">An integer argument.</param>
    /// <param name="arg2">A float argument.</param>
    [ExecutableFunction]
    public void DoSomething(int arg1, float arg2)
    {
        // DoSomething
    }
}
```

The graph editor uses XML comments from executable methods as function-node tooltips. Prefer concise `<summary>` text and add `<param>` / `<returns>` when the node is not self-explanatory.

## Static Function

For static methods, create a `partial` class derived from `ExecutableFunctionLibrary`, then mark each exposed method with `[ExecutableFunction]`.
   
   >You must add `partial` modifier to let source generator work. Source generator will register static function pointer to the flow reflection system instead of using MethodInfo to enhance runtime performance.
   

```csharp
using Ceres.Annotations;
using Ceres.Flow.Annotations;
using Ceres.Flow.Utilities;
using Ceres.Serialization;
using UnityEngine;
using UObject = UnityEngine.Object;

public partial class UnityExecutableFunctionLibrary: ExecutableFunctionLibrary
{
    // IsScriptMethod will consider UObject as function target type
    // IsSelfTarget will let graph pass self reference as first parameter if self is UObject
    [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetName")]
    public static string Flow_UObjectGetName(UObject uObject)
    {
        return uObject.name;
    }

    // ResolveReturnAttribute will let graph editor display return type by this parameter result
    // Only support SerializedType<T> from Ceres.Serialization
    [ExecutableFunction]
    public static UObject Flow_FindObjectOfType(
        [ResolveReturn] 
        SerializedType<UObject> type)
    {
        return UObject.FindObjectOfType(type);
    }
}
```

## Always-included assemblies

Patterns in `Project Settings > Ceres > Flow Settings > Always Included Assembly Wildcards` expose matching assembly methods without attributes. Keep the list narrow because it expands the editor reflection surface.

## Conventions and Restrictions

1. For methods defined in the same class and its inheritance hierarchy, 
   methods with the same name and the same parameter count can only have
    one marker `ExecutableFunctionAttribute`.

2. For methods with the same name but different number of parameters in 1, 
   recommend to use `CeresLabelAttribute` to distinguish their names displayed 
   in the editor, or they will be named with full method signature.

3. Generic methods are not supported using `ExecutableFunctionAttribute`, they
    need to be defined in a generic node which will be explained in 
    [Advanced/Generic Node](./flow_advanced.md#generic-node) below.

4. Try to keep the number of input parameters less than or equal to 6, otherwise the 
   editor will use Uber nodes to support method calls with any parameters. The 
   default parameter values ​​will not be serialized and the runtime overhead will 
   be greater.

Wrong example:

```csharp
[ExecutableFunction]
public static string Flow_GetName(UObject uObject)
{
    return uObject.name;
}

[ExecutableFunction]
public static string Flow_GetName(Component component)
{
    return component.name;
}

[ExecutableFunction]
public static void Flow_DoSomething(string arg1, int arg2)
{
    
}

[ExecutableFunction]
public static string Flow_DoSomething(string arg1)
{
    
}
```

Correct example:

```csharp
[ExecutableFunction]
public static string Flow_UObjectGetName(UObject uObject)
{
    return uObject.name;
}
[ExecutableFunction]
public static string Flow_ComponentGetName(Component component)
{
    return component.name;
}

[ExecutableFunction, CeresLabel("DoSomething with 2 Arguements")]
public static void Flow_DoSomething(string arg1, int arg2)
{
    
}

[ExecutableFunction]
public static string Flow_DoSomething(string arg1)
{
    
}
```

## Related guides

- Learn about [Function Library](./flow_function_library.md) for source-generated static functions
- Explore [Generic Nodes](./flow_generic_node.md) for generic method support
- Check [Custom Nodes](./flow_custom_node.md) for creating reusable node logic
- See [Code Generation](./flow_codegen.md) for technical details on function registration
