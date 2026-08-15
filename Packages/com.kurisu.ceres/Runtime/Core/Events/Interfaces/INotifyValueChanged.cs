namespace Ceres.Events
{
    /// <summary>
    /// Interface for controls that hold a value and can notify when it is changed by user input.
    /// </summary>
    public interface INotifyValueChanged<T>
    {
        /// <summary>
        /// The value the control holds.
        /// </summary>
        T Value { get; set; }
        /// <summary>
        /// Sets the value and, even if different, doesn't notify registers callbacks with a <see cref="ChangeEvent{T}"/>
        /// </summary>
        /// <param name="newValue">The new value to be set.</param>
        void SetValueWithoutNotify(T newValue);
    }
    /// <summary>
    /// INotifyValueChangedExtensions is a set of extension methods useful for objects implementing <see cref="INotifyValueChanged{T}"/>.
    /// </summary>
    public static class INotifyValueChangedExtensions
    {
        /// <summary>
        /// Register this callback to receive <see cref="ChangeEvent{T}"/> when the value is changed.
        /// </summary>
        public static bool RegisterValueChangedCallback<T>(this INotifyValueChanged<T> control, EventCallback<ChangeEvent<T>> callback)
        {
            if (control is CallbackEventHandler handler)
            {
                handler.RegisterCallback(callback, TrickleDown.NoTrickleDown);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Unregister this callback from receiving <see cref="ChangeEvent{T}"/> when the value is changed.
        /// </summary>
        public static bool UnregisterValueChangedCallback<T>(this INotifyValueChanged<T> control, EventCallback<ChangeEvent<T>> callback)
        {
            if (control is CallbackEventHandler handler)
            {
                handler.UnregisterCallback(callback);
                return true;
            }
            return false;
        }
    }
}