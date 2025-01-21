using Ceres.Graph.Flow;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph.Flow
{
    
    [CustomEditor(typeof(FlowGraphAsset), true)]
    public class FlowGraphAssetEditor : UnityEditor.Editor
    {
        private FlowGraphAsset Asset => (FlowGraphAsset)target;
        
        public override VisualElement CreateInspectorGUI()
        {
            var myInspector = new VisualElement();
            myInspector.Add(new PropertyField(serializedObject.FindProperty(nameof(FlowGraphAsset.containerType))));
            myInspector.Add(new FlowGraphDebugButton(Asset));
            return myInspector;
        }
    }
}