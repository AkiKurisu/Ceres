using System;
using Chris.Events;
using UObject = UnityEngine.Object;
namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Weak delegate structure for <see cref="ExecutableEvent"/>
    /// </summary>
    public abstract class EventDelegateBase
    {
        private WeakReference<FlowGraph> _graph;

        private string _eventName;

        internal void Initialize(FlowGraph graph, string eventName)
        {
            _graph = new WeakReference<FlowGraph>(graph);
            _eventName = eventName;
        }

        protected void InvokeInternal(UObject contextObject, EventBase eventBase)
        {
            if(_graph.TryGetTarget(out var graph))
            {
                graph.ExecuteEvent(contextObject, _eventName, eventBase);
            }  
        }

        protected string GetEventName()
        {
            return _eventName;
        }

        protected bool IsValid()
        {
            return _graph.TryGetTarget(out _);
        }
    }
    
    public sealed class EventDelegate: EventDelegateBase
    {
        public void Invoke(UObject contextObject)
        {
            InvokeInternal(contextObject, null);
        }
        
        public Action Create(UObject contextObject)
        {
            if (!IsValid()) return null;
            return () => Invoke(contextObject);
        }
    }
    
    public sealed class EventDelegate<T1>: EventDelegateBase
    {
        public void Invoke(UObject contextObject, T1 input1)
        {
            using var evt = ExecuteFlowEvent<T1>.Create(GetEventName(), input1);
            InvokeInternal(contextObject, evt);
        }

        public Action<T1> Create(UObject contextObject)
        {
            if (!IsValid()) return null;
            return input1 => Invoke(contextObject, input1);
        }
    }
    
    public sealed class EventDelegate<T1, T2>: EventDelegateBase
    {
        public void Invoke(UObject contextObject, T1 input1, T2 input2)
        {
            using var evt = ExecuteFlowEvent<T1, T2>.Create(GetEventName(), input1, input2);
            InvokeInternal(contextObject, evt);
        }
        
        public Action<T1, T2> Create(UObject contextObject)
        {
            if (!IsValid()) return null;
            return (input1, input2) => Invoke(contextObject, input1, input2);
        }
    }
    
    public sealed class EventDelegate<T1, T2, T3>: EventDelegateBase
    {
        public void Invoke(UObject contextObject, T1 input1, T2 input2, T3 input3)
        {
            using var evt = ExecuteFlowEvent<T1, T2, T3>.Create(GetEventName(), input1, input2, input3);
            InvokeInternal(contextObject, evt);
        }
        
        public Action<T1, T2, T3> Create(UObject contextObject)
        {
            if (!IsValid()) return null;
            return (input1, input2, input3) => Invoke(contextObject, input1, input2, input3);
        }
    }
    
    public sealed class EventDelegate<T1, T2, T3, T4>: EventDelegateBase
    {
        public void Invoke(UObject contextObject, T1 input1, T2 input2, T3 input3, T4 input4)
        {
            using var evt = ExecuteFlowEvent<T1, T2, T3, T4>.Create(GetEventName(), input1, input2, input3, input4);
            InvokeInternal(contextObject, evt);
        }
        
        public Action<T1, T2, T3, T4> Create(UObject contextObject)
        {
            if (!IsValid()) return null;
            return (input1, input2, input3, input4) => Invoke(contextObject, input1, input2, input3, input4);
        }
    }

    public interface IDelegatePort
    {
        void CreateDelegate(FlowGraph flowGraph, string eventNodeEventName);
    }

    [Serializable]
    public sealed class DelegatePort<TDelegate> : CeresPort<TDelegate>, IDelegatePort where TDelegate: EventDelegateBase, new()
    {
        public DelegatePort()
        {
     
        }
        
        public DelegatePort(TDelegate value): base(value)
        {
    
        }

        public void CreateDelegate(FlowGraph flowGraph, string eventNodeEventName)
        {
            defaultValue = new TDelegate();
            defaultValue.Initialize(flowGraph, eventNodeEventName);
        }
    }
}