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

        internal bool IsValidInternal()
        {
            return _graph.TryGetTarget(out _);
        }

        protected UObject GetContextObject()
        {
            return _graph.TryGetTarget(out var graph) ? graph.GetExecutionContext().Context : null;
        }

        internal static void StaticInitialize<TDelegate>() where TDelegate: EventDelegateBase
        {
            /* Create CDO to invoke static constructor */
            Activator.CreateInstance<TDelegate>();
        }
    }
    
    public sealed class EventDelegate: EventDelegateBase
    {
        static EventDelegate()
        {
            CeresPort<EventDelegate>.MakeCompatibleTo<Action>(x=> x);
        }
        
        public void Invoke(UObject contextObject)
        {
            InvokeInternal(contextObject, null);
        }
        
        public static implicit operator Action(EventDelegate @delegate)
        {
            if (!@delegate.IsValid()) return null;
            var contextObject = @delegate.GetContextObject();
            return () => @delegate.Invoke(contextObject);
        }
    }
    
    public sealed class EventDelegate<T1>: EventDelegateBase
    {
        static EventDelegate()
        {
            CeresPort<EventDelegate<T1>>.MakeCompatibleTo<Action<T1>>(x=> x);
        }
        
        public void Invoke(UObject contextObject, T1 input1)
        {
            using var evt = ExecuteFlowEvent<T1>.Create(GetEventName(), input1);
            InvokeInternal(contextObject, evt);
        }

        public static implicit operator Action<T1>(EventDelegate<T1> @delegate)
        {
            if (!@delegate.IsValid()) return null;
            var contextObject = @delegate.GetContextObject();
            return input1 => @delegate.Invoke(contextObject, input1);
        }
    }
    
    public sealed class EventDelegate<T1, T2>: EventDelegateBase
    {
        static EventDelegate()
        {
            CeresPort<EventDelegate<T1, T2>>.MakeCompatibleTo<Action<T1, T2>>(x=> x);
        }
        
        public void Invoke(UObject contextObject, T1 input1, T2 input2)
        {
            using var evt = ExecuteFlowEvent<T1, T2>.Create(GetEventName(), input1, input2);
            InvokeInternal(contextObject, evt);
        }
        
        public static implicit operator Action<T1, T2>(EventDelegate<T1, T2> @delegate)
        {
            if (!@delegate.IsValid()) return null;
            var contextObject = @delegate.GetContextObject();
            return (input1, input2) => @delegate.Invoke(contextObject, input1, input2);
        }
    }
    
    public sealed class EventDelegate<T1, T2, T3>: EventDelegateBase
    {
        static EventDelegate()
        {
            CeresPort<EventDelegate<T1, T2, T3>>.MakeCompatibleTo<Action<T1, T2, T3>>(x=> x);
        }
        
        public void Invoke(UObject contextObject, T1 input1, T2 input2, T3 input3)
        {
            using var evt = ExecuteFlowEvent<T1, T2, T3>.Create(GetEventName(), input1, input2, input3);
            InvokeInternal(contextObject, evt);
        }
        
        public static implicit operator Action<T1, T2, T3>(EventDelegate<T1, T2, T3> @delegate)
        {
            if (!@delegate.IsValid()) return null;
            var contextObject = @delegate.GetContextObject();
            return (input1, input2, input3) => @delegate.Invoke(contextObject, input1, input2, input3);
        }
    }
    
    public sealed class EventDelegate<T1, T2, T3, T4>: EventDelegateBase
    {
        static EventDelegate()
        {
            CeresPort<EventDelegate<T1, T2, T3, T4>>.MakeCompatibleTo<Action<T1, T2, T3, T4>>(x=> x);
        }
        
        public void Invoke(UObject contextObject, T1 input1, T2 input2, T3 input3, T4 input4)
        {
            using var evt = ExecuteFlowEvent<T1, T2, T3, T4>.Create(GetEventName(), input1, input2, input3, input4);
            InvokeInternal(contextObject, evt);
        }
        
        public static implicit operator Action<T1, T2, T3, T4>(EventDelegate<T1, T2, T3, T4> @delegate)
        {
            if (!@delegate.IsValid()) return null;
            var contextObject = @delegate.GetContextObject();
            return (input1, input2, input3, input4) => @delegate.Invoke(contextObject, input1, input2, input3, input4);
        }
    }
    
    public sealed class EventDelegate<T1, T2, T3, T4, T5>: EventDelegateBase
    {
        static EventDelegate()
        {
            CeresPort<EventDelegate<T1, T2, T3, T4, T5>>.MakeCompatibleTo<Action<T1, T2, T3, T4, T5>>(x=> x);
        }
        
        public void Invoke(UObject contextObject, T1 input1, T2 input2, T3 input3, T4 input4, T5 input5)
        {
            using var evt = ExecuteFlowEvent<T1, T2, T3, T4, T5>.Create(GetEventName(), input1, input2, input3, input4, input5);
            InvokeInternal(contextObject, evt);
        }
        
        public static implicit operator Action<T1, T2, T3, T4, T5>(EventDelegate<T1, T2, T3, T4, T5> @delegate)
        {
            if (!@delegate.IsValid()) return null;
            var contextObject = @delegate.GetContextObject();
            return (input1, input2, input3, input4, input5) => @delegate.Invoke(contextObject, input1, input2, input3, input4, input5);
        }
    }
    
    public sealed class EventDelegate<T1, T2, T3, T4, T5, T6>: EventDelegateBase
    {
        static EventDelegate()
        {
            CeresPort<EventDelegate<T1, T2, T3, T4, T5, T6>>.MakeCompatibleTo<Action<T1, T2, T3, T4, T5, T6>>(x=> x);
        }
        
        public void Invoke(UObject contextObject, T1 input1, T2 input2, T3 input3, T4 input4, T5 input5, T6 input6)
        {
            using var evt = ExecuteFlowEvent<T1, T2, T3, T4, T5, T6>.Create(GetEventName(), input1, input2, input3, input4, input5, input6);
            InvokeInternal(contextObject, evt);
        }
        
        public static implicit operator Action<T1, T2, T3, T4, T5, T6>(
            EventDelegate<T1, T2, T3, T4, T5, T6> @delegate)
        {
            if (!@delegate.IsValid()) return null;
            var contextObject = @delegate.GetContextObject();
            return (input1, input2, input3, input4, input5, input6) => @delegate.Invoke(contextObject, input1, input2, input3, input4, input5, input6);
        }
    }

    public static class EventDelegateExtensions
    {
        public static bool IsValid(this EventDelegateBase @delegate)
        {
            return @delegate != null && @delegate.IsValidInternal();
        }
    }

    public interface IDelegatePort
    {
        void CreateDelegate(FlowGraph flowGraph, string eventNodeEventName);
    }

    [Serializable]
    public sealed class DelegatePort<TDelegate> : CeresPort<TDelegate>, IDelegatePort where TDelegate: EventDelegateBase, new()
    {
        static DelegatePort()
        {
            EventDelegateBase.StaticInitialize<TDelegate>();
        }
        
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