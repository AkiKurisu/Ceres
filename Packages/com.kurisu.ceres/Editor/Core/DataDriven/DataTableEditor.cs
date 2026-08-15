using System;
using System.IO;
using Ceres.Editor;
using Ceres.Serialization;
using Ceres.Serialization.Editor;
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UEditor = UnityEditor.Editor;

namespace Ceres.DataDriven.Editor
{
    [CustomEditor(typeof(DataTable))]
    public class DataTableEditor : UEditor
    {
        public DataTable Table => target as DataTable;
        
        private DataTableRowView _dataTableRowView;
        
        private const string NullType = "Null";
        
        /// <summary>
        /// Subscribe to add custom toolbar
        /// </summary>
        public DrawToolBarDelegate OnDrawToolBar;
        
        public DataTableRowView GetDataTableRowView()
        {
            _dataTableRowView ??= CreateDataTableRowView(Table);
            return _dataTableRowView;
        }
        
        /// <summary>
        /// Implement to use customized <see cref="DataTableRowView"/>  
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        protected virtual DataTableRowView CreateDataTableRowView(DataTable table)
        {
            var rowView = new DataTableRowView(table);
            if (CeresSettings.Instance.inlineRowReadOnly)
            {
                rowView.ReadOnly = true;
            }
            return rowView;
        }
        
        public override void OnInspectorGUI()
        {
            DrawToolBar();
            GUILayout.Space(10);
            DrawRowView();
        }
        
        protected virtual void DrawRowView()
        {
            _dataTableRowView = GetDataTableRowView();
            DrawRowTypeSelector();
            GUILayout.Space(5);
            _dataTableRowView.DrawGUI(serializedObject);
        }

        private void DrawRowTypeSelector()
        {
            var typeProp = serializedObject.FindProperty("m_rowType");
            var reference = typeProp.FindPropertyRelative("serializedTypeString");
            Type type;
            try
            {
                type = SerializedType.FromString(reference.stringValue);
            }
            catch
            {
                type = null;
            }
            string id = type != null ? type.Name : NullType;
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Row Type", "Set DataTable Row Struct Type"), GUILayout.Width(80));
            if (EditorGUILayout.DropdownButton(new GUIContent(id), FocusType.Keyboard))
            {
                var provider = CreateInstance<TypeSearchWindow>();
                provider.Initialize(typeof(IDataTableRow), selectType =>
                {
                    reference.stringValue = selectType != null ? SerializedType.ToString(selectType) : NullType;
                    serializedObject.ApplyModifiedProperties();
                    RequestDataTableUpdate();
                });
                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), provider);
            }
            GUILayout.EndHorizontal();
        }
        
        #region Cleanup

        // DataTableEditor use global object manager to cache wrapper.
        // However, the soft object handle is not persistent.
        // We should ensure not to conflict with other modules.


        protected virtual void OnEnable()
        {
            GlobalObjectManager.Cleanup();
            Undo.undoRedoEvent += OnUndo;
        }

        protected virtual void OnDisable()
        {
            GlobalObjectManager.Cleanup();
            Undo.undoRedoEvent -= OnUndo;
            Table.Cleanup();
            /* Trigger save assets to force cleanup editor cache */
            EditorUtility.SetDirty(Table);
            /* Auto register table if it has AddressableDataTableAttribute */
            DataTableEditorUtils.RegisterTableToAssetGroup(Table);
        }

        #endregion
        
        /// <summary>
        /// Draw editor toolbar
        /// </summary>
        protected virtual void DrawToolBar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(Styles.ExportJsonButtonLabel, DataTableEditorUtils.ToolBarButtonStyle))
            {
                string path = EditorUtility.SaveFilePanel("Select json file export path", Application.dataPath, Table.name, "json");
                if (!string.IsNullOrEmpty(path))
                {
                    var jsonData = DataTableEditorUtils.ExportJson(Table);
                    File.WriteAllText(path, jsonData);
                    Debug.Log($"<color=#3aff48>DataTable</color>: Export data to json file succeed!");
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }

            if (GUILayout.Button(Styles.ImportJsonButtonLabel, DataTableEditorUtils.ToolBarButtonStyle))
            {
                string path = EditorUtility.OpenFilePanel("Select json file to import", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(path))
                {
                    var data = File.ReadAllText(path);
                    DataTableEditorUtils.ImportJson(Table, data);
                    RequestDataTableUpdate();
                    Debug.Log($"<color=#3aff48>DataTable</color>: Import data from json file succeed!");
                    EditorUtility.SetDirty(Table);
                    AssetDatabase.SaveAssets();
                    GUIUtility.ExitGUI();
                }
            }
            OnDrawToolBar?.Invoke(this);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// Request change DataTable in editor
        /// </summary>
        protected void RequestDataTableUpdate()
        {
            DataTableEditorUtils.OnDataTablePreUpdate?.Invoke(Table);
            RebuildEditorView();
            DataTableEditorUtils.OnDataTablePostUpdate?.Invoke(Table);
        }
        
        /// <summary>
        /// Rebuild editor gui view, called on DataTable changed
        /// </summary>
        protected virtual void RebuildEditorView()
        {
            _dataTableRowView?.Rebuild();
            serializedObject.Update();
        }
        
        protected virtual void OnUndo(in UndoRedoInfo undo)
        {
            // Manually rebuild row view after undo
            RebuildEditorView();
        }
        
        private static class Styles
        {
            public static readonly GUIContent ImportJsonButtonLabel = new("  Import from Json", EditorGUIUtility.IconContent("d_Import@2x").image);

            public static readonly GUIContent ExportJsonButtonLabel = new("  Export to Json", EditorGUIUtility.IconContent("SaveAs@2x").image);
        }
    }
}