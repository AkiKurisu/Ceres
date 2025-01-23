using Ceres.Annotations;
using UnityEngine;
namespace Ceres.Graph
{
    [CeresGroup(Annotations.CeresGroup.Hidden)]
    [CeresLabel(NodeLabel)]
    [NodeInfo(NodeInfo)]
    internal sealed class InvalidNode : CeresNode
    {
        [Multiline]
        public string nodeType;
        [Multiline]
        public string serializedData;

        public const string NodeInfo =
            "The presence of this node indicates that the namespace, class name, or assembly of the node may be changed.";
        
        public const string NodeLabel =
            "<color=#FFE000><b>Class Missing!</b></color>";
    }
}