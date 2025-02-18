using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.Graph;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UObject = UnityEngine.Object;
using USearchWindow = UnityEditor.Experimental.GraphView.SearchWindow;
namespace Ceres.Editor.Graph
{
    public abstract class CeresGraphView: GraphView, IVariableSource
    {
        public List<SharedVariable> SharedVariables { get; } = new();
        
        public CeresGraphEditorWindow EditorWindow { get; set; }
        
        public CeresBlackboard Blackboard { get; private set; }
        
        public NodeGroupHandler NodeGroupHandler { get; private set; }

        public ContextualMenuRegistry ContextualMenuRegistry { get; } = new();

        public HashSet<ICeresNodeView> NodeViews { get; } = new();
        
        public CeresNodeSearchWindow SearchWindow { get; private set; }

        private static readonly Dictionary<string, StyleSheet> StyleSheetsCache = new();

        protected CeresGraphView(CeresGraphEditorWindow editorWindow)
        {
            EditorWindow = editorWindow;
            style.flexGrow = 1;
            style.flexShrink = 1;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            Insert(0, new GridBackground());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new FreehandSelector());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new DragDropManipulator(this, OnDragDropObjectPerform, OnDragDropElementPerform));
            canPasteSerializedData = CanPasteSerializedGraph;
            serializeGraphElements = OnCopySerializedGraph;
            unserializeAndPaste = OnPasteSerializedGraph;
            nodeCreationRequest = OnNodeCreationRequest;
            RegisterCallback<DetachFromPanelEvent>(OnGraphViewDestroy);
        }

        protected virtual void OnNodeCreationRequest(NodeCreationContext context)
        {
            OpenSearch(context.screenMousePosition);
        }

        protected virtual void OnDragDropObjectPerform(UObject data, Vector3 mousePosition)
        {
        }
        
        protected virtual void OnDragDropElementPerform(List<ISelectable> selectables,GraphElement graphElement, Vector3 mousePosition)
        {
        }
        
        protected virtual bool CanPasteSerializedGraph(string serializedData)
        {
            return true;
        }
        
        protected virtual string OnCopySerializedGraph(IEnumerable<GraphElement> elements)
        {
            return string.Empty;
        }

        protected virtual void OnPasteSerializedGraph(string operationName, string serializedData)
        {
            
        }

        /// <summary>
        /// Add custom blackboard to graph
        /// </summary>
        /// <param name="blackboard"></param>
        public void AddBlackboard(CeresBlackboard blackboard)
        {
            AddBlackboard(blackboard, new Rect(20, 70, 250, 400));
        }
        
        /// <summary>
        /// Add custom blackboard to graph
        /// </summary>
        /// <param name="blackboard"></param>
        /// <param name="rect"></param>
        public void AddBlackboard(CeresBlackboard blackboard, Rect rect)
        {
            Blackboard = blackboard;
            Blackboard.SetPosition(rect);
            Add(blackboard);
        }

        /// <summary>
        /// Add custom node search window to graph
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void AddSearchWindow<T>() where T : CeresNodeSearchWindow
        {
            SearchWindow = ScriptableObject.CreateInstance<T>();
        }
        
        /// <summary>
        /// Add custom <see cref="NodeGroupHandler"/> to graph
        /// </summary>
        /// <param name="groupHandler"></param>
        public void AddNodeGroupHandler(NodeGroupHandler groupHandler)
        {
            UnregisterCallback<KeyDownEvent>(OnGroupKeyDown);
            NodeGroupHandler = groupHandler;
            if(groupHandler != null)
            {
                RegisterCallback<KeyDownEvent>(OnGroupKeyDown);
            }
        }

        private void OnGroupKeyDown(KeyDownEvent evt)
        {
            if (evt.ctrlKey) return;
            if (evt.keyCode != KeyCode.C) return;
            if (evt.altKey)
            {
                NodeGroupHandler.DoUnGroup();
            }
            else
            {
                NodeGroupHandler.DoGroup();
            }
        }

        /// <summary>
        /// Add custom <see cref="StyleSheet"/> to graph
        /// </summary>
        /// <param name="resourcePath"></param>
        public void AddStyleSheet(string resourcePath)
        {
            styleSheets.Add(GetOrLoadStyleSheet(resourcePath));
        }
        
        /// <summary>
        /// Get or load custom <see cref="StyleSheet"/>
        /// </summary>
        /// <param name="resourcePath"></param>
        /// <returns></returns>
        public static StyleSheet GetOrLoadStyleSheet(string resourcePath)
        {
            if (!StyleSheetsCache.TryGetValue(resourcePath, out var styleSheet))
            {
                styleSheet = Resources.Load<StyleSheet>(resourcePath);
                StyleSheetsCache.Add(resourcePath, styleSheet);
            }

            return styleSheet;
        }
        
        /// <summary>
        /// Add custom node view to graph
        /// </summary>
        /// <param name="nodeView"></param>
        public virtual void AddNodeView(ICeresNodeView nodeView)
        {
            NodeViews.Add(nodeView);
            AddElement(nodeView.NodeElement);
            
            /* Sync node view lifetime scope with NodeElement */
            nodeView.NodeElement.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                NodeViews.Remove(nodeView);
            });
            nodeView.NodeElement.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                NodeViews.Add(nodeView);
            });
        }

        /// <summary>
        /// Find node by guid
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public ICeresNodeView FindNodeView(string guid)
        {
            return NodeViews.FirstOrDefault(x => x.Guid == guid);
        }

        /// <summary>
        /// Find generic node by guid
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public TNodeView FindNodeView<TNodeView>(string guid) where TNodeView: class, ICeresNodeView
        {
            return NodeViews.FirstOrDefault(x => x.Guid == guid) as TNodeView;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            if (startPort is not CeresPortElement startPortView)
            {
                CeresLogger.LogWarning($"{startPort.GetType()} is not supported in Ceres default graph view");
                return compatiblePorts;
            }

            ports.ForEach(port =>
            {
                if (port is CeresPortElement portElement && portElement.IsCompatibleTo(startPortView))
                {
                    compatiblePorts.Add(portElement);
                }
            });

            return compatiblePorts;
        }

        /// <summary>
        /// Add shared variables to graph's blackboard
        /// </summary>
        /// <param name="variables"></param>
        /// <param name="duplicateWhenConflict"></param>
        public virtual void AddSharedVariables(List<SharedVariable> variables, bool duplicateWhenConflict)
        {
            if(Blackboard == null) return;
            foreach (var variable in variables.Where(variable => variable != null &&
                                                                 (duplicateWhenConflict 
                                                                  || SharedVariables.All(x => x.Name != variable.Name))))
            {
                // In play mode, use original variable to observe value change
                Blackboard.AddVariable(Application.isPlaying ? variable : variable.Clone(), false);
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            ContextualMenuRegistry.BuildContextualMenu(ContextualMenuType.Graph, evt);
        }

        /// <summary>
        /// Open node search window
        /// </summary>
        /// <param name="screenPosition"></param>
        public virtual void OpenSearch(Vector2 screenPosition)
        {
            var settings = NodeSearchContext.Default;
            settings.AllowGeneric = true;
            SearchWindow.Initialize(this, settings);
            USearchWindow.Open(new SearchWindowContext(screenPosition), SearchWindow);
        }
        
        /// <summary>
        /// Open node search window with input port
        /// </summary>
        /// <param name="screenPosition"></param>
        /// <param name="portView"></param>
        public virtual void OpenSearch(Vector2 screenPosition, CeresPortView portView)
        {
            var settings = NodeSearchContext.Default;
            settings.AllowGeneric = true;
            settings.ParameterType = portView.PortElement.portType; /* Final display type */
            settings.RequestPortView = portView;
            SearchWindow.Initialize(this, settings);
            USearchWindow.Open(new SearchWindowContext(screenPosition), SearchWindow);
        }

        private void OnGraphViewDestroy(DetachFromPanelEvent evt)
        {
            OnDestroy();
        }

        /// <summary>
        /// Called after detached from editor window
        /// </summary>
        protected virtual void OnDestroy()
        {
            
        }
    }

    public static class CeresGraphViewExtensions
    {
        /// <summary>
        /// Add custom node view to graph with world rect
        /// </summary>
        /// <param name="graphView"></param>
        /// <param name="node"></param>
        /// <param name="worldRect"></param>
        public static void AddNodeView(this CeresGraphView graphView ,ICeresNodeView node, Rect worldRect)
        {
            node.NodeElement.SetPosition(worldRect);
            graphView.AddNodeView(node);
        }

        /// <summary>
        /// Convert screen position to graph view local position
        /// </summary>
        /// <param name="graphView"></param>
        /// <param name="mousePosition"></param>
        /// <returns></returns>
        public static Vector2 Screen2GraphPosition(this CeresGraphView graphView, Vector2 mousePosition)
        {
            var worldMousePosition = graphView.EditorWindow.rootVisualElement.ChangeCoordinatesTo(graphView.EditorWindow.rootVisualElement.parent,mousePosition - graphView.EditorWindow.position.position);
            var localMousePosition = graphView.contentViewContainer.WorldToLocal(worldMousePosition);
            return localMousePosition;
        }
        
        /// <summary>
        /// Get container object of this graph
        /// </summary>
        /// <param name="graphView"></param>
        /// <returns></returns>
        public static UObject GetContainerObject(this CeresGraphView graphView)
        {
            return graphView.EditorWindow.Container.Object;
        }
        
        /// <summary>
        /// Get container type of this graph
        /// </summary>
        /// <param name="graphView"></param>
        /// <returns></returns>
        public static Type GetContainerType(this CeresGraphView graphView)
        {
            return graphView.EditorWindow.GetContainerType();
        }

        /// <summary>
        /// Connect input port to output port
        /// </summary>
        /// <param name="graphView"></param>
        /// <param name="input"></param>
        /// <param name="output"></param>
        public static void ConnectPorts(this CeresGraphView graphView, CeresPortView input, CeresPortView output)
        {
            var edge = new CeresEdge
            {
                input = input.PortElement,
                output = output.PortElement,
            };
            graphView.AddElement(edge);
            input.PortElement.Connect(edge);
            output.PortElement.Connect(edge);
        }
    }
}