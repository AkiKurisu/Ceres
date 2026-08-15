using UnityEngine.Assertions;

namespace Ceres.Gameplay
{
    /// <summary>
    /// Struct to represent a Gameplay actor level entity
    /// </summary>
    public readonly struct ActorHandle
    {
        public ulong Handle { get; }

        private const int IndexBits = 24;

        private const int SerialNumberBits = 40;
        
        public const int MaxIndex = 1 << IndexBits;

        private const ulong MaxSerialNumber = (ulong)1 << SerialNumberBits;
        
        public int GetIndex() => (int)(Handle & MaxIndex - 1);
        
        public ulong GetSerialNumber() => Handle >> IndexBits;
        
        public bool IsValid()
        {
            return Handle != 0;
        }
        
        public ActorHandle(ulong serialNum, int index)
        {
            Assert.IsTrue(index >= 0 && index < MaxIndex);
            Assert.IsTrue(serialNum < MaxSerialNumber);
#pragma warning disable CS0675
            Handle = (serialNum << IndexBits) | (ulong)index;
#pragma warning restore CS0675
        }

        public static bool operator ==(ActorHandle left, ActorHandle right)
        {
            return left.Handle == right.Handle;
        }
        
        public static bool operator !=(ActorHandle left, ActorHandle right)
        {
            return left.Handle != right.Handle;
        }
        
        public override bool Equals(object obj)
        {
            if (obj is not ActorHandle handle) return false;
            return handle.Handle == Handle;
        }
        
        public override int GetHashCode()
        {
            return Handle.GetHashCode();
        }
    }

}