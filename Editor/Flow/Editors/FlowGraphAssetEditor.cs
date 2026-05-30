using System.Linq;
using Ceres.Graph.Flow;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UEditor = UnityEditor.Editor;

namespace Ceres.Editor.Graph.Flow
{
    [CustomEditor(typeof(FlowGraphAsset), true)]
    public class FlowGraphAssetEditor : UEditor
    {
        /// <summary>
        /// This is a clickable button that will open flow graph editor after being clicked
        /// </summary>
        private class OpenFlowGraphButton : Button
        {
            private const string ButtonText = "Open Flow Graph";

            public OpenFlowGraphButton(IFlowGraphContainer container) : base(() => FlowGraphEditorWindow.Show(container))
            {
                style.fontSize = 15;
                style.unityFontStyleAndWeight = FontStyle.Bold;
                style.color = Color.white;
                style.backgroundColor = new StyleColor(new Color(89 / 255f, 133 / 255f, 141 / 255f));
                text = ButtonText;
                Add(new Image
                {
                    style =
                    {
                        backgroundImage = Resources.Load<Texture2D>("Ceres/editor_icon"),
                        height = 20,
                        width = 20
                    }
                });
                style.height = 25;
            }
        }
        
        private FlowGraphAsset Asset => (FlowGraphAsset)target;

        private SerializedProperty _runtimeType;

        public void OnEnable()
        {
            _runtimeType = serializedObject.FindProperty(nameof(FlowGraphAsset.runtimeType));
        }

        public override VisualElement CreateInspectorGUI()
        {
            var myInspector = new VisualElement();
            myInspector.styleSheets.Add(CeresGraphView.GetOrLoadStyleSheet("Ceres/Flow/FlowGraphAssetInspector"));
            myInspector.AddToClassList("flow-asset-inspector");

            var runtimeTypePropField = new PropertyField(_runtimeType);
            runtimeTypePropField.Bind(serializedObject);
            var runtimeSection = CreateSection("Runtime");
            runtimeSection.Add(runtimeTypePropField);
            myInspector.Add(runtimeSection);

            var blackboardPanel = new BlackboardInspectorPanel(
                () => Asset.GetFlowGraph(),
                () => ((IFlowGraphContainer)Asset).GetFlowGraphData().saveTimestamp, 
                instance =>
                {
                    // Do not serialize data in playing mode
                    if (Application.isPlaying) return;
                    
                    var graphData = ((IFlowGraphContainer)Asset).GetFlowGraphData().CloneT<FlowGraphData>();
                    graphData.variableData = instance.variables.Where(variable => variable is not LocalFunction)
                        .Select(variable => variable.GetSerializedData())
                        .ToArray();
                    Asset.SetGraphData(graphData);
                    EditorUtility.SetDirty(target);
                });
            blackboardPanel.AddToClassList("flow-asset-inspector-section");
            myInspector.Add(blackboardPanel);
            
            var actions = new VisualElement();
            actions.AddToClassList("flow-asset-inspector-actions");
            actions.Add(new OpenFlowGraphButton(Asset));
            myInspector.Add(actions);

            return myInspector;
        }

        private VisualElement CreateSection(string title, VisualElement trailing = null)
        {
            var section = new VisualElement();
            section.AddToClassList("flow-asset-inspector-section");

            var header = new VisualElement();
            header.AddToClassList("flow-asset-inspector-section-header");
            header.Add(new Label(title));
            if (trailing != null)
            {
                trailing.AddToClassList("flow-asset-inspector-section-trailing");
                header.Add(trailing);
            }
            section.Add(header);
            return section;
        }

    }
}
