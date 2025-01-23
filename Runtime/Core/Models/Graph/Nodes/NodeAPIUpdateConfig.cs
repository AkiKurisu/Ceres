using System;
using System.Linq;
using Chris;
using Chris.Serialization;
using UnityEngine;
namespace Ceres.Graph
{
    [CreateAssetMenu(fileName = "NodeAPIUpdateConfig", menuName = "Ceres/Node API Update Config")]
    public class NodeAPIUpdateConfig : ScriptableObject
    {
        [Serializable]
        public class SerializeNodeType: IEquatable<SerializeNodeType>
        {

            public SerializedType<CeresNode> nodeType;
            
            public Optional<string> overrideType;
            
            public Type ToType()
            {
                if (!overrideType.Enabled) return nodeType;
                var tokens = overrideType.Value.Split(' ');
                return new CeresNodeData.NodeType(tokens[0], tokens[1], tokens[2]).ToType();

            }
            
            public string GetFullTypeName()
            {
                var type = ToType();
                return $"{type.Assembly.GetName().Name} {type.FullName}";
            }
            
            public SerializeNodeType() { }
            
            public SerializeNodeType(CeresNodeData.NodeType inNodeType)
            {
                overrideType = new Optional<string>($"{inNodeType._class} {inNodeType._ns} {inNodeType._asm}");
            }

            public CeresNodeData.NodeType ToNodeType()
            {
                if (!overrideType.Enabled) return new CeresNodeData.NodeType(nodeType);
                var tokens = overrideType.Value.Split(' ');
                return new CeresNodeData.NodeType(tokens[0], tokens[1], tokens[2]);
            }
            
            public bool Equals(SerializeNodeType other)
            {
                return other != null && ToNodeType().Equals(other.ToNodeType());
            }
        }
        
        [Serializable]
        public class SerializeNodeTypeRedirector
        {
            public SerializeNodeType sourceNodeType;
            
            public SerializeNodeType targetNodeType;
        }
        
        public SerializeNodeTypeRedirector[] redirectors;
        
        public Type Redirect(CeresNodeData.NodeType nodeType)
        {
            var serializeType = new SerializeNodeType(nodeType);
            var redirector = redirectors.FirstOrDefault(x => x.sourceNodeType.Equals(serializeType));
            return redirector?.targetNodeType.ToType();
        }

        public static NodeAPIUpdateConfig Get()
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets($"t:{nameof(NodeAPIUpdateConfig)}");
            if (guids.Length == 0)
            {
                return null;
            }
            return UnityEditor.AssetDatabase.LoadAssetAtPath<NodeAPIUpdateConfig>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
            return null;
#endif
        }
    }
}
