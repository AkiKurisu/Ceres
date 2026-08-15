using System;
using System.Collections.Generic;
using Ceres.Events;
using Ceres.Pool;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Pool;

namespace Ceres.Tasks
{
    public enum TaskStatus
    {
        /// <summary>
        /// Task is enabled to run and can be updated
        /// </summary>
        Running,
        
        /// <summary>
        /// Task is paused and will be ignored
        /// </summary>
        Paused,
        
        /// <summary>
        /// Task is completed and wait to broadcast complete event
        /// </summary>
        Completed,
        
        /// <summary>
        /// Task is stopped and will not broadcast complete event
        /// </summary>
        Stopped
    }
    
    public interface ITaskEvent { }
    
    public sealed class TaskCompleteEvent : EventBase<TaskCompleteEvent>, ITaskEvent
    {
        [JsonIgnore]
        public TaskBase Task { get; private set; }
        
        /// <summary>
        /// Soft reference to subtask, listener may be disposed before broadcast this event.
        /// So we check subtask's prerequisite whether contains this event to identify its lifetime version.
        /// </summary>
        /// <returns></returns>
        [JsonIgnore]
        public readonly List<TaskBase> Listeners = new();
        
        public static TaskCompleteEvent Create(TaskBase task)
        {
            var evt = GetPooled();
            evt.Task = task;
            evt.Listeners.Clear();
            return evt;
        }

        protected override void Init()
        {
            base.Init();
            Propagation = EventPropagation.Bubbles | EventPropagation.TricklesDown;
        }

        public void AddListenerTask(TaskBase taskBase)
        {
            Listeners.Add(taskBase);
        }
        
        public void RemoveListenerTask(TaskBase taskBase)
        {
            Listeners.Remove(taskBase);
        }
    }
    
    /// <summary>
    /// Base class for framework task
    /// </summary>
    public abstract class TaskBase : CallbackEventHandler, IDisposable
    {
        protected TaskStatus Status;

        private IEventCoordinator _coordinator;

        public override IEventCoordinator Coordinator => _coordinator;

        public override void SendEvent(EventBase e, DispatchMode dispatchMode = DispatchMode.Default)
        {
            e.Target = this;
            EventSystem.Instance.Dispatch(e, dispatchMode, MonoDispatchType.Update);
        }

        public virtual TaskStatus GetStatus()
        {
            return Status;
        }
        
        public abstract string GetTaskID();
        
        /// <summary>
        /// Debug usage
        /// </summary>
        /// <returns></returns>
        protected virtual string GetTaskName()
        {
#if UNITY_EDITOR
            return GetType().Name;
#else
            return string.Empty;
#endif
        }
        
        internal string InternalGetTaskName() => GetTaskName();
        
        #region Lifetime Cycle
        
        protected virtual void Init()
        {
            _completeEvent = TaskCompleteEvent.Create(this);
            _coordinator = EventSystem.Instance;
            Status = TaskStatus.Stopped;
        }
        
        public virtual void Stop()
        {
            Status = TaskStatus.Stopped;
        }
        
        public virtual void Start()
        {
            Status = TaskStatus.Running;
        }
        
        public virtual void Pause()
        {
            Status = TaskStatus.Paused;
        }

        public virtual void Tick()
        {

        }
        
        protected void CompleteTask()
        {
            Status = TaskStatus.Completed;
        }
        
        protected virtual void Reset()
        {
            Status = TaskStatus.Stopped;
        }
        
        public virtual void Acquire()
        {

        }
        
        public virtual void Dispose()
        {
            if (_prerequisites != null)
            {
                HashSetPool<TaskCompleteEvent>.Release(_prerequisites);
                _prerequisites = null;
            }
            if (_completeEvent != null)
            {
                _completeEvent.Dispose();
                _completeEvent = null;
            }

            Parent = null;
        }
        #endregion
        
        #region Prerequistes Management
        
        private HashSet<TaskCompleteEvent> _prerequisites;
        
        private TaskCompleteEvent _completeEvent;
        
        internal void PostComplete()
        {
            SendEvent(_completeEvent);
            HandleEventAtTargetPhase(_completeEvent);
            _completeEvent.Dispose();
            _completeEvent = null;
        }
        
        /// <summary>
        /// Release prerequisite if contains its reference
        /// </summary>
        /// <param name="evt"></param>
        /// <returns></returns>
        internal bool ReleasePrerequisite(TaskCompleteEvent evt)
        {
            return _prerequisites.Remove(evt);
        }
        
        internal bool HasPrerequisite()
        {
            return _prerequisites != null && _prerequisites.Count > 0;
        }
        
        /// <summary>
        /// Get task complete event
        /// </summary>
        /// <returns></returns>
        public TaskCompleteEvent GetCompleteEvent()
        {
            return _completeEvent;
        }
        
        /// <summary>
        /// Add a prerequisite task before this task run
        /// </summary>
        /// <param name="taskBase"></param>
        public void RegisterPrerequisite(TaskBase taskBase)
        {
            var evt = taskBase.GetCompleteEvent();
            evt.AddListenerTask(this);
            _prerequisites ??= HashSetPool<TaskCompleteEvent>.Get();
            _prerequisites.Add(evt);
        }
        
        /// <summary>
        /// Remove a prerequisite task if exist
        /// </summary>
        /// <param name="taskBase"></param>
        public bool UnregisterPrerequisite(TaskBase taskBase)
        {
            if (_prerequisites == null) return false;
            var evt = taskBase.GetCompleteEvent();
            // should not edit collections when is being dispatched
            if (!evt.Dispatch)
                evt.RemoveListenerTask(this);
            return _prerequisites.Remove(evt);
        }
        
        #endregion
    }
    
    public abstract class PooledTaskBase<T> : TaskBase where T : PooledTaskBase<T>, new()
    {
        private int _refCount;
        
        private bool _pooled;
        
        private static readonly _ObjectPool<T> Pool = new(() => new T());
        
        private static readonly string DefaultName;
        
        static PooledTaskBase()
        {
            DefaultName = typeof(T).Name;
        }

        public sealed override void Dispose()
        {
            if (--_refCount == 0)
            {
                base.Dispose();
                ReleasePooled((T)this);
            }
        }
        
        private static void ReleasePooled(T evt)
        {
            if (evt._pooled)
            {
                evt.Reset();
                Pool.Release(evt);
                evt._pooled = false;
            }
        }
        
        public static T GetPooled()
        {
            T t = Pool.Get();
            t.Init();
            t._pooled = true;
            return t;
        }
        
        protected override void Init()
        {
            base.Init();
            if (_refCount != 0)
            {
                Debug.LogWarning($"Task improperly released, reference count {_refCount}.");
                _refCount = 0;
            }
        }
        
        public override void Acquire()
        {
            _refCount++;
        }
        
        public override string GetTaskID()
        {
            return DefaultName;
        }
    }
}
