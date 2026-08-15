using Ceres.Graph;
using Ceres.Flow.Annotations;
using UnityEngine;
namespace Ceres.Flow
{
    /// <summary>
    /// ScriptableObject contains Flow Graph that can be executed alone.
    /// </summary>
    [GenerateFlow(GenerateRuntime = true, GenerateImplementation = false)]
    [CreateAssetMenu(fileName = "FlowGraphScriptableObject", menuName = "Ceres/Flow Graph ScriptableObject")]
    public partial class FlowGraphScriptableObject : FlowGraphScriptableObjectBase
    {
    }
}
