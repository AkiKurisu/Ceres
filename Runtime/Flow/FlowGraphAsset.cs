using System;
using Ceres.Graph.Flow.Annotations;
using Chris.Serialization;
using UnityEngine;

namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Interface for <see cref="IFlowGraphContainer"/> that use specific <see cref="IFlowGraphRuntime"/> type instance
    /// </summary>
    public interface IRedirectFlowGraphRuntimeType
    {
        /// <summary>
        /// Get the specific <see cref="IFlowGraphRuntime"/> type instance
        /// </summary>
        /// <returns></returns>
        Type GetRuntimeType();
    }
    
    /// <summary>
    /// Base class for <see cref="ScriptableObject"/> contains Flow Graph.
    /// </summary>
    [GenerateFlow(GenerateRuntime = false, GenerateImplementation = true)]
    public abstract partial class FlowGraphScriptableObjectBase: ScriptableObject
    {

    }
    
    /// <summary>
    /// Asset contains <see cref="FlowGraphData"/> that can be shared between multi <see cref="IFlowGraphRuntime"/> instances.
    /// </summary>
    [CreateAssetMenu(fileName = "FlowGraphAsset", menuName = "Ceres/Flow Graph Asset")]
    public class FlowGraphAsset: FlowGraphScriptableObjectBase, IRedirectFlowGraphRuntimeType
    {
        /// <summary>
        /// Specific <see cref="IFlowGraphRuntime"/> type this asset act as at runtime
        /// </summary>
        public SerializedType<IFlowGraphRuntime> runtimeType;
        
        [ExecutableFunction]
        public virtual Type GetRuntimeType()
        {
            return runtimeType.GetObjectType() ?? typeof(FlowGraphInstanceObject);
        }
    }
}