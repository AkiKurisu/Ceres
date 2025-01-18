using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Editor;
using Ceres.Editor.Graph;
using Chris;
using Chris.Serialization;
using R3;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
using UObject = UnityEngine.Object;
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
            foreach (var edgeToCreate in edgesToCreate)
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
            if (type.IsSubclassOf(typeof(UObject)) && type != typeof(Component) && type != typeof(GameObject))
            {
                AddToClassList("typeObject");
            }
            if (type.IsGenericType)
            {
                AddToClassList("type" + type.GetGenericTypeDefinition().Name.Split('`')[0]);
            }
            tooltip = CreatePortTooltip(type);
            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
        }

        protected virtual void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.ClearItems();
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Disconnect", _ =>
            {
                var edge = connections.First();
                edge.input.Disconnect(edge);
                edge.output.Disconnect(edge);
                View.NodeOwner.GraphView.DeleteElements(new []{ edge });
            }, _ => connections.Any() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Hidden));
            View.NodeOwner.GraphView.ContextualMenuRegistry.BuildContextualMenu(ContextualMenuType.Port, evt, portType);
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
                portName = ownerView.Binding.DisplayName.Value,
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

        /// <summary>
        /// Whether this can connect other ports
        /// </summary>
        /// <returns></returns>
        public bool IsConnectable()
        {
            /* Can not connect when invisible */
            if (style.display == DisplayStyle.None) return false;
            return !connected || capacity != Capacity.Single;
        }

        /// <summary>
        /// Whether this can connect other port with value compatible validation
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool CanConnect(CeresPortElement other)
        {
            return IsConnectable() && IsCompatibleTo(other);
        }
        
        /// <summary>
        /// Whether this port's value is compatible to other port's value
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool IsCompatibleTo(CeresPortElement other)
        {
            if (other.direction == direction) return false;
            if (other.portType == portType) return true;
            if (direction == Direction.Input)
            {
                return other.View.Binding.IsCompatibleTo(portType);
            }
            return View.Binding.IsCompatibleTo(other.portType);
        }

        public void SetEditorFieldVisible(bool isVisible)
        {
            if(EditorField == null) return;
            
            if(isVisible)
            {
                if(!m_ConnectorBox.parent.Contains(EditorField))
                {
                    m_ConnectorBox.parent.Add(EditorField);
                }
            }
            else
            {
                if(m_ConnectorBox.parent.Contains(EditorField))
                {
                    m_ConnectorBox.parent.Remove(EditorField);
                }
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
        
        public ReactiveProperty<Type> DisplayType { get; }

        public ReactiveProperty<string> DisplayName { get; }

        public Type CeresPortType { get; private set; }
        
        public ReactiveProperty<string> Tooltip { get; } = new(string.Empty);

        private const string DefaultOutputPortName = "Return Value";

        public CeresPortViewBinding(string defaultDisplayName, Type defaultDisplayType)
        {
            DisplayName = new ReactiveProperty<string>(defaultDisplayName);
            DisplayType = new ReactiveProperty<Type>(defaultDisplayType);
            DisplayType.Subscribe(x =>
            {
                CeresPortType = typeof(CeresPort<>).MakeGenericType(x);
                CeresPort.AssignValueType(x);
                Tooltip.OnNext(Tooltip.Value);
            });
        }
        
        public void BindView(CeresPortElement portElement)
        {
            DisplayType.Subscribe(x =>
            {
                portElement.portType = x;
            });
            DisplayName.Subscribe(x => portElement.portName = x);
            Tooltip.Subscribe(x => portElement.tooltip = CeresPortElement.CreatePortTooltip(DisplayType.Value) + x);
        }

        public static CeresPortViewBinding BindField(CeresPortData portData, FieldInfo portFieldInfo, FieldInfo resolvedFieldInfo = null)
        {
            resolvedFieldInfo ??= portFieldInfo;
            return new CeresPortViewBinding(CeresLabel.GetLabel(resolvedFieldInfo), portData.GetValueType())
            {
                PortFieldInfo = portFieldInfo,
                ResolvedFieldInfo = resolvedFieldInfo,
                BindingType = PortBindingType.Field
            };
        }
        
        public static CeresPortViewBinding BindParameter(CeresPortData portData, FieldInfo portFieldInfo, ParameterInfo parameterInfo)
        {
            string displayName;
            /* Case when parameter is return parameter */
            if(string.IsNullOrEmpty(parameterInfo.Name))
            {
                displayName = DefaultOutputPortName;
            }
            else
            {
               
                displayName = CeresLabel.GetLabel(parameterInfo.Name); 
            }
            return new CeresPortViewBinding(displayName, portData.GetValueType())
            {
                PortFieldInfo = portFieldInfo,
                ResolvedParameterInfo = parameterInfo,
                BindingType = PortBindingType.Parameter
            };
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

        public bool IsCompatibleTo(Type type)
        {
            return CeresPort.IsCompatibleTo(DisplayType.Value, type);
        }
        
        public bool IsRemappedFieldPort()
        {
            return BindingType == PortBindingType.Field && PortFieldInfo != ResolvedFieldInfo;
        }

        public bool IsHideInGraphEditor()
        {
            if (BindingType == PortBindingType.Parameter) return true;
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
            if(BindingType != PortBindingType.Field) return;
            
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
            NodeOwner = nodeView;
            
            /* Create editor field */
            bool isInput = binding.GetDirection() == Direction.Input && !binding.IsRemappedFieldPort();
            if(isInput && !binding.IsHideInGraphEditor() && IsPortSupportValueField(portData.GetValueType()))
            {
                FieldResolver = FieldResolverFactory.Get().Create(binding.GetPortValueField());
            }
            else
            {
                FieldResolver = null;
            }
            
            /* Bind visual element */
            PortElement = CeresPortElement.Create(this);
            Binding.BindView(PortElement);
        }

        private static bool IsPortSupportValueField(Type type)
        {
            if (type == typeof(string)) return true;
            if (type.IsEnum) return true;
            
            if (ReflectionUtility.IsSerializableNumericTypes(type)) return true;
            if (ReflectionUtility.IsUnityBuiltinTypes(type)) return true;
            if (type.IsSubclassOf(typeof(UObject)))
            {
                return true;
            }
            
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
                if(port != null && port.PortElement.IsConnectable())
                {
                    NodeOwner.GraphView.ConnectPorts(port, this);
                }
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
        
        /// <summary>
        /// Set display name and default value from <see cref="ParameterInfo"/>
        /// </summary>
        /// <param name="parameterInfo"></param>
        public void SetDisplayDataFromParameterInfo(ParameterInfo parameterInfo)
        {
            if(Binding.BindingType == CeresPortViewBinding.PortBindingType.Field)
            {
                /* Set default value if exist */
                if (FieldResolver != null && parameterInfo.HasDefaultValue)
                {
                    FieldResolver.Value = parameterInfo.DefaultValue;
                }
                SetDisplayName(parameterInfo.Name);
                bool fieldShowInEditor = parameterInfo.GetCustomAttribute<HideInGraphEditorAttribute>() == null;
                PortElement.SetEditorFieldVisible(fieldShowInEditor);
            }
            else
            {
                CeresGraph.LogWarning($"Port can only be remapped from {nameof(ParameterInfo)} in {CeresPortViewBinding.PortBindingType.Field} binding");
            }
        }

        public void SetDisplayName(string displayName)
        {
            Binding.DisplayName.Value = CeresLabel.GetLabel(displayName);
        }
        
        public void SetTooltip(string tooltip)
        {
            Binding.Tooltip.Value = tooltip;
        }
        
        public void SetDisplayType(Type displayType)
        {
            Binding.DisplayType.Value = displayType;
        }
    }

    public static class PortViewFactory
    {
        public static CeresPortView CreateInstance(FieldInfo fieldInfo, CeresNodeView nodeView, CeresPortData portData = null)
        {
            Assert.IsNotNull(fieldInfo);
            portData ??= CeresPortData.FromFieldInfo(fieldInfo);
            return new CeresPortView(CeresPortViewBinding.BindField(portData, fieldInfo), nodeView, portData);
        }
        
        public static CeresPortView CreateInstance(FieldInfo portFieldInfo, FieldInfo valueFieldInfo, CeresNodeView nodeView, CeresPortData portData = null)
        {
            Assert.IsNotNull(portFieldInfo);
            Assert.IsNotNull(valueFieldInfo);
            portData ??= CeresPortData.FromFieldInfo(valueFieldInfo);
            return new CeresPortView(CeresPortViewBinding.BindField(portData, portFieldInfo, valueFieldInfo), nodeView, portData);
        }
        
        public static CeresPortView CreateInstance(FieldInfo portFieldInfo, ParameterInfo parameterInfo, CeresNodeView nodeView, CeresPortData portData = null)
        {
            Assert.IsNotNull(portFieldInfo);
            Assert.IsNotNull(parameterInfo);
            portData ??= CeresPortData.FromParameterInfo(parameterInfo);
            return new CeresPortView(CeresPortViewBinding.BindParameter(portData, portFieldInfo, parameterInfo), nodeView, portData);
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
        
        /// <summary>
        /// Whether port is visible
        /// </summary>
        /// <param name="portView"></param>
        /// <returns></returns>
        public static bool IsVisible(this CeresPortView portView)
        {
            return portView.PortElement.style.display == DisplayStyle.Flex;
        }
    }
}