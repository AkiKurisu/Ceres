using UnityEngine;
using Ceres.Graph.Flow.Annotations;
namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Base class for <see cref="MonoBehaviour"/> contains Flow Graph.
    /// </summary>
    [GenerateFlow]
    public partial class FlowGraphObject : MonoBehaviour, IFlowGraphContainer
    {
        
    }
}
