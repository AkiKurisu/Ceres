# Graph Tracker Guide

`FlowGraphTracker` observes entry and exit for executable nodes. Use it for diagnostics that cannot be obtained from the Flow editor debugger.

## Basic Usage

### Using TrackerAutoScope

The easiest way to use a tracker is with the `Auto()` method, which returns a `TrackerAutoScope`:

```csharp
using Ceres;
using Ceres.Flow;
using Ceres.Flow.Annotations;

public class MyFlowObject : FlowGraphObject
{
    [ImplementableEvent]
    private void Start()
    {
        // Create a tracker and use it for this execution
        using (new FlowGraphDependencyTracker(this.GetRuntimeFlowGraph()).Auto())
        {
            // Execute the graph - tracker will monitor execution
            this.ProcessEvent();
        }
        // Tracker is automatically disposed when scope ends
    }
}
```

### Setting Active Tracker

You can also set a tracker as the active tracker for all graph executions:

```csharp
var tracker = new FlowGraphDependencyTracker(graph);
FlowGraphTracker.SetActiveTracker(tracker);

// All graph executions will use this tracker
runtime.ProcessEvent(eventName: "Start");

// Clean up when done
tracker.Dispose();
```

## Built-in Tracker: FlowGraphDependencyTracker

Ceres provides a built-in tracker that logs node execution and dependencies:

```csharp
using Ceres;
using Ceres.Flow;
using Cysharp.Threading.Tasks;

public class FlowGraphDependencyTracker : FlowGraphTracker
{
    private readonly FlowGraph _flowGraph;
    
    public FlowGraphDependencyTracker(FlowGraph flowGraph)
    {
        _flowGraph = flowGraph;
    }
    
    public override UniTask EnterNode(ExecutableNode node)
    {
        CeresLogger.Log($"Enter node >>> [{node.GetTypeName()}]({node.Guid})");
        var dependencies = node.NodeData.GetDependencies();
        if (dependencies != null)
        {
            foreach (var dependency in dependencies)
            {
                var dependencyNode = _flowGraph.FindNode(dependency);
                if (dependencyNode != null)
                {
                    CeresLogger.Log($"Find dependency node [{dependencyNode.GetTypeName()}]({dependencyNode.Guid})");
                }
            }
        }
        return UniTask.CompletedTask;
    }
    
    public override UniTask ExitNode(ExecutableNode node)
    {
        CeresLogger.Log($"Exit node <<< [{node.GetTypeName()}]({node.Guid})");
        return UniTask.CompletedTask;
    }
}
```

**Usage:**
```csharp
using (new FlowGraphDependencyTracker(graph).Auto())
{
    runtime.ProcessEvent(eventName: "Start");
}
```

**Output:**
```text
Enter node >>> [FlowNode_Log](abc123)
Find dependency node [FlowNode_GetVariable](def456)
Exit node <<< [FlowNode_Log](abc123)
```

## Example 1: Execution Logger

Create a tracker that logs all node executions with timestamps:

```csharp
using System;
using System.Collections.Generic;
using Ceres.Flow;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ExecutionLoggerTracker : FlowGraphTracker
{
    private readonly List<LogEntry> _logEntries = new();
    
    private struct LogEntry
    {
        public string NodeName;
        public string NodeGuid;
        public DateTime Timestamp;
        public bool IsEnter;
    }
    
    public override UniTask EnterNode(ExecutableNode node)
    {
        _logEntries.Add(new LogEntry
        {
            NodeName = node.GetTypeName(),
            NodeGuid = node.Guid,
            Timestamp = DateTime.Now,
            IsEnter = true
        });
        
        Debug.Log($"[{DateTime.Now:HH:mm:ss.fff}] Enter: {node.GetTypeName()}");
        return UniTask.CompletedTask;
    }
    
    public override UniTask ExitNode(ExecutableNode node)
    {
        _logEntries.Add(new LogEntry
        {
            NodeName = node.GetTypeName(),
            NodeGuid = node.Guid,
            Timestamp = DateTime.Now,
            IsEnter = false
        });
        
        Debug.Log($"[{DateTime.Now:HH:mm:ss.fff}] Exit: {node.GetTypeName()}");
        return UniTask.CompletedTask;
    }
    
    public void PrintSummary()
    {
        Debug.Log($"Total nodes executed: {_logEntries.Count / 2}");
        foreach (var entry in _logEntries)
        {
            Debug.Log($"{entry.Timestamp:HH:mm:ss.fff} - {(entry.IsEnter ? "Enter" : "Exit")}: {entry.NodeName}");
        }
    }
    
    public override void Dispose()
    {
        PrintSummary();
        _logEntries.Clear();
        base.Dispose();
    }
}
```

**Usage:**
```csharp
var logger = new ExecutionLoggerTracker();
using (logger.Auto())
{
    runtime.ProcessEvent(eventName: "Start");
}
// Summary is printed automatically on dispose
```

## Example 2: Performance Profiler

Track execution time for each node:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Ceres.Flow;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PerformanceProfilerTracker : FlowGraphTracker
{
    private readonly Dictionary<string, NodeProfile> _profiles = new();
    private readonly Stack<NodeProfile> _executionStack = new();
    
    private class NodeProfile
    {
        public string NodeName;
        public string NodeGuid;
        public Stopwatch Stopwatch = new();
        public int ExecutionCount;
        public long TotalTicks;
    }
    
    public override UniTask EnterNode(ExecutableNode node)
    {
        var guid = node.Guid;
        if (!_profiles.TryGetValue(guid, out var profile))
        {
            profile = new NodeProfile
            {
                NodeName = node.GetTypeName(),
                NodeGuid = guid
            };
            _profiles[guid] = profile;
        }
        
        profile.ExecutionCount++;
        profile.Stopwatch.Restart();
        _executionStack.Push(profile);
        
        return UniTask.CompletedTask;
    }
    
    public override UniTask ExitNode(ExecutableNode node)
    {
        if (_executionStack.Count > 0)
        {
            var profile = _executionStack.Pop();
            profile.Stopwatch.Stop();
            profile.TotalTicks += profile.Stopwatch.ElapsedTicks;
        }
        
        return UniTask.CompletedTask;
    }
    
    public void PrintReport()
    {
        Debug.Log("=== Performance Profile ===");
        foreach (var kvp in _profiles)
        {
            var profile = kvp.Value;
            var avgMs = (profile.TotalTicks / (double)Stopwatch.Frequency) / profile.ExecutionCount * 1000;
            var totalMs = (profile.TotalTicks / (double)Stopwatch.Frequency) * 1000;
            
            Debug.Log($"{profile.NodeName}: " +
                     $"{profile.ExecutionCount} executions, " +
                     $"Avg: {avgMs:F3}ms, " +
                     $"Total: {totalMs:F3}ms");
        }
    }
    
    public override void Dispose()
    {
        PrintReport();
        _profiles.Clear();
        _executionStack.Clear();
        base.Dispose();
    }
}
```

**Usage:**
```csharp
var profiler = new PerformanceProfilerTracker();
using (profiler.Auto())
{
    runtime.ProcessEvent(eventName: "Start");
}
// Report is printed automatically
```

**Output:**
```text
=== Performance Profile ===
FlowNode_Log: 1 executions, Avg: 0.123ms, Total: 0.123ms
FlowNode_GetVariable: 1 executions, Avg: 0.045ms, Total: 0.045ms
FlowNode_Calculate: 5 executions, Avg: 0.234ms, Total: 1.170ms
```

## Example 3: Execution Flow Visualizer

Track execution order and create a visual representation:

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using Ceres.Flow;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ExecutionFlowTracker : FlowGraphTracker
{
    private readonly List<string> _executionOrder = new();
    private int _indentLevel = 0;
    
    public override UniTask EnterNode(ExecutableNode node)
    {
        var indent = new string(' ', _indentLevel * 2);
        _executionOrder.Add($"{indent}→ {node.GetTypeName()}");
        _indentLevel++;
        return UniTask.CompletedTask;
    }
    
    public override UniTask ExitNode(ExecutableNode node)
    {
        _indentLevel--;
        var indent = new string(' ', _indentLevel * 2);
        _executionOrder.Add($"{indent}← {node.GetTypeName()}");
        return UniTask.CompletedTask;
    }
    
    public void PrintFlow()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Execution Flow ===");
        foreach (var entry in _executionOrder)
        {
            sb.AppendLine(entry);
        }
        Debug.Log(sb.ToString());
    }
    
    public override void Dispose()
    {
        PrintFlow();
        _executionOrder.Clear();
        base.Dispose();
    }
}
```

**Output:**
```text
=== Execution Flow ===
→ FlowNode_Start
  → FlowNode_GetVariable
  ← FlowNode_GetVariable
  → FlowNode_Log
  ← FlowNode_Log
← FlowNode_Start
```

## Advanced: Conditional Tracking

Track only specific nodes or conditions:

```csharp
using Ceres.Flow;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ConditionalTracker : FlowGraphTracker
{
    private readonly System.Func<ExecutableNode, bool> _condition;
    private int _matchedCount = 0;
    
    public ConditionalTracker(System.Func<ExecutableNode, bool> condition)
    {
        _condition = condition;
    }
    
    public override UniTask EnterNode(ExecutableNode node)
    {
        if (_condition(node))
        {
            _matchedCount++;
            Debug.Log($"Matched node: {node.GetTypeName()}");
        }
        return UniTask.CompletedTask;
    }
    
    public override void Dispose()
    {
        Debug.Log($"Total matched nodes: {_matchedCount}");
        base.Dispose();
    }
}
```

**Usage:**
```csharp
// Track only nodes with "Log" in the name
var tracker = new ConditionalTracker(node => node.GetTypeName().Contains("Log"));
using (tracker.Auto())
{
    runtime.ProcessEvent(eventName: "Start");
}
```

## Constraints

### 1. Use TrackerAutoScope

Always use `Auto()` for automatic cleanup:

```csharp
// Good
using (tracker.Auto())
{
    runtime.ProcessEvent(eventName: "Start");
}

// Avoid
FlowGraphTracker.SetActiveTracker(tracker);
runtime.ProcessEvent(eventName: "Start");
tracker.Dispose(); // Easy to forget
```

### 2. Keep Trackers Lightweight

Trackers are called for every node execution, so keep them fast:

```csharp
// Good: Simple logging
public override UniTask EnterNode(ExecutableNode node)
{
    Debug.Log(node.GetTypeName());
    return UniTask.CompletedTask;
}

// Avoid: Heavy operations
public override UniTask EnterNode(ExecutableNode node)
{
    File.WriteAllText("log.txt", node.GetTypeName()); // Too slow!
    return UniTask.CompletedTask;
}
```

### 3. Handle Async Properly

If you need async operations, use `UniTask`:

```csharp
public override async UniTask EnterNode(ExecutableNode node)
{
    await SomeAsyncOperation();
    Debug.Log(node.GetTypeName());
}
```

### 4. Clean Up Resources

Always clean up in `Dispose()`:

```csharp
public override void Dispose()
{
    _data.Clear();
    _cache = null;
    base.Dispose();
}
```

### 5. Use Conditional Compilation

Disable trackers in release builds if needed:

```csharp
#if DEVELOPMENT_BUILD || UNITY_EDITOR
using (tracker.Auto())
{
    runtime.ProcessEvent(eventName: "Start");
}
#else
runtime.ProcessEvent(eventName: "Start");
#endif
```

## Performance Considerations

- Tracker callbacks run for every executable node unless `CERES_DISABLE_TRACKER` is defined.
- Keep callbacks lightweight and bound retained diagnostic data.
- `FlowGraphTracker` has one active tracker; setting a custom tracker replaces the active tracker rather than composing with it.
- An `Auto()` scope disposes its tracker when the scope ends. Keep the scope alive for the complete asynchronous graph execution, or manage a long-lived tracker explicitly.

## Related guides

- Learn about [Custom Nodes](./flow_custom_node.md) for creating reusable logic
- Explore [Debugging](./flow_debugging.md) for editor debugging features
- Check [Advanced Features](./flow_advanced.md) for more patterns

