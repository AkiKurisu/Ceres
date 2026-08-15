using System;
using UnityEngine.Assertions;
namespace Ceres.Schedulers
{
    /// <summary>
    /// Handle give you access to track scheduled task
    /// </summary>
    public readonly struct SchedulerHandle : IDisposable
    {
        /// <summary>
        /// Handle id for scheduled task
        /// </summary>
        /// <value></value>
        public ulong Handle { get; }
        
        public const int IndexBits = 24;
        
        public const int SerialNumberBits = 40;
        
        public const int MaxIndex = 1 << IndexBits;
        
        public const ulong MaxSerialNumber = (ulong)1 << SerialNumberBits;
        
        public int GetIndex() => (int)(Handle & MaxIndex - 1);
        
        public ulong GetSerialNumber() => Handle >> IndexBits;
        
        /// <summary>
        /// Get scheduled task whether is valid
        /// </summary>
        /// <value></value>
        public bool IsValid()
        {
            if (!SchedulerRunner.IsInitialized) return default;
            return Handle != 0;
        }
        
        /// <summary>
        /// Get scheduled task whether is done
        /// </summary>
        /// <value></value>
        public bool IsDone()
        {
            return SchedulerRunner.IsInitialized ? SchedulerRunner.Get().IsDone(this) : default;
        }
        
        public SchedulerHandle(ulong handle)
        {
            Handle = handle;
        }
        
        public SchedulerHandle(ulong serialNum, int index)
        {
            Assert.IsTrue(index >= 0 && index < MaxIndex);
            Assert.IsTrue(serialNum < MaxSerialNumber);
#pragma warning disable CS0675
            Handle = (serialNum << IndexBits) | (ulong)index;
#pragma warning restore CS0675
        }
        
        /// <summary>
        /// Cancel a scheduled task if is valid
        /// </summary>
        /// <value></value>
        public void Cancel()
        {
            if (!SchedulerRunner.IsInitialized) return;
            if (!IsValid()) return;
            SchedulerRunner.Get().Cancel(this);
        }
        
        /// <summary>
        /// Pause a scheduled task if is valid
        /// </summary>
        /// <value></value>
        public void Pause()
        {
            if (!SchedulerRunner.IsInitialized) return;
            if (!IsValid()) return;
            SchedulerRunner.Get().Pause(this);
        }
        
        /// <summary>
        /// Resume a scheduled task if is valid
        /// </summary>
        /// <value></value>
        public void Resume()
        {
            if (!SchedulerRunner.IsInitialized) return;
            if (!IsValid()) return;
            SchedulerRunner.Get().Resume(this);
        }

        public void Dispose()
        {
            Cancel();
        }
        
        public static bool operator ==(SchedulerHandle left, SchedulerHandle right)
        {
            return left.Handle == right.Handle;
        }
        
        public static bool operator !=(SchedulerHandle left, SchedulerHandle right)
        {
            return left.Handle != right.Handle;
        }
        
        public override bool Equals(object obj)
        {
            if (obj is not SchedulerHandle handle) return false;
            return handle.Handle == Handle;
        }
        
        public override int GetHashCode()
        {
            return Handle.GetHashCode();
        }
    }
}