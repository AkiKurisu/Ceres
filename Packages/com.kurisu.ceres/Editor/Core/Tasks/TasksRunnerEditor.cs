using UnityEditor;
using UnityEngine;
using UEditor = UnityEditor.Editor;

namespace Ceres.Tasks.Editor
{
    [CustomEditor(typeof(TaskRunner))]
    public class TaskRunnerEditor : UEditor
    {
        private TaskRunner Manager => target as TaskRunner;
        
        private int ManagedTaskCount => Manager.Tasks.Count;
        
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            EditorApplication.update += Repaint;
        }
        
        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            EditorApplication.update -= Repaint;
        }
        
        public override void OnInspectorGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter play mode to track tasks", MessageType.Info);
                return;
            }
            var style = new GUIStyle(GUI.skin.label) { richText = true };
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Managed task count: {ManagedTaskCount}");
            foreach (var task in Manager.Tasks)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(task.InternalGetTaskName());
                GUILayout.Label($"Status: {StatusToString(task.GetStatus())}", style);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }
        
        private static string StatusToString(TaskStatus status)
        {
            return status switch
            {
                TaskStatus.Running => "<color=#92F2FF>Running</color>",
                TaskStatus.Paused => "<color=#FFF892>Paused</color>",
                TaskStatus.Completed => "<color=#FFF892>Completed</color>",
                _ => "<color=#FF787E>Stopped</color>"
            };
        }
    }
}
