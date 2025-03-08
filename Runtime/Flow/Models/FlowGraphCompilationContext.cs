using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace Ceres.Graph.Flow
{
    public sealed class FlowGraphCompilationContext: ICeresGraphCompilationContext, IDisposable
    {
        private static readonly ObjectPool<FlowGraphCompilationContext> Pool = new(() => new FlowGraphCompilationContext());

        private bool _isPooled;

        private readonly Dictionary<FlowGraphFunctionAsset, FlowGraph> _functionGraphInstances = new();

        private FlowGraphCompilationContext()
        {
            
        }

        public static FlowGraphCompilationContext GetPooled()
        {
            var context = Pool.Get();
            context._isPooled = true;
            return context;
        }
        
        public void PreCompileGraph(CeresGraph source)
        {
            
        }

        public void PostCompileGraph(CeresGraph source)
        {
            /* Bind the SubGraph generated during compilation to the life cycle of Top Level Graph */
            foreach (var pair in _functionGraphInstances)
            {
                var id = $"CompilerGenerated_{pair.Key.GetInstanceID()}";
                /* This will let subGraph hidden in flow graph editor */
                source.AddSubGraphSlot<FlowGraph>(new CeresSubGraphSlot
                {
                    Name = id,
                    Guid = id,
                    Graph = pair.Value
                });
            }
        }

        public FlowGraph AddOrCreateFunctionSubGraph(CeresGraphCompiler compiler, FlowGraphFunctionAsset asset)
        {
            if (_functionGraphInstances.TryGetValue(asset, out var graph)) return graph;
            
            graph = asset.GetFlowGraph();
            _functionGraphInstances.Add(asset, graph);
            /* Prevent self reference cycle */
            graph.Compile(compiler);
            return graph;
        }

        public void Dispose()
        {
            _functionGraphInstances.Clear();
            if (!_isPooled) return;
            Pool.Release(this);
        }
    }
}