using System;
using Ceres.Annotations;
using Chris.Configs;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Configs")]
    [CeresLabel("Get {0}")]
    [CeresMetadata("style = ConstNode")]
    public sealed class FlowNode_GetConfigT<TConfig> : FlowNode where TConfig : Config<TConfig>, new()
    {
        [OutputPort] 
        public CeresPort<TConfig> result;

        protected override void LocalExecute(ExecutionContext executionContext)
        {
            result.Value = ConfigSystem.GetConfig<TConfig>();
        }
    }
}