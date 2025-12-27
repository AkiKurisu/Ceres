using System;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph.Flow
{
    /// <summary>
    /// Inspector panel for Flow Graph Editor
    /// Displays information about selected nodes and allows editing their properties
    /// </summary>
    public class FlowGraphInspectorPanel
    {
        private readonly FlowGraphEditorWindow _editorWindow;

        private VisualElement _container;

        private ScrollView _content;

        private ExecutableNodeInspector _currentInspector;

        private ExecutableNodeView _currentNodeView;

        private Label _positionLabel;

        private bool _needsRebuild;

        private const int DefaultFontSize = 12;

        private const int SectionFontSize = 14;

        public FlowGraphInspectorPanel(FlowGraphEditorWindow editorWindow)
        {
            _editorWindow = editorWindow;
        }

        /// <summary>
        /// Create inspector panel UI
        /// </summary>
        /// <returns>Inspector panel container</returns>
        public VisualElement CreatePanel()
        {
            _container = new VisualElement
            {
                name = "InspectorPanel"
            };

            // Inspector header
            var header = new VisualElement
            {
                name = "InspectorHeader"
            };
            var headerLabel = new Label("Inspector");
            header.Add(headerLabel);

            _container.Add(header);

            // Inspector content area (scrollable)
            _content = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "InspectorContent"
            };

            // Placeholder content
            var contentLabel = new Label("Select a node to inspect");
            _content.Add(contentLabel);

            _container.Add(_content);

            return _container;
        }

        /// <summary>
        /// Attach selection listener for inspector panel updates
        /// </summary>
        /// <param name="rootElement">Root visual element to attach scheduler</param>
        public void AttachSelectionListener(VisualElement rootElement)
        {
            // Use schedule to periodically check for selection changes
            rootElement.schedule.Execute(CheckAndUpdateSelection).Every(100); // Check every 100ms
        }

        /// <summary>
        /// Check if selection changed and update inspector if needed
        /// </summary>
        private void CheckAndUpdateSelection()
        {
            var graphView = _editorWindow.GetGraphView();
            if (graphView == null || _content == null) return;

            ExecutableNodeView selectedNodeView = null;
            var selection = graphView.selection.OfType<ExecutableNodeElement>().ToArray();
            if (selection.Length == 1)
            {
                selectedNodeView = selection[0].View;
            }

            // Check if selection changed
            if (selectedNodeView != _currentNodeView)
            {
                BuildInspectorContent();
            }
            else if (_currentNodeView != null)
            {
                // Check if inspector needs rebuild due to port changes
                if (_needsRebuild)
                {
                    BuildInspectorContent();
                    _needsRebuild = false;
                }
                else
                {
                    UpdateInspectorContent();
                }
            }
        }

        /// <summary>
        /// Build inspector content based on current selection
        /// </summary>
        private void BuildInspectorContent()
        {
            var graphView = _editorWindow.GetGraphView();
            if (_content == null || graphView == null) return;

            DestroyCurrentInspector();

            var selection = graphView.selection.OfType<ExecutableNodeElement>().ToArray();
            if (selection.Length == 0)
            {
                // No selection - show placeholder
                var label = new Label("Select a node to inspect");
                _content.Add(label);
                return;
            }

            if (selection.Length > 1)
            {
                // Currently not allowed multiple selection.
                var label = new Label($"Multiple nodes selected ({selection.Length})");
                _content.Add(label);
                return;
            }

            DrawNodeInspector(selection[0].View);
        }

        /// <summary>
        /// Update dynamic information that changes frequently (e.g., position)
        /// </summary>
        private void UpdateInspectorContent()
        {
            if (_currentNodeView == null || _positionLabel == null) return;

            var position = _currentNodeView.NodeElement.GetPosition();
            _positionLabel.text = $"Position: ({position.x:F1}, {position.y:F1})";
        }

        /// <summary>
        /// Display a node view inspector
        /// </summary>
        /// <param name="nodeView">Node view to inspect</param>
        private void DrawNodeInspector(ExecutableNodeView nodeView)
        {
            _currentNodeView = nodeView;
            _currentInspector = new ExecutableNodeInspector(nodeView);

            // Subscribe to port array changes if applicable
            if (nodeView is ExecutablePortArrayNodeView portArrayNodeView)
            {
                portArrayNodeView.OnPortArrayChanged += OnPortArrayChanged;
            }

            // Node title section
            AddSectionTitle("Node Information");

            // Node type
            var nodeTypeName = nodeView.NodeType.Name;
            if (nodeView.NodeType.IsGenericType)
            {
                nodeTypeName = nodeView.NodeType.GetGenericTypeDefinition().Name;
            }
            AddInfoLabel($"Type: {nodeTypeName}");

            // Node GUID
            AddInfoLabel($"GUID: {nodeView.Guid}", new Color(0.7f, 0.7f, 0.7f));

            // Node position
            var position = nodeView.NodeElement.GetPosition();
            _positionLabel = new Label($"Position: ({position.x:F1}, {position.y:F1})")
            {
                style =
                {
                    marginLeft = 10,
                    marginTop = 5,
                    fontSize = DefaultFontSize,
                    whiteSpace = WhiteSpace.Normal
                }
            };
            _content.Add(_positionLabel);

            // Port information section
            DrawPortsSection(nodeView);

            // Field resolvers section
            DrawFieldsSection();
        }

        /// <summary>
        /// Display ports section
        /// </summary>
        /// <param name="nodeView">Node view</param>
        private void DrawPortsSection(ExecutableNodeView nodeView)
        {
            AddSectionTitle("Ports", 15);

            var portViews = nodeView.GetAllPortViews().ToArray();
            foreach (var portView in portViews)
            {
                var portName = portView.Binding.DisplayName.Value;
                if (string.IsNullOrEmpty(portName))
                {
                    portName = portView.Binding.GetPortName();
                }
                var direction = portView.Binding.GetDirection() == Direction.Input ? "Input" : "Output";
                var portType = portView.PortElement.portType?.Name ?? "Unknown";

                var portLabel = new Label($"[{direction}] {portName}: {portType}")
                {
                    style =
                    {
                        marginLeft = 10,
                        marginTop = 3,
                        fontSize = DefaultFontSize,
                        whiteSpace = WhiteSpace.Normal
                    }
                };
                _content.Add(portLabel);

                // Show connection status
                if (portView.PortElement.connected)
                {
                    var connectionLabel = new Label($"Connected ({portView.PortElement.connections.Count()})")
                    {
                        style =
                        {
                            marginLeft = 15,
                            marginTop = 2,
                            fontSize = DefaultFontSize,
                            color = new Color(0.4f, 0.8f, 0.4f)
                        }
                    };
                    _content.Add(connectionLabel);
                }

                // Draw port field using NodeInspector if available
                if (_currentInspector != null)
                {
                    var imguiContainer = new IMGUIContainer(() =>
                    {
                        _currentInspector.DrawPortField(portView);
                    })
                    {
                        style =
                        {
                            marginLeft = 12,
                            marginRight = 10,
                            marginTop = 3,
                            marginBottom = 5
                        }
                    };
                    _content.Add(imguiContainer);
                }
            }
        }

        /// <summary>
        /// Display fields section
        /// </summary>
        private void DrawFieldsSection()
        {
            AddSectionTitle("Fields", 15);

            try
            {
                // Create IMGUIContainer as bridge between UIElements and IMGUI
                var imguiContainer = new IMGUIContainer(_currentInspector.OnGUI)
                {
                    style =
                    {
                        marginLeft = 10,
                        marginRight = 10,
                        marginTop = 5,
                        marginBottom = 10
                    }
                };

                _content.Add(imguiContainer);
            }
            catch (Exception ex)
            {
                var errorLabel = new Label($"Error creating inspector: {ex.Message}")
                {
                    style =
                    {
                        marginLeft = 10,
                        color = new Color(1f, 0.3f, 0.3f)
                    }
                };
                _content.Add(errorLabel);
            }
        }

        /// <summary>
        /// Add section title label
        /// </summary>
        /// <param name="title">Title text</param>
        /// <param name="topMargin">Top margin</param>
        private void AddSectionTitle(string title, int topMargin = 10)
        {
            var titleLabel = new Label(title)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = SectionFontSize,
                    marginTop = topMargin,
                    marginLeft = 10,
                    marginBottom = 5
                }
            };
            _content.Add(titleLabel);
        }

        /// <summary>
        /// Add info label
        /// </summary>
        /// <param name="text">Label text</param>
        /// <param name="color">Text color (optional)</param>
        private void AddInfoLabel(string text, Color? color = null)
        {
            var label = new Label(text)
            {
                style =
                {
                    marginLeft = 10,
                    marginTop = 5,
                    fontSize = DefaultFontSize,
                    whiteSpace = WhiteSpace.Normal
                }
            };

            if (color.HasValue)
            {
                label.style.color = color.Value;
            }

            _content.Add(label);
        }

        /// <summary>
        /// Callback for port array changes
        /// </summary>
        private void OnPortArrayChanged()
        {
            _needsRebuild = true;
        }

        /// <summary>
        /// Destroy current inspector if exists
        /// </summary>
        private void DestroyCurrentInspector()
        {
            // Unsubscribe from port array changes
            if (_currentNodeView is ExecutablePortArrayNodeView portArrayNodeView)
            {
                portArrayNodeView.OnPortArrayChanged -= OnPortArrayChanged;
            }

            if (_currentInspector != null)
            {
                _currentInspector.Dispose();
                _currentInspector = null;
            }
            _currentNodeView = null;
            _positionLabel = null;
            _content.Clear();
        }
    }
}


