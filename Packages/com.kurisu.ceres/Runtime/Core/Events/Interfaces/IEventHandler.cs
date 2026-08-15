using UnityEngine;
namespace Ceres.Events
{
    /// <summary>
    /// Interface for class capable of handling events.
    /// </summary>
    public interface IEventHandler
    {
        /// <summary>
        /// Sends an event to the event handler.
        /// </summary>
        /// <param name="e">The event to send.</param>
        /// <param name="dispatchMode">The event dispatch mode.</param>
        void SendEvent(EventBase e, DispatchMode dispatchMode = DispatchMode.Default);

        /// <summary>
        /// Handle an event.
        /// </summary>
        /// <param name="evt">The event to handle.</param>
        void HandleEvent(EventBase evt);

    }
    /// <summary>
    /// Interface for class have <see cref="MonoBehaviour"/> lifetime scope
    /// </summary>
    public interface IBehaviourScope
    {
        /// <summary>
        /// Attached <see cref="MonoBehaviour"/>
        /// </summary>
        MonoBehaviour Behaviour { get; }
    }
}
