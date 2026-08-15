using System;
using System.Collections.Generic;
using System.Diagnostics;
using Ceres.Pool;
using Debug = UnityEngine.Debug;
namespace Ceres.Events
{
    public enum DispatchMode
    {
        Default = Queued,
        
        Queued = 1,
        
        Immediate = 2,
    }
    
    /// <summary>
    /// Gates control when the dispatcher processes events.
    /// </summary>
    public readonly struct EventDispatcherGate : IDisposable, IEquatable<EventDispatcherGate>
    {
        private readonly EventDispatcher m_Dispatcher;
        
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="d">The dispatcher controlled by this gate.</param>
        public EventDispatcherGate(EventDispatcher d)
        {
            m_Dispatcher = d ?? throw new ArgumentNullException(nameof(d));
            m_Dispatcher.CloseGate();
        }

        /// <summary>
        /// Implementation of IDisposable.Dispose. Opens the gate. If all gates are open, events in the queue are processed.
        /// </summary>
        public void Dispose()
        {
            m_Dispatcher.OpenGate();
        }

        public bool Equals(EventDispatcherGate other)
        {
            return Equals(m_Dispatcher, other.m_Dispatcher);
        }
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            return obj is EventDispatcherGate gate && Equals(gate);
        }
        public override int GetHashCode()
        {
            return m_Dispatcher != null ? m_Dispatcher.GetHashCode() : 0;
        }

        public static bool operator ==(EventDispatcherGate left, EventDispatcherGate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EventDispatcherGate left, EventDispatcherGate right)
        {
            return !left.Equals(right);
        }
    }
    /// <summary>
    /// Dispatches events to a <see cref="IEventCoordinator"/>.
    /// </summary>
    public sealed class EventDispatcher
    {
        private struct EventRecord
        {
            public EventBase m_Event;
            public IEventCoordinator m_Coordinator;
#if UNITY_EDITOR
            internal StackTrace m_StackTrace;
            internal readonly string StackTrace => m_StackTrace?.ToString() ?? string.Empty;
#endif
        }

        private readonly List<IEventDispatchingStrategy> m_DispatchingStrategies;
        private static readonly _ObjectPool<Queue<EventRecord>> k_EventQueuePool = new(() => new Queue<EventRecord>());
        private Queue<EventRecord> m_Queue;
        private uint m_GateCount;
        private readonly DebuggerEventDispatchingStrategy m_DebuggerEventDispatchingStrategy;
        private struct DispatchContext
        {
            public uint m_GateCount;
            public Queue<EventRecord> m_Queue;
        }
        private readonly List<IEventDispatchingListener> dispatchingListeners;
        private readonly Stack<DispatchContext> m_DispatchContexts = new();
        public EventDispatcher(IList<IEventDispatchingStrategy> strategies)
        {
            dispatchingListeners = new List<IEventDispatchingListener>();
            m_DispatchingStrategies = new List<IEventDispatchingStrategy>();
#if UNITY_EDITOR
            m_DebuggerEventDispatchingStrategy = new DebuggerEventDispatchingStrategy();
            m_DispatchingStrategies.Add(m_DebuggerEventDispatchingStrategy);
#endif
            m_DispatchingStrategies.AddRange(strategies);
            m_Queue = k_EventQueuePool.Get();
        }
        private static readonly IEventDispatchingStrategy[] defaultStrategies =
        {
            new DefaultDispatchingStrategy()
        };
        public static EventDispatcher CreateDefault()
        {
            return new EventDispatcher(defaultStrategies);
        }
        private readonly bool m_Immediate = false;
        private bool DispatchImmediately
        {
            get { return m_Immediate || m_GateCount == 0; }
        }

        public bool ProcessingEvents { get; private set; }

        public void Dispatch(EventBase evt, IEventCoordinator coordinator, DispatchMode dispatchMode)
        {
            evt.MarkReceivedByDispatcher();

            if (DispatchImmediately || (dispatchMode == DispatchMode.Immediate))
            {
                ProcessEvent(evt, coordinator);
            }
            else
            {
                evt.Acquire();
                m_Queue.Enqueue(new EventRecord
                {
                    m_Event = evt,
                    m_Coordinator = coordinator,
#if UNITY_EDITOR
                    m_StackTrace = new StackTrace()
#endif
                });
            }
        }

        public void PushDispatcherContext()
        {
            foreach (var listener in dispatchingListeners)
            {
                listener.OnPushDispatcherContext();
            }

            // Drain the event queue before pushing a new context.
            ProcessEventQueue();

            m_DispatchContexts.Push(new DispatchContext() { m_GateCount = m_GateCount, m_Queue = m_Queue });
            m_GateCount = 0;
            m_Queue = k_EventQueuePool.Get();
        }

        public void PopDispatcherContext()
        {
            Debug.Assert(m_GateCount == 0, "All gates should have been opened before popping dispatch context.");
            Debug.Assert(m_Queue.Count == 0, "Queue should be empty when popping dispatch context.");

            k_EventQueuePool.Release(m_Queue);

            m_GateCount = m_DispatchContexts.Peek().m_GateCount;
            m_Queue = m_DispatchContexts.Peek().m_Queue;
            m_DispatchContexts.Pop();

            foreach (var listener in dispatchingListeners)
            {
                listener.OnPopDispatcherContext();
            }
        }

        internal void CloseGate()
        {
            m_GateCount++;
        }

        internal void OpenGate()
        {
            Debug.Assert(m_GateCount > 0);

            if (m_GateCount > 0)
            {
                m_GateCount--;
            }

            if (m_GateCount == 0)
            {
                ProcessEventQueue();
            }
        }

        private void ProcessEventQueue()
        {
            // While processing the current queue, we need a new queue to store additional events that
            // might be generated during current queue events processing. Thanks to the gate mechanism,
            // events put in the new queue will be processed before the remaining events in the current
            // queue (but after processing of the event generating them is completed).

            Queue<EventRecord> queueToProcess = m_Queue;
            m_Queue = k_EventQueuePool.Get();

            try
            {
                ProcessingEvents = true;
                while (queueToProcess.Count > 0)
                {
                    EventRecord eventRecord = queueToProcess.Dequeue();
                    EventBase evt = eventRecord.m_Event;
                    IEventCoordinator coordinator = eventRecord.m_Coordinator;
                    try
                    {
                        ProcessEvent(evt, coordinator);
                    }
                    finally
                    {
                        // Balance the Acquire when the event was put in queue.
                        evt.Dispose();
                    }
                }
            }
            finally
            {
                ProcessingEvents = false;
                k_EventQueuePool.Release(queueToProcess);
            }
        }
        private void ProcessEvent(EventBase evt, IEventCoordinator coordinator)
        {
            using (new EventDispatcherGate(this))
            {
                evt.PreDispatch(coordinator);

                if (!evt.StopDispatch && !evt.IsPropagationStopped)
                {
                    ApplyDispatchingStrategies(evt, coordinator);
                }

                // Last chance to build a path. Some dispatching strategies (e.g. PointerCaptureDispatchingStrategy)
                // don't call PropagateEvents but still need to call ExecuteDefaultActions on composite roots.
                var path = evt.Path;
                if (path == null && evt.BubblesOrTricklesDown && evt.LeafTarget is CallbackEventHandler leafTarget)
                {
                    path = PropagationPaths.Build(leafTarget, evt);
                    evt.Path = path;
                    EventDebugger.LogPropagationPaths(evt, path);
                }

                if (path != null)
                {
                    foreach (var element in path.TargetElements)
                    {
                        if (element.Coordinator == coordinator)
                        {
                            evt.Target = element;
                            EventDispatchUtilities.ExecuteDefaultAction(evt);
                        }
                    }

                    // Reset target to leaf target
                    evt.Target = evt.LeafTarget;
                }
                else
                {
                    // If no propagation path, make sure EventDispatchUtilities.ExecuteDefaultAction has a target
                    if (evt.Target is not CallbackEventHandler target)
                    {
                        evt.Target = target = coordinator.GetCallbackEventHandler();
                    }

                    if (target?.Coordinator == coordinator)
                    {
                        EventDispatchUtilities.ExecuteDefaultAction(evt);
                    }
                }

#if UNITY_EDITOR
                m_DebuggerEventDispatchingStrategy.PostDispatch(evt, coordinator);
#endif

                evt.PostDispatch(coordinator);
            }
        }

        private void ApplyDispatchingStrategies(EventBase evt, IEventCoordinator coordinator)
        {
            foreach (var strategy in m_DispatchingStrategies)
            {
                if (strategy.CanDispatchEvent(evt))
                {
                    strategy.DispatchEvent(evt, coordinator);

                    if (evt.StopDispatch || evt.IsPropagationStopped)
                        break;
                }
            }
        }
        
        public TListener GetEventDispatchingListener<TListener>() where TListener : IEventDispatchingListener
        {
            foreach (var listener in dispatchingListeners)
            {
                if (listener is TListener dispatchingListener) return dispatchingListener;
            }
            return default;
        }
        
        public void AddEventDispatchingListener(IEventDispatchingListener listener)
        {
            dispatchingListeners.Add(listener);
        }
    }
}