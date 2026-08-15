using Ceres.Graph;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    // Used in UIElements events debugger category
    public interface ICeresEvent
    {
        
    }
    
    public enum VariableChangeType
    {
        /// <summary>
        /// Call on variable is added
        /// </summary>
        Add,
        /// <summary>
        /// Call on variable value change
        /// </summary>
        Value,
        /// <summary>
        /// Call on variable name change
        /// </summary>
        Name,
        /// <summary>
        /// Call on variable type change if has dynamic type
        /// </summary>
        Type,
        /// <summary>
        /// Call on variable is removed
        /// </summary>
        Remove
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
