using Ceres.Graph.Flow.Annotations;
using UnityEngine;
namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Base class for <see cref="ScriptableObject"/> contains Flow Graph.
    /// </summary>
    [GenerateFlow]
    [CreateAssetMenu(fileName = "FlowGraphAsset", menuName = "Ceres/Flow Graph Asset")]
    public partial class FlowGraphAsset: ScriptableObject, IFlowGraphContainer
    {
        // Implementation will be generated by Ceres.SourceGenerator
    }
}