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
        internal class Redirector<T>
        {
            public T source;
            
            public T target;
        }
        
        [Serializable]
        internal class SerializedManagedReferenceType<T>: IEquatable<SerializedManagedReferenceType<T>>
        {
            public SerializedType<T> serializedType;
            
            public Optional<string> overrideType;
            
            public Type ToType()
            {
                if (!overrideType.Enabled) return serializedType;
                var tokens = overrideType.Value.Split(' ');
                return new ManagedReferenceType(tokens[0], tokens[1], tokens[2]).ToType();
            }
            
            public string GetFullTypeName()
            {
                var type = ToType();
                return $"{type.Assembly.GetName().Name} {type.FullName}";
            }
            
            public SerializedManagedReferenceType() { }
            
            public SerializedManagedReferenceType(ManagedReferenceType inNodeType)
            {
                overrideType = new Optional<string>($"{inNodeType._class} {inNodeType._ns} {inNodeType._asm}");
            }

            public ManagedReferenceType ToManagedReferenceTypeType()
            {
                if (!overrideType.Enabled) return new ManagedReferenceType(serializedType);
                var tokens = overrideType.Value.Split(' ');
                return new ManagedReferenceType(tokens[0], tokens[1], tokens[2]);
            }
            
            public bool Equals(SerializedManagedReferenceType<T> other)
            {
                return other != null && ToManagedReferenceTypeType().Equals(other.ToManagedReferenceTypeType());
            }
        }
        
        [Header("Node")]
        [SerializeField]
        internal Redirector<SerializedManagedReferenceType<CeresNode>>[] nodeRedirectors;
        
        [Header("Variable")]
        [SerializeField]
        private Redirector<SerializedManagedReferenceType<SharedVariable>>[] variableRedirectors;
        
        [Header("Global")]
        [SerializeField]
        private Redirector<string>[] assemblyRedirectors;
        
        [SerializeField]
        private Redirector<string>[] namespaceRedirectors;
        
        [Header("Settings")]
        [SerializeField, Tooltip("Enable auto redirect serialized type which will observe all possible serialized type " +
                                 "deserialization in editor. Note that this does not fix type missing, you still need to " +
                                 "fix them manually.")]
        private bool enableAutoRedirectSerializedType;
        
        public Type RedirectNode(in ManagedReferenceType nodeType)
        {
            var serializeType = new SerializedManagedReferenceType<CeresNode>(nodeType);
            var redirector = nodeRedirectors.FirstOrDefault(x => x.source.Equals(serializeType));
            return redirector?.target.ToType() ?? RedirectManagedReference(nodeType);
        }
        
        public Type RedirectVariable(in ManagedReferenceType variableType)
        {
            var serializeType = new SerializedManagedReferenceType<SharedVariable>(variableType);
            var redirector = variableRedirectors.FirstOrDefault(x => x.source.Equals(serializeType));
            return redirector?.target.ToType() ?? RedirectManagedReference(variableType);
        }

        public Type RedirectManagedReference(ManagedReferenceType managedReferenceType)
        {
            ManagedReferenceType redirectedReferenceType = managedReferenceType;
            var assemblyRedirector = assemblyRedirectors.FirstOrDefault(r => managedReferenceType._asm.StartsWith(r.source));
            if (assemblyRedirector != null)
            {
                redirectedReferenceType._asm = redirectedReferenceType._asm.Replace(assemblyRedirector.source, assemblyRedirector.target);
            }
            
            var namespaceRedirector = namespaceRedirectors.FirstOrDefault(r => managedReferenceType._ns.StartsWith(r.source));
            if (namespaceRedirector != null)
            {
                redirectedReferenceType._ns = redirectedReferenceType._ns.Replace(namespaceRedirector.source, namespaceRedirector.target);
            }

            if (redirectedReferenceType.Equals(managedReferenceType))
            {
                return null;
            }

            return redirectedReferenceType.ToType();
        }

        public Type RedirectSerializedType(string serializedType)
        {
            string redirectedSerializedType = serializedType;
            var assemblyRedirector = assemblyRedirectors.FirstOrDefault(r => serializedType.Contains(r.source));
            if (assemblyRedirector != null)
            {
                redirectedSerializedType = redirectedSerializedType.Replace(assemblyRedirector.source, assemblyRedirector.target);
            }
            
            var namespaceRedirector = namespaceRedirectors.FirstOrDefault(r => serializedType.Contains(r.source));
            if (namespaceRedirector != null)
            {
                redirectedSerializedType = redirectedSerializedType.Replace(namespaceRedirector.source, namespaceRedirector.target);
            }

            if (redirectedSerializedType.Equals(serializedType))
            {
                return null;
            }

            CeresLogger.Log($"Redirect serialized type {serializedType} to {redirectedSerializedType}");
            return SerializedType.FromString(redirectedSerializedType);
        }

        public static ConfigAutoScope AutoScope()
        {
            return new ConfigAutoScope(GetConfig_Internal());
        }

        private static APIUpdateConfig _activeConfig;
        
        /// <summary>
        /// Internal config survive until the next config is activated
        /// </summary>
        private static APIUpdateConfig _internalActiveConfig;

        /// <summary>
        /// Current active config, can be null if no valid config
        /// </summary>
        public static APIUpdateConfig Current => _activeConfig;
        
        private static APIUpdateConfig GetConfig_Internal()
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
        
        public readonly struct ConfigAutoScope: IDisposable
        {
            public ConfigAutoScope(APIUpdateConfig updateConfig)
            {
                _activeConfig = updateConfig;
                if (_internalActiveConfig)
                {
                    SerializedTypeRedirector.RedirectSerializedType -= _internalActiveConfig.RedirectSerializedType;
                }
                _internalActiveConfig = updateConfig;
                if (_internalActiveConfig&& _internalActiveConfig.enableAutoRedirectSerializedType)
                {
                    SerializedTypeRedirector.RedirectSerializedType += _internalActiveConfig.RedirectSerializedType;
                }
            }

            public void Dispose()
            {
                _activeConfig = null;
            }
        }
    }
}
