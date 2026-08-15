namespace Ceres.Events
{
    internal class EventDebuggerTrace
    {
        public EventDebuggerEventRecord EventBase { get; }
        public long Duration { get; set; }
        public IEventCoordinator Coordinator { get; }
        public EventDebuggerTrace(IEventCoordinator coordinator, EventBase evt, long duration)
        {
            EventBase = new EventDebuggerEventRecord(evt);
            Coordinator = coordinator;
            Duration = duration;
        }
    }
    internal class EventDebuggerCallTrace : EventDebuggerTrace
    {
        public int CallbackHashCode { get; }
        public string CallbackName { get; }
        public bool PropagationHasStopped { get; }
        public bool ImmediatePropagationHasStopped { get; }
        public bool DefaultHasBeenPrevented { get; }

        public EventDebuggerCallTrace(IEventCoordinator coordinator, EventBase evt, int cbHashCode, string cbName,
                                      bool propagationHasStopped,
                                      bool immediatePropagationHasStopped,
                                      bool defaultHasBeenPrevented,
                                      long duration)
            : base(coordinator, evt, duration)
        {
            CallbackHashCode = cbHashCode;
            CallbackName = cbName;
            PropagationHasStopped = propagationHasStopped;
            ImmediatePropagationHasStopped = immediatePropagationHasStopped;
            DefaultHasBeenPrevented = defaultHasBeenPrevented;
        }
    }

    internal class EventDebuggerDefaultActionTrace : EventDebuggerTrace
    {
        public PropagationPhase Phase { get; }

        public string TargetName
        {
            get { return EventBase.Target.GetType().FullName; }
        }

        public EventDebuggerDefaultActionTrace(IEventCoordinator coordinator, EventBase evt, PropagationPhase phase, long duration)
            : base(coordinator, evt, duration)
        {
            Phase = phase;
        }
    }
    class EventDebuggerPathTrace : EventDebuggerTrace
    {
        public PropagationPaths Paths { get; }

        public EventDebuggerPathTrace(IEventCoordinator coordinator, EventBase evt, PropagationPaths paths)
            : base(coordinator, evt, -1)
        {
            Paths = paths;
        }
    }
}
