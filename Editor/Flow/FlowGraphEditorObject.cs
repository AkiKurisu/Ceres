using System.Linq;
using Ceres.Graph;
using Ceres.Graph.Flow;
using UnityEngine;

namespace Ceres.Editor
{
    /// <summary>
    /// Editor flow graph data container
    /// </summary>
    public class FlowGraphEditorObject : ScriptableObject
    {
        /// <summary>
        /// Undoable editor time graph data
        /// </summary>
        [field: SerializeField]
        public FlowGraphData GraphData { get; set; }
        
        public FlowGraph GraphInstance { get; private set; }
        
        public GUIContent[] GraphNameContents  { get; private set; }

        public string[] GraphNames { get; private set; }
        
        public static FlowGraphEditorObject CreateTemporary(IFlowGraphContainer container)
        {
            var editorObject = CreateInstance<FlowGraphEditorObject>();
            editorObject.hideFlags = HideFlags.HideAndDontSave;
            editorObject.GraphData = container.GetFlowGraphData();
            if (Application.isPlaying && container is IFlowGraphRuntime runtimeContainer)
            {
                editorObject.GraphInstance = runtimeContainer.GetRuntimeFlowGraph();
            }
            else
            {
                editorObject.GraphInstance = container.GetFlowGraph();
            }
            var contents =  editorObject.GraphInstance.SubGraphSlots.Select(x => x.Name).ToList();
            /* Slot 0 use uber graph name */
            contents.Insert(0, $"{container.GetIdentifier().boundObject.name} (Main)");
            editorObject.GraphNames = contents.ToArray();
            editorObject.GraphNameContents = contents.Select(x => new GUIContent(x)).ToArray();
            return editorObject;
        }
        
        public void DestroyTemporary()
        {
            DestroyImmediate(this);
        }
    }
}
