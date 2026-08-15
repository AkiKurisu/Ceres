using System;
namespace Ceres.Schedulers
{
    /// <summary>
    /// Scheduler tick frame type
    /// </summary>
    public enum TickFrame
    {
        Update,
        FixedUpdate,
        LateUpdate,
    }
    
    /// <summary>
    /// Unsafe binding to provide zero-allocation delegate
    /// </summary>
    public readonly unsafe struct SchedulerUnsafeBinding
    {
        private const byte Ptr = 0;
        
        private const byte Delegate = 1;
        
        private readonly byte _assigned;
        
        private readonly byte _mType;
        
        private readonly object _mObj;
        
        private readonly void* _mPtr;
        
        private readonly Action _mDelegate;
        
        internal Action GetDelegate() => _mDelegate;
        
        public SchedulerUnsafeBinding(object instance, delegate* managed<object, void> mPtr)
        {
            _mType = Ptr;
            _mObj = instance;
            _mPtr = mPtr;
            _mDelegate = null;
            _assigned = 1;
        }
        
        public SchedulerUnsafeBinding(delegate* managed<void> mPtr)
        {
            _mType = Ptr;
            _mObj = null;
            _mPtr = mPtr;
            _mDelegate = null;
            _assigned = 1;
        }
        
        public SchedulerUnsafeBinding(Action @delegate)
        {
            _mType = Delegate;
            _mObj = null;
            _mPtr = default;
            _mDelegate = @delegate;
            _assigned = 1;
        }

        public void Invoke()
        {
            if (!IsValid()) return;
            if (_mType == Delegate)
            {
                _mDelegate?.Invoke();
                return;
            }
            if (_mObj != null)
                ((delegate* managed<object, void>)_mPtr)(_mObj);
            else
                ((delegate* managed<void>)_mPtr)();
        }
        
        public bool IsValid()
        {
            if (_assigned == 0) return false;
            if (_mType == Delegate) return _mDelegate != null;
            return _mPtr != null;
        }
        
        public static implicit operator SchedulerUnsafeBinding(Action action)
        {
            return new SchedulerUnsafeBinding(action);
        }
        
        public static implicit operator SchedulerUnsafeBinding(delegate* managed<void> ptr)
        {
            return new SchedulerUnsafeBinding(ptr);
        }
    }
    
    /// <summary>
    /// Unsafe binding to provide zero-allocation delegate
    /// </summary>
    public readonly unsafe struct SchedulerUnsafeBinding<T>
    {
        private const byte Ptr = 0;
        
        private const byte Delegate = 1;
        
        private readonly byte _assigned;
        
        private readonly byte _mType;
        
        private readonly object _mObj;
        
        private readonly void* _mPtr;
        
        private readonly Action<T> _mDelegate;
        
        internal Action<T> GetDelegate() => _mDelegate;
        
        public SchedulerUnsafeBinding(object instance, delegate* managed<object, T, void> mPtr)
        {
            _mType = Ptr;
            _mObj = instance;
            _mPtr = mPtr;
            _mDelegate = null;
            _assigned = 1;
        }
        
        public SchedulerUnsafeBinding(delegate* managed<T, void> mPtr)
        {
            _mType = Ptr;
            _mObj = null;
            _mPtr = mPtr;
            _mDelegate = null;
            _assigned = 1;
        }
        
        public SchedulerUnsafeBinding(Action<T> @delegate)
        {
            _mType = Delegate;
            _mObj = null;
            _mPtr = default;
            _mDelegate = @delegate;
            _assigned = 1;
        }

        public void Invoke(T arg)
        {
            if (!IsValid()) return;
            if (_mType == Delegate)
            {
                _mDelegate?.Invoke(arg);
                return;
            }
            if (_mObj != null)
                ((delegate* managed<object, T, void>)_mPtr)(_mObj, arg);
            else
                ((delegate* managed<T, void>)_mPtr)(arg);
        }
        
        public bool IsValid()
        {
            if (_assigned == 0) return false;
            if (_mType == Delegate) return _mDelegate != null;
            return _mPtr != null;
        }
        
        public static implicit operator SchedulerUnsafeBinding<T>(Action<T> action)
        {
            return new SchedulerUnsafeBinding<T>(action);
        }
        
        public static implicit operator SchedulerUnsafeBinding<T>(delegate* managed<T, void> ptr)
        {
            return new SchedulerUnsafeBinding<T>(ptr);
        }
    }
    
    /// <summary>
    /// Main api for schedulers
    /// </summary>
    public static class Scheduler
    {
        /// <summary>
        /// Delay some time and invoke callBack
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="onComplete"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <param name="ignoreTimeScale"></param>
        /// <returns></returns>
        [StackTraceFrame]
        public static SchedulerHandle DelayUnsafe(float delay, SchedulerUnsafeBinding onComplete, TickFrame tickFrame = TickFrame.Update, bool isLooped = false, bool ignoreTimeScale = false)
        {
            return Timer.Register(delay, onComplete, default, tickFrame, isLooped, ignoreTimeScale).Handle;
        }

        /// <summary>
        /// Delay some time and invoke callBack
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="onUpdate"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <param name="ignoreTimeScale"></param>
        /// <returns></returns>
        [StackTraceFrame]
        public static SchedulerHandle DelayUnsafe(float delay, SchedulerUnsafeBinding<float> onUpdate, TickFrame tickFrame = TickFrame.Update, bool isLooped = false, bool ignoreTimeScale = false)
        {
            return Timer.Register(delay, default, onUpdate, tickFrame, isLooped, ignoreTimeScale).Handle;
        }

        /// <summary>
        /// Delay some time and invoke callBack
        /// </summary>
        /// <param name="onComplete"></param>
        /// <param name="onUpdate"></param>
        /// <param name="delay"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <param name="ignoreTimeScale"></param>
        /// <returns></returns>
        [StackTraceFrame]
        public static SchedulerHandle DelayUnsafe(float delay, SchedulerUnsafeBinding onComplete, SchedulerUnsafeBinding<float> onUpdate, TickFrame tickFrame = TickFrame.Update, bool isLooped = false, bool ignoreTimeScale = false)
        {
            return Timer.Register(delay, onComplete, onUpdate, tickFrame, isLooped, ignoreTimeScale).Handle;
        }

        /// <summary>
        /// Delay some time and invoke callBack
        /// </summary>
        /// <param name="delaySeconds"></param>
        /// <param name="onComplete"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <param name="ignoreTimeScale"></param>
        /// <returns></returns>
        [StackTraceFrame]
        public static SchedulerHandle Delay(float delaySeconds, Action onComplete, TickFrame tickFrame = TickFrame.Update, bool isLooped = false, bool ignoreTimeScale = false)
        {
            return Timer.Register(delaySeconds, onComplete, default, tickFrame, isLooped, ignoreTimeScale).Handle;
        }

        /// <summary>
        /// Delay some time and invoke callBack
        /// </summary>
        /// <param name="delaySeconds"></param>
        /// <param name="onUpdate"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <param name="ignoreTimeScale"></param>
        /// <returns></returns>
        [StackTraceFrame]
        public static SchedulerHandle Delay(float delaySeconds, Action<float> onUpdate, TickFrame tickFrame = TickFrame.Update, bool isLooped = false, bool ignoreTimeScale = false)
        {
            return Timer.Register(delaySeconds, default, onUpdate, tickFrame, isLooped, ignoreTimeScale).Handle;
        }

        /// <summary>
        /// Delay some time and invoke callBack
        /// </summary>
        /// <param name="onComplete"></param>
        /// <param name="onUpdate"></param>
        /// <param name="delaySeconds"></param>
        /// <param name="isLooped"></param>
        /// <param name="ignoreTimeScale"></param>
        /// <param name="tickFrame"></param>
        /// <returns></returns>
        [StackTraceFrame]
        public static SchedulerHandle Delay(float delaySeconds, Action onComplete, Action<float> onUpdate,
        TickFrame tickFrame = TickFrame.Update, bool isLooped = false, bool ignoreTimeScale = false)
        {
            return Timer.Register(delaySeconds, onComplete, onUpdate, tickFrame, isLooped, ignoreTimeScale).Handle;
        }

        /// <summary>
        /// Wait some frames and invoke callBack
        /// </summary>
        /// <param name="onComplete"></param>
        /// <param name="frame"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <returns></returns>
        [StackTraceFrame]
        public static SchedulerHandle WaitFrameUnsafe(int frame, SchedulerUnsafeBinding onComplete,
         TickFrame tickFrame = TickFrame.Update, bool isLooped = false)
        {
            return FrameCounter.Register(frame, onComplete, default, tickFrame, isLooped).Handle;
        }

        /// <summary>
        /// Wait some frames and invoke callBack
        /// </summary>
        /// <param name="onUpdate"></param>
        /// <param name="frame"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <returns></returns>
        [StackTraceFrame]
        public static SchedulerHandle WaitFrameUnsafe(int frame, SchedulerUnsafeBinding<int> onUpdate,
        TickFrame tickFrame = TickFrame.Update, bool isLooped = false)
        {
            return FrameCounter.Register(frame, default, onUpdate, tickFrame, isLooped).Handle;
        }

        /// <summary>
        /// Wait some frames and invoke callBack
        /// </summary>
        /// <param name="onComplete"></param>
        /// <param name="onUpdate"></param>
        /// <param name="frame"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <returns></returns>
        [StackTraceFrame]
        public static SchedulerHandle WaitFrameUnsafe(int frame, SchedulerUnsafeBinding onComplete, SchedulerUnsafeBinding<int> onUpdate,
         TickFrame tickFrame = TickFrame.Update, bool isLooped = false)
        {
            return FrameCounter.Register(frame, onComplete, onUpdate, tickFrame, isLooped).Handle;
        }

        /// <summary>
        /// Wait some frames and invoke callBack
        /// </summary>
        /// <param name="onComplete"></param>
        /// <param name="frame"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <returns></returns>
        [StackTraceFrame]
        public static SchedulerHandle WaitFrame(int frame, Action onComplete, TickFrame tickFrame = TickFrame.Update, bool isLooped = false)
        {
            return FrameCounter.Register(frame, onComplete, default, tickFrame, isLooped).Handle;
        }

        /// <summary>
        /// Wait some frames and invoke callBack
        /// </summary>
        /// <param name="onUpdate"></param>
        /// <param name="frame"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <returns></returns>
        [StackTraceFrame]
        public static SchedulerHandle WaitFrame(int frame, Action<int> onUpdate, TickFrame tickFrame = TickFrame.Update, bool isLooped = false)
        {
            return FrameCounter.Register(frame, default, onUpdate, tickFrame, isLooped).Handle;
        }

        /// <summary>
        /// Wait some frames and invoke callBack
        /// </summary>
        /// <param name="onComplete"></param>
        /// <param name="onUpdate"></param>
        /// <param name="frame"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <returns></returns>
        [StackTraceFrame]
        public static SchedulerHandle WaitFrame(int frame, Action onComplete, Action<int> onUpdate,
         TickFrame tickFrame = TickFrame.Update, bool isLooped = false)
        {
            return FrameCounter.Register(frame, onComplete, onUpdate, tickFrame, isLooped).Handle;
        }
        #region Unreal Style

        /// <summary>
        /// Delay some time and invoke callBack
        /// </summary>
        /// <param name="handle">Handle to overwrite</param>
        /// <param name="delaySeconds"></param>
        /// <param name="onComplete"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <param name="ignoreTimeScale"></param>
        [StackTraceFrame]
        public static void DelayUnsafe(ref SchedulerHandle handle, float delaySeconds, SchedulerUnsafeBinding onComplete,
         TickFrame tickFrame = TickFrame.Update, bool isLooped = false, bool ignoreTimeScale = false)
        {
            Timer.Register(delaySeconds, onComplete, default, tickFrame, isLooped, ignoreTimeScale).Assign(ref handle);
        }

        /// <summary>
        /// Delay some time and invoke callBack
        /// </summary>
        /// <param name="handle">Handle to overwrite</param>
        /// <param name="delaySeconds"></param>
        /// <param name="onUpdate"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <param name="ignoreTimeScale"></param>
        /// <returns></returns>
        [StackTraceFrame]
        public static void DelayUnsafe(ref SchedulerHandle handle, float delaySeconds, SchedulerUnsafeBinding<float> onUpdate,
        TickFrame tickFrame = TickFrame.Update, bool isLooped = false, bool ignoreTimeScale = false)
        {
            Timer.Register(delaySeconds, default, onUpdate, tickFrame, isLooped, ignoreTimeScale).Assign(ref handle);
        }

        /// <summary>
        /// Delay some time and invoke callBack
        /// </summary>
        /// <param name="handle">Handle to overwrite</param>
        /// <param name="delaySeconds"></param>
        /// <param name="onComplete"></param>
        /// <param name="onUpdate"></param>
        /// <param name="isLooped"></param>
        /// <param name="ignoreTimeScale"></param>
        /// <param name="tickFrame"></param>
        [StackTraceFrame]
        public static void DelayUnsafe(ref SchedulerHandle handle, float delaySeconds, SchedulerUnsafeBinding onComplete,
         SchedulerUnsafeBinding<float> onUpdate, TickFrame tickFrame = TickFrame.Update, bool isLooped = false, bool ignoreTimeScale = false)
        {
            Timer.Register(delaySeconds, onComplete, onUpdate, tickFrame, isLooped, ignoreTimeScale).Assign(ref handle);
        }

        /// <summary>
        /// Delay some time and invoke callBack
        /// </summary>
        /// <param name="handle">Handle to overwrite</param>
        /// <param name="delaySeconds"></param>
        /// <param name="onComplete"></param>
        /// <param name="isLooped"></param>
        /// <param name="tickFrame"></param>
        /// <param name="ignoreTimeScale"></param>
        [StackTraceFrame]
        public static void Delay(ref SchedulerHandle handle, float delaySeconds, Action onComplete, bool isLooped = false,
         TickFrame tickFrame = TickFrame.Update, bool ignoreTimeScale = false)
        {
            Timer.Register(delaySeconds, onComplete, default, tickFrame, isLooped, ignoreTimeScale).Assign(ref handle);
        }

        /// <summary>
        /// Delay some time and invoke callBack
        /// </summary>
        /// <param name="handle">Handle to overwrite</param>
        /// <param name="delaySeconds"></param>
        /// <param name="onUpdate"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <param name="ignoreTimeScale"></param>
        /// <returns></returns>
        [StackTraceFrame]
        public static void Delay(ref SchedulerHandle handle, float delaySeconds, Action<float> onUpdate,
        TickFrame tickFrame = TickFrame.Update, bool isLooped = false, bool ignoreTimeScale = false)
        {
            Timer.Register(delaySeconds, default, onUpdate, tickFrame, isLooped, ignoreTimeScale).Assign(ref handle);
        }

        /// <summary>
        /// Delay some time and invoke callBack
        /// </summary>
        /// <param name="handle">Handle to overwrite</param>
        /// <param name="delaySeconds"></param>
        /// <param name="onComplete"></param>
        /// <param name="onUpdate"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <param name="ignoreTimeScale"></param>
        [StackTraceFrame]
        public static void Delay(ref SchedulerHandle handle, float delaySeconds, Action onComplete, Action<float> onUpdate,
        TickFrame tickFrame = TickFrame.Update, bool isLooped = false, bool ignoreTimeScale = false)
        {
            Timer.Register(delaySeconds, onComplete, onUpdate, tickFrame, isLooped, ignoreTimeScale).Assign(ref handle);
        }

        /// <summary>
        /// Wait some frames and invoke callBack
        /// </summary>
        /// <param name="handle">Handle to overwrite</param>
        /// <param name="frame"></param>
        /// <param name="onComplete"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        [StackTraceFrame]
        public static void WaitFrameUnsafe(ref SchedulerHandle handle, int frame, SchedulerUnsafeBinding onComplete,
         TickFrame tickFrame = TickFrame.Update, bool isLooped = false)
        {
            FrameCounter.Register(frame, onComplete, default, tickFrame, isLooped).Assign(ref handle);
        }

        /// <summary>
        /// Wait some frames and invoke callBack
        /// </summary>
        /// <param name="handle">Handle to overwrite</param>
        /// <param name="frame"></param>
        /// <param name="onUpdate"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        [StackTraceFrame]
        public static void WaitFrameUnsafe(ref SchedulerHandle handle, int frame, SchedulerUnsafeBinding<int> onUpdate,
          TickFrame tickFrame = TickFrame.Update, bool isLooped = false)
        {
            FrameCounter.Register(frame, default, onUpdate, tickFrame, isLooped).Assign(ref handle);
        }

        /// <summary>
        /// Wait some frames and invoke callBack
        /// </summary>
        /// <param name="handle">Handle to overwrite</param>
        /// <param name="frame"></param>
        /// <param name="onComplete"></param>
        /// <param name="onUpdate"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        [StackTraceFrame]
        public static void WaitFrameUnsafe(ref SchedulerHandle handle, int frame, SchedulerUnsafeBinding onComplete,
         SchedulerUnsafeBinding<int> onUpdate, TickFrame tickFrame = TickFrame.Update, bool isLooped = false)
        {
            FrameCounter.Register(frame, onComplete, onUpdate, tickFrame, isLooped).Assign(ref handle);
        }

        /// <summary>
        /// Wait some frames and invoke callBack
        /// </summary>
        /// <param name="handle">Handle to overwrite</param>
        /// <param name="frame"></param>
        /// <param name="onComplete"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        [StackTraceFrame]
        public static void WaitFrame(ref SchedulerHandle handle, int frame, Action onComplete,
         TickFrame tickFrame = TickFrame.Update, bool isLooped = false)
        {
            FrameCounter.Register(frame, onComplete, default, tickFrame, isLooped).Assign(ref handle);
        }

        /// <summary>
        /// Wait some frames and invoke callBack
        /// </summary>
        /// <param name="handle">Handle to overwrite</param>
        /// <param name="frame"></param>
        /// <param name="onUpdate"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        [StackTraceFrame]
        public static void WaitFrame(ref SchedulerHandle handle, int frame, Action<int> onUpdate,
        TickFrame tickFrame = TickFrame.Update, bool isLooped = false)
        {
            FrameCounter.Register(frame, default, onUpdate, tickFrame, isLooped).Assign(ref handle);
        }

        /// <summary>
        /// Wait some frames and invoke callBack
        /// </summary>
        /// <param name="handle">Handle to overwrite</param>
        /// <param name="frame"></param>
        /// <param name="onComplete"></param>
        /// <param name="onUpdate"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        [StackTraceFrame]
        public static void WaitFrame(ref SchedulerHandle handle, int frame, Action onComplete, Action<int> onUpdate,
        TickFrame tickFrame = TickFrame.Update, bool isLooped = false)
        {
            FrameCounter.Register(frame, onComplete, onUpdate, tickFrame, isLooped).Assign(ref handle);
        }
        #endregion
    }
}
