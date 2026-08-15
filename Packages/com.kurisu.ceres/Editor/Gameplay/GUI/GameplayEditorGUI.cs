using System;
using UnityEditor;
using UnityEngine;

namespace Ceres.Gameplay.Editor
{
    public static class GameplayEditorGUI
    {
        private static readonly GUIStyle FoldoutStyle = new("ShurikenModuleTitle")
        {
            font = new GUIStyle(EditorStyles.label).font,
            border = new RectOffset(15, 7, 4, 4),
            fixedHeight = 22,
            contentOffset = new Vector2(20f, -2f)
        };

        public static void Foldout(
            Rect rect,
            string foldKey,
            string title = null,
            Action drawAct = null,
            bool enable = true
        )
        {
            var color = GUI.backgroundColor;
            GUI.backgroundColor = Color.white;
            GUI.Box(rect, title ?? foldKey, FoldoutStyle);
            GUI.backgroundColor = color;

            var e = Event.current;
            bool foldout = EditorPrefs.GetBool(foldKey);

            if (e.type == EventType.Repaint)
            {
                var arrowRect = new Rect(rect.x + 4f, rect.y + 2f, 13f, 13f);
                EditorStyles.foldout.Draw(arrowRect, false, false, foldout, false);
            }

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                foldout = !foldout;
                EditorPrefs.SetBool(foldKey, foldout);
                e.Use();
            }

            if (foldout && drawAct != null)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    using (new EditorGUI.DisabledScope(!enable))
                    {
                        drawAct();
                    }
                }
            }
        }
    }
}