using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ceres.Graph.Flow.Utilities
{
    public abstract class FlowNode_MakeArray : ExecutableNode
    {
    }
    
    /// <summary>
    /// Make an array of type <see cref="T"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Make {0} Array")]
    [CeresMetadata("style = ConstNode", "path = Dependency", "ResolverOnly")]
    public class FlowNode_MakeArrayT<T>: FlowNode_MakeArray, ISerializationCallbackReceiver, IPortArrayNode
    {
        [InputPort, CeresMetadata("DefaultLength = 1")]
        public CeresPort<T>[] items;
        
        [HideInGraphEditor]
        public int inputCount;
        
        [OutputPort]
        public CeresPort<T[]> array;

        protected sealed override UniTask Execute(ExecutionContext executionContext)
        {
            for (int i = 0; i < inputCount; i++)
            {
                array.Value[i] = items[i].Value;
            }
            return UniTask.CompletedTask;
        }

        public void OnBeforeSerialize()
        {
            
        }

        public void OnAfterDeserialize()
        {
            array.Value = new T[inputCount];
        }

        public int GetPortArrayLength()
        {
            return inputCount;
        }

        public string GetPortArrayFieldName()
        {
            return nameof(items);
        }

        public void SetPortArrayLength(int newLength)
        {
            inputCount = newLength;
            items = new CeresPort<T>[inputCount];
            for (int i = 0; i < newLength; i++)
            {
                items[i] = new CeresPort<T>();
            }
        }
    }
}