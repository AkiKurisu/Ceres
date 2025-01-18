using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Utilities;
using Ceres.Annotations;
using Ceres.Graph;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using NodeElement = UnityEditor.Experimental.GraphView.Node;
namespace Ceres.Editor.Graph
{
    /// <summary>
    /// Interface for node views managed by <see cref="CeresGraphView"/>
    /// </summary>
    public interface ICeresNodeView
    {
        /// <summary>
        /// Graph scope node view guid
        /// </summary>
        public string Guid { get; }
        /// <summary>
        /// Node visual element of this view
        /// </summary>
        public NodeElement NodeElement { get; }
    }
    
    public abstract class CeresNodeView: ICeresNodeView
    {
        /// <summary>
        /// Graph scope node view guid
        /// </summary>
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
        
        /// <summary>
        /// Node visual element of this view
        /// </summary>
        public NodeElement NodeElement { get; private set; }
        
        /// <summary>
        /// The graph this view attached to
        /// </summary>
        public CeresGraphView GraphView { get; private set; }
        
        /// <summary>
        /// Node instance type contained by this view
        /// </summary>
        public Type NodeType { get; private set; }
        
        /// <summary>
        /// Node instance contained by this view
        /// </summary>
        public CeresNode NodeInstance { get; private set; }

        /// <summary>
        /// Node instance visible <see cref="IFieldResolver"/>
        /// </summary>
        protected readonly List<IFieldResolver> FieldResolvers = new();

        /// <summary>
        /// Node instance visible <see cref="FieldInfo"/>
        /// </summary>
        protected readonly List<FieldInfo> FieldInfos = new();

        /// <summary>
        /// Node port views
        /// </summary>
        protected readonly List<CeresPortView> PortViews = new();
        
        /// <summary>
        /// Default constructor without initialization, please initialize node view in implementation
        /// </summary>
        protected CeresNodeView()
        {
            
        }
        
        /// <summary>
        /// Initialize node view
        /// </summary>
        /// <param name="nodeType"></param>
        /// <param name="graphView"></param>
        public void Initialize(Type nodeType, CeresGraphView graphView)
        {
            SetNodeInstanceType(nodeType);
            SetGraphOwner(graphView);
        }

        /// <summary>
        /// Set node element of this view
        /// </summary>
        /// <param name="nodeElement"></param>
        public void SetupNodeElement(NodeElement nodeElement)
        {
            NodeElement = nodeElement;
            var styles = CeresMetadata.GetMetadata(NodeType, "style");
            foreach (var style in styles)
            {
                NodeElement.AddToClassList(style);
            }
            OnSetupNodeElement();
        }

        /// <summary>
        /// Called in graph view initialize node style stage
        /// </summary>
        /// <returns>Visual node element of this view</returns>
        protected virtual void OnSetupNodeElement()
        {
            
        }

        /// <summary>
        /// Set node view's <see cref="GraphView"/>
        /// </summary>
        /// <param name="graphView"></param>
        public void SetGraphOwner(CeresGraphView graphView)
        {
            GraphView = graphView;
            OnSetGraphView();
        }
        
        /// <summary>
        /// Called after graph view setup
        /// </summary>
        protected virtual void OnSetGraphView()
        {
            
        }
        
        /// <summary>
        /// Set node view's <see cref="NodeType"/>
        /// </summary>
        /// <param name="nodeType"></param>
        public void SetNodeInstanceType(Type nodeType)
        {
            NodeType = nodeType;
            OnSetNodeInstanceType();
        }
        
        /// <summary>
        /// Called after node setup or change node instance type
        /// </summary>
        protected virtual void OnSetNodeInstanceType()
        {
            
        }

        /// <summary>
        /// Find port view with property name if existed
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="portIndex"></param>
        /// <returns></returns>
        public CeresPortView FindPortView(string propertyName, int portIndex = 0)
        {
            return PortViews.FirstOrDefault(x => x.Binding.GetPortName() == propertyName && x.PortData.arrayIndex == portIndex);
        }
        
        /// <summary>
        /// Find port view with display name if existed
        /// </summary>
        /// <param name="displayName"></param>
        /// <param name="portIndex"></param>
        /// <returns></returns>
        public CeresPortView FindPortViewWithDisplayName(string displayName, int portIndex = 0)
        {
            return PortViews.FirstOrDefault(x => x.Binding.DisplayName.Value == displayName && x.PortData.arrayIndex == portIndex);
        }
        
        /// <summary>
        /// Find port view with display type if existed
        /// </summary>
        /// <param name="displayType"></param>
        /// <param name="portIndex"></param>
        /// <returns></returns>
        public CeresPortView FindPortViewWithDisplayType(Type displayType, int portIndex = 0)
        {
            return PortViews.FirstOrDefault(x => x.Binding.DisplayType.Value == displayType && x.PortData.arrayIndex == portIndex);
        }

        /// <summary>
        /// Find port view that is compatible to connect
        /// </summary>
        /// <param name="portView"></param>
        /// <returns></returns>
        public CeresPortView FindConnectablePortView(CeresPortView portView)
        {
            return PortViews.FirstOrDefault(x => x.PortElement.CanConnect(portView.PortElement));
        }
        
        /// <summary>
        /// Get all port views
        /// </summary>
        /// <returns></returns>
        public CeresPortView[] GetAllPortViews()
        {
            return PortViews.ToArray();
        }
        
        /// <summary>
        /// Find field resolver with field name if existed
        /// </summary>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public IFieldResolver FindFieldResolver(string fieldName)
        {
            var index = FieldInfos.FindIndex(x => x.Name == fieldName);
            return index != -1 ? FieldResolvers[index] : null;
        }
        
        public T FindFieldResolver<T>(string fieldName) where T: class, IFieldResolver
        {
            return FindFieldResolver(fieldName) as T;
        }
        
        /// <summary>
        /// Fill node element with default properties
        /// </summary>
        protected void FillDefaultNodeProperties()
        {
            if(NodeType == null || GraphView == null || NodeElement == null) return;

            var fieldContainer = new VisualElement();
            var nodeTemplate = Activator.CreateInstance(NodeType) as CeresNode;
            NodeType.GetGraphEditorPropertyFields()
            .ForEach((p) =>
            {
                try
                {
                    var fieldResolver = FieldResolverFactory.Get().Create(p);
                    fieldResolver.Restore(nodeTemplate);
                    var editorField = fieldResolver.GetField(GraphView);
                    fieldContainer.Add(editorField);
                    FieldResolvers.Add(fieldResolver);
                    FieldInfos.Add(p);
                }
                catch(Exception e)
                {
                    CeresGraph.LogWarning($"Can not draw property {NodeType.Name}.{p.Name}, {e}");
                }
            });
            NodeElement.mainContainer.Add(fieldContainer);
        }

        /// <summary>
        /// Fill node element with default title
        /// </summary>
        protected void FillDefaultNodeTitle()
        {
            var title = CeresLabel.GetLabel(NodeType);
            if (NodeType.IsGenericType)
            {
                var definitionType = NodeType.GetGenericTypeDefinition();
                var template = GenericNodeTemplateRegistry.GetTemplate(definitionType);
                if(template != null)
                {
                    NodeElement.title = template.GetGenericNodeName(title, NodeType.GetGenericArguments());
                    return;
                }
            }
            NodeElement.title = title;
        }

        /// <summary>
        /// Fill node element with default ports
        /// </summary>
        protected void FillDefaultNodePorts()
        {
            if(NodeType == null || GraphView == null || NodeElement == null) return;
            
            NodeType.GetGraphEditorPortFields()
            .ForEach((p) =>
            {
                try
                {
                    if(!p.FieldType.IsArray)
                    {
                        AddPortView(PortViewFactory.CreateInstance(p, this));
                    }
                }
                catch(Exception e)
                {
                    CeresGraph.LogWarning($"Can not draw port {NodeType.Name}.{p.Name}, {e}");
                }
            });
        }

        protected Type GetContainerType()
        {
            return GraphView.GetContainerType();
        }

        public virtual void AddPortView(CeresPortView portView)
        {
            if (portView.PortElement.direction == Direction.Input)
            {
                NodeElement.inputContainer.Add(portView.PortElement);
            }
            else
            {
                NodeElement.outputContainer.Add(portView.PortElement);
            }
            PortViews.Add(portView);
        }

        public virtual void RemovePortView(CeresPortView portView)
        {
            portView.PortElement?.RemoveFromHierarchy();
            PortViews.Remove(portView);
        }

        /// <summary>
        /// Set node instance attached to this view and restore all properties
        /// </summary>
        /// <param name="ceresNode"></param>
        public virtual void SetNodeInstance(CeresNode ceresNode)
        {
            NodeInstance = ceresNode;
            foreach (var resolver in FieldResolvers)
            {
                resolver.Restore(ceresNode);
            }
            Guid = ceresNode.Guid;
            NodeElement.SetPosition(ceresNode.GraphPosition);
            PortViews.ForEach(x =>
            {
                x.Restore(ceresNode, ceresNode.NodeData.FindPortData(x.PortData.propertyName, x.PortData.arrayIndex));
            });
        }

        /// <summary>
        /// Reconnect edge from current metadata
        /// </summary>
        public void ReconnectEdges()
        {
            PortViews.ForEach(x=> x.Connect());
        }
    }
}