using Ceres.Graph;
using UnityEngine;

namespace Ceres.Editor.Graph
{
    public static class CeresGraphEditorExtensions
    {
        /// <summary>
        /// Calculate paste offset based on graph centroid and target mouse position
        /// </summary>
        public static Vector2 CalculatePasteOffset(this CeresGraph graph, Vector2 targetMousePosition)
        {
            var centroid = graph.CalculateGraphCentroid();
            return targetMousePosition - centroid;
        }

        /// <summary>
        /// Calculate centroid (average position) of all graph nodes
        /// </summary>
        private static Vector2 CalculateGraphCentroid(this CeresGraph graph)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;

            // Sum up all node positions
            foreach (var nodeInstance in graph.nodes)
            {
                sum += nodeInstance.GraphPosition.position;
                count++;
            }

            // Include relay nodes in centroid calculation
            if (graph.relayNodes != null)
            {
                foreach (var relayNode in graph.relayNodes)
                {
                    sum += relayNode.graphPosition.position;
                    count++;
                }
            }

            // Return average position (centroid)
            return count > 0 ? sum / count : Vector2.zero;
        }

        /// <summary>
        /// Apply offset to all graph elements (nodes and node groups)
        /// </summary>
        public static void ApplyOffsetToGraph(this CeresGraph graph, Vector2 offset)
        {
            // Apply offset to all nodes
            foreach (var nodeInstance in graph.nodes)
            {
                var rect = nodeInstance.GraphPosition;
                rect.x += offset.x;
                rect.y += offset.y;
                nodeInstance.GraphPosition = rect;
            }

            // Apply offset to all node groups
            foreach (var nodeGroup in graph.nodeGroups)
            {
                var rect = nodeGroup.position;
                rect.x += offset.x;
                rect.y += offset.y;
                nodeGroup.position = rect;
            }
        }
    }
}