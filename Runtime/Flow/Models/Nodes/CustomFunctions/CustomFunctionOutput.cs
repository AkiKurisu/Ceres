using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine.Assertions;

namespace Ceres.Graph.Flow.CustomFunctions
{
    [Serializable]
    public class CustomFunctionOutputParameter: CustomFunctionParameter
    {
        public bool hasReturn;
    }

    [Serializable]
    [CeresGroup("Hidden")]
    [CeresLabel("Function Output")]
    [CeresMetadata("style = CustomFunctionOutput")]
    public class CustomFunctionOutput: ForwardNode
    {
        /* Bridge port, connected port will fetch internal port value */
        [InputPort, CeresLabel("Return"), HideInGraphEditor]
        public CeresPort<CeresPort> returnValue = new();
        
        [HideInGraphEditor]
        public CustomFunctionOutputParameter parameter;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            Assert.IsNotNull(executionContext.GetEvent());
            var evt = executionContext.GetEventT<ExecuteSubFlowEvent>();
            if (parameter.hasReturn)
            {
                evt.Return = returnValue.Value;
            }
            return UniTask.CompletedTask;
        }
    }
}