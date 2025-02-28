using System.Linq;
using Ceres.Graph;
using Ceres.Graph.Flow;
using UnityEngine;

namespace Ceres.Editor.Graph.Flow
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
        
        private IFlowGraphContainer _container;

        private FlowGraph _instance;

        public FlowGraph GraphInstance
        {
            get
            {
                if (_instance == null)
                {
                    if (Application.isPlaying && _container is IFlowGraphRuntime runtimeContainer)
                    {
                        _instance = runtimeContainer.GetRuntimeFlowGraph();
                    }
                    else
                    {
                        _instance = _container.GetFlowGraph();
                    }
                    
                    /* Append custom function variables */
                    _instance.variables.AddRange(
                        _instance.SubGraphSlots
                                    .OfType<FlowSubGraphSlot>()
                                    .Where(x=> x.Usage == FlowGraphUsage.Function)
                                    .Select(x=> new CustomFunction(x.Name)
                                    {
                                        Value = x.Guid
                                    })
                        );
                    Update();
                }

                return _instance;
            }
        }
        
        public GUIContent[] GraphNameContents  { get; private set; }

        public string[] GraphNames { get; private set; }

        public static FlowGraphEditorObject CreateTemporary(IFlowGraphContainer container)
        {
            var editorObject = CreateInstance<FlowGraphEditorObject>();
            editorObject._container = container;
            editorObject.hideFlags = HideFlags.HideAndDontSave;
            /* Use clone instead of modifying persistent data */
            editorObject.GraphData = container.GetFlowGraphData().CloneT<FlowGraphData>();
            return editorObject;
        }

        public void Update()
        {
            var contents =  GraphInstance.SubGraphSlots.Select(x => x.Name).ToList();
            /* Slot 0 use uber graph name */
            contents.Insert(0, $"{_container.GetIdentifier().boundObject.name} (Main)");
            GraphNames = contents.ToArray();
            GraphNameContents = contents.Select(x => new GUIContent(x)).ToArray();
        }
        
        public void DestroyTemporary()
        {
            DestroyImmediate(this);
        }
    }
}
