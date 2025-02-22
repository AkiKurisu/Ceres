using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine.Assertions;

namespace Ceres.Graph.Flow.CustomFunctions
{
    [Serializable]
    [CeresGroup("Hidden")]
    [CeresMetadata("style = CustomFunctionOutput")]
    public class CustomFunctionOutput: ForwardNode
    {
        /* Bridge port, connected port will map to the internal port at runtime */
        [InputPort, CeresLabel("Return"), HideInGraphEditor]
        public CeresPort<CeresPort> returnValue =new();
        
        [HideInGraphEditor]
        public bool hasReturn;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            Assert.IsNotNull(executionContext.GetEvent());
            var evt = executionContext.GetEventT<ExecuteSubFlowEvent>();
            if (hasReturn)
            {
                evt.Return = returnValue.Value;
            }
            return UniTask.CompletedTask;
        }
    }
}