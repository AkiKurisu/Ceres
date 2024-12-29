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

        public static ExecuteFlowEvent Create(string functionName, params object[] arguments)
        {
            var evt = GetPooled();
            evt.Args = arguments;
            evt.FunctionName = functionName;
            return evt;
        }
        
        public static ExecuteFlowEvent Create(string functionName)
        {
            var evt = GetPooled();
            evt.Args = null;
            evt.FunctionName = functionName;
            return evt;
        }
    }
}