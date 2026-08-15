namespace Ceres.Graph
{
    /// <summary>
    /// Interface for variables
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IVariable<T>
    {
        T Value { get; set; }
    }
}