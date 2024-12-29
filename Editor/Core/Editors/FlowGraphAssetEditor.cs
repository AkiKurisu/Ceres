using Ceres.Graph.Flow;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph.Flow
{
    internal sealed class FlowGraphDebugButton : Button
    {
        private const string ButtonText = "Edit Flow";
        
        private const string DebugText = "Debug Flow";
        
        public FlowGraphDebugButton(IFlowGraphContainer container) : base(() => FlowGraphEditorWindow.Show(container))
        {
            style.fontSize = 15;
            style.unityFontStyleAndWeight = FontStyle.Bold;
            style.color = Color.white;
            if (!Application.isPlaying)
            {
                style.backgroundColor = new StyleColor(new Color(137 / 255f, 179 / 255f, 187 / 255f));
                text = ButtonText;
            }
            else
            {
                text = DebugText;
                style.backgroundColor = new StyleColor(new Color(253 / 255f, 163 / 255f, 180 / 255f));
            }
        }
    }
    
   
    [CustomEditor(typeof(FlowGraphAsset))]
    public class FlowGraphAssetEditor : UnityEditor.Editor
    {
        private const string LabelText = "Ceres Flow Graph";

        public override VisualElement CreateInspectorGUI()
        {
            var myInspector = new VisualElement();
            var asset = target as FlowGraphAsset;
            var label = new Label(LabelText)
            {
                style =
                {
                    fontSize = 20,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            myInspector.Add(label);
            myInspector.Add(new FlowGraphDebugButton(asset));
            return myInspector;
        }
    }

}