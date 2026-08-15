using Newtonsoft.Json;
namespace Ceres.Events
{
    /// <summary>
    /// Base interface for ChangeEvent.
    /// </summary>
    public interface IChangeEvent
    {
    }
    /// <summary>
    /// Sends an event when a value in a field changes.
    /// </summary>
    public class ChangeEvent<T> : EventBase<ChangeEvent<T>>, IChangeEvent
    {
        static ChangeEvent()
        {
            SetCreateFunction(() => new ChangeEvent<T>());
        }

        /// <summary>
        /// The value before the change occured.
        /// </summary>
        [JsonProperty]
        public T PreviousValue { get; protected set; }
        /// <summary>
        /// The new value.
        /// </summary>
        [JsonProperty]
        public T NewValue { get; protected set; }

        /// <summary>
        /// Sets the event to its initial state.
        /// </summary>
        protected override void Init()
        {
            base.Init();
            LocalInit();
        }

        private void LocalInit()
        {
            PreviousValue = default;
            NewValue = default;
        }

        /// <summary>
        /// Gets an event from the event pool and initializes it with the given values. Use this function instead of creating new events. Events obtained using this method need to be released back to the pool. You can use `Dispose()` to release them.
        /// </summary>
        /// <param name="previousValue">The previous value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>An initialized event.</returns>
        public static ChangeEvent<T> GetPooled(T previousValue, T newValue)
        {
            ChangeEvent<T> e = GetPooled();
            e.PreviousValue = previousValue;
            e.NewValue = newValue;
            return e;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ChangeEvent()
        {
            LocalInit();
        }
    }
}