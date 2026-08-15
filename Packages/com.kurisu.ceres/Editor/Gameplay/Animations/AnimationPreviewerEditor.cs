using System.Linq;
using UnityEditor;
using UnityEngine;
using UEditor = UnityEditor.Editor;
namespace Ceres.Gameplay.Animations.Editor
{
    [CustomEditor(typeof(AnimationPreviewer))]
    public class AnimationPreviewerEditor : UEditor
    {
        private AnimationPreviewer Previewer => (AnimationPreviewer)target;
        
        public override void OnInspectorGUI()
        {
            var timeProp = serializedObject.FindProperty(nameof(AnimationPreviewer.normalizedTime));
            DrawDefaultInspector();
            GUI.enabled = Previewer.animationClip && Previewer.animator;
            if (IsPlaying())
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.Slider(timeProp, 0f, 1f, new GUIContent("Normalized Time"));
                if (EditorGUI.EndChangeCheck())
                {
                    float time = timeProp.floatValue * Previewer.animationClip.length;
                    SetTime(time);
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
                if (GUILayout.Button("Stop"))
                {
                    Stop();
                }
                if (!Application.isPlaying)
                {
                    if (GUILayout.Button("Snapshot"))
                    {
                        Snapshot();
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Preview"))
                {
                    Preview();
                }
            }
            GUI.enabled = true;
        }
        
        private bool IsPlaying()
        {
            if (Application.isPlaying)
            {
                return Previewer.IsPlaying();
            }
            return AnimationMode.InAnimationMode();
        }

        private void SetTime(float time)
        {
            if (Application.isPlaying)
            {
                Previewer.SetTime(time);
            }
            else
            {
                AnimationMode.SampleAnimationClip(Previewer.animator.gameObject, Previewer.animationClip, time);
            }
        }
        
        private void Preview()
        {
            if (Application.isPlaying)
            {
                Previewer.Preview();
            }
            else
            {
                AnimationMode.StartAnimationMode();
                AnimationMode.SampleAnimationClip(Previewer.animator.gameObject, Previewer.animationClip, Previewer.normalizedTime * Previewer.animationClip.length);
            }
        }
        
        private void Stop()
        {
            if (Application.isPlaying)
            {
                Previewer.Preview();
            }
            else
            {
                AnimationMode.EndSampling();
                AnimationMode.StopAnimationMode();
            }
        }
        
        private void Snapshot()
        {
            if (Application.isPlaying)
            {
                return;
            }

            var snapshots = Previewer.animator.GetComponentsInChildren<Transform>()
                .Select(transform => (transform, transform.position, transform.rotation))
                .ToArray();
            AnimationMode.EndSampling();
            AnimationMode.StopAnimationMode();
            Undo.RecordObjects(snapshots.Select(x=> x.transform).OfType<Object>().ToArray(), "Snapshot");
            foreach (var node in snapshots)
            {
                node.transform.SetPositionAndRotation(node.position, node.rotation);
            }
            EditorUtility.SetDirty(Previewer.animator.gameObject);
        }
    }
}
