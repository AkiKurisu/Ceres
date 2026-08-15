using System;
using System.Collections.Generic;
using Ceres.Collections;
using Ceres.Pool;
using Unity.Profiling;
using UnityEngine;

namespace Ceres.Schedulers
{
    /// <summary>
    /// Manages updating all the <see cref="IScheduled"/> tasks that are running in the scene.
    /// This will be instantiated the first time you create a task.
    /// You do not need to add it into the scene manually. Similar to TimerManager in Unreal.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    internal class SchedulerRunner : MonoBehaviour
    {
        /// <summary>
        /// Class for easier dispose control
        /// </summary>
        internal class ScheduledItem : IDisposable
        {
            private static readonly _ObjectPool<ScheduledItem> Pool = new(() => new ScheduledItem());
            
#if UNITY_EDITOR
            public double Timestamp { get; private set; }
#endif
            public IScheduled Value { get; private set; }
            
            private bool _delay;
            public TickFrame TickFrame { get; private set; }
            
            private static readonly ProfilerMarker ProfilerMarker = new("SchedulerRunner.UpdateAll.UpdateStep.UpdateItem");
            
            public static ScheduledItem GetPooled(IScheduled scheduled, TickFrame tickFrame, bool delay)
            {
                var item = Pool.Get();
                item.Value = scheduled;
#if UNITY_EDITOR
                item.Timestamp = Time.timeSinceLevelLoadAsDouble;
#endif
                item._delay = delay;
                item.TickFrame = tickFrame;
                return item;
            }
            
            /// <summary>
            /// Whether internal scheduled task is done
            /// </summary>
            /// <returns></returns>
            public bool IsDone() => Value.IsDone;
            
            public void Update(TickFrame tickFrame)
            {
                using (ProfilerMarker.Auto())
                {
                    if (Value.IsDone) return;
                    if (TickFrame != tickFrame) return;
                    if (_delay)
                    {
                        _delay = false;
                        return;
                    }
                    Value.Update();
                }
            }
            
            /// <summary>
            /// Cancel internal scheduled task
            /// </summary>
            public void Cancel()
            {
                if (!Value.IsDone) Value.Cancel();
            }
            
            /// <summary>
            /// Dispose self and internal scheduled task
            /// </summary>
            public void Dispose()
            {
                Value?.Dispose();
                Value = default;
#if UNITY_EDITOR
                Timestamp = default;
#endif
                _delay = default;
                Pool.Release(this);
            }
            
            public void Pause()
            {
                Value.Pause();
            }
            
            public void Resume()
            {
                Value.Resume();
            }
        }
        
        private const int InitialCapacity = 100;
        
        internal readonly SparseArray<ScheduledItem> ScheduledItems = new(InitialCapacity, SchedulerHandle.MaxIndex + 1);
       
        private ulong _serialNum = 1;
        
        // buffer adding tasks, so we don't edit a collection during iteration
        private readonly List<SchedulerHandle> _pendingHandles = new(InitialCapacity);
        
        private readonly List<SchedulerHandle> _activeHandles = new(InitialCapacity);
        
        private bool _isDestroyed;
        
        private bool _isGateOpen;
        
        private int _lastFrame;
        
        public static bool IsInitialized => (bool)_instance;
        
        private static SchedulerRunner _instance;
        
        private static readonly ProfilerMarker UpdateStepProfilerMarker = new("SchedulerRunner.UpdateAll.UpdateStep");
        
        public static SchedulerRunner Get()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Debug.LogError("[Scheduler] Scheduler can not be used in Editor Mode.");
                return null;
            }
#endif
            if (!_instance)
            {
                _instance = new GameObject(nameof(SchedulerRunner))
                {
                    hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable
                }.AddComponent<SchedulerRunner>();
            }
            return _instance;
        }
        
        private void Update()
        {
            UpdateAll(TickFrame.Update);
        }
        
        private void FixedUpdate()
        {
            UpdateAll(TickFrame.FixedUpdate);
        }

        private void LateUpdate()
        {
            UpdateAll(TickFrame.LateUpdate);
            _lastFrame = Time.frameCount;
        }

        private void OnDestroy()
        {
            _isDestroyed = true;
            foreach (var scheduledItem in ScheduledItems)
            {
                scheduledItem.Cancel();
                scheduledItem.Dispose();
            }
            SchedulerRegistry.CleanListeners();
            ScheduledItems.Clear();
            _pendingHandles.Clear();
        }

        /// <summary>
        /// Register scheduled task to managed
        /// </summary>
        /// <param name="scheduled"></param>
        /// <param name="tickFrame"></param>
        /// <param name="delegate"></param>
        public void Register(IScheduled scheduled, TickFrame tickFrame, Delegate @delegate)
        {
            if (_isDestroyed)
            {
                Debug.LogWarning("[Scheduler] Can not schedule task when scene is destroying.");
                scheduled.Dispose();
                return;
            }
            // schedule one frame if register before runner update
            bool needDelayFrame = _lastFrame < Time.frameCount;
            int index = scheduled.Handle.GetIndex();
            var item = ScheduledItem.GetPooled(scheduled, tickFrame, needDelayFrame);
            // Assign item
            ScheduledItems[index] = item;
            _pendingHandles.Add(scheduled.Handle);
#if UNITY_EDITOR
            if (SchedulerConfig.EnableStackTrace)
            {
                SchedulerRegistry.RegisterListener(scheduled, @delegate);
            }
#endif
        }
        
        public SchedulerHandle NewHandle()
        {
            // Allocate placement, not really add
            return new SchedulerHandle(_serialNum, ScheduledItems.AddUninitialized());
        }

        /// <summary>
        ///  Unregister scheduled task from managed
        /// </summary>
        /// <param name="scheduled"></param>
        /// <param name="delegate"></param>
        public void Unregister(IScheduled scheduled, Delegate @delegate)
        {
            ScheduledItems.RemoveAt(scheduled.Handle.GetIndex());
#if UNITY_EDITOR
            if (SchedulerConfig.EnableStackTrace)
            {
                SchedulerRegistry.UnregisterListener(scheduled, @delegate);
            }
#endif
        }
        
        /// <summary>
        /// Cancel all scheduled task
        /// </summary>
        public void CancelAll()
        {
            foreach (var handle in _activeHandles)
            {
                var item = FindItem(handle);
                item.Cancel();
                if (_isGateOpen)
                {
                    item.Dispose();
                }
            }
            if (_isGateOpen)
            {
                _activeHandles.Clear();
            }
            _pendingHandles.Clear();
        }
        
        /// <summary>
        /// Pause all scheduled task
        /// </summary>
        public void PauseAll()
        {
            foreach (var handle in _pendingHandles)
            {
                var item = FindItem(handle);
                item.Pause();
            }
            foreach (var handle in _activeHandles)
            {
                var item = FindItem(handle);
                item.Pause();
            }
        }
        
        /// <summary>
        /// Resume all scheduled task
        /// </summary>
        public void ResumeAll()
        {
            foreach (var handle in _pendingHandles)
            {
                var item = FindItem(handle);
                item.Resume();
            }
            foreach (var handle in _activeHandles)
            {
                var item = FindItem(handle);
                item.Resume();
            }
        }
        
        private void UpdateAll(TickFrame tickFrame)
        {
            _isGateOpen = false;
            // Add
            if (_pendingHandles.Count > 0)
            {
                _activeHandles.AddRange(_pendingHandles);
                _pendingHandles.Clear();
                // increase serial
                _serialNum++;
            }

            // Update
            using (UpdateStepProfilerMarker.Auto())
            {
                for (int i = _activeHandles.Count - 1; i >= 0; --i)
                {
                    var item = FindItem(_activeHandles[i]);
                    item.Update(tickFrame);
                    if (item.IsDone())
                    {
                        _activeHandles.RemoveAt(i);
                        item.Dispose();
                    }
                }
            }

            // Shrink
            if (tickFrame == TickFrame.LateUpdate)
            {
                // check match shrink threshold that capacity is more than initial capacity
                bool canShrink = ScheduledItems.InternalCapacity > 2 * InitialCapacity;
                // shrink list when non-allocated elements are far more than allocated ones
                bool overlapAllocated = ScheduledItems.NumFreeIndices > 4 * ScheduledItems.Count;
                if (canShrink && overlapAllocated)
                {
                    ScheduledItems.Shrink();
                }
            }
            _isGateOpen = true;
        }
        
        private ScheduledItem FindItem(SchedulerHandle handle)
        {
            int handleIndex = handle.GetIndex();
            ulong handleSerial = handle.GetSerialNumber();
            var scheduledItem = ScheduledItems[handleIndex];
            if (scheduledItem == null || scheduledItem.Value.Handle.GetSerialNumber() != handleSerial) return null;
            return scheduledItem;
        }
        
        /// <summary>
        /// Whether internal scheduled task is done
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public bool IsDone(SchedulerHandle handle)
        {
            var item = FindItem(handle);
            return item == null || item.IsDone();
        }
        
        /// <summary>
        /// Cancel target scheduled task
        /// </summary>
        /// <param name="handle"></param>
        public void Cancel(SchedulerHandle handle)
        {
            var item = FindItem(handle);
            if (item == null) return;
            item.Cancel();
            // ensure pending buffer also remove task
            if (_pendingHandles.Remove(handle))
            {
                item.Dispose();
            }
            else if (_isGateOpen)
            {
                _activeHandles.Remove(handle);
                item.Dispose();
            }
        }
        
        /// <summary>
        /// Pause target scheduled task
        /// </summary>
        /// <param name="handle"></param>
        public void Pause(SchedulerHandle handle)
        {
            var item = FindItem(handle);
            item?.Pause();
        }
        
        /// <summary>
        /// Resume target scheduled task
        /// </summary>
        /// <param name="handle"></param>
        public void Resume(SchedulerHandle handle)
        {
            var item = FindItem(handle);
            item?.Resume();
        }
    }
}