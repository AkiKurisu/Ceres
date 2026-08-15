using UnityEngine.Assertions;

namespace Ceres.Serialization
{
    /// <summary>
    /// Soft Object Ptr
    /// </summary>
    public readonly struct SoftObjectHandle
    {
        public ulong Handle { get; }
        
        public const int IndexBits = 24;
        
        public const int SerialNumberBits = 40;
        
        public const int MaxIndex = 1 << IndexBits;
        
        public const ulong MaxSerialNumber = (ulong)1 << SerialNumberBits;
        
        public int GetIndex() => (int)(Handle & MaxIndex - 1);
        
        public ulong GetSerialNumber() => Handle >> IndexBits;
        
        public bool IsValid()
        {
            return Handle != 0;
        }
        
        internal SoftObjectHandle(ulong handle)
        {
            Handle = handle;
        }
        
        internal SoftObjectHandle(ulong serialNum, int index)
        {
            Assert.IsTrue(index >= 0 && index < MaxIndex);
            Assert.IsTrue(serialNum < MaxSerialNumber);
#pragma warning disable CS0675
            Handle = (serialNum << IndexBits) | (ulong)index;
#pragma warning restore CS0675
        }

        /// <summary>
        /// Create a new soft object handle from object
        /// </summary>
        /// <param name="object"></param>
        public SoftObjectHandle(object @object)
        {
            SoftObjectHandle handle = default;
            GlobalObjectManager.RegisterObject(@object, ref handle);
            Handle = handle.Handle;
        }

        public static bool operator ==(SoftObjectHandle left, SoftObjectHandle right)
        {
            return left.Handle == right.Handle;
        }
        
        public static bool operator !=(SoftObjectHandle left, SoftObjectHandle right)
        {
            return left.Handle != right.Handle;
        }
        
        public override bool Equals(object obj)
        {
            if (obj is not SoftObjectHandle handle) return false;
            return handle.Handle == Handle;
        }
        
        public override int GetHashCode()
        {
            return Handle.GetHashCode();
        }
        
        /// <summary>
        /// Get object if has been loaded
        /// </summary>
        /// <returns></returns>
        public object GetObject()
        {
            return GlobalObjectManager.GetObject(this);
        }
    }
}