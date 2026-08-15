using UnityEditor;
using UnityEngine;

namespace Ceres.Gameplay.Editor
{
    public static class GameplayEditorGUILayout
    {
        private static readonly GUIStyle FoldoutStyle = new("ShurikenModuleTitle")
        {
            font = new GUIStyle(EditorStyles.label).font,
            border = new RectOffset(15, 7, 4, 4),
            fixedHeight = 22,
            contentOffset = new Vector2(20f, -2f)
        };

        public static bool Foldout(bool foldout, string content)
        {
            var color = GUI.backgroundColor;
            GUI.backgroundColor = Color.white;
            
            var rect = GUILayoutUtility.GetRect(new GUIContent(content), FoldoutStyle);
            GUI.Box(rect, content, FoldoutStyle);
            GUI.backgroundColor = color;

            var e = Event.current;
            if (e.type == EventType.Repaint)
            {
                var arrowRect = new Rect(rect.x + 4f, rect.y + 2f, 13f, 13f);
                EditorStyles.foldout.Draw(arrowRect, false, false, foldout, false);
            }

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                foldout = !foldout;
                e.Use();
            }

            return foldout;
        }
        
        /// <summary>
        /// Draws the built-in Inspector without showing Script field.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static bool DrawDefaultInspectorWithoutScript(SerializedObject obj)
        {
            EditorGUI.BeginChangeCheck();
            obj.UpdateIfRequiredOrScript();
            SerializedProperty iterator = obj.GetIterator();
            for (bool enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = false)
            {
                if ("m_Script" == iterator.propertyPath) continue;
                EditorGUILayout.PropertyField(iterator, true);
            }
            obj.ApplyModifiedProperties();
            return EditorGUI.EndChangeCheck();
        }
    }
}