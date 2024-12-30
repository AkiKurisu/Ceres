using Ceres.Graph.Flow;
using UnityEditor;
using UnityEngine;
namespace Ceres.Editor.Graph.Flow
{
    [CustomPropertyDrawer(typeof(FlowGraphData))]
    public class FlowGraphDataPropertyDrawer: PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return 25;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var icon = Resources.Load<Texture>("Ceres/editor_icon");
            if (GUI.Button(position, new GUIContent("Open Flow Graph", icon)))
            {
                var target = property.serializedObject.targetObject;
                if (target is IFlowGraphContainer flowGraphContainer)
                {
                    FlowGraphEditorWindow.Show(flowGraphContainer);
                }
            }
            EditorGUI.EndProperty();
        }
    }
}