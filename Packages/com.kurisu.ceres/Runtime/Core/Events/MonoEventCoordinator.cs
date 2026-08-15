using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ceres.Events
{
    public enum MonoDispatchType
    {
        Update,
        FixedUpdate = 1,
        LateUpdate = 2,
    }

    /// <summary>
    /// MonoBehaviour based EventCoordinator that can be enabled and disabled, and can be tracked by the debugger
    /// </summary>
    public abstract class MonoEventCoordinator : MonoBehaviour, IEventCoordinator
    {
        public virtual EventDispatcher EventDispatcher { get; protected set; }
        
        public MonoDispatchType DispatchStatus { get; private set; }
        
        private readonly HashSet<ICoordinatorDebugger> _debuggers = new();
        
        private readonly Queue<EventBase> _updateQueue = new();
        
        private readonly Queue<EventBase> _lateUpdateQueue = new();
        
        private readonly Queue<EventBase> _fixedUpdateQueue = new();
        
        protected virtual void Awake()
        {
            EventDispatcher = EventDispatcher.CreateDefault();
        }
        
        protected virtual void Update()
        {
            DispatchStatus = MonoDispatchType.Update;
            DrainQueue(DispatchStatus);
            EventDispatcher.PushDispatcherContext();
            EventDispatcher.PopDispatcherContext();
        }
        
        protected virtual void FixedUpdate()
        {
            DispatchStatus = MonoDispatchType.FixedUpdate;
            DrainQueue(DispatchStatus);
            EventDispatcher.PushDispatcherContext();
            EventDispatcher.PopDispatcherContext();
        }
        
        protected virtual void LateUpdate()
        {
            DispatchStatus = MonoDispatchType.LateUpdate;
            DrainQueue(DispatchStatus);
            EventDispatcher.PushDispatcherContext();
            EventDispatcher.PopDispatcherContext();
        }
        
        protected virtual void OnDestroy()
        {
            DetachAllDebuggers();
        }
        
        public void Dispatch(EventBase evt, DispatchMode dispatchMode, MonoDispatchType monoDispatchType)
        {
            if (dispatchMode == DispatchMode.Immediate || monoDispatchType == DispatchStatus)
            {
                EventDispatcher.Dispatch(evt, this, dispatchMode);
                Refresh();
            }
            else
            {
                //Acquire to ensure not released
                evt.Acquire();
                GetDispatchQueue(monoDispatchType).Enqueue(evt);
            }
        }
        
        private void DrainQueue(MonoDispatchType monoDispatchType)
        {
            var queue = GetDispatchQueue(monoDispatchType);
            foreach (var evt in queue)
            {
                try
                {
                    EventDispatcher.Dispatch(evt, this, DispatchMode.Queued);
                }
                finally
                {
                    // Balance the Acquire when the event was put in queue.
                    evt.Dispose();
                }
            }
            queue.Clear();
            Refresh();
        }
        
        private Queue<EventBase> GetDispatchQueue(MonoDispatchType monoDispatchType)
        {
            return monoDispatchType switch
            {
                MonoDispatchType.Update => _updateQueue,
                MonoDispatchType.FixedUpdate => _fixedUpdateQueue,
                MonoDispatchType.LateUpdate => _lateUpdateQueue,
                _ => throw new ArgumentOutOfRangeException(nameof(monoDispatchType)),
            };
        }
        
        internal void AttachDebugger(ICoordinatorDebugger debugger)
        {
            if (debugger != null && _debuggers.Add(debugger))
            {
                debugger.CoordinatorDebug = this;
            }
        }
        
        internal void DetachDebugger(ICoordinatorDebugger debugger)
        {
            if (debugger != null)
            {
                debugger.CoordinatorDebug = null;
                _debuggers.Remove(debugger);
            }
        }
        
        internal void DetachAllDebuggers()
        {
            foreach (var debugger in _debuggers)
            {
                debugger.CoordinatorDebug = null;
                debugger.Disconnect();
            }
        }
        
        internal IEnumerable<ICoordinatorDebugger> GetAttachedDebuggers()
        {
            return _debuggers;
        }
        
        public void Refresh()
        {
            foreach (var debugger in _debuggers)
            {
                debugger.Refresh();
            }
        }
        
        public bool InterceptEvent(EventBase ev)
        {
            bool intercepted = false;
            foreach (var debugger in _debuggers)
            {
                intercepted |= debugger.InterceptEvent(ev);
            }
            return intercepted;
        }

        public void PostProcessEvent(EventBase ev)
        {
            foreach (var debugger in _debuggers)
            {
                debugger.PostProcessEvent(ev);
            }
        }

        public abstract CallbackEventHandler GetCallbackEventHandler();
    }
}