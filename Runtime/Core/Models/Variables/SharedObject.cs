using System;
using Chris.Serialization;
using UnityEngine;
namespace Ceres
{
    /// <summary>
    /// Shared variable for any object
    /// </summary>
    [Serializable]
    public class SharedObject : SharedVariable<object>, ISerializationCallbackReceiver
    {
        public SerializedObjectBase serializedObject = new();
        
        public SharedObject(object value)
        {
            this.value = value;
        }
        
        public SharedObject()
        {

        }
        
        protected override SharedVariable<object> CloneT()
        {
            return new SharedObject { Value = value, serializedObject = serializedObject.Clone() };
        }

        public void OnBeforeSerialize()
        {
            
        }

        public void OnAfterDeserialize()
        {
            try
            {
                value = serializedObject.CreateInstance();
            }
            catch(NullReferenceException)
            {
                value = null;
            }
        }
        
        public override Type GetValueType()
        {
            return serializedObject.GetObjectType() ?? base.GetValueType();
        }
    }
}
