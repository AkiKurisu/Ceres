using System;
using Ceres.Graph.Flow.Annotations;
using Chris.Serialization;
using UnityEngine;
namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Base class for <see cref="ScriptableObject"/> contains Flow Graph.
    /// </summary>
    [GenerateFlow(GenerateRuntime = false, GenerateImplementation = true)]
    public abstract partial class FlowGraphScriptableObjectBase: ScriptableObject
    {
        /// <summary>
        /// Get the specific <see cref="IFlowGraphRuntime"/> type instance
        /// </summary>
        /// <returns></returns>
        public abstract Type GetRuntimeType();
    }
    
    /// <summary>
    /// Asset contains <see cref="FlowGraphData"/> that can be shared between multi <see cref="IFlowGraphRuntime"/> instances.
    /// </summary>
    [CreateAssetMenu(fileName = "FlowGraphAsset", menuName = "Ceres/Flow Graph Asset")]
    public class FlowGraphAsset: FlowGraphScriptableObjectBase
    {
        /// <summary>
        /// Specific <see cref="IFlowGraphRuntime"/> type this asset act as at runtime
        /// </summary>
        public SerializedType<IFlowGraphRuntime> runtimeType;
        
        public override Type GetRuntimeType()
        {
            return runtimeType.GetObjectType() ?? typeof(FlowGraphInstanceObject);
        }
    }
}