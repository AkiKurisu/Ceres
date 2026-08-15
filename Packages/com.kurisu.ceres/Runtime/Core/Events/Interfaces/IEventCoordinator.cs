namespace Ceres.Events
{
    /// <summary>
    /// Interface for specified event processing environment
    /// </summary>
    public interface IEventCoordinator
    {
        /// <summary>
        /// Get coordinator's default event handler
        /// </summary>
        /// <value></value>
        CallbackEventHandler GetCallbackEventHandler();
        
        /// <summary>
        /// Get coordinator's dispatcher
        /// </summary>
        /// <value></value>
        EventDispatcher EventDispatcher { get; }
    }
}
