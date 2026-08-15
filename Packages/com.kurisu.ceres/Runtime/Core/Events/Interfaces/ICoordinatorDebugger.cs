namespace Ceres.Events
{
    internal interface ICoordinatorDebugger
    {
        MonoEventCoordinator CoordinatorDebug { get; set; }
        void Disconnect();
        void Refresh();
        bool InterceptEvent(EventBase ev);
        void PostProcessEvent(EventBase ev);
    }
}
