using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Resource.Editor;
using UnityEditor;
using UnityEngine;

namespace Ceres.DataDriven.Editor
{
    /// <summary>
    /// Delegate for draw editor toolbar
    /// </summary>
    public delegate void DrawToolBarDelegate(DataTableEditor tableEditor);

    /// <summary>
    /// Delegate for observe DataTable document update in editor
    /// </summary>
    public delegate void DataTableUpdateDelegate(DataTableDocument document);

    /// <summary>
    /// Utility for editing DataTable in editor script
    /// </summary>
    public static class DataTableEditorUtils
    {
        public static GUIStyle ToolBarButtonStyle => EditorStyles.toolbarButton;

        /// <summary>
        /// Call before a document is rebuilt in the editor
        /// </summary>
        public static DataTableUpdateDelegate OnDataTablePreUpdate;

        /// <summary>
        /// Call after a document is rebuilt in the editor
        /// </summary>
        public static DataTableUpdateDelegate OnDataTablePostUpdate;

        /// <summary>
        /// Create a .datatable source with the given row type and import it
        /// </summary>
        public static DataTable CreateDataTable(string assetPath, Type rowType)
        {
            using var document = DataTableDocument.Create(assetPath, rowType);
            document.Save();
            return AssetDatabase.LoadAssetAtPath<DataTable>(assetPath);
        }

        private static int _batchDepth;

        private static readonly HashSet<DataTableDocument> BatchDocuments = new();

        /// <summary>
        /// Open the source of a table, apply an edit and save it when the edit changed the document
        /// </summary>
        public static void Edit(DataTable dataTable, Action<DataTableDocument> edit)
        {
            Edit(AssetDatabase.GetAssetPath(dataTable), edit);
        }

        /// <summary>
        /// Open a source, apply an edit and save it when the edit changed the document.
        /// Inside <see cref="BeginBatch"/> the document stays open and saves when the batch ends.
        /// </summary>
        public static void Edit(string assetPath, Action<DataTableDocument> edit)
        {
            Edit(() => DataTableDocument.Open(assetPath), edit);
        }

        /// <summary>
        /// Open the rows an asset stores inline, apply an edit and write them back when the edit changed them
        /// </summary>
        public static void Edit(UnityEngine.Object owner, InlineDataTable table, Action<DataTableDocument> edit)
        {
            Edit(() => DataTableDocument.Open(owner, table), edit);
        }

        private static void Edit(Func<DataTableDocument> open, Action<DataTableDocument> edit)
        {
            var document = open();
            if (_batchDepth > 0)
            {
                if (!BatchDocuments.Add(document)) document.Dispose();
                edit(document);
                return;
            }
            try
            {
                edit(document);
                if (document.IsModified) document.Save();
            }
            finally
            {
                document.Dispose();
            }
        }

        /// <summary>
        /// Defer saving of every <see cref="Edit(string, Action{DataTableDocument})"/> until the returned scope is disposed;
        /// the changed tables then import together
        /// </summary>
        public static IDisposable BeginBatch()
        {
            _batchDepth++;
            return new BatchScope();
        }

        private static void EndBatch()
        {
            if (--_batchDepth > 0) return;
            var documents = BatchDocuments.Where(document => document).ToArray();
            BatchDocuments.Clear();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var document in documents)
                {
                    if (document.IsModified) document.Save();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                foreach (var document in documents)
                {
                    document.Dispose();
                }
            }
        }

        private sealed class BatchScope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                EndBatch();
            }
        }

        /// <summary>
        /// Set row struct type
        /// </summary>
        public static void SetRowStruct<T>(DataTableDocument document) where T : class, IDataTableRow
        {
            SetRowStruct(document, typeof(T));
        }

        /// <summary>
        /// Set row struct type
        /// </summary>
        public static void SetRowStruct(DataTableDocument document, Type rowType)
        {
            Undo.RecordObject(document, "Update DataTable Row Struct");
            document.SetRowStruct(rowType);
        }

        /// <summary>
        /// Export the document as source text
        /// </summary>
        public static string ExportJson(DataTableDocument document)
        {
            return document.ToSource().Write();
        }

        /// <summary>
        /// Replace the document with source text
        /// </summary>
        public static void ImportJson(DataTableDocument document, string jsonData)
        {
            Undo.RecordObject(document, "Overwrite DataTable from Json");
            document.Load(DataTableSource.Parse(jsonData));
            document.SetModified();
        }

        /// <summary>
        /// Validate input row id and out right one
        /// </summary>
        public static bool ValidateNewRowId(DataTableDocument document, string oldRowId, string newRowId, out string result)
        {
            if (document.ContainsRow(newRowId))
            {
                result = oldRowId;
                return false;
            }
            result = newRowId;
            return true;
        }

        /// <summary>
        /// Get a valid new row id
        /// </summary>
        public static string GetNewRowId(DataTableDocument document)
        {
            return document.NewRowId();
        }

        /// <summary>
        /// Read copies of all rows from a table's source, including Editor-only members
        /// </summary>
        public static T[] GetAllRowsSafe<T>(DataTable dataTable) where T : class, IDataTableRow
        {
            using var document = DataTableDocument.Open(dataTable);
            return document.GetAllRows<T>();
        }

        /// <summary>
        /// Read copies of all rows from a table's source, including Editor-only members
        /// </summary>
        public static IDataTableRow[] GetAllRowsSafe(DataTable dataTable)
        {
            using var document = DataTableDocument.Open(dataTable);
            return document.GetAllRows();
        }

        /// <summary>
        /// Read copies of all rows keyed by row id from a table's source, including Editor-only members
        /// </summary>
        public static Dictionary<string, IDataTableRow> GetRowMapSafe(DataTable dataTable)
        {
            using var document = DataTableDocument.Open(dataTable);
            return document.GetRowMap();
        }

        /// <summary>
        /// Copies of all inline rows, including unsaved edits of an open document
        /// </summary>
        public static T[] GetAllRows<T>(UnityEngine.Object owner, InlineDataTable table) where T : class, IDataTableRow
        {
            using var document = DataTableDocument.Open(owner, table);
            return document.GetAllRows<T>();
        }

        /// <summary>
        /// Copies of all inline rows keyed by row id, including unsaved edits of an open document
        /// </summary>
        public static Dictionary<string, IDataTableRow> GetRowMap(UnityEngine.Object owner, InlineDataTable table)
        {
            using var document = DataTableDocument.Open(owner, table);
            return document.GetRowMap();
        }

        /// <summary>
        /// Register a DataTable if defined <see cref="AddressableDataTableAttribute"/>
        /// </summary>
        public static void RegisterTableToAssetGroup(DataTable dataTable)
        {
            var structType = dataTable.GetRowStructType();

            var addressableAttribute = structType?.GetCustomAttribute<AddressableDataTableAttribute>();
            if (addressableAttribute == null) return;

            string address = addressableAttribute.Address ?? dataTable.name;
            var group = ResourceEditorUtils.GetOrCreateAssetGroup(addressableAttribute.Group);
            using (group.Modify())
            {
                group.AddAsset(dataTable, addressableAttribute.Labels).address = address;
            }
        }

        /// <summary>
        /// Register all DataTable in project if defined <see cref="AddressableDataTableAttribute"/>
        /// </summary>
        public static void RegisterAllDataTablesToAssetGroup()
        {
            var dataTables = AssetDatabase.FindAssets($"t:{nameof(DataTable)}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<DataTable>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(table => table)
                .ToArray();
            foreach (var dataTable in dataTables)
            {
                RegisterTableToAssetGroup(dataTable);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Assets/Create/Ceres/DataTable", false, 0)]
        private static void CreateDataTableAsset()
        {
            ProjectWindowUtil.CreateAssetWithContent("New DataTable." + DataTableSource.Extension,
                new DataTableSource(string.Empty, Array.Empty<DataTableSource.Row>()).Write());
        }
    }
}
