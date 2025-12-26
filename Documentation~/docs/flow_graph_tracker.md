# Graph Tracker Guide

`FlowGraphTracker` is a powerful debugging and analysis tool that allows you to track and monitor the execution of Flow graphs. This guide explains how to use and create custom trackers for advanced debugging scenarios.

## Basic Usage

### Using TrackerAutoScope

The easiest way to use a tracker is with the `Auto()` method, which returns a `TrackerAutoScope`:

```csharp
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Annotations;

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
graph.ExecuteEventAsync(context, "Start", evt);

// Clean up when done
tracker.Dispose();
```

## Built-in Tracker: FlowGraphDependencyTracker

Ceres provides a built-in tracker that logs node execution and dependencies:

```csharp
using Ceres.Graph.Flow;

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
    graph.ExecuteEventAsync(context, "Start", evt);
}
```

**Output:**
```
Enter node >>> [FlowNode_Log](abc123)
Find dependency node [FlowNode_GetVariable](def456)
Exit node <<< [FlowNode_Log](abc123)
```

## Example 1: Execution Logger

Create a tracker that logs all node executions with timestamps:

```csharp
using System;
using System.Collections.Generic;
using Ceres.Graph.Flow;
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
    graph.ExecuteEventAsync(context, "Start", evt);
}
// Summary is printed automatically on dispose
```

## Example 2: Performance Profiler

Track execution time for each node:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Ceres.Graph.Flow;
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
    graph.ExecuteEventAsync(context, "Start", evt);
}
// Report is printed automatically
```

**Output:**
```
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
using Ceres.Graph.Flow;
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
```
=== Execution Flow ===
→ FlowNode_Start
  → FlowNode_GetVariable
  ← FlowNode_GetVariable
  → FlowNode_Log
  ← FlowNode_Log
← FlowNode_Start
```

## Example 4: Error Tracker

Track errors and exceptions during execution:

```csharp
using System;
using System.Collections.Generic;
using Ceres.Graph.Flow;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ErrorTracker : FlowGraphTracker
{
    private readonly List<ErrorEntry> _errors = new();
    
    private struct ErrorEntry
    {
        public string NodeName;
        public string NodeGuid;
        public string ErrorMessage;
        public DateTime Timestamp;
    }
    
    public override UniTask EnterNode(ExecutableNode node)
    {
        try
        {
            // Node execution happens here
            // We can't catch exceptions here, but we can track which node was executing
        }
        catch (Exception ex)
        {
            _errors.Add(new ErrorEntry
            {
                NodeName = node.GetTypeName(),
                NodeGuid = node.Guid,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.Now
            });
            
            Debug.LogError($"Error in {node.GetTypeName()}: {ex.Message}");
        }
        
        return UniTask.CompletedTask;
    }
    
    public void PrintErrors()
    {
        if (_errors.Count == 0)
        {
            Debug.Log("No errors detected");
            return;
        }
        
        Debug.LogWarning($"=== {_errors.Count} Errors Detected ===");
        foreach (var error in _errors)
        {
            Debug.LogError($"[{error.Timestamp:HH:mm:ss}] {error.NodeName}: {error.ErrorMessage}");
        }
    }
    
    public override void Dispose()
    {
        PrintErrors();
        _errors.Clear();
        base.Dispose();
    }
}
```

## Advanced: Conditional Tracking

Track only specific nodes or conditions:

```csharp
using Ceres.Graph.Flow;
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
    graph.ExecuteEventAsync(context, "Start", evt);
}
```

## Best Practices

### 1. Use TrackerAutoScope

Always use `Auto()` for automatic cleanup:

```csharp
// Good
using (tracker.Auto())
{
    graph.ExecuteEventAsync(context, "Start", evt);
}

// Avoid
FlowGraphTracker.SetActiveTracker(tracker);
graph.ExecuteEventAsync(context, "Start", evt);
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
    graph.ExecuteEventAsync(context, "Start", evt);
}
#else
graph.ExecuteEventAsync(context, "Start", evt);
#endif
```

## Integration with Editor Debugging

The Flow editor uses a built-in tracker for debugging. You can access it:

```csharp
// In editor, the debugger tracker is automatically set
// Your custom tracker will work alongside it
```

## Performance Considerations

- **Overhead**: Trackers add minimal overhead (~0.001ms per node)
- **Memory**: Keep tracked data minimal
- **Disable in Release**: Consider disabling detailed tracking in release builds

## Next Steps

- Learn about [Custom Nodes](./flow_custom_node.md) for creating reusable logic
- Explore [Debugging](./flow_debugging.md) for editor debugging features
- Check [Advanced Features](./flow_advanced.md) for more patterns

