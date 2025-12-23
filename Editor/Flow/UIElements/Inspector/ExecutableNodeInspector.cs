using System;
using System.Collections.Generic;
using Ceres.Graph;
using Chris.Serialization;
using Chris.Serialization.Editor;
using UnityEditor;
using UnityEngine;

namespace Ceres.Editor.Graph.Flow
{
    /// <summary>
    /// Inspector for displaying and editing executable node fields using IMGUI
    /// Only draws fields that have FieldResolver
    /// </summary>
    public class ExecutableNodeInspector : IDisposable
    {
        private readonly ExecutableNodeView _nodeView;

        private readonly HashSet<string> _allowedFieldNames;

        private readonly FieldResolverInfo[] _fieldResolverInfos;

        private readonly Dictionary<CeresPortView, PortEditorInfo> _portEditorInfos;

        private SerializedObjectWrapper _nodeWrapper;

        private SoftObjectHandle _wrapperHandle;

        private class PortEditorInfo
        {
            public CeresPortView PortView;

            public SerializedObjectWrapper Wrapper;

            public SoftObjectHandle Handle;
        }

        /// <summary>
        /// Create inspector for the given node view
        /// </summary>
        /// <param name="nodeView">Node view to inspect</param>
        public ExecutableNodeInspector(ExecutableNodeView nodeView)
        {
            _nodeView = nodeView ?? throw new ArgumentNullException(nameof(nodeView));

            // Build allowed field names from FieldResolverInfos
            _fieldResolverInfos = _nodeView.GetAllFieldResolverInfos();
            _allowedFieldNames = new HashSet<string>();
            foreach (var info in _fieldResolverInfos)
            {
                _allowedFieldNames.Add(info.FieldInfo.Name);
            }

            // Initialize port editors
            _portEditorInfos = new Dictionary<CeresPortView, PortEditorInfo>();
            InitializePortEditors();

            InitializeNodeWrapper();
        }

        /// <summary>
        /// Initialize port editors for editable ports
        /// </summary>
        private void InitializePortEditors()
        {
            var portViews = _nodeView.GetAllPortViews();

            foreach (var portView in portViews)
            {
                // Only include ports that have FieldResolver and are editable
                if (portView.FieldResolver == null) continue;
                if (!portView.PortElement.GetEditorFieldVisibility()) continue;

                try
                {
                    // Compile port to get CeresPort instance
                    var portInstance = CompilePort(portView);
                    if (portInstance == null) continue;

                    var portType = portInstance.GetType();

                    // Create wrapper for entire CeresPort object
                    var handle = new SoftObjectHandle();
                    var wrapper = SerializedObjectWrapperManager.CreateWrapper(portType, ref handle);
                    wrapper.Value = portInstance;

                    var portEditorInfo = new PortEditorInfo
                    {
                        PortView = portView,
                        Wrapper = wrapper,
                        Handle = handle
                    };

                    _portEditorInfos[portView] = portEditorInfo;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to create wrapper for port {portView.Binding.DisplayName.Value}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Compile port view to get CeresPort instance
        /// Similar to CompileNode but for ports
        /// </summary>
        /// <param name="portView">Port view to compile</param>
        /// <returns>CeresPort instance</returns>
        private static CeresPort CompilePort(CeresPortView portView)
        {
            var binding = portView.Binding;
            if (binding.BindingType != CeresPortViewBinding.PortBindingType.Field) return null;
            if (binding.ResolvedFieldInfo == null) return null;

            var portType = binding.GetPortType();
            if (portType == null) return null;

            // Create port instance
            var portInstance = (CeresPort)Activator.CreateInstance(portType);

            // Commit current value from FieldResolver to port
            portView.FieldResolver?.Commit(portInstance);

            return portInstance;
        }

        /// <summary>
        /// Initialize wrapper for entire node instance
        /// </summary>
        private void InitializeNodeWrapper()
        {
            try
            {
                // Get node data snapshot from current node view state
                var nodeInstance = _nodeView.CompileNode();

                // Create wrapper for entire node
                _nodeWrapper = SerializedObjectWrapperManager.CreateWrapper(_nodeView.NodeType, ref _wrapperHandle);
                _nodeWrapper.Value = nodeInstance;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create wrapper for node: {ex.Message}");
            }
        }

        /// <summary>
        /// Draw node fields using IMGUI
        /// Called from IMGUIContainer.onGUIHandler
        /// </summary>
        public void OnGUI()
        {
            if (!_nodeWrapper)
            {
                EditorGUILayout.HelpBox("No node data available", MessageType.Warning);
                return;
            }

            // Draw node fields
            if (_allowedFieldNames.Count > 0)
            {
                DrawFilteredFields();
            }
            else
            {
                EditorGUILayout.LabelField("No fields to display", EditorStyles.helpBox);
            }
        }

        /// <summary>
        /// Draw fields filtered by FieldResolverInfos
        /// </summary>
        private void DrawFilteredFields()
        {
            using var serializedObject = new SerializedObject(_nodeWrapper);
            SerializedProperty prop = serializedObject.FindProperty("m_Value");

            EditorGUILayout.BeginVertical();

            if (prop != null && prop.NextVisible(true))
            {
                do
                {
                    if (!IsPropertyAllowed(prop)) continue;

                    EditorGUILayout.PropertyField(prop, true);
                    EditorGUILayout.Space(3);
                }
                while (prop.NextVisible(false));
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                // Properties were modified, sync back to FieldResolvers
                SyncToFieldResolvers();
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draw port field for the given port view if is editable
        /// </summary>
        /// <param name="portView">Port view to draw field for</param>
        /// <returns>True if port has editable field and was drawn</returns>
        public bool DrawPortField(CeresPortView portView)
        {
            if (!_portEditorInfos.TryGetValue(portView, out var portInfo))
            {
                return false;
            }

            DrawPortField(portInfo);
            return true;
        }

        /// <summary>
        /// Draw a single port field
        /// </summary>
        /// <param name="portInfo">Port editor info</param>
        private static void DrawPortField(PortEditorInfo portInfo)
        {
            using var serializedObject = new SerializedObject(portInfo.Wrapper);
            SerializedProperty prop = serializedObject.FindProperty("m_Value");
            
            EditorGUILayout.BeginVertical();
            if (prop != null && prop.NextVisible(true))
            {
                do
                {
                    EditorGUILayout.PropertyField(prop, true);
                }
                while (prop.NextVisible(false));
            }
            
            if (serializedObject.ApplyModifiedProperties())
            {
                // Port value changed, sync back to FieldResolver
                SyncPortToFieldResolver(portInfo);
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Sync modified port value back to FieldResolver
        /// </summary>
        /// <param name="portInfo">Port editor info</param>
        private static void SyncPortToFieldResolver(PortEditorInfo portInfo)
        {
            try
            {
                // Restore the port value back to FieldResolver
                if (portInfo.Wrapper.Value is not CeresPort portInstance) return;

                portInfo.PortView.FieldResolver.Restore(portInstance);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to sync port {portInfo.PortView.Binding.DisplayName.Value} to resolver: {ex.Message}");
            }
        }

        /// <summary>
        /// Sync modified values from wrapper back to FieldResolvers
        /// This updates the UI in GraphView
        /// </summary>
        private void SyncToFieldResolvers()
        {
            if (_nodeWrapper?.Value == null) return;

            var nodeInstance = _nodeWrapper.Value;

            foreach (var resolverInfo in _fieldResolverInfos)
            {
                try
                {
                    // Write back to FieldResolver
                    var fieldValue = resolverInfo.FieldInfo.GetValue(nodeInstance);
                    resolverInfo.Resolver.Value = fieldValue;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to sync field {resolverInfo.FieldInfo.Name} to resolver: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Check if a serialized property should be displayed
        /// </summary>
        /// <param name="prop">Serialized property</param>
        /// <returns>True if property should be displayed</returns>
        private bool IsPropertyAllowed(SerializedProperty prop)
        {
            // Extract field name from property path (e.g., "m_Value.fieldName" -> "fieldName")
            var propertyPath = prop.propertyPath;
            var fieldName = propertyPath.Replace("m_Value.", "");

            // Check if it's a direct child of m_Value (not nested property)
            if (fieldName.Contains("."))
            {
                // This is a nested property, allow it if its root field is allowed
                var rootField = fieldName.Split('.')[0];
                return _allowedFieldNames.Contains(rootField);
            }

            return _allowedFieldNames.Contains(fieldName);
        }

        /// <summary>
        /// Get current node data from inspector
        /// </summary>
        /// <returns>Node instance with current inspector values</returns>
        public object GetNodeData()
        {
            return _nodeWrapper?.Value;
        }

        /// <summary>
        /// Clean up resources
        /// </summary>
        public void Dispose()
        {
            if (_nodeWrapper)
            {
                GlobalObjectManager.UnregisterObject(_wrapperHandle);
                _nodeWrapper = null;
            }

            foreach (var portInfo in _portEditorInfos.Values)
            {
                GlobalObjectManager.UnregisterObject(portInfo.Handle);
            }
            _portEditorInfos.Clear();
        }
    }
}
