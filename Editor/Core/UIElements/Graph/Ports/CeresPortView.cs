using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Editor.Graph;
using Chris;
using Chris.Serialization;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
namespace Ceres.Graph
{
    public class CeresEdge : Edge
    {
        public CeresEdge()
        {
            AddToClassList(nameof(CeresEdge));
        }
    }
    
    /// <summary>
    /// Edge listener connecting <see cref="CeresEdge"/> between <see cref="CeresPortElement"/>
    /// </summary>
    public class CeresEdgeListener : IEdgeConnectorListener
    {
        private readonly GraphViewChange _graphViewChange;
        
        private readonly List<Edge> _edgesToCreate;
        
        private readonly List<GraphElement> _edgesToDelete;
        
        public CeresGraphView GraphView { get; }
        
        public CeresEdgeListener(CeresGraphView ceresGraphView)
        {
            GraphView = ceresGraphView;
            _edgesToCreate = new List<Edge>();
            _edgesToDelete = new List<GraphElement>();
            _graphViewChange.edgesToCreate = _edgesToCreate;
        }
        
        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            var screenPosition = GUIUtility.GUIToScreenPoint(
                Event.current.mousePosition
            );
            
            if (edge.output != null)
            {
                GraphView.OpenSearch(
                    screenPosition,
                    ((CeresPortElement)edge.output.edgeConnector.edgeDragHelper.draggedPort).View
                );
            }
            else if (edge.input != null)
            {
                GraphView.OpenSearch(
                    screenPosition,
                    ((CeresPortElement)edge.input.edgeConnector.edgeDragHelper.draggedPort).View
                );
            }
        }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            _edgesToCreate.Clear();
            _edgesToCreate.Add(edge);
            _edgesToDelete.Clear();
            
            if (edge.input.capacity == Port.Capacity.Single)
            {
                foreach (var connection in edge.input.connections)
                {
                    if (connection != edge)
                        _edgesToDelete.Add(connection);
                }
            }
            
            if (edge.output.capacity == Port.Capacity.Single)
            {
                foreach (var connection in edge.output.connections)
                {
                    if (connection != edge)
                        _edgesToDelete.Add(connection);
                }
            }
            
            if (_edgesToDelete.Count > 0)
            {
                graphView.DeleteElements(_edgesToDelete);
            }
            
            var edgesToCreate = _edgesToCreate;
            if (graphView.graphViewChanged != null)
            {
                edgesToCreate = graphView.graphViewChanged(_graphViewChange).edgesToCreate;
            }
            foreach (Edge edgeToCreate in edgesToCreate)
            {
                graphView.AddElement(edgeToCreate);
                edgeToCreate.input.Connect(edgeToCreate);
                edgeToCreate.output.Connect(edgeToCreate);
            }
        }
    }
    public class CeresPortElement: Port
    {
        public CeresPortView View { get; private set; }
        
        public VisualElement EditorField { get; private set; }

        private CeresPortElement(Orientation portOrientation, Direction portDirection, Capacity portCapacity, Type type) : base(portOrientation, portDirection, portCapacity, type)
        {
            AddToClassList(nameof(CeresPortElement));
            CeresPort.AssignValueType(type);
            styleSheets.Add(CeresGraphView.GetOrLoadStyleSheet("Ceres/CeresPortElement"));
            if (type.IsGenericType)
            {
                AddToClassList("type" + type.GetGenericTypeDefinition().Name.Split('`')[0]);
            }
            tooltip = CreatePortTooltip(type);
        }

        public static string CreatePortTooltip(Type valueType)
        {
            if(valueType == typeof(NodeReference))
            {
                return string.Empty;
            }
            return CeresLabel.GetLabel(valueType);
        }

        public static CeresPortElement Create(CeresPortView ownerView)
        {
            var valueType = ownerView.PortData.GetValueType();
            var view = new CeresPortElement(Orientation.Horizontal, ownerView.Binding.GetDirection(), 
                                            ownerView.Binding.GetCapacity(), valueType) 
            {
                m_EdgeConnector = new EdgeConnector<CeresEdge>(new CeresEdgeListener(ownerView.NodeOwner.GraphView)),
                portName = ownerView.Binding.DisplayName,
                View = ownerView
            };
            view.AddManipulator(view.m_EdgeConnector);
            if (ownerView.FieldResolver == null || view.direction == Direction.Output) return view;
            
            // Add editor field
            view.EditorField = ownerView.FieldResolver.GetField(ownerView.NodeOwner.GraphView);
            view.m_ConnectorBox.parent.Add(view.EditorField);
            view.EditorField.style.display = DisplayStyle.Flex;
            view.EditorField.style.flexDirection = FlexDirection.Column;
            return view;
        }

        public bool IsConnectable()
        {
            return !connected || capacity != Capacity.Single;
        }

        public bool CanConnect(CeresPortElement other)
        {
            if (IsConnectable()) return false;
            if (other.direction == direction) return false;
            if (other.portType == portType) return true;
            if (direction == Direction.Input)
            {
                return portType.IsAssignableFrom(other.portType);
            }
            return portType.IsAssignableTo(other.portType);
        }

        public void SetEditorFieldVisible(bool isVisible)
        {
            if(EditorField == null) return;
            
            if(isVisible)
            {
                m_ConnectorBox.parent.Add(EditorField);
            }
            else
            {
                m_ConnectorBox.parent.Remove(EditorField);
            }
        }

        public override void Connect(Edge edge)
        {
            base.Connect(edge);
            SetEditorFieldVisible(!connections.Any() || direction == Direction.Output);
        }

        public override void Disconnect(Edge edge)
        {
            base.Disconnect(edge);
            SetEditorFieldVisible(!connections.Any() || direction == Direction.Output);
        }
    }

    public class CeresPortViewBinding
    {
        public enum PortBindingType
        {
            Field,
            Parameter
        }
        
        public FieldInfo PortFieldInfo { get; private set; }
        
        public FieldInfo ResolvedFieldInfo { get; private set; }
        
        public ParameterInfo ResolvedParameterInfo { get; private set; }

        public PortBindingType BindingType { get; private set; }

        private string _displayName;

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(_displayName))
                {
                    _displayName = GetDisplayName();
                }
                return _displayName;
            }
            internal set => _displayName = value;
        }

        public Type DisplayType { get; internal set; }

        public string Tooltip { get; set; } = string.Empty;

        private const string DefaultOutputPortName = "Return Value";

        public static CeresPortViewBinding BindField(FieldInfo portFieldInfo, 
                                                     FieldInfo resolvedFieldInfo = null)
        {
            return new CeresPortViewBinding
            {
                PortFieldInfo = portFieldInfo,
                ResolvedFieldInfo = resolvedFieldInfo ?? portFieldInfo,
                BindingType = PortBindingType.Field
            };
        }
        
        public static CeresPortViewBinding BindParameter(FieldInfo portFieldInfo, ParameterInfo parameterInfo)
        {
            return new CeresPortViewBinding
            {
                PortFieldInfo = portFieldInfo,
                ResolvedParameterInfo = parameterInfo,
                BindingType = PortBindingType.Parameter,
                DisplayType = parameterInfo.ParameterType
            };
        }

        private string GetDisplayName()
        {
            if (BindingType == PortBindingType.Parameter)
            {
                /* Case when parameter is return parameter */
                if(string.IsNullOrEmpty(ResolvedParameterInfo.Name))
                {
                    DisplayName = DefaultOutputPortName;
                    return DisplayName;
                }
                return DisplayName = CeresLabel.GetLabel(ResolvedParameterInfo.Name);
            }
            
            return DisplayName = CeresLabel.GetLabel(ResolvedFieldInfo);
        }
        
        public Direction GetDirection()
        {
            var inputPort = PortFieldInfo.GetCustomAttribute<InputPortAttribute>();
            return inputPort == null ? Direction.Output : Direction.Input;
        }
        
        public Port.Capacity GetCapacity()
        {
            if (GetDirection() == Direction.Input)
            {
                return PortFieldInfo.FieldType == typeof(NodePort) ? Port.Capacity.Multi : Port.Capacity.Single;
            }
            var outputPort = PortFieldInfo.GetCustomAttribute<OutputPortAttribute>();
            return outputPort?.AllowMulti ?? true ? Port.Capacity.Multi : Port.Capacity.Single;
        }

        public bool IsRemappedFieldPort()
        {
            return BindingType == PortBindingType.Field && PortFieldInfo != ResolvedFieldInfo;
        }

        public bool IsHideInGraphEditor()
        {
            if (BindingType != PortBindingType.Field) return true;
            return PortFieldInfo.GetCustomAttribute<HideInGraphEditorAttribute>() != null;
        }
        
        public FieldInfo GetPortValueField()
        {
            if (BindingType != PortBindingType.Field) return null;
            
            FieldInfo fieldInfo = ResolvedFieldInfo;
            if (fieldInfo.FieldType.IsSubclassOf(typeof(CeresPort)))
            {
                var type = fieldInfo.FieldType;
                while (!type!.IsGenericType && type.GetGenericTypeDefinition()!= typeof(CeresPort<>))
                {
                    type = type.BaseType;
                }
                fieldInfo = type.GetField("defaultValue", BindingFlags.Instance | BindingFlags.Public);
            }

            return fieldInfo;
        }

        public Type GetPortType()
        {
            if (BindingType != PortBindingType.Field) return null;
            
            return ResolvedFieldInfo.FieldType;
        }
        
        public string GetPortName()
        {
            return PortFieldInfo.Name;
        }

        public void SetValue(CeresNode nodeInstance, CeresPort portInstance)
        {
            if(BindingType!=PortBindingType.Field) return;
            
            ResolvedFieldInfo.SetValue(nodeInstance, portInstance);
        }

        public CeresPort GetValue(CeresNode nodeInstance)
        {
            if(BindingType != PortBindingType.Field) return null;
            
            return (CeresPort)ResolvedFieldInfo.GetValue(nodeInstance);
        }
    }
    
    /// <summary>
    /// Port view for <see cref="CeresNodeView"/>
    /// </summary>
    public class CeresPortView
    {
        public CeresPortViewBinding Binding { get; }
        
        public CeresPortData PortData { get; private set; }
        
        public CeresPortElement PortElement { get; }
        
        public CeresNodeView NodeOwner { get; }
        
        public IFieldResolver FieldResolver { get; }

        public CeresPortView(CeresPortViewBinding binding, CeresNodeView nodeView, CeresPortData portData)
        {
            PortData = portData;
            Binding = binding;
            Binding.DisplayType = portData.GetValueType();
            NodeOwner = nodeView;
            bool isInput = binding.GetDirection() == Direction.Input && !binding.IsRemappedFieldPort();
            if(isInput && !binding.IsHideInGraphEditor() && IsPortSupportValueField(portData.GetValueType()))
            {
                FieldResolver = FieldResolverFactory.Get().Create(binding.GetPortValueField());
            }
            else
            {
                FieldResolver = null;
            }
            PortElement = CeresPortElement.Create(this);
        }

        private static bool IsPortSupportValueField(Type type)
        {
            if (type == typeof(string)) return true;
            if (type.IsEnum) return true;
            
            if (ReflectionUtility.IsSerializableNumericTypes(type)) return true;
            if (ReflectionUtility.IsUnityBuiltinTypes(type)) return true;
            
            if(type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(SerializedType<>))
                    return true;
                return false;
            }
            return false;
        }

        /// <summary>
        /// Link all ports to <see cref="CeresNode"/> instance
        /// </summary>
        /// <param name="nodeInstance"></param>
        public void Commit(CeresNode nodeInstance)
        {
            // Commit default value
            if(PortElement.direction == Direction.Input && !Binding.IsRemappedFieldPort() && Binding.ResolvedFieldInfo != null)
            {
                var portInstance = (CeresPort)Binding.ResolvedFieldInfo.GetValue(nodeInstance);
                portInstance ??= (CeresPort)Activator.CreateInstance(Binding.GetPortType());
                FieldResolver?.Commit(portInstance);
                Binding.SetValue(nodeInstance, portInstance);
            }
            
            var connectionDataList = new List<PortConnectionData>();
            
            // Skip input ports since outputs will save connection
            if(PortElement.direction == Direction.Output)
            {
                foreach (var connection in PortElement.connections)
                {
                    var port = (CeresPortElement)connection.input;
                    var connectionData = new PortConnectionData
                    {
                        portIndex = port.View.PortData.arrayIndex,
                        portId = port.View.PortData.propertyName,
                        nodeId = port.View.NodeOwner.Guid
                    };
                    connectionDataList.Add(connectionData);
                }
            }
            PortData.connections = connectionDataList.ToArray();
            nodeInstance.NodeData.AddPortData(PortData);
        }

        /// <summary>
        /// Connect port element based on current <see cref="CeresPortData"/>
        /// </summary>
        public void Connect()
        {
            // Skip input ports since outputs will connect it
            if(PortElement.direction == Direction.Input) return;
            
            foreach (var connection in PortData.connections)
            {
                var node = NodeOwner.GraphView.FindNodeView<CeresNodeView>(connection.nodeId);
                var port = node?.FindPortView(connection.portId, connection.portIndex);
                if(port == null) continue;
                
                NodeOwner.GraphView.ConnectPorts(port, this);
            }
        }

        public void Restore(CeresNode nodeInstance, CeresPortData portData)
        {
            if (FieldResolver != null && !Binding.IsRemappedFieldPort())
            {
                try
                {
                    var portInstance = Binding.GetValue(nodeInstance);
                    FieldResolver.Restore(portInstance);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
            if(portData == null) return;
            PortData = portData;
        }

        public void SetPortDisplayName(string displayName)
        {
            Binding.DisplayName = PortElement.portName = CeresLabel.GetLabel(displayName);
        }
        
        public void SetPortTooltip(string tooltip)
        {
            Binding.Tooltip = PortElement.tooltip = CeresPortElement.CreatePortTooltip(Binding.DisplayType) + tooltip;
        }
        
        public void SetPortDisplayType(Type displayType)
        {
            Binding.DisplayType = PortElement.portType = displayType;
            CeresPort.AssignValueType(displayType);
            SetPortTooltip(Binding.Tooltip);
        }
    }

    public static class PortViewFactory
    {
        public static CeresPortView CreateInstance(FieldInfo fieldInfo, CeresNodeView nodeView, CeresPortData portData = null)
        {
            Assert.IsNotNull(fieldInfo);
            return new CeresPortView(CeresPortViewBinding.BindField(fieldInfo), nodeView, 
                portData ?? CeresPortData.FromFieldInfo(fieldInfo));
        }
        
        public static CeresPortView CreateInstance(FieldInfo portFieldInfo, FieldInfo valueFieldInfo, CeresNodeView nodeView, CeresPortData portData = null)
        {
            Assert.IsNotNull(portFieldInfo);
            Assert.IsNotNull(valueFieldInfo);
            return new CeresPortView(CeresPortViewBinding.BindField(portFieldInfo, valueFieldInfo), 
                nodeView, portData ?? CeresPortData.FromFieldInfo(valueFieldInfo));
        }
        
        public static CeresPortView CreateInstance(FieldInfo portFieldInfo, ParameterInfo parameterInfo, CeresNodeView nodeView, CeresPortData portData = null)
        {
            Assert.IsNotNull(portFieldInfo);
            Assert.IsNotNull(parameterInfo);
            return new CeresPortView(CeresPortViewBinding.BindParameter(portFieldInfo, parameterInfo), 
                nodeView, portData ?? CeresPortData.FromParameterInfo(parameterInfo));
        }
    }

    public static class PortViewExtensions
    {
        /// <summary>
        /// Hide port in node view
        /// </summary>
        /// <param name="portView"></param>
        public static void HidePort(this CeresPortView portView)
        {
            portView.PortElement.style.display = DisplayStyle.None;
        }
    }
}