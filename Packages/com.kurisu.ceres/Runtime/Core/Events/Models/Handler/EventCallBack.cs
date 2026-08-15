using System;
namespace Ceres.Events
{
    /// <summary>
    /// Defines the structure of a callback that can be registered onto an element for an event type
    /// </summary>
    /// <param name="evt">The event instance</param>
    /// <typeparam name="TEventType">The type of event to register the callback for</typeparam>
    public delegate void EventCallback<in TEventType>(TEventType evt);

    /// <summary>
    /// Defines the structure of a callback that can be registered onto an element for an event type,
    /// along with a custom user defined argument.
    /// </summary>
    /// <param name="evt">The event instance.</param>
    /// <param name="userArgs">The user argument instance.</param>
    /// <typeparam name="TEventType">The type of event registered for the callback.</typeparam>
    /// <typeparam name="TCallbackArgs">The type of the user argument.</typeparam>
    public delegate void EventCallback<in TEventType, in TCallbackArgs>(TEventType evt, TCallbackArgs userArgs);
    internal abstract class EventCallbackFunctorBase
    {
        public CallbackPhase Phase { get; }
        public InvokePolicy InvokePolicy { get; }

        protected EventCallbackFunctorBase(CallbackPhase phase, InvokePolicy invokePolicy)
        {
            Phase = phase;
            InvokePolicy = invokePolicy;
        }

        public abstract void Invoke(EventBase evt, PropagationPhase propagationPhase);

        public abstract bool IsEquivalentTo(long eventTypeId, Delegate callback, CallbackPhase phase);

        protected bool PhaseMatches(PropagationPhase propagationPhase)
        {
            switch (Phase)
            {
                case CallbackPhase.TrickleDownAndTarget:
                    if (propagationPhase != PropagationPhase.TrickleDown && propagationPhase != PropagationPhase.AtTarget)
                        return false;
                    break;

                case CallbackPhase.TargetAndBubbleUp:
                    if (propagationPhase != PropagationPhase.AtTarget && propagationPhase != PropagationPhase.BubbleUp)
                        return false;
                    break;
            }

            return true;
        }
    }
    internal class EventCallbackFunctor<TEventType> : EventCallbackFunctorBase where TEventType : EventBase<TEventType>, new()
    {
        private readonly EventCallback<TEventType> m_Callback;
        private readonly long m_EventTypeId;

        public EventCallbackFunctor(EventCallback<TEventType> callback, CallbackPhase phase, InvokePolicy invokePolicy = default) : base(phase, invokePolicy)
        {
            m_Callback = callback;
            m_EventTypeId = EventBase<TEventType>.TypeId();
        }

        public override void Invoke(EventBase evt, PropagationPhase propagationPhase)
        {
            if (evt == null)
                throw new ArgumentNullException(nameof(evt));

            if (evt.EventTypeId != m_EventTypeId)
                return;

            if (PhaseMatches(propagationPhase))
            {
                using (new EventDebuggerLogCall(m_Callback, evt))
                {
                    m_Callback(evt as TEventType);
                }
            }
        }

        public override bool IsEquivalentTo(long eventTypeId, Delegate callback, CallbackPhase phase)
        {
            return (m_EventTypeId == eventTypeId) && ((Delegate)m_Callback) == callback && (this.Phase == phase);
        }
    }

    internal class EventCallbackFunctor<TEventType, TCallbackArgs> : EventCallbackFunctorBase where TEventType : EventBase<TEventType>, new()
    {
        private readonly EventCallback<TEventType, TCallbackArgs> m_Callback;
        private readonly long m_EventTypeId;

        internal TCallbackArgs UserArgs { get; set; }

        public EventCallbackFunctor(EventCallback<TEventType, TCallbackArgs> callback, TCallbackArgs userArgs, CallbackPhase phase, InvokePolicy invokePolicy) : base(phase, invokePolicy)
        {
            UserArgs = userArgs;
            m_Callback = callback;
            m_EventTypeId = EventBase<TEventType>.TypeId();
        }

        public override void Invoke(EventBase evt, PropagationPhase propagationPhase)
        {
            if (evt == null)
                throw new ArgumentNullException(nameof(evt));

            if (evt.EventTypeId != m_EventTypeId)
                return;

            if (PhaseMatches(propagationPhase))
            {
                using (new EventDebuggerLogCall(m_Callback, evt))
                {
                    m_Callback(evt as TEventType, UserArgs);
                }
            }
        }

        public override bool IsEquivalentTo(long eventTypeId, Delegate callback, CallbackPhase phase)
        {
            return (m_EventTypeId == eventTypeId) && ((Delegate)m_Callback) == callback && (this.Phase == phase);
        }
    }
}
