using System;
using System.Linq;
using Chris;
using Chris.Serialization;
using UnityEngine;

namespace Ceres.Graph
{
    [CreateAssetMenu(fileName = "APIUpdateConfig", menuName = "Ceres/API Update Config")]
    public class APIUpdateConfig : ScriptableObject
    {
        [Serializable]
        private class SerializedNodeType: IEquatable<SerializedNodeType>
        {
            public SerializedType<CeresNode> nodeType;
            
            public Optional<string> overrideType;
            
            public Type ToType()
            {
                if (!overrideType.Enabled) return nodeType;
                var tokens = overrideType.Value.Split(' ');
                return new ManagedReferenceType(tokens[0], tokens[1], tokens[2]).ToType();
            }
            
            public string GetFullTypeName()
            {
                var type = ToType();
                return $"{type.Assembly.GetName().Name} {type.FullName}";
            }
            
            public SerializedNodeType() { }
            
            public SerializedNodeType(ManagedReferenceType inNodeType)
            {
                overrideType = new Optional<string>($"{inNodeType._class} {inNodeType._ns} {inNodeType._asm}");
            }

            public ManagedReferenceType ToNodeType()
            {
                if (!overrideType.Enabled) return new ManagedReferenceType(nodeType);
                var tokens = overrideType.Value.Split(' ');
                return new ManagedReferenceType(tokens[0], tokens[1], tokens[2]);
            }
            
            public bool Equals(SerializedNodeType other)
            {
                return other != null && ToNodeType().Equals(other.ToNodeType());
            }
        }
        
        [Serializable]
        private class SerializedNodeTypeRedirector
        {
            public SerializedNodeType sourceNodeType;
            
            public SerializedNodeType targetNodeType;
        }
        
        [Serializable]
        private class SerializedVariableType: IEquatable<SerializedVariableType>
        {
            public SerializedType<CeresNode> nodeType;
            
            public Optional<string> overrideType;
            
            public Type ToType()
            {
                if (!overrideType.Enabled) return nodeType;
                var tokens = overrideType.Value.Split(' ');
                return new ManagedReferenceType(tokens[0], tokens[1], tokens[2]).ToType();
            }
            
            public string GetFullTypeName()
            {
                var type = ToType();
                return $"{type.Assembly.GetName().Name} {type.FullName}";
            }
            
            public SerializedVariableType() { }
            
            public SerializedVariableType(ManagedReferenceType inNodeType)
            {
                overrideType = new Optional<string>($"{inNodeType._class} {inNodeType._ns} {inNodeType._asm}");
            }

            public ManagedReferenceType ToNodeType()
            {
                if (!overrideType.Enabled) return new ManagedReferenceType(nodeType);
                var tokens = overrideType.Value.Split(' ');
                return new ManagedReferenceType(tokens[0], tokens[1], tokens[2]);
            }
            
            public bool Equals(SerializedVariableType other)
            {
                return other != null && ToNodeType().Equals(other.ToNodeType());
            }
        }
        
        [Serializable]
        private class SerializedVariableTypeRedirector
        {
            public SerializedVariableType sourceVariableType;
            
            public SerializedVariableType targetVariableType;
        }
        
        [SerializeField]
        private SerializedNodeTypeRedirector[] nodeRedirectors;
        
        [SerializeField]
        private SerializedVariableTypeRedirector[] variableRedirectors;
        
        public Type RedirectNode(ManagedReferenceType nodeType)
        {
            var serializeType = new SerializedNodeType(nodeType);
            var redirector = nodeRedirectors.FirstOrDefault(x => x.sourceNodeType.Equals(serializeType));
            return redirector?.targetNodeType.ToType();
        }
        
        public Type RedirectVariable(ManagedReferenceType variableType)
        {
            var serializeType = new SerializedVariableType(variableType);
            var redirector = variableRedirectors.FirstOrDefault(x => x.sourceVariableType.Equals(serializeType));
            return redirector?.targetVariableType.ToType();
        }

        public static APIUpdateConfig Get()
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets($"t:{nameof(APIUpdateConfig)}");
            if (guids.Length == 0)
            {
                return null;
            }
            return UnityEditor.AssetDatabase.LoadAssetAtPath<APIUpdateConfig>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
            return null;
#endif
        }
    }
}
