using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using System.Collections.Generic;
using Ceres.Editor.Graph;
using Ceres.Graph;

namespace Ceres.Editor
{
    internal class VirtualGraphView : GraphView { }
    
    internal class VariableSourceProxy : IVariableSource
    {
        public List<SharedVariable> SharedVariables { get; } = new();
        
        private readonly IVariableSource _source;
        
        private readonly Object _dirtyObject;
        public VariableSourceProxy(IVariableSource source, Object dirtyObject)
        {
            _source = source;
            _dirtyObject = dirtyObject;
        }
        public void Update()
        {
            _source.SharedVariables.Clear();
            _source.SharedVariables.AddRange(SharedVariables);
            EditorUtility.SetDirty(_dirtyObject);
            AssetDatabase.SaveAssets();
        }
    }
    
    [CustomEditor(typeof(GameVariableScope))]
    public class GameVariableScopeEditor : UnityEditor.Editor
    {
        private bool _isDirty;
        
        public override VisualElement CreateInspectorGUI()
        {
            var source = (GameVariableScope)target;
            var myInspector = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column
                }
            };
            var proxy = new VariableSourceProxy(source, source);
            //Need attached to a virtual graphView to send event
            //It's an interesting hack so that you can use blackBoard outside graphView
            var blackBoard = new CeresBlackboard(proxy, new VirtualGraphView()) { AlwaysExposed = true };
            foreach (var variable in source.SharedVariables)
            {
                //In play mode, use original variable to observe value change
                if (Application.isPlaying)
                {
                    blackBoard.AddVariable(variable, false);
                }
                else
                {
                    blackBoard.AddVariable(variable.Clone(), false);
                }
            }
            blackBoard.style.position = Position.Relative;
            blackBoard.style.width = Length.Percent(100f);
            myInspector.Add(blackBoard);

            if (Application.isPlaying) return myInspector;

            myInspector.RegisterCallback<DetachFromPanelEvent>(_ => { if (_isDirty) proxy.Update(); });
            blackBoard.RegisterCallback<VariableChangeEvent>(_ => _isDirty = true);
            myInspector.Add(new PropertyField(serializedObject.FindProperty("parentScope"), "Parent Scope"));
            return myInspector;
        }
    }
    
    [CustomEditor(typeof(SceneVariableScope))]
    public class SceneVariableScopeEditor : UnityEditor.Editor
    {
        private bool _isDirty;
        
        public override VisualElement CreateInspectorGUI()
        {
            var source = (SceneVariableScope)target;
            var myInspector = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column
                }
            };
            var proxy = new VariableSourceProxy(source, source);
            // Need attached to a virtual graphView to send event
            // It's an interesting hack so that you can use blackBoard outside graphView
            var blackBoard = new CeresBlackboard(proxy, new VirtualGraphView()) { AlwaysExposed = true };
            foreach (var variable in source.SharedVariables)
            {
                //In play mode, use original variable to observe value change
                if (Application.isPlaying)
                {
                    blackBoard.AddVariable(variable, false);
                }
                else
                {
                    blackBoard.AddVariable(variable.Clone(), false);
                }
            }
            blackBoard.style.position = Position.Relative;
            blackBoard.style.width = Length.Percent(100f);
            myInspector.Add(blackBoard);

            if (Application.isPlaying) return myInspector;

            myInspector.RegisterCallback<DetachFromPanelEvent>(_ => { if (_isDirty) proxy.Update(); });
            blackBoard.RegisterCallback<VariableChangeEvent>(_ => _isDirty = true);
            myInspector.Add(new PropertyField(serializedObject.FindProperty("parentScope"), "Parent Scope"));
            return myInspector;
        }
    }
}