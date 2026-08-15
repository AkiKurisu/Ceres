namespace Ceres.Events
{
    internal class DebuggerEventDispatchingStrategy : IEventDispatchingStrategy
    {

        public bool CanDispatchEvent(EventBase evt)
        {
#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        public void DispatchEvent(EventBase evt, IEventCoordinator coordinator)
        {
#if UNITY_EDITOR
            var panelDebug = coordinator as MonoEventCoordinator;
            if (panelDebug == null) return;
            if (panelDebug.InterceptEvent(evt))
            {
                evt.StopPropagation();
                evt.PreventDefault();
                evt.StopDispatch = true;
            }
#endif
        }

        public void PostDispatch(EventBase evt, IEventCoordinator coordinator)
        {
#if UNITY_EDITOR
            if (coordinator is MonoEventCoordinator monoEventCoordinator)
            {
                monoEventCoordinator.PostProcessEvent(evt);
            }
#endif
        }
    }
}
