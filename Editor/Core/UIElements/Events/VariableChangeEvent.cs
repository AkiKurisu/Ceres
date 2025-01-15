using UnityEngine.UIElements;
namespace Ceres.Editor
{
    // Used in UIElements events debugger category
    public interface ICeresEvent
    {
        
    }
    
    public enum VariableChangeType
    {
        Create,
        Value,
        Name,
        Type,
        Delete
    }
    
    public class VariableChangeEvent : EventBase<VariableChangeEvent>, ICeresEvent
    {
        public SharedVariable Variable { get; private set; }
        
        public VariableChangeType ChangeType { get; private set; }
        
        public static VariableChangeEvent GetPooled(SharedVariable notifyVariable, VariableChangeType changeType)
        {
            var changeEvent = GetPooled();
            changeEvent.Variable = notifyVariable;
            changeEvent.ChangeType = changeType;
            return changeEvent;
        }
    }
}
