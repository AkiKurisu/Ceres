using Ceres.Graph.Flow;
using UnityEditor;
using UnityEngine;

namespace Ceres.Editor.Graph.Flow
{
    [CustomPropertyDrawer(typeof(FlowGraphData))]
    public class FlowGraphDataPropertyDrawer: PropertyDrawer
    {
        private const float ButtonHeight = 25f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return ButtonHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var icon = Resources.Load<Texture>("Ceres/editor_icon");
            var buttonRect = new Rect(position.x, position.y, position.width, ButtonHeight);
            if (GUI.Button(buttonRect, new GUIContent("Open Flow Graph", icon)))
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
