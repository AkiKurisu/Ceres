# Quick Start

Here is an example of using Flow to output a "Hello World" message.

1. Ceate a new C# script `MyFlowObject.cs` and make it inherit from `FlowGraphObject`.

2. Add a `Start` method to the newly created class so that Unity can call this method when the game starts.

3. Add `ImplementableEventAttribute` to `Start` method so that we can implement its logic in Flow Graph.

```C#
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Annotations;
public class MyFlowObject: FlowGraphObject
{
    [ImplementableEvent]
    private void Start()
    {

    }
}
```

4. Now create a new GameObject in the scene and attach `MyFlowObject` component to it.

5. Click `Open Flow Graph` in the Inspector panel to open the Flow Graph Editor.

    ![Open Flow Graph](../resources/Images/flow_quick_start_1.png)

6. Right click graph and click `Create Node/Select Events/Implement Start`.

    ![Create Node](../resources/Images/flow_quick_start_2.png)

7. Then click `Create Node` and search `Log String`, connect the white port (exec) to the `Start` node's output (exec). 

8. Fill in "Hello World!" in the `In String` field of the `Log String` node.
    
    ![Log String](../resources/Images/flow_quick_start_3.png)

9. Click save button in the left upper corner.

10. Play the game and you will see "Hello World!" in the console.

## Next Steps

- Learn about [Flow Concept](./flow_concept.md) to understand Flow's architecture
- Explore [Executable Events](./flow_executable_event.md) for different event types
- Check [Executable Functions](./flow_executable_function.md) for exposing C# methods to Flow
- See [Runtime Architecture](./flow_runtime_architecture.md) for container types and usage patterns