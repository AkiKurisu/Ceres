using System;
using System.Collections.Generic;
using Ceres.Pool;
using Newtonsoft.Json;
using UnityEngine;
namespace Ceres.Events
{
    /// <summary>
    /// The base class for all events.
    /// The class implements IDisposable to ensure proper release of the event from the pool
    /// and of any unmanaged resources, when necessary.
    /// </summary>
    /// <remarks>
    /// Ported from <see cref="UnityEngine.UIElements.EventBase"/>.
    /// </remarks>
    public abstract class EventBase : IDisposable
    {
        [Flags]
        private enum LifeCycleStatus
        {
            None = 0,
            PropagationStopped = 1,
            ImmediatePropagationStopped = 2,
            DefaultPrevented = 4,
            Dispatching = 8,
            Pooled = 16,
            StopDispatch = 32,
            Dispatched = 64,
            Processed = 128,
        }

        [Flags]
        public enum EventPropagation
        {
            None = 0,
            Bubbles = 1,
            TricklesDown = 2,
            Cancellable = 4,
            SkipDisabledElements = 8,
            IgnoreCompositeRoots = 16,
        }

        private static long s_LastTypeId;

        /// <summary>
        /// Registers an event class to the event type system.
        /// </summary>
        /// <returns>The type ID.</returns>
        protected static long RegisterEventType() { return ++s_LastTypeId; }

        /// <summary>
        /// Retrieves the type ID for this event instance.
        /// </summary>
        [JsonIgnore]
        public virtual long EventTypeId => -1;

        private static ulong s_NextEventId;

        // Read-only state
        /// <summary>
        /// The time when the event was created, in milliseconds.
        /// </summary>
        /// <remarks>
        /// This value is relative to the start time of the current application.
        /// </remarks>
        [JsonIgnore]
        public long Timestamp { get; private set; }

        internal ulong EventId { get; private set; }

        internal ulong TriggerEventId { get; private set; }

        internal PropagationPaths Path { get; set; }

        public EventPropagation Propagation { get; protected set; }

        internal void SetTriggerEventId(ulong id)
        {
            TriggerEventId = id;
        }

        private LifeCycleStatus Status { get; set; }

        /// <summary>
        /// The current propagation phase for this event. 
        /// </summary>
        [JsonIgnore]
        public PropagationPhase PropagationPhase { get; internal set; }

        /// <summary>
        /// Allows subclasses to perform custom logic before the event is dispatched.
        /// </summary>
        protected internal virtual void PreDispatch(IEventCoordinator coordinator)
        {
        }

        /// <summary>
        /// Allows subclasses to perform custom logic after the event has been dispatched.
        /// </summary>
        protected internal virtual void PostDispatch(IEventCoordinator coordinator)
        {
            Processed = true;
        }

        /// <summary>
        /// Returns whether this event type bubbles up in the event propagation path.
        /// </summary>
        [JsonIgnore]
        public bool Bubbles
        {
            get => (Propagation & EventPropagation.Bubbles) != 0;
            protected set
            {
                if (value)
                {
                    Propagation |= EventPropagation.Bubbles;
                }
                else
                {
                    Propagation &= ~EventPropagation.Bubbles;
                }
            }
        }

        /// <summary>
        /// Returns whether this event is sent down the event propagation path during the TrickleDown phase.
        /// </summary>
        [JsonIgnore]
        public bool TricklesDown
        {
            get => (Propagation & EventPropagation.TricklesDown) != 0;
            protected set
            {
                if (value)
                {
                    Propagation |= EventPropagation.TricklesDown;
                }
                else
                {
                    Propagation &= ~EventPropagation.TricklesDown;
                }
            }
        }

        internal bool BubblesOrTricklesDown => (Propagation & (EventPropagation.Bubbles | EventPropagation.TricklesDown)) != 0;

        private IEventHandler m_Target;

        /// <summary>
        /// The target handler that received this event.
        /// Unlike currentTarget, this target does not change when the event is sent to other elements along the propagation path.
        /// </summary>
        [JsonIgnore]
        public IEventHandler Target
        {
            get => m_Target;
            set
            {
                m_Target = value;
                LeafTarget ??= value;
            }
        }

        // Original target. May be different from 'target' when propagating event and 'target.isCompositeRoot' is true.
        internal IEventHandler LeafTarget { get; private set; }

        internal List<IEventHandler> SkipElements { get; } = new();

        internal bool Skip(IEventHandler h)
        {
            return SkipElements.Contains(h);
        }
        internal bool SkipDisabledElements
        {
            get => (Propagation & EventPropagation.SkipDisabledElements) != 0;
            set
            {
                if (value)
                {
                    Propagation |= EventPropagation.SkipDisabledElements;
                }
                else
                {
                    Propagation &= ~EventPropagation.SkipDisabledElements;
                }
            }
        }

        internal bool IgnoreCompositeRoots
        {
            get { return (Propagation & EventPropagation.IgnoreCompositeRoots) != 0; }
            set
            {
                if (value)
                {
                    Propagation |= EventPropagation.IgnoreCompositeRoots;
                }
                else
                {
                    Propagation &= ~EventPropagation.IgnoreCompositeRoots;
                }
            }
        }

        /// <summary>
        /// Whether StopPropagation() was called for this event.
        /// </summary>
        [JsonIgnore]
        public bool IsPropagationStopped
        {
            get => (Status & LifeCycleStatus.PropagationStopped) != LifeCycleStatus.None;
            internal set
            {
                if (value)
                {
                    Status |= LifeCycleStatus.PropagationStopped;
                }
                else
                {
                    Status &= ~LifeCycleStatus.PropagationStopped;
                }
            }
        }

        /// <summary>
        /// Indicates whether the default actions are prevented from being executed for this event.
        /// </summary>
        public void PreventDefault()
        {
            if ((Propagation & EventPropagation.Cancellable) == EventPropagation.Cancellable)
            {
                IsDefaultPrevented = true;
            }
        }

        /// <summary>
        /// Stops propagating this event. The event is not sent to other elements along the propagation path. This method does not prevent other event handlers from executing on the current target.
        /// </summary>
        public void StopPropagation()
        {
            IsPropagationStopped = true;
        }

        /// <summary>
        /// Indicates whether StopImmediatePropagation() was called for this event.
        /// </summary>
        [JsonIgnore]
        public bool IsImmediatePropagationStopped
        {
            get => (Status & LifeCycleStatus.ImmediatePropagationStopped) != LifeCycleStatus.None;
            private set
            {
                if (value)
                {
                    Status |= LifeCycleStatus.ImmediatePropagationStopped;
                }
                else
                {
                    Status &= ~LifeCycleStatus.ImmediatePropagationStopped;
                }
            }
        }

        /// <summary>
        /// Immediately stops the propagation of the event.
        /// The event isn't sent to other elements along the propagation path.
        /// This method prevents other event handlers from executing on the current target.
        /// </summary>
        public void StopImmediatePropagation()
        {
            IsPropagationStopped = true;
            IsImmediatePropagationStopped = true;
        }

        /// <summary>
        /// Returns true if the default actions should not be executed for this event.
        /// </summary>
        [JsonIgnore]
        public bool IsDefaultPrevented
        {
            get => (Status & LifeCycleStatus.DefaultPrevented) != LifeCycleStatus.None;
            private set
            {
                if (value)
                {
                    Status |= LifeCycleStatus.DefaultPrevented;
                }
                else
                {
                    Status &= ~LifeCycleStatus.DefaultPrevented;
                }
            }
        }

        private IEventHandler m_CurrentTarget;

        /// <summary>
        /// The current target of the event.
        /// This is the eventHandler, in the propagation path, for which event handlers are currently being executed.
        /// </summary>
        [JsonIgnore]
        public virtual IEventHandler CurrentTarget
        {
            get => m_CurrentTarget;
            internal set => m_CurrentTarget = value;
        }

        /// <summary>
        /// Indicates whether the event is being dispatched to a eventHandler.
        /// An event cannot be re-dispatched while it being dispatched. If you need to recursively dispatch an event, it is recommended that you use a copy of the event.
        /// </summary>
        [JsonIgnore]
        public bool Dispatch
        {
            get => (Status & LifeCycleStatus.Dispatching) != LifeCycleStatus.None;
            internal set
            {
                if (value)
                {
                    Status |= LifeCycleStatus.Dispatching;
                    Dispatched = true;
                }
                else
                {
                    Status &= ~LifeCycleStatus.Dispatching;
                }
            }
        }

        internal void MarkReceivedByDispatcher()
        {
            Debug.Assert(Dispatched == false, "Events cannot be dispatched more than once.");
            Dispatched = true;
        }

        private bool Dispatched
        {
            get => (Status & LifeCycleStatus.Dispatched) != LifeCycleStatus.None;
            set
            {
                if (value)
                {
                    Status |= LifeCycleStatus.Dispatched;
                }
                else
                {
                    Status &= ~LifeCycleStatus.Dispatched;
                }
            }
        }

        internal bool Processed
        {
            get => (Status & LifeCycleStatus.Processed) != LifeCycleStatus.None;
            private set
            {
                if (value)
                {
                    Status |= LifeCycleStatus.Processed;
                }
                else
                {
                    Status &= ~LifeCycleStatus.Processed;
                }
            }
        }

        public bool StopDispatch
        {
            get { return (Status & LifeCycleStatus.StopDispatch) != LifeCycleStatus.None; }
            set
            {
                if (value)
                {
                    Status |= LifeCycleStatus.StopDispatch;
                }
                else
                {
                    Status &= ~LifeCycleStatus.StopDispatch;
                }
            }
        }

#if UNITY_EDITOR
        internal EventDebugger EventLogger { get; set; }

        internal bool Log => EventLogger != null;
#endif

        /// <summary>
        /// Resets all event members to their initial values.
        /// </summary>
        protected virtual void Init()
        {
            LocalInit();
        }

        private void LocalInit()
        {
            Timestamp = TimeSinceStartupMs();

            TriggerEventId = 0;
            EventId = s_NextEventId++;

            Target = null;
            LeafTarget = null;

            Path?.Release();
            Path = null;
            SkipElements.Clear();
            Propagation = EventPropagation.None;
            PropagationPhase = PropagationPhase.None;
            IsPropagationStopped = false;
            IsImmediatePropagationStopped = false;
            IsDefaultPrevented = false;


            m_CurrentTarget = null;

            Dispatch = false;
            StopDispatch = false;

            Dispatched = false;
            Processed = false;
            Pooled = false;
#if UNITY_EDITOR
            EventLogger = null;
#endif
        }

        public static long TimeSinceStartupMs()
        {
            return (long)(Time.realtimeSinceStartup * 1000.0f);
        }

        /// <summary>
        /// Constructor. Avoid creating new event instances. Instead, use GetPooled() to get an instance from a pool of reusable event instances.
        /// </summary>
        protected EventBase()
        {
            LocalInit();
        }

        /// <summary>
        /// Whether the event is allocated from a pool of events.
        /// </summary>
        protected bool Pooled
        {
            get => (Status & LifeCycleStatus.Pooled) != LifeCycleStatus.None;
            set
            {
                if (value)
                {
                    Status |= LifeCycleStatus.Pooled;
                }
                else
                {
                    Status &= ~LifeCycleStatus.Pooled;
                }
            }
        }

        public abstract void Acquire();

        /// <summary>
        /// Implementation of <see cref="IDisposable"/>.
        /// </summary>
        public abstract void Dispose();
    }

    /// <summary>
    /// Generic base class for events, implementing event pooling and automatic registration to the event type system.
    /// </summary>
    public abstract class EventBase<T> : EventBase where T : EventBase<T>, new()
    {
        private static readonly long s_TypeId = RegisterEventType();

        private static readonly _ObjectPool<T> s_Pool = new(() => new T());

        internal static void SetCreateFunction(Func<T> createMethod)
        {
            s_Pool.CreateFunc = createMethod;
        }

        private int m_RefCount;

        // ReSharper disable once ConvertConstructorToMemberInitializers
        protected EventBase()
        {
            m_RefCount = 0;
        }

        /// <summary>
        /// Retrieves the type ID for this event instance.
        /// </summary>
        /// <returns>The type ID.</returns>
        public static long TypeId()
        {
            return s_TypeId;
        }

        /// <summary>
        /// Resets all event members to their initial values.
        /// </summary>
        protected override void Init()
        {
            base.Init();

            if (m_RefCount != 0)
            {
                Debug.LogWarning($"Event improperly released, reference count {m_RefCount}.");
                m_RefCount = 0;
            }
        }

        /// <summary>
        /// Gets an event from the event pool. Use this function instead of creating new events.
        /// Events obtained using this method need to be released back to the pool. You can use `Dispose()` to release them.
        /// </summary>
        /// <returns>An initialized event.</returns>
        public static T GetPooled()
        {
            T t = s_Pool.Get();
            t.Init();
            t.Pooled = true;
            t.Acquire();
            return t;
        }

        internal static T GetPooled(EventBase e)
        {
            T t = GetPooled();
            if (e != null)
            {
                t.SetTriggerEventId(e.EventId);
            }
            return t;
        }

        static void ReleasePooled(T evt)
        {
            if (evt.Pooled)
            {
                // Reset the event before pooling to avoid leaking
                evt.Init();

                s_Pool.Release(evt);

                // To avoid double release from pool
                evt.Pooled = false;
            }
        }

        public override void Acquire()
        {
            m_RefCount++;
        }

        /// <summary>
        /// Implementation of IDispose.
        /// </summary>
        /// <remarks>
        /// If the event was instantiated from an event pool, the event is released when Dispose is called.
        /// </remarks>
        public sealed override void Dispose()
        {
            if (--m_RefCount == 0)
            {
                ReleasePooled((T)this);
            }
        }

        /// <summary>
        /// Retrieves the type ID for this event instance.
        /// </summary>
        [JsonIgnore]
        public override long EventTypeId => s_TypeId;
    }
}
