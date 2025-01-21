using System;
using Ceres.Graph.Flow.Annotations;
using UnityEngine;
namespace Ceres.Graph.Flow
{
    /// <summary>
    /// ScriptableObject contains Flow Graph that can be executed alone.
    /// </summary>
    [GenerateFlow(GenerateBridges = true, GenerateImplementation = false)]
    [CreateAssetMenu(fileName = "FlowGraphScriptableObject", menuName = "Ceres/Flow Graph ScriptableObject")]
    public partial class FlowGraphScriptableObject : FlowGraphScriptableObjectBase
    {
        public override Type GetContainerType()
        {
            return typeof(FlowGraphScriptableObject);
        }
    }
}
