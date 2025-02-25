using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.CustomFunctions
{
    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// Node for executing custom function implemented in flow graph
    /// </summary>
    [Serializable]
    [CeresMetadata("style = CustomFunctionNode")]
    public abstract class FlowNode_ExecuteCustomFunction: FlowNode
    {
        [HideInGraphEditor]
        public string functionName;
        
        protected sealed override async UniTask Execute(ExecutionContext executionContext)
        {
            var subGraph = executionContext.Graph.FindSubGraph<FlowGraph>(functionName);
            if (subGraph == null) return;
            using var evt = ExecuteSubFlowEvent.Create(functionName);
            PreExecuteCustomFunction(evt);
            await subGraph.ExecuteEventAsyncInternal(executionContext.Context, nameof(CustomFunctionInput), evt);
            PostExecuteCustomFunction(evt);
            executionContext.SetNext(exec.GetT<ExecutableNode>());
        }

        private protected virtual void PreExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            
        }
        
        private protected virtual void PostExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            
        }
    }
    
    // ReSharper disable once InconsistentNaming
    public abstract class FlowNode_ExecuteCustomFunctionVoid: FlowNode_ExecuteCustomFunction
    {

    }
    
    // ReSharper disable once InconsistentNaming
    public abstract class FlowNode_ExecuteCustomFunctionReturn: FlowNode_ExecuteCustomFunction
    {

    }
    
    // ReSharper disable once InconsistentNaming
    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTVoid : FlowNode_ExecuteCustomFunctionVoid
    {

    }
    
    // ReSharper disable once InconsistentNaming
    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTReturn<TR> : FlowNode_ExecuteCustomFunctionReturn
    {
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;

        private protected override void PostExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            var returnPort = (CeresPort<TR>)evt.Return;
            output.Value = returnPort.Value;
        }
    }
    
    // ReSharper disable once InconsistentNaming
    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTVoid<TP1> : FlowNode_ExecuteCustomFunctionVoid
    {
        [InputPort]
        public CeresPort<TP1> input1;
        
        private protected override void PreExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            evt.Args.Add(input1);
        }
    }
    
    // ReSharper disable once InconsistentNaming
    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTReturn<TP1, TR> : FlowNode_ExecuteCustomFunctionReturn
    {
        [InputPort]
        public CeresPort<TP1> input1;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
        
        private protected override void PreExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            evt.Args.Add(input1);
        }
        
        private protected override void PostExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            var returnPort = (CeresPort<TR>)evt.Return;
            output.Value = returnPort.Value;
        }
    }
    
    // ReSharper disable once InconsistentNaming
    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTVoid<TP1, TP2> : 
        FlowNode_ExecuteCustomFunctionVoid
    {
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
        
        private protected override void PreExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            evt.Args.Add(input1);
            evt.Args.Add(input2);
        }
    }
    
    // ReSharper disable once InconsistentNaming
    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTReturn<TP1, TP2, TR> : 
        FlowNode_ExecuteCustomFunctionReturn
    {
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
        
        private protected override void PreExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            evt.Args.Add(input1);
            evt.Args.Add(input2);
        }
        
        private protected override void PostExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            var returnPort = (CeresPort<TR>)evt.Return;
            output.Value = returnPort.Value;
        }
    }
    
    // ReSharper disable once InconsistentNaming
    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTVoid<TP1, TP2, TP3> : 
        FlowNode_ExecuteCustomFunctionVoid
    {
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
        
        [InputPort]
        public CeresPort<TP3> input3;
        
        private protected override void PreExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            evt.Args.Add(input1);
            evt.Args.Add(input2);
            evt.Args.Add(input3);
        }
    }
    
    // ReSharper disable once InconsistentNaming
    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTReturn<TP1, TP2, TP3, TR> : 
        FlowNode_ExecuteCustomFunctionReturn
    {
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
        
        [InputPort]
        public CeresPort<TP3> input3;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
        
        private protected override void PreExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            evt.Args.Add(input1);
            evt.Args.Add(input2);
            evt.Args.Add(input3);
        }
        
        private protected override void PostExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            var returnPort = (CeresPort<TR>)evt.Return;
            output.Value = returnPort.Value;
        }
    }
    
    // ReSharper disable once InconsistentNaming
    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTVoid<TP1, TP2, TP3, TP4> : 
        FlowNode_ExecuteCustomFunctionVoid
    {
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
        
        [InputPort]
        public CeresPort<TP3> input3;

        [InputPort] 
        public CeresPort<TP4> input4;
        
        private protected override void PreExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            evt.Args.Add(input1);
            evt.Args.Add(input2);
            evt.Args.Add(input3);
            evt.Args.Add(input4);
        }
    }
    
    // ReSharper disable once InconsistentNaming
    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTReturn<TP1, TP2, TP3, TP4, TR> : 
        FlowNode_ExecuteCustomFunctionReturn
    {
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
        
        [InputPort]
        public CeresPort<TP3> input3;
        
        [InputPort]
        public CeresPort<TP4> input4;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
        
        private protected override void PreExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            evt.Args.Add(input1);
            evt.Args.Add(input2);
            evt.Args.Add(input3);
            evt.Args.Add(input4);
        }
        
        private protected override void PostExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            var returnPort = (CeresPort<TR>)evt.Return;
            output.Value = returnPort.Value;
        }
    }
    
    // ReSharper disable once InconsistentNaming
    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTVoid<TP1, TP2, TP3, TP4, TP5> : 
        FlowNode_ExecuteCustomFunctionVoid
    {
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
        
        [InputPort]
        public CeresPort<TP3> input3;

        [InputPort] 
        public CeresPort<TP4> input4;
        
        [InputPort] 
        public CeresPort<TP5> input5;
        
        private protected override void PreExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            evt.Args.Add(input1);
            evt.Args.Add(input2);
            evt.Args.Add(input3);
            evt.Args.Add(input4);
            evt.Args.Add(input5);
        }
    }
    
    // ReSharper disable once InconsistentNaming
    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTReturn<TP1, TP2, TP3, TP4, TP5, TR> : 
        FlowNode_ExecuteCustomFunctionReturn
    {
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
        
        [InputPort]
        public CeresPort<TP3> input3;
        
        [InputPort]
        public CeresPort<TP4> input4;
        
        [InputPort]
        public CeresPort<TP5> input5;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
        
                
        private protected override void PreExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            evt.Args.Add(input1);
            evt.Args.Add(input2);
            evt.Args.Add(input3);
            evt.Args.Add(input4);
            evt.Args.Add(input5);
        }
        
        private protected override void PostExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            var returnPort = (CeresPort<TR>)evt.Return;
            output.Value = returnPort.Value;
        }
    }
    
    // ReSharper disable once InconsistentNaming
    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTVoid<TP1, TP2, TP3, TP4, TP5, TP6> : 
        FlowNode_ExecuteCustomFunctionVoid
    {
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
        
        [InputPort]
        public CeresPort<TP3> input3;

        [InputPort] 
        public CeresPort<TP4> input4;
        
        [InputPort]
        public CeresPort<TP5> input5;
        
        [InputPort]
        public CeresPort<TP6> input6;
        
        private protected override void PreExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            evt.Args.Add(input1);
            evt.Args.Add(input2);
            evt.Args.Add(input3);
            evt.Args.Add(input4);
            evt.Args.Add(input5);
            evt.Args.Add(input6);
        }
    }

    // ReSharper disable once InconsistentNaming
    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTReturn<TP1, TP2, TP3, TP4, TP5, TP6, TR> :
        FlowNode_ExecuteCustomFunctionReturn
    {
        [InputPort] 
        public CeresPort<TP1> input1;

        [InputPort] 
        public CeresPort<TP2> input2;

        [InputPort] 
        public CeresPort<TP3> input3;

        [InputPort] 
        public CeresPort<TP4> input4;

        [InputPort] 
        public CeresPort<TP5> input5;

        [InputPort] 
        public CeresPort<TP6> input6;

        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;

        private protected override void PreExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            evt.Args.Add(input1);
            evt.Args.Add(input2);
            evt.Args.Add(input3);
            evt.Args.Add(input4);
            evt.Args.Add(input5);
            evt.Args.Add(input6);
        }
        
        private protected override void PostExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {
            var returnPort = (CeresPort<TR>)evt.Return;
            output.Value = returnPort.Value;
        }
    }
}