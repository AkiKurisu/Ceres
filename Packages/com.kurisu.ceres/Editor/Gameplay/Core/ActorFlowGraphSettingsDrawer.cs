using Ceres.Flow;
using UnityEditor;
using UnityEngine;

namespace Ceres.Gameplay.Editor
{
    [CustomPropertyDrawer(typeof(Actor.FlowGraphSettings))]
    public class ActorFlowGraphSettingsDrawer : PropertyDrawer
    {
        private const string FoldoutKey = "Actor_Flow_Advanced_Settings";

        private static class Styles
        {
            public static readonly GUIContent ExportRemoteAssetLabel;

            static Styles()
            {
                var image = EditorGUIUtility.IconContent("Download-Available@2x").image;
                ExportRemoteAssetLabel = new GUIContent(image, 
                    "Export actor flow graph data to a remote asset for hot update");
            }
        }
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect foldoutRect = position;
            foldoutRect.y += 2;
            foldoutRect.height = EditorGUIUtility.singleLineHeight;
            GameplayEditorGUI.Foldout(foldoutRect, FoldoutKey, "Advanced Settings", () =>
            {
                position.height = EditorGUIUtility.singleLineHeight;
                position.y += position.height + 10;
                EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(Actor.FlowGraphSettings.graphAsset)));
                position.y += position.height + 5;
                float width = position.width;
                position.width = width - 35;
                var addressProp = property.FindPropertyRelative(nameof(Actor.FlowGraphSettings.actorAddress));
                EditorGUI.PropertyField(position, addressProp);
                position.x += position.width + 5;
                position.width = 30;
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(addressProp.stringValue)))
                {
                    if (GUI.Button(position, Styles.ExportRemoteAssetLabel))
                    {
                        var actor = (IFlowGraphContainer)property.serializedObject.targetObject;
                        EditorApplication.delayCall += () => ActorFlowGraphEditorUtils.ExportRemoteAsset(actor, addressProp.stringValue);
                    }
                }
            }, !Application.isPlaying);
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            bool foldOut = EditorPrefs.GetBool(FoldoutKey);
            if (foldOut)
            {
                return EditorGUIUtility.singleLineHeight * 3 + 5 * 2 + 2;
            }
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
