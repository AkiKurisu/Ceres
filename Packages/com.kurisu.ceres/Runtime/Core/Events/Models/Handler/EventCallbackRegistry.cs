using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
namespace Ceres.Events
{
    /// <summary>
    /// Use this enum to specify during which phases the event handler is executed.
    /// </summary>
    public enum TrickleDown
    {
        /// <summary>
        /// The event handler should be executed during the AtTarget and BubbleUp phases.
        /// </summary>
        NoTrickleDown = 0,
        /// <summary>
        /// The event handler should be executed during the AtTarget and TrickleDown phases.
        /// </summary>
        TrickleDown = 1
    }

    internal enum CallbackPhase
    {
        TargetAndBubbleUp = 1 << 0,
        TrickleDownAndTarget = 1 << 1
    }

    internal enum InvokePolicy
    {
        Default = default,
        IncludeDisabled
    }

    internal class EventCallbackListPool
    {
        readonly Stack<EventCallbackList> m_Stack = new();

        public EventCallbackList Get(EventCallbackList initializer)
        {
            EventCallbackList element;
            if (m_Stack.Count == 0)
            {
                if (initializer != null)
                    element = new EventCallbackList(initializer);
                else
                    element = new EventCallbackList();
            }
            else
            {
                element = m_Stack.Pop();
                if (initializer != null)
                    element.AddRange(initializer);
            }
            return element;
        }

        public void Release(EventCallbackList element)
        {
            element.Clear();
            m_Stack.Push(element);
        }
    }

    internal class EventCallbackList
    {
        private readonly List<EventCallbackFunctorBase> m_List;
        public int TrickleDownCallbackCount { get; private set; }
        public int BubbleUpCallbackCount { get; private set; }

        public EventCallbackList()
        {
            m_List = new List<EventCallbackFunctorBase>();
            TrickleDownCallbackCount = 0;
            BubbleUpCallbackCount = 0;
        }

        public EventCallbackList(EventCallbackList source)
        {
            m_List = new List<EventCallbackFunctorBase>(source.m_List);
            TrickleDownCallbackCount = 0;
            BubbleUpCallbackCount = 0;
        }

        public bool Contains(long eventTypeId, Delegate callback, CallbackPhase phase)
        {
            return Find(eventTypeId, callback, phase) != null;
        }

        public EventCallbackFunctorBase Find(long eventTypeId, Delegate callback, CallbackPhase phase)
        {
            for (int i = 0; i < m_List.Count; i++)
            {
                if (m_List[i].IsEquivalentTo(eventTypeId, callback, phase))
                {
                    return m_List[i];
                }
            }
            return null;
        }

        public bool Remove(long eventTypeId, Delegate callback, CallbackPhase phase)
        {
            for (int i = 0; i < m_List.Count; i++)
            {
                if (m_List[i].IsEquivalentTo(eventTypeId, callback, phase))
                {
                    m_List.RemoveAt(i);

                    if (phase == CallbackPhase.TrickleDownAndTarget)
                    {
                        TrickleDownCallbackCount--;
                    }
                    else if (phase == CallbackPhase.TargetAndBubbleUp)
                    {
                        BubbleUpCallbackCount--;
                    }

                    return true;
                }
            }
            return false;
        }

        public void Add(EventCallbackFunctorBase item)
        {
            m_List.Add(item);

            if (item.Phase == CallbackPhase.TrickleDownAndTarget)
            {
                TrickleDownCallbackCount++;
            }
            else if (item.Phase == CallbackPhase.TargetAndBubbleUp)
            {
                BubbleUpCallbackCount++;
            }
        }

        public void AddRange(EventCallbackList list)
        {
            m_List.AddRange(list.m_List);

            foreach (var item in list.m_List)
            {
                if (item.Phase == CallbackPhase.TrickleDownAndTarget)
                {
                    TrickleDownCallbackCount++;
                }
                else if (item.Phase == CallbackPhase.TargetAndBubbleUp)
                {
                    BubbleUpCallbackCount++;
                }
            }
        }

        public int Count
        {
            get { return m_List.Count; }
        }

        public EventCallbackFunctorBase this[int i]
        {
            get { return m_List[i]; }
            set { m_List[i] = value; }
        }

        public void Clear()
        {
            m_List.Clear();
            TrickleDownCallbackCount = 0;
            BubbleUpCallbackCount = 0;
        }
    }

    internal class EventCallbackRegistry
    {
        private static readonly EventCallbackListPool s_ListPool = new();

        private static EventCallbackList GetCallbackList(EventCallbackList initializer = null)
        {
            return s_ListPool.Get(initializer);
        }

        private static void ReleaseCallbackList(EventCallbackList toRelease)
        {
            s_ListPool.Release(toRelease);
        }

        private EventCallbackList m_Callbacks;
        private EventCallbackList m_TemporaryCallbacks;
        private int m_IsInvoking;

        public EventCallbackRegistry()
        {
            m_IsInvoking = 0;
        }

        private EventCallbackList GetCallbackListForWriting()
        {
            if (m_IsInvoking > 0)
            {
                if (m_TemporaryCallbacks == null)
                {
                    if (m_Callbacks != null)
                    {
                        m_TemporaryCallbacks = GetCallbackList(m_Callbacks);
                    }
                    else
                    {
                        m_TemporaryCallbacks = GetCallbackList();
                    }
                }

                return m_TemporaryCallbacks;
            }
            else
            {
                m_Callbacks ??= GetCallbackList();

                return m_Callbacks;
            }
        }

        private EventCallbackList GetCallbackListForReading()
        {
            if (m_TemporaryCallbacks != null)
            {
                return m_TemporaryCallbacks;
            }

            return m_Callbacks;
        }

        // bool ShouldRegisterCallback(long eventTypeId, Delegate callback, CallbackPhase phase)
        // {
        //     if (callback == null)
        //     {
        //         return false;
        //     }

        //     EventCallbackList callbackList = GetCallbackListForReading();
        //     if (callbackList != null)
        //     {
        //         return !callbackList.Contains(eventTypeId, callback, phase);
        //     }

        //     return true;
        // }

        private bool UnregisterCallback(long eventTypeId, Delegate callback, TrickleDown useTrickleDown)
        {
            if (callback == null)
            {
                return false;
            }

            EventCallbackList callbackList = GetCallbackListForWriting();
            var callbackPhase = useTrickleDown == TrickleDown.TrickleDown ? CallbackPhase.TrickleDownAndTarget : CallbackPhase.TargetAndBubbleUp;
            return callbackList.Remove(eventTypeId, callback, callbackPhase);
        }

        public void RegisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown, InvokePolicy invokePolicy = default) where TEventType : EventBase<TEventType>, new()
        {
            if (callback == null)
                throw new ArgumentException("callback parameter is null");

            long eventTypeId = EventBase<TEventType>.TypeId();
            var callbackPhase = useTrickleDown == TrickleDown.TrickleDown ? CallbackPhase.TrickleDownAndTarget : CallbackPhase.TargetAndBubbleUp;

            EventCallbackList callbackList = GetCallbackListForReading();
            if (callbackList == null || callbackList.Contains(eventTypeId, callback, callbackPhase) == false)
            {
                callbackList = GetCallbackListForWriting();
                callbackList.Add(new EventCallbackFunctor<TEventType>(callback, callbackPhase, invokePolicy));
            }
        }

        public void RegisterCallback<TEventType, TCallbackArgs>(EventCallback<TEventType, TCallbackArgs> callback, TCallbackArgs userArgs, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown, InvokePolicy invokePolicy = default) where TEventType : EventBase<TEventType>, new()
        {
            if (callback == null)
                throw new ArgumentException("callback parameter is null");

            long eventTypeId = EventBase<TEventType>.TypeId();
            var callbackPhase = useTrickleDown == TrickleDown.TrickleDown ? CallbackPhase.TrickleDownAndTarget : CallbackPhase.TargetAndBubbleUp;

            EventCallbackList callbackList = GetCallbackListForReading();
            if (callbackList != null)
            {
                if (callbackList.Find(eventTypeId, callback, callbackPhase) is EventCallbackFunctor<TEventType, TCallbackArgs> functor)
                {
                    functor.UserArgs = userArgs;
                    return;
                }
            }
            callbackList = GetCallbackListForWriting();
            callbackList.Add(new EventCallbackFunctor<TEventType, TCallbackArgs>(callback, userArgs, callbackPhase, invokePolicy));
        }

        public bool UnregisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown) where TEventType : EventBase<TEventType>, new()
        {
            long eventTypeId = EventBase<TEventType>.TypeId();
            return UnregisterCallback(eventTypeId, callback, useTrickleDown);
        }

        public bool UnregisterCallback<TEventType, TCallbackArgs>(EventCallback<TEventType, TCallbackArgs> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown) where TEventType : EventBase<TEventType>, new()
        {
            long eventTypeId = EventBase<TEventType>.TypeId();
            return UnregisterCallback(eventTypeId, callback, useTrickleDown);
        }

        internal bool TryGetUserArgs<TEventType, TCallbackArgs>(EventCallback<TEventType, TCallbackArgs> callback, TrickleDown useTrickleDown, out TCallbackArgs userArgs) where TEventType : EventBase<TEventType>, new()
        {
            userArgs = default;

            if (callback == null)
                return false;

            EventCallbackList list = GetCallbackListForReading();
            long eventTypeId = EventBase<TEventType>.TypeId();
            var callbackPhase = useTrickleDown == TrickleDown.TrickleDown ? CallbackPhase.TrickleDownAndTarget : CallbackPhase.TargetAndBubbleUp;

            if (list.Find(eventTypeId, callback, callbackPhase) is not EventCallbackFunctor<TEventType, TCallbackArgs> functor)
                return false;

            userArgs = functor.UserArgs;

            return true;
        }

        public void InvokeCallbacks(EventBase evt, PropagationPhase propagationPhase)
        {
            if (m_Callbacks == null)
            {
                return;
            }

            m_IsInvoking++;
            var requiresIncludeDisabledPolicy = evt.SkipDisabledElements && evt.CurrentTarget is IBehaviourScope ve && !ve.Behaviour.isActiveAndEnabled;
            for (var i = 0; i < m_Callbacks.Count; i++)
            {
                if (evt.IsImmediatePropagationStopped)
                    break;

                if (requiresIncludeDisabledPolicy &&
                    m_Callbacks[i].InvokePolicy != InvokePolicy.IncludeDisabled)
                {
                    continue;
                }

                m_Callbacks[i].Invoke(evt, propagationPhase);
            }

            m_IsInvoking--;

            if (m_IsInvoking == 0)
            {
                // If callbacks were modified during callback invocation, update them now.
                if (m_TemporaryCallbacks != null)
                {
                    ReleaseCallbackList(m_Callbacks);
                    m_Callbacks = GetCallbackList(m_TemporaryCallbacks);
                    ReleaseCallbackList(m_TemporaryCallbacks);
                    m_TemporaryCallbacks = null;
                }
            }
        }

        public bool HasTrickleDownHandlers()
        {
            return m_Callbacks != null && m_Callbacks.TrickleDownCallbackCount > 0;
        }

        public bool HasBubbleHandlers()
        {
            return m_Callbacks != null && m_Callbacks.BubbleUpCallbackCount > 0;
        }
    }
    internal static class GlobalCallbackRegistry
    {
        private static bool m_IsEventDebuggerConnected = false;
        public static bool IsEventDebuggerConnected
        {
            get { return m_IsEventDebuggerConnected; }
            set
            {
                if (!value)
                    s_Listeners.Clear();

                m_IsEventDebuggerConnected = value;
            }
        }

        internal struct ListenerRecord
        {
            public int hashCode;
            public string name;
            public string fileName;
            public int lineNumber;
        }

        internal static readonly Dictionary<CallbackEventHandler, Dictionary<Type, List<ListenerRecord>>> s_Listeners =
            new();

        public static void CleanListeners()
        {
            var listeners = s_Listeners.ToList();
            foreach (var eventRegistrationListener in listeners)
            {
                var key = eventRegistrationListener.Key as IBehaviourScope; // Behavior that sends events
                if (key?.Behaviour == null)
                    s_Listeners.Remove(eventRegistrationListener.Key);
            }
        }
        public static void RegisterListeners<TEventType>(CallbackEventHandler ceh, Delegate callback, TrickleDown useTrickleDown)
        {
            if (!IsEventDebuggerConnected)
                return;
            if (!s_Listeners.TryGetValue(ceh, out Dictionary<Type, List<ListenerRecord>> dict))
            {
                dict = new Dictionary<Type, List<ListenerRecord>>();
                s_Listeners.Add(ceh, dict);
            }

            string itemName = DiagnosticsUtility.GetDelegatePath(callback);

            if (!dict.TryGetValue(typeof(TEventType), out List<ListenerRecord> callbackRecords))
            {
                callbackRecords = new List<ListenerRecord>();
                dict.Add(typeof(TEventType), callbackRecords);
            }

            StackFrame frame = DiagnosticsUtility.GetCurrentStackFrame();

            callbackRecords.Add(new ListenerRecord
            {
                hashCode = callback.GetHashCode(),
                name = itemName,
                fileName = frame.GetFileName(),
                lineNumber = frame.GetFileLineNumber()
            });
        }

        public static void UnregisterListeners<TEventType>(CallbackEventHandler ceh, Delegate callback)
        {
            if (!IsEventDebuggerConnected)
                return;
            if (!s_Listeners.TryGetValue(ceh, out Dictionary<Type, List<ListenerRecord>> dict))
                return;

            string itemName = DiagnosticsUtility.GetDelegatePath(callback);

            if (!dict.TryGetValue(typeof(TEventType), out List<ListenerRecord> callbackRecords))
                return;

            for (var i = callbackRecords.Count - 1; i >= 0; i--)
            {
                var callbackRecord = callbackRecords[i];
                if (callbackRecord.name == itemName)
                {
                    callbackRecords.RemoveAt(i);
                }
            }
            s_Listeners.Remove(ceh);
        }
    }
}
