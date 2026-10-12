using System;
using MessagePack;
using UnityEngine;

namespace Ceres
{
    /// <summary>
    /// A replacement of <see cref="Nullable{T}"/> that can be edited in the inspector
    /// </summary>
    [Serializable, MessagePackObject(true)]
    public struct Optional<T>
    {
        [SerializeField] 
        private bool enabled;
        
        [SerializeField] 
        private T value;

        public readonly bool Enabled => enabled;
        
        public readonly T Value => value;

        [SerializationConstructor]
        public Optional(T value, bool enabled = true)
        {
            this.enabled = enabled;
            this.value = value;
        }
    }
}
