using System;
using Chris.Events;
namespace Ceres.Graph.Flow
{
    public interface IFlowEvent
    {
        
    }

    public abstract class ExecuteFlowEventBase<TEvent> : EventBase<TEvent>, IFlowEvent 
        where TEvent: ExecuteFlowEventBase<TEvent>, new()
    {
        public string FunctionName { get; protected set; }
    }

    /// <summary>
    /// Event bridge between Flow Graph and GameObjects
    /// </summary>
    public sealed class ExecuteFlowEvent : ExecuteFlowEventBase<ExecuteFlowEvent>
    {
        public object[] Args { get; private set; }
        

        public static readonly object[] DefaultArgs = Array.Empty<object>();

        public static ExecuteFlowEvent Create(string functionName, params object[] arguments)
        {
            var evt = GetPooled();
            evt.FunctionName = functionName;
            evt.Args = arguments;
            return evt;
        }
    }
    
    public sealed class ExecuteFlowEvent<T1> : ExecuteFlowEventBase<ExecuteFlowEvent<T1>>
    {
        public T1 Arg1 { get; private set; }

        public static ExecuteFlowEvent<T1> Create(string functionName, T1 arg1)
        {
            var evt = GetPooled();
            evt.FunctionName = functionName;
            evt.Arg1 = arg1;
            return evt;
        }
    }
    
    public sealed class ExecuteFlowEvent<T1, T2> : ExecuteFlowEventBase<ExecuteFlowEvent<T1, T2>>
    {
        public T1 Arg1 { get; private set; }
        
        public T2 Arg2 { get; private set; }

        public static ExecuteFlowEvent<T1, T2> Create(string functionName, T1 arg1, T2 arg2)
        {
            var evt = GetPooled();
            evt.FunctionName = functionName;
            evt.Arg1 = arg1;
            evt.Arg2 = arg2;
            return evt;
        }
    }
    
    public sealed class ExecuteFlowEvent<T1, T2, T3> : ExecuteFlowEventBase<ExecuteFlowEvent<T1, T2, T3>>, IFlowEvent
    {
        public T1 Arg1 { get; private set; }
        
        public T2 Arg2 { get; private set; }
        
        public T3 Arg3 { get; private set; }

        public static ExecuteFlowEvent<T1, T2, T3> Create(string functionName, T1 arg1, T2 arg2, T3 arg3)
        {
            var evt = GetPooled();
            evt.FunctionName = functionName;
            evt.Arg1 = arg1;
            evt.Arg2 = arg2;
            evt.Arg3 = arg3;
            return evt;
        }
    }
    
    public sealed class ExecuteFlowEvent<T1, T2, T3, T4> : ExecuteFlowEventBase<ExecuteFlowEvent<T1, T2, T3, T4>>
    {
        public T1 Arg1 { get; private set; }
        
        public T2 Arg2 { get; private set; }
        
        public T3 Arg3 { get; private set; }
        
        public T4 Arg4 { get; private set; }

        public static ExecuteFlowEvent<T1, T2, T3, T4> Create(string functionName, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            var evt = GetPooled();
            evt.FunctionName = functionName;
            evt.Arg1 = arg1;
            evt.Arg2 = arg2;
            evt.Arg3 = arg3;
            evt.Arg4 = arg4;
            return evt;
        }
    }
    
    public sealed class ExecuteFlowEvent<T1, T2, T3, T4, T5> : 
        ExecuteFlowEventBase<ExecuteFlowEvent<T1, T2, T3, T4, T5>>
    {
        public T1 Arg1 { get; private set; }
        
        public T2 Arg2 { get; private set; }
        
        public T3 Arg3 { get; private set; }
        
        public T4 Arg4 { get; private set; }
        
        public T5 Arg5 { get; private set; }

        public static ExecuteFlowEvent<T1, T2, T3, T4, T5> Create(
            string functionName, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            var evt = GetPooled();
            evt.FunctionName = functionName;
            evt.Arg1 = arg1;
            evt.Arg2 = arg2;
            evt.Arg3 = arg3;
            evt.Arg4 = arg4;
            evt.Arg5 = arg5;
            return evt;
        }
    }
    
    public sealed class ExecuteFlowEvent<T1, T2, T3, T4, T5, T6> : 
        ExecuteFlowEventBase<ExecuteFlowEvent<T1, T2, T3, T4, T5, T6>>
    {
        public T1 Arg1 { get; private set; }
        
        public T2 Arg2 { get; private set; }
        
        public T3 Arg3 { get; private set; }
        
        public T4 Arg4 { get; private set; }
        
        public T5 Arg5 { get; private set; }
        
        public T6 Arg6 { get; private set; }

        public static ExecuteFlowEvent<T1, T2, T3, T4, T5, T6> Create(
            string functionName, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            var evt = GetPooled();
            evt.FunctionName = functionName;
            evt.Arg1 = arg1;
            evt.Arg2 = arg2;
            evt.Arg3 = arg3;
            evt.Arg4 = arg4;
            evt.Arg5 = arg5;
            evt.Arg6 = arg6;
            return evt;
        }
    }
}