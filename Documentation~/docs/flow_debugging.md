# Debugging

To enable and disable debug mode, click `debug` button in the upper right corner.

Then, you can click `Next Frame` to execute the graph node by node.

## Use Breakpoint

You can right click node and `Add Breakpoint`, and click `Next Breakpoint` in toolbar to execute the graph breakpoint by breakpoint.

![Debug](../resources/Images/flow_debugger.png)

## Use Graph Tracker

`FlowGraphTracker` is a class that can be used to track the execution of the graph.

For more details, you can see the sample [FlowGraphDependencyTracker](https://github.com/AkiKurisu/Ceres/blob/main/Runtime/Flow/Models/FlowGraphTracker.cs).