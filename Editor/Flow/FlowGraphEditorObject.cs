using System.Collections.Generic;
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
                    if (_instance.SubGraphSlots != null)
                    {
                        _instance.variables.AddRange(
                            _instance.SubGraphSlots
                                .OfType<FlowSubGraphSlot>()
                                .Where(x => x.Usage == FlowGraphUsage.Function)
                                .Select(x => new LocalFunction(x.Name)
                                {
                                    Value = x.Guid
                                })
                        );
                    }
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
            /* Slot 0 use uber graph name */
            var contents = new List<string>{ $"{_container.GetIdentifier().boundObject.name} (Main)" };
            if (GraphInstance.SubGraphSlots != null)
            {
                contents.AddRange(GraphInstance.SubGraphSlots.Select(x => x.Name));   
            }
            GraphNames = contents.ToArray();
            GraphNameContents = contents.Select(x => new GUIContent(x)).ToArray();
        }
        
        public void DestroyTemporary()
        {
            DestroyImmediate(this);
        }
    }
}
