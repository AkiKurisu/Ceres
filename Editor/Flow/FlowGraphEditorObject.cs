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

        private FlowGraph _instance;

        public FlowGraph GraphInstance
        {
            get
            {
                if (_instance == null)
                {
                    if (Application.isPlaying && Container is IFlowGraphRuntime runtimeContainer)
                    {
                        _instance = runtimeContainer.GetRuntimeFlowGraph();
                    }
                    else
                    {
                        _instance = Container.GetFlowGraph();
                    }
                    Update();
                }

                return _instance;
            }
        }
        
        public GUIContent[] GraphNameContents  { get; private set; }

        public string[] GraphNames { get; private set; }

        public IFlowGraphContainer Container { get; private set; }

        public static FlowGraphEditorObject CreateTemporary(IFlowGraphContainer container)
        {
            var editorObject = CreateInstance<FlowGraphEditorObject>();
            editorObject.Container = container;
            editorObject.hideFlags = HideFlags.HideAndDontSave;
            editorObject.GraphData = container.GetFlowGraphData();
            return editorObject;
        }

        private void Update()
        {
            var contents =  GraphInstance.SubGraphSlots.Select(x => x.Name).ToList();
            /* Slot 0 use uber graph name */
            contents.Insert(0, $"{Container.GetIdentifier().boundObject.name} (Main)");
            GraphNames = contents.ToArray();
            GraphNameContents = contents.Select(x => new GUIContent(x)).ToArray();
        }
        
        public void DestroyTemporary()
        {
            DestroyImmediate(this);
        }

        /// <summary>
        /// Create a new subGraph to present custom function
        /// </summary>
        public void AddNewFunctionSubGraph()
        {
            var json = Resources.Load<TextAsset>("Ceres/Flow/TemplateSubGraphData").text;
            var templateSubGraph = new FlowGraph(JsonUtility.FromJson<FlowGraphSerializedData>(json));
            var uniqueName = "New Function";
            int id = 1;
            while (GraphNames.Contains(uniqueName))
            {
                uniqueName = $"New Function {id++}";
            }
            GraphInstance.AddSubGraph(uniqueName, templateSubGraph);
            Update();
        }
    }
}
