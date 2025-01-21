using System;
using Ceres.Graph.Flow.Annotations;
using Chris.Serialization;
using UnityEngine;
namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Base class for <see cref="ScriptableObject"/> contains Flow Graph.
    /// </summary>
    [GenerateFlow(GenerateBridges = false, GenerateImplementation = true)]
    public abstract partial class FlowGraphScriptableObjectBase: ScriptableObject
    {
        /// <summary>
        /// Get the specific container type instance at runtime
        /// </summary>
        /// <returns></returns>
        public abstract Type GetContainerType();
    }
    
    /// <summary>
    /// Asset contains Flow Graph that can be shared between multi container instance.
    /// </summary>
    [CreateAssetMenu(fileName = "FlowGraphAsset", menuName = "Ceres/Flow Graph Asset")]
    public class FlowGraphAsset: FlowGraphScriptableObjectBase
    {
        /// <summary>
        /// Specific container type this asset act as at runtime
        /// </summary>
        public SerializedType<IFlowGraphContainer> containerType;
        
        public override Type GetContainerType()
        {
            return containerType.GetObjectType() ?? typeof(FlowGraphObject);
        }
    }
}