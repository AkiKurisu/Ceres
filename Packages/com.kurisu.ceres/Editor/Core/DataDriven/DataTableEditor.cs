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
    [CustomEditor(typeof(DataTableDocument))]
    public class DataTableEditor : UEditor
    {
        public DataTableDocument Document => target as DataTableDocument;

        private DataTableRowView _dataTableRowView;

        private const string NullType = "Null";

        /// <summary>
        /// Subscribe to add custom toolbar
        /// </summary>
        public DrawToolBarDelegate OnDrawToolBar;

        public DataTableRowView GetDataTableRowView()
        {
            _dataTableRowView ??= CreateDataTableRowView(Document);
            return _dataTableRowView;
        }

        /// <summary>
        /// Implement to use customized <see cref="DataTableRowView"/>
        /// </summary>
        protected virtual DataTableRowView CreateDataTableRowView(DataTableDocument document)
        {
            var rowView = new DataTableRowView(document);
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
            var typeProp = serializedObject.FindProperty(nameof(DataTableDocument.m_rowType));
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
                    if (selectType == null) return;
                    DataTableEditorUtils.SetRowStruct(Document, selectType);
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
            if (Document) Document.Cleanup();
        }

        #endregion

        /// <summary>
        /// Draw editor toolbar
        /// </summary>
        protected virtual void DrawToolBar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            var iconSize = EditorGUIUtility.GetIconSize();
            // Toolbar icons have different source sizes; a fixed size keeps button widths consistent.
            EditorGUIUtility.SetIconSize(new Vector2(16, 16));
            try
            {
                using (new EditorGUI.DisabledScope(!Document.IsModified))
                {
                    if (GUILayout.Button(Styles.SaveButtonLabel, DataTableEditorUtils.ToolBarButtonStyle))
                    {
                        Document.Save();
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button(Styles.ExportJsonButtonLabel, DataTableEditorUtils.ToolBarButtonStyle))
                {
                    string path = EditorUtility.SaveFilePanel("Select json file export path", Application.dataPath, Document.name, "json");
                    if (!string.IsNullOrEmpty(path))
                    {
                        File.WriteAllText(path, DataTableEditorUtils.ExportJson(Document));
                        Debug.Log("<color=#3aff48>DataTable</color>: Export data to json file succeed!");
                    }
                }

                if (GUILayout.Button(Styles.ImportJsonButtonLabel, DataTableEditorUtils.ToolBarButtonStyle))
                {
                    string path = EditorUtility.OpenFilePanel("Select json file to import", Application.dataPath, "json");
                    if (!string.IsNullOrEmpty(path))
                    {
                        try
                        {
                            DataTableEditorUtils.ImportJson(Document, File.ReadAllText(path));
                            RequestDataTableUpdate();
                            Debug.Log("<color=#3aff48>DataTable</color>: Import data from json file succeed!");
                        }
                        catch (FormatException e)
                        {
                            Debug.LogError($"[DataTable] {path} is not a valid source: {e.Message}");
                        }
                        GUIUtility.ExitGUI();
                    }
                }
                OnDrawToolBar?.Invoke(this);
            }
            finally
            {
                EditorGUIUtility.SetIconSize(iconSize);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Request change DataTable in editor
        /// </summary>
        protected void RequestDataTableUpdate()
        {
            DataTableEditorUtils.OnDataTablePreUpdate?.Invoke(Document);
            Document.SetModified();
            RebuildEditorView();
            DataTableEditorUtils.OnDataTablePostUpdate?.Invoke(Document);
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
            if (Document) Document.SetModified();
            RebuildEditorView();
        }

        private static class Styles
        {
            public static readonly GUIContent SaveButtonLabel = new("  Save", EditorGUIUtility.IconContent("SaveActive").image);

            public static readonly GUIContent ImportJsonButtonLabel = new("  Import Json", EditorGUIUtility.IconContent("d_Import@2x").image);

            public static readonly GUIContent ExportJsonButtonLabel = new("  Export Json", EditorGUIUtility.IconContent("SaveAs@2x").image);
        }
    }
}
