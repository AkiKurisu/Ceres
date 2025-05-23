﻿using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.CustomFunctions
{
    /// <summary>
    /// Execute custom function implemented in flow graph
    /// </summary>
    [Serializable]
    [CeresMetadata("style = CustomFunctionNode")]
    public abstract class FlowNode_ExecuteCustomFunction : FlowNode, IRuntimeCompiledNode
    {
        [HideInGraphEditor]
        public string functionName;

        [HideInGraphEditor]
        public FlowGraphFunctionAsset functionAsset;

        private WeakReference<FlowGraph> _functionGraphReference;

        protected sealed override async UniTask Execute(ExecutionContext executionContext)
        {
            FlowGraph functionGraph;
            if (functionAsset && _functionGraphReference != null)
            {
                _functionGraphReference.TryGetTarget(out functionGraph);
            }
            else
            {
                functionGraph = executionContext.Graph.FindSubGraph<FlowGraph>(functionName);
            }

            if (functionGraph == null)
            {
                CeresLogger.LogWarning($"Function {functionName} not exist in subGraphs which is not expected");
            }
            else
            {
                using var evt = ExecuteSubFlowEvent.Create(functionName);
                PreExecuteCustomFunction(evt);
                await functionGraph.ExecuteEventAsyncInternal(executionContext.Context, nameof(CustomFunctionInput),
                    evt);
                PostExecuteCustomFunction(evt);
            }
            executionContext.SetNext(exec.GetT<ExecutableNode>());
        }

        private protected virtual void PreExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {

        }

        private protected virtual void PostExecuteCustomFunction(ExecuteSubFlowEvent evt)
        {

        }

        public void Compile(CeresGraphCompiler compiler)
        {
            if (!functionAsset) return;

            if (compiler.Context is not FlowGraphCompilationContext context)
            {
                return;
            }
            _functionGraphReference = new WeakReference<FlowGraph>(context.AddOrCreateFunctionSubGraph(compiler, functionAsset));
        }
    }

    public abstract class FlowNode_ExecuteCustomFunctionVoid : FlowNode_ExecuteCustomFunction
    {

    }

    public abstract class FlowNode_ExecuteCustomFunctionReturn : FlowNode_ExecuteCustomFunction
    {

    }

    [Serializable]
    [CeresGroup("Hidden")]
    public sealed class FlowNode_ExecuteCustomFunctionTVoid : FlowNode_ExecuteCustomFunctionVoid
    {

    }

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