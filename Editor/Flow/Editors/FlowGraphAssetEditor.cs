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
        
        public override VisualElement CreateInspectorGUI()
        {
            var myInspector = new VisualElement();
            myInspector.Add(new PropertyField(serializedObject.FindProperty(nameof(FlowGraphAsset.runtimeType))));
            myInspector.Add(new OpenFlowGraphButton(Asset));
            return myInspector;
        }
    }
}