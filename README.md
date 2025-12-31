<div align="center">

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/AkiKurisu/Ceres)
[![Zhihu](https://img.shields.io/badge/知乎-AkiKurisu-0084ff?style=flat-square)](https://www.zhihu.com/people/akikurisu)
[![Bilibili](https://img.shields.io/badge/Bilibili-爱姬Kurisu-00A1D6?style=flat-square)](https://space.bilibili.com/20472331)

# Ceres

Powerful visual scripting toolkit for Unity.

![banner](./Documentation~/resources/Images/ceres_banner.png)

</div>

## Table of Contents

- [Install](#install)
- [Highlights](#highlights)
- [Platforms](#platforms)
- [Core Concepts](#core-concepts)
- [Quick Start Guide](#quick-start-guide)
- [Flow Visual Scripting](#flow-visual-scripting)
- [ExecutableReflection](#executablereflection)
- [Debugging](#debugging)
- [Advanced Features](#advanced-features)
- [Type Preservation](#type-preservation)
- [Code Generation](#code-generation)
- [Performance](#performance)
- [Documentation](#documentation)
- [Implementation](#implementation)
- [Articles](#articles)
- [License](#license)

## 📦 Install

### Install from UPM

Add following dependencies to `manifest.json`.

```json
"dependencies": {
    "com.kurisu.chris": "https://github.com/AkiKurisu/Chris.git",
    "com.cysharp.unitask":"https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
}
```

Use git URL to download package by Unity Package Manager ```https://github.com/AkiKurisu/Ceres.git```.

### Install from Disk

Clone repo to your project `Packages` folder.

Use `Tools/Ceres/Installer` to install dependencies.

![installer](./Documentation~/resources/Images/installer.png)

## ✨ Highlights

> **Powerful visual scripting toolkit** that brings the best of both worlds - visual programming and C# code integration.

### 🚀 **Core Features**

- **🎯 Generic & Delegate Support** - Full support for C# generics and delegates in visual scripting
- **🔗 Graph & C# Integration** - Seamless integration between visual graphs and traditional C# code
- **🐛 Editor Debugging** - Built-in debugging tools with breakpoints and step-by-step execution
- **⚡ Easy Implementation** - Simple setup with minimal configuration required
- **🏃‍♂️ Optimized Runtime Performance** - High-performance execution with IL2CPP optimizations
- **📱 IL2CPP Compatible** - Fully compatible with IL2CPP builds and recommended for production

### 🎨 **Visual Excellence**

- **Intuitive Node-Based Editor** - Clean, modern interface for creating visual scripts
- **Real-time Preview** - See your logic flow as you build it
- **Professional Workflow** - Industry-standard tools and practices

## Platforms

Unity 2022.3 LTS or later, compatible with Unity 6.

## Core Concepts

Understanding the fundamental concepts of Ceres will help you get started quickly.

### Node
`CeresNode` is the logic and data container that forms the building blocks of your visual scripts.

### Port
`CeresPort` enables you to get data from other nodes. Ceres uses `CeresPort<T>` to get generic data from other nodes and `NodePort` to get a `NodeReference` which can convert to `CeresNode` at runtime.

### Graph
`CeresGraph` contains everything and acts as a virtual machine that executes your visual scripts.

### Data
Ceres serializes node, port and graph to additional data structures: `CeresNodeData`, `CeresPortData` and `CeresGraphData` which contain the actual serialized data and metadata.

### Variable
`SharedVariable` is a data container that can be shared between nodes and graphs. Compared with `CeresPort`, `SharedVariable` can be edited outside of the graph and doesn't contain any connection data since it doesn't need to know where the data comes from.

![Variables](./Documentation~/resources/Images/variables.png)

### Execution Path
Nodes can be executed in two ways:

**Forward**: Graph can execute nodes one by one.

**Dependency**: Graph should execute node's dependencies before executing the node.

![Execution Path](./Documentation~/resources/Images/ceres_concept_execution_path.png)

As shown in the figure, to execute `Log String`, we need to first get the variable `message`. However, since `Get message` has no outer connection, it has not been executed before. So the graph needs to consider `Get message` as a dependency and execute it before executing `Log String`.

## Quick Start Guide

Here's a step-by-step example of using Flow to output a "Hello World" message:

### 1. Create a Flow Object
Create a new C# script `MyFlowObject.cs` and make it inherit from `FlowGraphObject`:

```csharp
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

### 2. Setup in Scene
Create a new GameObject in the scene and attach the `MyFlowObject` component to it.

### 3. Open Flow Graph Editor
Click `Open Flow Graph` in the Inspector panel to open the Flow Graph Editor.

![Open Flow Graph](./Documentation~/resources/Images/flow_quick_start_1.png)

### 4. Create Start Event
Right-click the graph and click `Create Node/Select Events/Implement Start`.

![Create Node](./Documentation~/resources/Images/flow_quick_start_2.png)

### 5. Add Log String Node
Click `Create Node` and search `Log String`, then connect the white port (exec) to the `Start` node's output (exec). Fill in "Hello World!" in the `In String` field of the `Log String` node.

![Log String](./Documentation~/resources/Images/flow_quick_start_3.png)

### 6. Save and Test
Click the save button in the left upper corner, then play the game. You will see "Hello World!" in the console.

## Flow Visual Scripting

Flow thinks of game logic as an execution chain to let game objects do things in order according to your design. Flow visualizes these executions as nodes so that you can connect them to implement gameplay and functionality.

### Events
Each execution chain starts from an event which can contain input data.

![Events](./Documentation~/resources/Images/flow_events.png)

You can define events in Flow Graph or C# scripts.

### Functions
Functions are components that implement game functions. They complete specified functions by calling the engine or your custom API. Through wiring, you can combine these method calls to complete your creativity.

### Container
For any FlowGraph instance, there needs to be a specified Container at runtime. It can be your custom `MonoBehaviour` (need to implement interface) or inherit from a series of parent classes provided by Ceres.

## ExecutableReflection

ExecutableReflection allows you to expose C# methods to Flow graphs, making them available as nodes. There are three main ways to define executable functions:

### 1. Instance Functions

For instance methods, add `ExecutableFunctionAttribute` directly to your method:

```csharp
using Ceres.Graph.Flow.Annotations;
using UnityEngine;

public class MyComponent : MonoBehaviour
{
    [ExecutableFunction]
    public void DoSomething(int arg1, float arg2)
    {
        Debug.Log($"Doing something with {arg1} and {arg2}");
    }
    
    [ExecutableFunction]
    public string GetPlayerName()
    {
        return "Player";
    }
}
```

### 2. Static Functions

For static methods, create a **partial** class that inherits from `ExecutableFunctionLibrary` and add `ExecutableFunctionAttribute`:

```csharp
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Annotations;
using Ceres.Annotations;
using UnityEngine;

public partial class GameplayFunctionLibrary : ExecutableFunctionLibrary
{
    [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true)]
    [CeresLabel("Get GameObject Name")]
    public static string Flow_GetGameObjectName(GameObject gameObject)
    {
        return gameObject.name;
    }
    
    [ExecutableFunction(ExecuteInDependency = true)]
    public static float Flow_CalculateDistance(Vector3 pointA, Vector3 pointB)
    {
        return Vector3.Distance(pointA, pointB);
    }
    
    [ExecutableFunction]
    public static Component Flow_FindComponent(
        GameObject target,
        [ResolveReturn] SerializedType<Component> componentType)
    {
        return target.GetComponent(componentType);
    }
}
```

> **Important**: You must add the `partial` modifier to let the source generator work. The source generator registers static function pointers to enhance runtime performance instead of using MethodInfo.

### 3. Always Included Assemblies

Flow automatically includes assemblies matched by `Always Included Assembly Wildcards` in `Project Settings/Ceres/Flow Settings`. Methods in these assemblies can be invoked without adding attributes:

```csharp
public class UtilityClass
{
    public void AutomaticallyIncludedMethod(string message)
    {
        Debug.Log($"Auto-included: {message}");
    }
}
```

### Best Practices and Conventions

1. **Unique Method Names**: Methods with the same name and parameter count in the same class hierarchy can only have one `ExecutableFunctionAttribute`.

2. **Method Overloads**: For methods with the same name but different parameters, use `CeresLabelAttribute` to distinguish them:

```csharp
[ExecutableFunction, CeresLabel("Log Message")]
public static void Flow_Log(string message)
{
    Debug.Log(message);
}

[ExecutableFunction, CeresLabel("Log Message with Color")]
public static void Flow_Log(string message, Color color)
{
    Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>");
}
```

3. **Parameter Limits**: Keep input parameters ≤ 6 for optimal editor performance. Methods with more parameters use Uber nodes with greater runtime overhead.

4. **Generic Methods**: Generic methods are not supported with `ExecutableFunctionAttribute`. Use [Generic Nodes](#generic-nodes) instead.

## Debugging

Ceres provides powerful debugging capabilities to help you troubleshoot your visual scripts.

### Enable Debug Mode
To enable and disable debug mode, click the `debug` button in the upper right corner. Then, you can click `Next Frame` to execute the graph node by node.

### Breakpoints
You can right click any node and select `Add Breakpoint`, then click `Next Breakpoint` in the toolbar to execute the graph breakpoint by breakpoint.

![Debug](./Documentation~/resources/Images/flow_debugger.png)

### Port Debugging

Ceres editor can display the current value of an input port when the mouse hovers over the port of the node at the current breakpoint.

![Port Debug](./Documentation~/resources/Images/port_debug.png)

### Graph Tracker
`FlowGraphTracker` is a class that can be used to track the execution of the graph for advanced debugging scenarios.

For more details, see the [Graph Tracker](https://akikurisu.github.io/Ceres/docs/flow_graph_tracker.html) documentation.

### Hot Reload

Ceres supports hot reloading of `FlowGraphObject`. When you modify a `FlowGraphObject` in playing mode, the changes can be reflected immediately.

To enable hot reload, you need toggle the `Hot Reload` option in Flow Editor's toolbar and then try to save your graph in playing mode.

![Hot Reload](./Documentation~/resources/Images/hot_reload.png)

## Advanced Features

- **Generic Nodes** - Create reusable nodes that work with different types without code duplication. See [Generic Nodes Guide](https://akikurisu.github.io/Ceres/docs/flow_generic_node.html)
- **Custom Functions** - Define local functions within graphs or shared functions using `FlowGraphFunctionAsset`
- **Port Arrays** - Create nodes with dynamic port collections using `IPortArrayNode`
- **Type Preservation** - Automatic IL2CPP type preservation system prevents code stripping

For more details, see the [Advanced Features](https://akikurisu.github.io/Ceres/docs/flow_advanced.html) documentation.

## Code Generation

Ceres uses multiple code generation techniques to enhance runtime performance:

### Source Generator
Analyzes partial classes annotated with `GenerateFlowAttribute` and generates their implementation automatically, reducing code duplication.

### IL Post Process (ILPP)
Uses ILPP to emit IL for initialization logic of `CeresNode` and methods annotated with `ImplementableEventAttribute` to enhance runtime performance.

Recommended to use the newest version of Rider to view the IL code after ILPP directly.

![View IL](./Documentation~/resources/Images/rider_il.png)

## Performance

Ceres provides a variety of performance enhancements to improve the runtime performance of your visual scripts.

### Flat Relay Nodes
Relay nodes are completely flattened during serialization. At runtime, connections bypass relay nodes entirely, connecting source nodes directly to target nodes.

![Relay Nodes](./Documentation~/resources/Images/relay_node.png)

The picture above shows relay nodes do not effect execution flow (only forward execution three times for `Start`, `Find GameObject`, and `Log String`).

### IL2CPP Call

Ceres implements sophisticated IL2CPP optimizations to achieve near-native performance for executable function calls. The optimization strategy varies by platform to maximize performance while maintaining compatibility.

## Documentation

Comprehensive documentation is available online:

### For Users
- [Flow Quick Start](https://akikurisu.github.io/Ceres/docs/flow_startup.html) - Get started with Flow visual scripting
- [Flow Concepts](https://akikurisu.github.io/Ceres/docs/flow_concept.html) - Understanding Flow architecture
- [Executable Functions](https://akikurisu.github.io/Ceres/docs/flow_executable_function.html) - Expose C# methods to Flow
- [Custom Nodes](https://akikurisu.github.io/Ceres/docs/flow_custom_node.html) - Create your own Flow nodes
- [Generic Nodes](https://akikurisu.github.io/Ceres/docs/flow_generic_node.html) - Working with generic nodes
- [Debugging Guide](https://akikurisu.github.io/Ceres/docs/flow_debugging.html) - Debug your visual scripts

### For Developers
- [Ceres Core Concepts](https://akikurisu.github.io/Ceres/docs/ceres_concept.html) - Understanding Ceres architecture
- [Extending Ceres](https://akikurisu.github.io/Ceres/docs/ceres_extending.html) - Build custom visual scripting systems
- [API Reference](https://akikurisu.github.io/Ceres/api/Ceres.html) - Complete API documentation
- [Ceres Wiki](https://deepwiki.com/AkiKurisu/Ceres/) - Community wiki generated by [DeepWiki](https://deepwiki.com)

## Implementation

### Flow

Ceres built-in event-driven visual scripting solution. 

See [Startup Flow](https://akikurisu.github.io/Ceres/docs/flow_startup.html)

![Flow](./Documentation~/resources/Images/ceres_flow.png)

### Flow Hotupdate

Implement a Data-Driven and Per-Actor hotupdate solution.

See [Chris.Gameplay](https://github.com/AkiKurisu/Chris.Gameplay) for more details.

![Hotupdate](./Documentation~/resources/Images/flow_hotupdate.png)
 
### Next Gen Dialogue

AI powered dialogue visual designer for Unity.

See [Next-Gen-Dialogue](https://github.com/AkiKurisu/Next-Gen-Dialogue).

![Next-Gen-Dialogue](https://github.com/AkiKurisu/Next-Gen-Dialogue/raw/main/Documentation~/Images/banner.png)

## Articles

Technique articles related to Ceres.

### Design

[如何设计一个Unity可视化脚本框架（一）](https://zhuanlan.zhihu.com/p/20500696157)

[如何设计一个Unity可视化脚本框架（二）](https://zhuanlan.zhihu.com/p/20711259559)

[如何设计一个Unity可视化脚本框架（三）](https://zhuanlan.zhihu.com/p/23323693948)

### Performance

[让Unity IL2CPP下的反射性能提高100倍的方法](https://zhuanlan.zhihu.com/p/25806713882)

## License

MIT
