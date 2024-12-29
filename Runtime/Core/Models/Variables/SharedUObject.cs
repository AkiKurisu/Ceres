using System;
using Chris.Serialization;
using UObject = UnityEngine.Object;
namespace Ceres
{
    [Serializable]
    public class SharedUObject : SharedVariable<UObject>
    {
        /// <summary>
        /// Constraint UObject type
        /// </summary>
        public SerializedType<UObject> serializedType = SerializedType<UObject>.Default;
        
        public SharedUObject(UObject value)
        {
            this.value = value;
        }
        
        public SharedUObject()
        {

        }
        
        protected override SharedVariable<UObject> CloneT()
        {
            return new SharedUObject { Value = value, serializedType = serializedType };
        }

        public override Type GetValueType()
        {
            return serializedType.GetObjectType() ?? typeof(UObject);
        }
    }
    
    [Serializable]
    public class SharedUObject<TObject> : SharedVariable<TObject>, IBindableVariable<UObject> where TObject : UObject
    {
        // Special case of binding SharedTObject<T> to SharedObject
        UObject IBindableVariable<UObject>.Value
        {
            get => Value;

            set => Value = (TObject)value;
        }

        public SharedUObject(TObject value)
        {
            this.value = value;
        }
        
        public SharedUObject()
        {

        }
        
        protected override SharedVariable<TObject> CloneT()
        {
            return new SharedUObject<TObject> { Value = value };
        }
        
        public override void Bind(SharedVariable other)
        {
            //Special case of binding SharedObject to SharedTObject<T>
            if (other is IBindableVariable<UObject> sharedObject)
            {
                Bind(sharedObject);
            }
            else
            {
                base.Bind(other);
            }
        }
        
        public void Bind(IBindableVariable<UObject> other)
        {
            Getter = () => (TObject)other.Value;
            Setter = newValue => other.Value = newValue;
        }
    }
}