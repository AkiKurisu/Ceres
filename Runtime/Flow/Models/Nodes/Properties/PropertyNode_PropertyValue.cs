using System;
using Ceres.Annotations;
using UObject = UnityEngine.Object;

namespace Ceres.Graph.Flow.Properties
{
    [Serializable]
    public abstract class PropertyNode_PropertyValue : PropertyNode
    {
        [HideInGraphEditor] 
        public bool isSelfTarget;
        
        [HideInGraphEditor] 
        public bool isStatic;
        
        protected TValue GetTargetOrDefault<TValue>(CeresPort<TValue> inputPort, ExecutionContext context)
        {
            if (isStatic)
            {
                return default;
            }
            
            if (!isSelfTarget)
            {
                return inputPort.Value;
            }
            
            bool isNull;
            if(inputPort.Value is UObject value)
            {
                isNull = !value;
            }
            else
            {
                isNull = inputPort.Value == null;
            }
            
            if (isNull && context.Context is TValue tmpTarget)
            {
                return tmpTarget;
            }
            return inputPort.Value;
        }
    }
}