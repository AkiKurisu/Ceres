using Ceres.Graph.Flow;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UEditor = UnityEditor.Editor;
namespace Ceres.Editor.Graph.Flow
{
    [CustomEditor(typeof(FlowGraphAsset), true)]
    public class FlowGraphAssetEditor : UEditor
    {
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