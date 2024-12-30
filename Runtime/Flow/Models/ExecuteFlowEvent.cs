using System;
using Chris.Events;
namespace Ceres.Graph.Flow
{
    public interface IFlowEvent
    {
        
    }
    
    /// <summary>
    /// Event bridge between Flow Graph and GameObjects
    /// </summary>
    public sealed class ExecuteFlowEvent : EventBase<ExecuteFlowEvent>, IFlowEvent
    {
        public string FunctionName { get; private set; }
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
    
    public sealed class ExecuteFlowEvent<T1> : EventBase<ExecuteFlowEvent<T1>>, IFlowEvent
    {
        public string FunctionName { get; private set; }
        
        public T1 Arg1 { get; private set; }

        public static ExecuteFlowEvent<T1> Create(string functionName, T1 arg1)
        {
            var evt = GetPooled();
            evt.FunctionName = functionName;
            evt.Arg1 = arg1;
            return evt;
        }
    }
    
    public sealed class ExecuteFlowEvent<T1, T2> : EventBase<ExecuteFlowEvent<T1, T2>>, IFlowEvent
    {
        public string FunctionName { get; private set; }
        
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
    
    public sealed class ExecuteFlowEvent<T1, T2, T3> : EventBase<ExecuteFlowEvent<T1, T2, T3>>, IFlowEvent
    {
        public string FunctionName { get; private set; }
        
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
    
    public sealed class ExecuteFlowEvent<T1, T2, T3, T4> : EventBase<ExecuteFlowEvent<T1, T2, T3, T4>>, IFlowEvent
    {
        public string FunctionName { get; private set; }
        
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
}