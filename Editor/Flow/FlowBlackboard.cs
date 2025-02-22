using Ceres.Graph;
using UnityEditor;
using UnityEngine;

namespace Ceres.Editor.Graph.Flow
{
    public class FlowBlackboard: CeresBlackboard
    {
        private readonly FlowGraphView _flowGraphView;
        
        public FlowBlackboard(FlowGraphView graphView) : base(graphView)
        {
            _flowGraphView = graphView;
        }
        
        protected override void CreateBlackboardMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Variable/Int"), false, () => AddVariable(new SharedInt(), true));
            menu.AddItem(new GUIContent("Variable/Float"), false, () => AddVariable(new SharedFloat(), true));
            menu.AddItem(new GUIContent("Variable/Double"), false, () => AddVariable(new SharedDouble(), true));
            menu.AddItem(new GUIContent("Variable/Bool"), false, () => AddVariable(new SharedBool(), true));
            menu.AddItem(new GUIContent("Variable/Vector2Int"), false, () => AddVariable(new SharedVector2Int(), true));
            menu.AddItem(new GUIContent("Variable/Vector2"), false, () => AddVariable(new SharedVector2(), true));
            menu.AddItem(new GUIContent("Variable/Vector3"), false, () => AddVariable(new SharedVector3(), true));
            menu.AddItem(new GUIContent("Variable/Vector3Int"), false, () => AddVariable(new SharedVector3Int(), true));
            menu.AddItem(new GUIContent("Variable/String"), false, () => AddVariable(new SharedString(), true));
            menu.AddItem(new GUIContent("Variable/Unity Object"), false, () => AddVariable(new SharedUObject(), true));
            menu.AddItem(new GUIContent("Variable/Object"), false, () => AddVariable(new SharedObject(), true));
            menu.AddItem(new GUIContent("Function"), false, AddNewFunction);
        }

        private void AddNewFunction()
        {
            _flowGraphView.FlowGraphEditorWindow.EditorObject.AddNewFunctionSubGraph();
        }
    }
}