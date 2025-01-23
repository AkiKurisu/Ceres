using Ceres.Graph.Flow;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UEditor = UnityEditor.Editor;
namespace Ceres.Editor.Graph.Flow
{
    public class FlowGraphDebugButton : Button
    {
        private const string ButtonText = "Open Flow Graph";
        
        public FlowGraphDebugButton(IFlowGraphContainer container) : base(() => FlowGraphEditorWindow.Show(container))
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
    
   
    [CustomEditor(typeof(FlowGraphScriptableObjectBase), true)]
    public class FlowGraphScriptableObjectBaseEditor : UEditor
    {
        private FlowGraphScriptableObjectBase Asset => (FlowGraphScriptableObjectBase)target;
        
        public override VisualElement CreateInspectorGUI()
        {
            var myInspector = new VisualElement();
            myInspector.Add(new FlowGraphDebugButton(Asset));
            return myInspector;
        }
    }
}