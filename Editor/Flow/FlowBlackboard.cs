using System;
using Ceres.Graph;
using Ceres.Graph.Flow;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph.Flow
{
    public class FlowBlackboard: CeresBlackboard
    {
        private readonly FlowGraphView _flowGraphView;
        
        public FlowBlackboard(FlowGraphView graphView) : base(graphView)
        {
            _flowGraphView = graphView;
            RegisterCallback<VariableChangeEvent>(OnVariableChange);
        }

        private void OnVariableChange(VariableChangeEvent evt)
        {
            if (evt.Variable is not CustomFunction customFunction) return;
            switch (evt.ChangeType)
            {
                case VariableChangeType.Name:
                    _flowGraphView.FlowGraphEditorWindow.RenameSubgraph(customFunction.Value /* Guid */,
                        customFunction.Name);
                    break;
                case VariableChangeType.Remove:
                    _flowGraphView.FlowGraphEditorWindow.RemoveSubgraph(customFunction.Value);
                    break;
                case VariableChangeType.Add:
                case VariableChangeType.Value:
                case VariableChangeType.Type:
                default:
                    break;
            }
        }

        protected override bool CanVariableExposed(SharedVariable variable)
        {
            return variable is not CustomFunction;
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
            
            // Can only create function in uber graph
            if (_flowGraphView.FlowGraphEditorWindow.GraphIndex == 0)
            {
                menu.AddItem(new GUIContent("Function"), false, CreateFunction);
            }
        }

        private void CreateFunction()
        {
            _flowGraphView.FlowGraphEditorWindow.CreateFunctionSubGraph();
        }
        
        protected override BlackboardRow CreateVariableBlackboardRow(SharedVariable variable, BlackboardField blackboardField, VisualElement valueField)
        {
            var blackboardRow = base.CreateVariableBlackboardRow(variable, blackboardField, valueField);
            if (variable is not CustomFunction) return blackboardRow;
            
            blackboardRow.Q<Button>("expandButton").RemoveFromHierarchy();
            blackboardRow.AddToClassList("customFunction-blackboard");
            ((CeresBlackboardVariableRow)blackboardRow).CanDelete = false;
            return blackboardRow;
        }

        protected override void AddVariableRow(SharedVariable variable, BlackboardRow blackboardRow)
        {
            if (variable is CustomFunction)
            {
                GetOrAddSection("Functions").Add(blackboardRow);
                return;
            }
            base.AddVariableRow(variable, blackboardRow);
        }
        
        protected override void BuildBlackboardMenu(ContextualMenuPopulateEvent evt, CeresBlackboardVariableRow variableRow)
        {
            evt.menu.MenuItems().Clear();
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Delete", _ =>
            {
                if (variableRow.Variable is CustomFunction function)
                {
                    DisplayDeleteFunctionDialog(function);
                    return;
                }
                RemoveVariable(variableRow.Variable, true);
            }));
            if (variableRow.Variable is CustomFunction customFunction)
            {
                evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Edit Function", _ =>
                {
                    OpenFunctionSubgraphView(customFunction);
                }));
                return;
            }
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Duplicate", _ =>
            {
                AddVariable(variableRow.Variable.Clone(), true);
            }));
        }

        private void DisplayDeleteFunctionDialog(CustomFunction function)
        {
            if (!EditorUtility.DisplayDialog("Delete selected function?", 
                    $"Do you want to delete function {function.Name}", "Delete", "Cancel")) return;
            FindRow(function).CanDelete = true;
            RemoveVariable(function, true);
        }

        private void OpenFunctionSubgraphView(CustomFunction function)
        {
            _flowGraphView.FlowGraphEditorWindow.OpenSubgraphView(function.Name);
        }
    }
}