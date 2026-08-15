using UnityEngine;
using UnityEditor;
using Ceres.Gameplay.Capture;
using UEditor = UnityEditor.Editor;

namespace Ceres.Gameplay.Editor
{
    [CustomEditor(typeof(ScreenshotTool))]
    public class ScreenshotToolEditor : UEditor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox("Click the button above to take a screenshot using the current settings.", MessageType.Info);
            ScreenshotTool screenshotTool = (ScreenshotTool)target;
            if (GUILayout.Button("Take Screenshot", GUILayout.Height(30)))
            {
                screenshotTool.TakeScreenshot();
            }
            
        }
    }
}
