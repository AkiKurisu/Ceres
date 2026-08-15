namespace Ceres.Modules
{
    /// <summary>
    /// RuntimeModule is a base class for implementing logic before scene loaded in order to initialize earlier.
    /// </summary>
    public abstract class RuntimeModule
    {
        /// <summary>
        /// Module initialization order
        /// </summary>
        public virtual int Order { get; } = 100;

        /// <summary>
        /// Module initialization entry
        /// </summary>
        public abstract void Initialize();
    }
}