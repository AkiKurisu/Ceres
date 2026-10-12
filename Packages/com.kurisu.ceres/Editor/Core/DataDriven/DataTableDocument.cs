using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ceres.Collections;
using Ceres.Serialization;
using UnityEditor;
using UnityEngine;

namespace Ceres.DataDriven.Editor
{
    /// <summary>
    /// Editable copy of a .datatable source; <see cref="Save"/> writes the source back and reimports the table.
    /// Opening a source that is already open returns the same document, so every editor of a table shares its edits.
    /// </summary>
    public sealed class DataTableDocument : ScriptableObject, IDisposable
    {
        private static readonly Dictionary<string, DataTableDocument> OpenDocuments = new(StringComparer.OrdinalIgnoreCase);

        private int _references;

        private DateTime _sourceTime;

        [Serializable]
        internal class DataTableRow
        {
            public string RowId;

            public SerializedObject<IDataTableRow> RowData;

            public DataTableRow(string rowId, IDataTableRow row)
            {
                RowId = rowId;
                RowData = SerializedObject<IDataTableRow>.FromObject(row);
            }

            public DataTableRow(string rowId, string rowType, string json)
            {
                RowId = rowId;
                RowData = new SerializedObject<IDataTableRow>
                {
                    serializedTypeString = rowType,
                    jsonData = json
                };
            }

            internal void InternalUpdate()
            {
                RowData.InternalUpdate();
            }
        }

        [SerializeField]
        internal SerializedType<IDataTableRow> m_rowType = new();

        [SerializeField]
        internal DataTableRow[] m_rows = Array.Empty<DataTableRow>();

        public string AssetPath { get; private set; }

        public bool IsModified { get; private set; }

        public int RowCount => m_rows.Length;

        public IEnumerable<string> RowIds => m_rows.Select(row => row.RowId);

        public static DataTableDocument Open(DataTable table)
        {
            return Open(AssetDatabase.GetAssetPath(table));
        }

        /// <summary>
        /// Open a .datatable source; throws <see cref="FormatException"/> when the source is malformed
        /// </summary>
        public static DataTableDocument Open(string assetPath)
        {
            if (OpenDocuments.TryGetValue(assetPath, out var document) && document)
            {
                if (!document.IsModified && document._sourceTime != File.GetLastWriteTimeUtc(assetPath))
                {
                    document.Load(DataTableSource.Parse(File.ReadAllText(assetPath)));
                    document._sourceTime = File.GetLastWriteTimeUtc(assetPath);
                }
                document._references++;
                return document;
            }
            var source = DataTableSource.Parse(File.ReadAllText(assetPath));
            document = CreateDocument(assetPath);
            document.Load(source);
            document._sourceTime = File.GetLastWriteTimeUtc(assetPath);
            return document;
        }

        /// <summary>
        /// Create a document for a new source; nothing is written until <see cref="Save"/>
        /// </summary>
        public static DataTableDocument Create(string assetPath, Type rowType = null)
        {
            var document = CreateDocument(assetPath);
            if (rowType != null) document.SetRowStruct(rowType);
            document.IsModified = true;
            return document;
        }

        private static DataTableDocument CreateDocument(string assetPath)
        {
            var document = CreateInstance<DataTableDocument>();
            document.hideFlags = HideFlags.DontSave;
            document.name = Path.GetFileNameWithoutExtension(assetPath);
            document.AssetPath = assetPath;
            document._references = 1;
            OpenDocuments[assetPath] = document;
            return document;
        }

        internal void Load(DataTableSource source)
        {
            var rowType = DataTableSource.ResolveRowType(source.RowTypeName);
            if (rowType == null && !string.IsNullOrEmpty(source.RowTypeName))
            {
                throw new FormatException($"Row type '{source.RowTypeName}' was not found.");
            }
            m_rowType = rowType != null ? SerializedType<IDataTableRow>.FromType(rowType) : new SerializedType<IDataTableRow>();
            var typeString = m_rowType.serializedTypeString;
            m_rows = source.Rows.Select(row => new DataTableRow(row.Id, typeString, row.Json)).ToArray();
        }

        public DataTableSource ToSource()
        {
            var rowType = GetRowStructType();
            var rows = m_rows.Select(row =>
            {
                var value = rowType != null ? JsonUtility.FromJson(row.RowData.jsonData ?? "{}", rowType) : null;
                return new DataTableSource.Row(row.RowId, value != null ? JsonUtility.ToJson(value) : "{}");
            }).ToArray();
            return new DataTableSource(DataTableSource.GetRowTypeName(rowType), rows);
        }

        /// <summary>
        /// Write the source and reimport the table
        /// </summary>
        public void Save()
        {
            Save(false);
        }

        /// <summary>
        /// Write the source; a deferred import runs on the next editor update, for callers that cannot import now
        /// </summary>
        internal void Save(bool deferImport)
        {
            var text = ToSource().Write();
            IsModified = false;
            if (File.Exists(AssetPath) && File.ReadAllText(AssetPath) == text) return;
            var directory = Path.GetDirectoryName(AssetPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(AssetPath, text);
            _sourceTime = File.GetLastWriteTimeUtc(AssetPath);
            var path = AssetPath;
            if (deferImport)
            {
                EditorApplication.delayCall += () => Import(path);
            }
            else
            {
                Import(path);
            }
        }

        private static void Import(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            // Managers hold tables loaded before the reimport.
            DataTableManager.ReleaseAll();
        }

        /// <summary>
        /// Mark the document as changed after editing its serialized data directly
        /// </summary>
        public void SetModified()
        {
            InternalUpdate();
            IsModified = true;
        }

        /// <summary>
        /// Release this holder's reference without saving
        /// </summary>
        public void Dispose()
        {
            Close(false);
        }

        /// <summary>
        /// Release this holder's reference; the last holder saves unsaved changes when asked and destroys the document
        /// </summary>
        public void Close(bool saveChanges)
        {
            if (!this || --_references > 0) return;
            if (saveChanges && IsModified) Save(true);
            if (OpenDocuments.TryGetValue(AssetPath, out var document) && document == this) OpenDocuments.Remove(AssetPath);
            DestroyImmediate(this);
        }

        public Type GetRowStructType()
        {
            return m_rowType != null && m_rowType.IsValid() ? m_rowType.GetObjectType() : null;
        }

        public IDataTableRow GetRowStruct()
        {
            return GetRowStructType() != null ? m_rowType.GetObject() : null;
        }

        /// <summary>
        /// Copies of all rows; changing them does not change the document
        /// </summary>
        public IDataTableRow[] GetAllRows()
        {
            return m_rows.Select(row => row.RowData.NewObject()).ToArray();
        }

        /// <summary>
        /// Copies of all rows; changing them does not change the document
        /// </summary>
        public T[] GetAllRows<T>() where T : class, IDataTableRow
        {
            return m_rows.Select(row => row.RowData.NewObject() as T).ToArray();
        }

        /// <summary>
        /// Copy of a row, or null when the document has no such row
        /// </summary>
        public T GetRow<T>(string rowId) where T : class, IDataTableRow
        {
            return m_rows.FirstOrDefault(row => row.RowId == rowId)?.RowData.NewObject() as T;
        }

        /// <summary>
        /// Copies of all rows keyed by row id
        /// </summary>
        public Dictionary<string, IDataTableRow> GetRowMap()
        {
            return m_rows.ToDictionary(row => row.RowId, row => row.RowData.NewObject());
        }

        public bool ContainsRow(string rowId)
        {
            return m_rows.Any(row => row.RowId == rowId);
        }

        public void SetRowStruct(Type rowStructType)
        {
            m_rowType = SerializedType<IDataTableRow>.FromType(rowStructType);
            foreach (var row in m_rows)
            {
                row.RowData.serializedTypeString = SerializedType.ToString(rowStructType);
            }
            SetModified();
        }

        public bool AddRow(string rowId, IDataTableRow row)
        {
            if (ContainsRow(rowId)) return false;
            ArrayUtils.Add(ref m_rows, new DataTableRow(rowId, row));
            SetModified();
            return true;
        }

        public bool UpdateRow(string rowId, IDataTableRow newRow)
        {
            var row = m_rows.FirstOrDefault(x => x.RowId == rowId);
            if (row == null) return false;
            row.RowData = SerializedObject<IDataTableRow>.FromObject(newRow);
            SetModified();
            return true;
        }

        public void AddOrUpdateRow(string rowId, IDataTableRow newRow)
        {
            if (!UpdateRow(rowId, newRow)) AddRow(rowId, newRow);
        }

        public bool InsertRow(int index, string rowId, IDataTableRow row)
        {
            if (ContainsRow(rowId)) return false;
            ArrayUtils.Insert(ref m_rows, index, new DataTableRow(rowId, row));
            SetModified();
            return true;
        }

        public bool RemoveRow(string rowId)
        {
            int count = m_rows.Length;
            m_rows = m_rows.Where(row => row.RowId != rowId).ToArray();
            if (m_rows.Length == count) return false;
            SetModified();
            return true;
        }

        public void RemoveRow(List<int> rowIndex)
        {
            m_rows = m_rows.Where((_, index) => !rowIndex.Contains(index)).ToArray();
            SetModified();
        }

        public void RemoveAllRows()
        {
            m_rows = Array.Empty<DataTableRow>();
            SetModified();
        }

        public void ReorderRow(int fromIndex, int toIndex)
        {
            ArrayUtils.Reorder(ref m_rows, fromIndex, toIndex);
            SetModified();
        }

        public string NewRowId()
        {
            var id = "Row_0";
            int index = 1;
            while (ContainsRow(id))
            {
                id = $"Row_{index++}";
            }
            return id;
        }

        internal void InternalUpdate()
        {
            m_rowType?.InternalUpdate();
            foreach (var row in m_rows)
            {
                row.RowData.serializedTypeString = m_rowType?.serializedTypeString;
                row.InternalUpdate();
            }
        }

        internal void Cleanup()
        {
            foreach (var row in m_rows)
            {
                row.RowData.objectHandle = 0;
            }
        }
    }
}
