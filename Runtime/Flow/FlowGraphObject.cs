using System;
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
            _graph?.Dispose();
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
