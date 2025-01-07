using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Ceres.Graph.Flow
{
    [Serializable]
    [CeresMetadata("style = ExecutableEvent")]
    public abstract class ExecutableEvent : ExecutableNode
    {
        public string eventName = "New Event";

        [HideInGraphEditor]
        public bool isImplementable;
    }
    
    /// <summary>
    /// Event entry node to start an execution
    /// </summary>
    [Serializable]
    [NodeGroup("Hidden")]
    public class ExecutionEvent : ExecutableEvent
    {
        [OutputPort, HideInGraphEditor, CeresLabel("")]
        public DelegatePort<EventDelegate> eventDelegate;
        
        [OutputPort(false), CeresLabel("")]
        public NodePort exec;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            executionContext.SetNext(exec.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }
    }
    
    [Serializable]
    public abstract class ExecutionEventGeneric : ExecutableEvent
    {
        [OutputPort(false), CeresLabel("")]
        public NodePort exec;
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class ExecutionEventUber: ExecutableEvent, ISerializationCallbackReceiver
    {
        [OutputPort(false), CeresLabel("")]
        public NodePort exec;
        
        [OutputPort]
        public CeresPort<object>[] outputs = Array.Empty<CeresPort<object>>();

        [HideInGraphEditor]
        public int argumentCount;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            var evt = executionContext.GetEventT<ExecuteFlowEvent>();
            if(evt.Args != null)
            {
                for (var i = 0; i < evt.Args.Length; ++i)
                {
                    outputs[i].Value = evt.Args[i];
                }
            }
            executionContext.SetNext(exec.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }

        public void OnBeforeSerialize()
        {
            
        }

        public void OnAfterDeserialize()
        {
            outputs = new CeresPort<object>[argumentCount];
            for (int i = 0; i < outputs.Length; i++)
            {
                outputs[i] = new CeresPort<object>();
            }
        }
    }
    
    /// <summary>
    /// Event entry node to start an execution
    /// </summary>
    [Serializable]
    [NodeGroup("Hidden")]
    public class ExecutionEvent<T1> : ExecutionEventGeneric
    {
        [OutputPort, HideInGraphEditor, CeresLabel("")]
        public DelegatePort<EventDelegate<T1>> eventDelegate;
        
        [OutputPort] 
        public CeresPort<T1> output1;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            var evt = executionContext.GetEventT<ExecuteFlowEvent<T1>>();
            output1.Value = evt.Arg1;
            executionContext.SetNext(exec.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public class ExecutionEvent<T1, T2> : ExecutionEventGeneric
    {
        [OutputPort, HideInGraphEditor, CeresLabel("")]
        public DelegatePort<EventDelegate<T1, T2>> eventDelegate;
        
        [OutputPort] 
        public CeresPort<T1> output1;
        
        [OutputPort] 
        public CeresPort<T2> output2;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            var evt = executionContext.GetEventT<ExecuteFlowEvent<T1, T2>>();
            output1.Value = evt.Arg1;
            output2.Value = evt.Arg2;
            executionContext.SetNext(exec.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public class ExecutionEvent<T1, T2, T3> : ExecutionEventGeneric
    {
        [OutputPort, HideInGraphEditor, CeresLabel("")]
        public DelegatePort<EventDelegate<T1, T2, T3>> eventDelegate;
        
        [OutputPort] 
        public CeresPort<T1> output1;
        
        [OutputPort] 
        public CeresPort<T2> output2;
        
        [OutputPort] 
        public CeresPort<T3> output3;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            var evt = executionContext.GetEventT<ExecuteFlowEvent<T1, T2, T3>>();
            output1.Value = evt.Arg1;
            output2.Value = evt.Arg2;
            output3.Value = evt.Arg3;
            executionContext.SetNext(exec.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public class ExecutionEvent<T1, T2, T3, T4> : ExecutionEventGeneric
    {
        [OutputPort, HideInGraphEditor, CeresLabel("")]
        public DelegatePort<EventDelegate<T1, T2, T3, T4>> eventDelegate;
        
        [OutputPort] 
        public CeresPort<T1> output1;
        
        [OutputPort] 
        public CeresPort<T2> output2;
        
        [OutputPort] 
        public CeresPort<T3> output3;
        
        [OutputPort] 
        public CeresPort<T4> output4;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            var evt = executionContext.GetEventT<ExecuteFlowEvent<T1, T2, T3, T4>>();
            output1.Value = evt.Arg1;
            output2.Value = evt.Arg2;
            output3.Value = evt.Arg3;
            output4.Value = evt.Arg4;
            executionContext.SetNext(exec.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public class ExecutionEvent<T1, T2, T3, T4, T5> : ExecutionEventGeneric
    {
        [OutputPort, HideInGraphEditor, CeresLabel("")]
        public DelegatePort<EventDelegate<T1, T2, T3, T4, T5>> eventDelegate;
        
        [OutputPort] 
        public CeresPort<T1> output1;
        
        [OutputPort] 
        public CeresPort<T2> output2;
        
        [OutputPort] 
        public CeresPort<T3> output3;
        
        [OutputPort] 
        public CeresPort<T4> output4;
        
        [OutputPort] 
        public CeresPort<T5> output5;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            var evt = executionContext.GetEventT<ExecuteFlowEvent<T1, T2, T3, T4, T5>>();
            output1.Value = evt.Arg1;
            output2.Value = evt.Arg2;
            output3.Value = evt.Arg3;
            output4.Value = evt.Arg4;
            output5.Value = evt.Arg5;
            executionContext.SetNext(exec.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public class ExecutionEvent<T1, T2, T3, T4, T5, T6> : ExecutionEventGeneric
    {
        [OutputPort, HideInGraphEditor, CeresLabel("")]
        public DelegatePort<EventDelegate<T1, T2, T3, T4, T5, T6>> eventDelegate;
        
        [OutputPort] 
        public CeresPort<T1> output1;
        
        [OutputPort] 
        public CeresPort<T2> output2;
        
        [OutputPort] 
        public CeresPort<T3> output3;
        
        [OutputPort] 
        public CeresPort<T4> output4;
        
        [OutputPort] 
        public CeresPort<T5> output5;
        
        [OutputPort] 
        public CeresPort<T6> output6;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            var evt = executionContext.GetEventT<ExecuteFlowEvent<T1, T2, T3, T4, T5, T6>>();
            output1.Value = evt.Arg1;
            output2.Value = evt.Arg2;
            output3.Value = evt.Arg3;
            output4.Value = evt.Arg4;
            output5.Value = evt.Arg5;
            output6.Value = evt.Arg6;
            executionContext.SetNext(exec.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }
    }
}