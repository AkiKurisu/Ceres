using System;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Ceres.DataDriven.Editor
{
    /// <summary>
    /// Inspector of a .datatable source; edits a document and saves it when the inspector closes
    /// </summary>
    [CustomEditor(typeof(DataTableImporter))]
    internal sealed class DataTableImporterEditor : ScriptedImporterEditor
    {
        private DataTableDocument _document;

        private DataTableEditor _editor;

        private string _error;

        public override bool showImportedObject => false;

        protected override bool needsApplyRevert => false;

        public override void OnEnable()
        {
            base.OnEnable();
            var path = ((AssetImporter)target).assetPath;
            try
            {
                _document = DataTableDocument.Open(path);
                _editor = (DataTableEditor)CreateEditor(_document, typeof(DataTableEditor));
            }
            catch (FormatException e)
            {
                _error = e.Message;
            }
        }

        public override void OnDisable()
        {
            if (_editor) DestroyImmediate(_editor);
            if (_document) _document.Close(true);
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            if (_error != null)
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
                return;
            }
            if (!AssetDatabase.LoadMainAssetAtPath(((AssetImporter)target).assetPath))
            {
                EditorGUILayout.HelpBox("The table failed to import. See the Console for details.", MessageType.Error);
            }
            if (_editor) _editor.OnInspectorGUI();
        }
    }
}
