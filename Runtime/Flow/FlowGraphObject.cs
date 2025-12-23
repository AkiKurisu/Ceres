using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Ceres.Graph.Flow.Annotations;
using UObject = UnityEngine.Object;

namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Base class for <see cref="MonoBehaviour"/> contains Flow Graph.
    /// </summary>
    public abstract class FlowGraphObjectBase : MonoBehaviour, IFlowGraphRuntime
    {
        private static readonly List<FlowGraphObjectBase> RuntimeInstances = new();
        
        UObject IFlowGraphRuntime.Object => this;
        
        [NonSerialized]
        private FlowGraph _graph;
        
        FlowGraph IFlowGraphRuntime.Graph
        {
            get
            {
                if (_graph == null)
                {
                    _graph = CreateRuntimeFlowGraphInstance();
                    using var context = FlowGraphCompilationContext.GetPooled();
                    using var compiler = CeresGraphCompiler.GetPooled(_graph, context);
                    _graph.Compile(compiler);
                    
                    // Register instance for hot reload
                    RegisterInstance();
                }

                return _graph;
            }
        }

        protected abstract FlowGraph CreateRuntimeFlowGraphInstance();
        
        /// <summary>
        /// Release graph instance safely
        /// </summary>
        /// <returns></returns>
        protected void ReleaseGraph()
        {
            UnregisterInstance();
            _graph?.Dispose();
            _graph = null;
        }

        /// <summary>
        /// Register this instance for hot reload tracking
        /// </summary>
        private void RegisterInstance()
        {
            if (!RuntimeInstances.Contains(this))
            {
                RuntimeInstances.Add(this);
            }
        }

        /// <summary>
        /// Unregister this instance from hot reload tracking
        /// </summary>
        private void UnregisterInstance()
        {
            RuntimeInstances.Remove(this);
        }

        /// <summary>
        /// Get the container for this runtime instance
        /// </summary>
        internal IFlowGraphContainer GetContainer()
        {
            // Try to get container from FlowGraphInstanceObject
            if (this is FlowGraphInstanceObject instanceObject)
            {
                return instanceObject.graphAsset;
            }
            
            // Try to get container from FlowGraphObject (generated implementation)
            if (this is IFlowGraphContainer container)
            {
                return container;
            }
            
            return null;
        }

        /// <summary>
        /// Replace the graph instance (for hot reload)
        /// </summary>
        internal void ReplaceGraph(FlowGraph newGraph)
        {
            _graph?.Dispose();
            _graph = newGraph;
        }

        /// <summary>
        /// Get all active runtime instances
        /// </summary>
        internal static List<FlowGraphObjectBase> GetAllRuntimeInstances()
        {
            // Clean up destroyed instances
            RuntimeInstances.RemoveAll(instance => !instance);
            return RuntimeInstances.ToList();
        }

        /// <summary>
        /// Get runtime instances for a specific container
        /// </summary>
        internal static List<FlowGraphObjectBase> GetRuntimeInstances(IFlowGraphContainer container)
        {
            if (container == null)
            {
                return new List<FlowGraphObjectBase>();
            }
            
            return RuntimeInstances
                .Where(instance => instance && instance.GetContainer() == container)
                .ToList();
        }

        private void OnDestroy()
        {
            ReleaseGraph();
        }
    }
    
    /// <summary>
    /// <see cref="MonoBehaviour"/> contains persistent <see cref="FlowGraphData"/> and runtime instance.
    /// </summary>
    [GenerateFlow(GenerateRuntime = false, GenerateImplementation = true)]
    public partial class FlowGraphObject : FlowGraphObjectBase
    {
        protected sealed override FlowGraph CreateRuntimeFlowGraphInstance()
        {
            return GetFlowGraph();
        }
    }
}
