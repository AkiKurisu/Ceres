using UnityEngine;
using Ceres.Pool;

namespace Ceres.Schedulers
{
    /// <summary>
    /// Allows you to run events on a delay without the use of <see cref="Coroutine"/>s
    /// or <see cref="MonoBehaviour"/>s.
    ///
    /// To create and start a Timer, use the <see cref="Register"/> method.
    /// </summary>
    internal class Timer : IScheduled
    {
        private static readonly _ObjectPool<Timer> Pool = new(() => new Timer());
        
        #region Public Properties/Fields
        public SchedulerHandle Handle { get; private set; }
        
        /// <summary>
        /// How long the timer takes to complete from start to finish.
        /// </summary>
        public float Duration { get; private set; }

        /// <summary>
        /// Whether the timer will run again after completion.
        /// </summary>
        public bool IsLooped { get; set; }

        /// <summary>
        /// Whether the timer completed running. This is false if the timer was cancelled.
        /// </summary>
        public bool IsCompleted { get; private set; }

        /// <summary>
        /// Whether the timer uses real-time or game-time. Real time is unaffected by changes to the timescale
        /// of the game(e.g. pausing, slo-mo), while game time is affected.
        /// </summary>
        public bool UsesRealTime { get; private set; }

        /// <summary>
        /// Whether the timer is currently paused.
        /// </summary>
        public bool IsPaused => _timeElapsedBeforePause.HasValue;

        /// <summary>
        /// Whether the timer was cancelled.
        /// </summary>
        public bool IsCancelled => _timeElapsedBeforeCancel.HasValue;

        public bool IsDone => IsCompleted || IsCancelled;

        #endregion
        
        #region Public Static Methods
        
        /// <summary>
        /// Register a new timer that should fire an event after a certain amount of time
        /// has elapsed.
        ///
        /// Registered timers are destroyed when the scene changes.
        /// </summary>
        /// <param name="duration">The time to wait before the timer should fire, in seconds.</param>
        /// <param name="onComplete">An action to fire when the timer completes.</param>
        /// <param name="onUpdate">An action that should fire each time the timer is updated. Takes the amount
        /// of time passed in seconds since the start of the timer's current loop.</param>
        /// <param name="tickFrame">Whether the timer tick on which frame.</param>
        /// <param name="isLooped">Whether the timer should repeat after executing.</param>
        /// <param name="useRealTime">Whether the timer uses real-time(i.e. not affected by pauses,
        /// slow/fast motion) or game-time(will be affected by pauses and slow/fast-motion).</param>
        /// <returns>A timer object that allows you to examine stats and stop/resume progress.</returns>
        internal static Timer Register(float duration, SchedulerUnsafeBinding onComplete, SchedulerUnsafeBinding<float> onUpdate,
           TickFrame tickFrame = TickFrame.Update, bool isLooped = false, bool useRealTime = false)
        {
            Timer timer = Pool.Get();
            timer.Init(SchedulerRunner.Get().NewHandle(), duration, ref onComplete, ref onUpdate, isLooped, useRealTime);
            SchedulerRunner.Get().Register(timer, tickFrame, onComplete.IsValid() ? onComplete.GetDelegate() : onUpdate.GetDelegate());
            return timer;
        }
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Stop a timer that is in-progress or paused. The timer's on completion callback will not be called.
        /// </summary>
        public void Cancel()
        {
            if (IsDone) return;
            _timeElapsedBeforeCancel = GetTimeElapsed();
            _timeElapsedBeforePause = null;
        }
        
        public void Dispose()
        {
            SchedulerRunner.Get().Unregister(this, _onComplete.IsValid() ? _onComplete.GetDelegate() : _onUpdate.GetDelegate());
            _onUpdate = default;
            _onComplete = default;
            Pool.Release(this);
        }
        
        /// <summary>
        /// Pause a running timer. A paused timer can be resumed from the same point it was paused.
        /// </summary>
        public void Pause()
        {
            if (IsPaused || IsDone)
            {
                return;
            }

            _timeElapsedBeforePause = GetTimeElapsed();
        }

        /// <summary>
        /// Continue a paused timer. Does nothing if the timer has not been paused.
        /// </summary>
        public void Resume()
        {
            if (!IsPaused || IsDone)
            {
                return;
            }

            _timeElapsedBeforePause = null;
        }

        /// <summary>
        /// Get how many seconds have elapsed since the start of this timer's current cycle.
        /// </summary>
        /// <returns>The number of seconds that have elapsed since the start of this timer's current cycle, i.e.
        /// the current loop if the timer is looped, or the start if it isn't.
        ///
        /// If the timer has finished running, this is equal to the duration.
        ///
        /// If the timer was cancelled/paused, this is equal to the number of seconds that passed between the timer
        /// starting and when it was cancelled/paused.</returns>
        public float GetTimeElapsed()
        {
            if (IsCompleted || GetWorldTime() >= GetFireTime())
            {
                return Duration;
            }

            return _timeElapsedBeforeCancel ??
                   _timeElapsedBeforePause ??
                   GetWorldTime() - _startTime;
        }

        /// <summary>
        /// Get how many seconds remain before the timer completes.
        /// </summary>
        /// <returns>The number of seconds that remain to be elapsed until the timer is completed. A timer
        /// is only elapsing time if it is not paused, cancelled, or completed. This will be equal to zero
        /// if the timer completed.</returns>
        public float GetTimeRemaining()
        {
            return Duration - GetTimeElapsed();
        }

        /// <summary>
        /// Get how much progress the timer has made from start to finish as a ratio.
        /// </summary>
        /// <returns>A value from 0 to 1 indicating how much of the timer's duration has been elapsed.</returns>
        public float GetRatioComplete()
        {
            return GetTimeElapsed() / Duration;
        }

        /// <summary>
        /// Get how much progress the timer has left to make as a ratio.
        /// </summary>
        /// <returns>A value from 0 to 1 indicating how much of the timer's duration remains to be elapsed.</returns>
        public float GetRatioRemaining()
        {
            return GetTimeRemaining() / Duration;
        }

        #endregion
        
        #region Private Properties/Fields
        
        private SchedulerUnsafeBinding _onComplete;
        
        private SchedulerUnsafeBinding<float> _onUpdate;
        
        private float _startTime;
        
        private float _lastUpdateTime;

        // for pausing, we push the start time forward by the amount of time that has passed.
        // this will mess with the amount of time that elapsed when we're cancelled or paused if we just
        // check the start time versus the current world time, so we need to cache the time that was elapsed
        // before we paused/cancelled
        private float? _timeElapsedBeforeCancel;
        
        private float? _timeElapsedBeforePause;

        #endregion
        
        #region Private Constructor (use static Register method to create new timer)

        private void Init(SchedulerHandle handle, float duration, ref SchedulerUnsafeBinding onComplete, ref SchedulerUnsafeBinding<float> onUpdate,
            bool isLooped, bool usesRealTime)
        {
            Handle = handle;
            Duration = duration;
            _onComplete = onComplete;
            _onUpdate = onUpdate;

            IsLooped = isLooped;
            UsesRealTime = usesRealTime;

            _startTime = GetWorldTime();
            _lastUpdateTime = _startTime;

            IsCompleted = false;
            _timeElapsedBeforeCancel = null;
            _timeElapsedBeforePause = null;
        }

        #endregion
        
        #region Private Methods
        
        private float GetWorldTime()
        {
            return UsesRealTime ? Time.realtimeSinceStartup : Time.time;
        }

        private float GetFireTime()
        {
            return _startTime + Duration;
        }

        private float GetTimeDelta()
        {
            return GetWorldTime() - _lastUpdateTime;
        }

        public void Update()
        {
            if (IsDone)
            {
                return;
            }

            if (IsPaused)
            {
                _startTime += GetTimeDelta();
                _lastUpdateTime = GetWorldTime();
                return;
            }

            _lastUpdateTime = GetWorldTime();

            _onUpdate.Invoke(GetTimeElapsed());

            if (GetWorldTime() >= GetFireTime())
            {
                _onComplete.Invoke();

                if (IsLooped)
                {
                    _startTime = GetWorldTime();
                }
                else
                {
                    IsCompleted = true;
                }
            }
        }

        #endregion
    }
}