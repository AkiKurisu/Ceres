using System;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UObject = UnityEngine.Object;

namespace Ceres.Editor.Graph
{
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
        /// Connect port to port by direction
        /// </summary>
        /// <param name="graphView"></param>
        /// <param name="portLeft"></param>
        /// <param name="portRight"></param>
        public static void ConnectPorts(this CeresGraphView graphView, CeresPortView portLeft, CeresPortView portRight)
        {
            graphView.ConnectPorts(portLeft.PortElement, portRight.PortElement);
        }
        
        /// <summary>
        /// Connect port to port by direction
        /// </summary>
        /// <param name="graphView"></param>
        /// <param name="portLeft"></param>
        /// <param name="portRight"></param>
        public static void ConnectPorts(this CeresGraphView graphView, CeresPortElement portLeft, CeresPortElement portRight)
        {
            if (portLeft.direction == Direction.Input)
            {
                graphView.ConnectPorts_Internal(portLeft, portRight);
            }
            else
            {
                graphView.ConnectPorts_Internal(portRight, portLeft);
            }
        }
        
        private static void ConnectPorts_Internal(this CeresGraphView graphView, CeresPortElement input, CeresPortElement output)
        {
            var edge = new CeresEdge
            {
                input = input,
                output = output,
            };
            graphView.AddElement(edge);
            input.Connect(edge);
            output.Connect(edge);
        }
        
        /// <summary>
        /// Disconnect port first connection
        /// </summary>
        /// <param name="graphView"></param>
        /// <param name="port"></param>
        public static void DisconnectPort(this CeresGraphView graphView, CeresPortView port)
        {
            graphView.DisconnectPort(port.PortElement);
        }
        
        /// <summary>
        /// Disconnect port first connection
        /// </summary>
        /// <param name="graphView"></param>
        /// <param name="port"></param>
        public static void DisconnectPort(this CeresGraphView graphView, CeresPortElement port)
        {
            var edge = port.connections.First();
            edge.input.Disconnect(edge);
            edge.output.Disconnect(edge);
            graphView.RemoveElement(edge);
        }
    }
}