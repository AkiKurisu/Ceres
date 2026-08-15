using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ceres.Collections;
using Ceres.Serialization;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Ceres.DataDriven
{
    /// <summary>
    /// Implement this interface for custom DataTable Row struct
    /// </summary>
    public interface IDataTableRow { }

    /// <summary>
    /// Implement this interface to validate DataTable Row before saving
    /// </summary>
    public interface IValidateRow
    {
        bool ValidateRow(string rowId, out string reason);
    }

    [CreateAssetMenu(fileName = "DataTable", menuName = "Ceres/DataTable")]
    public class DataTable : ScriptableObject
    {
        [Serializable]
        private class DataTableRow
        {
            public string RowId;

            public SerializedObject<IDataTableRow> RowData;

            public DataTableRow(string rowId, IDataTableRow row)
            {
                RowId = rowId;
                RowData = SerializedObject<IDataTableRow>.FromObject(row);
            }

            [Conditional("UNITY_EDITOR")]
            internal void InternalUpdate()
            {
                if (RowData.NewObject() is IValidateRow validateRowId)
                {
                    if (!validateRowId.ValidateRow(RowId, out var reason))
                    {
                        Debug.LogWarning($"[DataTable] Validate row {RowId} failed with reason {reason}");
                    }
                }
                RowData.InternalUpdate();
            }
        }

        [SerializeField]
        private SerializedType<IDataTableRow> m_rowType;

        [SerializeField]
        private DataTableRow[] m_rows;

        /// <summary>
        /// Get default row struct
        /// </summary>
        /// <returns></returns>
        public IDataTableRow GetRowStruct()
        {
            return m_rowType.GetObject();
        }

        /// <summary>
        /// Get row struct type
        /// </summary>
        /// <returns></returns>
        public Type GetRowStructType()
        {
            return m_rowType;
        }

        /// <summary>
        /// Get data rows from table
        /// </summary>
        public T[] GetAllRows<T>() where T : class, IDataTableRow
        {
            return m_rows.Select(x => x.RowData.GetObject() as T).ToArray();
        }

        /// <summary>
        /// Get data rows from table
        /// </summary>
        /// <returns></returns>
        public IDataTableRow[] GetAllRows()
        {
            return m_rows.Select(x => x.RowData.GetObject()).ToArray();
        }

        /// <summary>
        /// Get data rows from table by predicate
        /// </summary>
        /// <param name="predicate"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T[] GetRows<T>(Predicate<T> predicate) where T : class, IDataTableRow
        {
            return m_rows.Select(x => x.RowData.GetObject()).OfType<T>().Where(x => predicate(x)).ToArray();
        }

        /// <summary>
        /// Get data row from table
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public T GetRow<T>(int index) where T : class, IDataTableRow
        {
            return m_rows[index].RowData.GetObject() as T;
        }

        /// <summary>
        /// Get data row from table by index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public IDataTableRow GetRow(int index)
        {
            return m_rows[index].RowData.GetObject();
        }

        /// <summary>
        /// Get data row from table by RowId
        /// </summary>
        /// <param name="rowId"></param>
        /// <returns></returns>
        public T GetRow<T>(string rowId) where T : class, IDataTableRow
        {
            foreach (var row in m_rows)
            {
                if (row.RowId == rowId)
                {
                    return row.RowData.GetObject() as T;
                }
            }
            return null;
        }

        /// <summary>
        /// Get data row from table by predicate
        /// </summary>
        /// <param name="predicate"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetRow<T>(Predicate<T> predicate) where T : class, IDataTableRow
        {
            foreach (var row in m_rows)
            {
                if (row.RowData.GetObject() is T tRow && predicate(tRow))
                {
                    return tRow;
                }
            }
            return null;
        }

        /// <summary>
        /// Get data row from table by RowId
        /// </summary>
        /// <param name="rowId"></param>
        /// <returns></returns>
        public IDataTableRow GetRow(string rowId)
        {
            foreach (var row in m_rows)
            {
                if (row.RowId == rowId)
                {
                    return row.RowData.GetObject();
                }
            }
            return null;
        }

        /// <summary>
        /// Add a data row to the table
        /// </summary>
        /// <param name="rowId"></param>
        /// <param name="row"></param>
        /// <returns></returns>
        public bool AddRow(string rowId, IDataTableRow row)
        {
            var rowKeys = m_rows.Select(x => x.RowId).ToList();
            if (rowKeys.Contains(rowId))
            {
                return false;
            }
            ArrayUtils.Add(ref m_rows, new DataTableRow(rowId, row));
            return true;
        }

        /// <summary>
        /// Update a data row from the table
        /// </summary>
        /// <param name="rowId"></param>
        /// <param name="newRow"></param>
        /// <returns></returns>
        public bool UpdateRow(string rowId, IDataTableRow newRow)
        {
            var internalMap = GetEditableRowMap();
            if (!internalMap.TryGetValue(rowId, out var row))
            {
                return false;
            }
            row.RowData = SerializedObject<IDataTableRow>.FromObject(newRow);
            return true;
        }

        /// <summary>
        /// Add or update a data row from the table
        /// </summary>
        /// <param name="rowId"></param>
        /// <param name="newRow"></param>
        /// <returns></returns>
        public void AddOrUpdateRow(string rowId, IDataTableRow newRow)
        {
            var internalMap = GetEditableRowMap();
            if (!internalMap.TryGetValue(rowId, out var row))
            {
                ArrayUtils.Add(ref m_rows, new DataTableRow(rowId, newRow));
                return;
            }
            row.RowData = SerializedObject<IDataTableRow>.FromObject(newRow);
        }

        /// <summary>
        /// Remove data rows from the table
        /// </summary>
        /// <param name="rowIndex"></param>
        public void RemoveRow(List<int> rowIndex)
        {
            m_rows = m_rows.Where((x, id) => !rowIndex.Contains(id)).ToArray();
        }

        /// <summary>
        /// Remove all data rows from the table
        /// </summary>
        public void RemoveAllRows()
        {
            m_rows = Array.Empty<DataTableRow>();
        }

        /// <summary>
        /// Insert a row to dataTable
        /// </summary>
        /// <param name="index"></param>
        /// <param name="rowId"></param>
        /// <param name="row"></param>
        /// <returns></returns>
        public bool InsertRow(int index, string rowId, IDataTableRow row)
        {
            var rowKeys = m_rows.Select(x => x.RowId).ToList();
            if (rowKeys.Contains(rowId))
            {
                return false;
            }
            ArrayUtils.Insert(ref m_rows, index, new DataTableRow(rowId, row));
            return true;
        }

        /// <summary>
        /// Reorder a row
        /// </summary>
        /// <param name="fromIndex"></param>
        /// <param name="toIndex"></param>
        public void ReorderRow(int fromIndex, int toIndex)
        {
            ArrayUtils.Reorder(ref m_rows, fromIndex, toIndex);
        }

        /// <summary>
        /// Get all data rows as map with RowId as key
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, IDataTableRow> GetRowMap()
        {
            return m_rows.ToDictionary(x => x.RowId, x => x.RowData.GetObject());
        }

        #region Internal API

        /// <summary>
        /// Get editable row map
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, DataTableRow> GetEditableRowMap()
        {
            return m_rows.ToDictionary(x => x.RowId, x => x);
        }

        /// <summary>
        /// Internal use only, since we should ensure incoming row type is valid
        /// </summary>
        /// <param name="rowStructType"></param>
        internal void SetRowStruct(Type rowStructType)
        {
            m_rowType = SerializedType<IDataTableRow>.FromType(rowStructType);
            foreach (var row in m_rows)
            {
                row.RowData.serializedTypeString = SerializedType.ToString(rowStructType);
            }
        }

        /// <summary>
        /// Get a valid new row id
        /// </summary>
        /// <returns></returns>
        internal string NewRowId()
        {
            var map = GetRowMap();
            var id = "Row_0";
            int index = 1;
            while (map.ContainsKey(id))
            {
                id = $"Row_{index++}";
            }
            return id;
        }

        /// <summary>
        /// Update dataTable struct and rows
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        internal void InternalUpdate()
        {
            m_rowType.InternalUpdate();
            for (int i = 0; i < m_rows.Length; ++i)
            {
                m_rows[i].RowData.serializedTypeString = m_rowType.serializedTypeString;
                m_rows[i].InternalUpdate();
            }
        }

        /// <summary>
        /// Clear editor object cache.
        /// </summary>
        internal void Cleanup()
        {
#if UNITY_EDITOR
            if (m_rows == null) return;
            for (int i = 0; i < m_rows.Length; ++i)
            {
                m_rows[i].RowData.objectHandle = 0;
            }
#endif
        }

        /// <summary>
        /// Get data rows from table without modify default object
        /// </summary>
        internal T[] GetAllRowsSafe<T>() where T : class, IDataTableRow
        {
            return m_rows.Select(x => x.RowData.NewObject() as T).ToArray();
        }

        /// <summary>
        /// Get data rows from table without modify default object
        /// </summary>
        /// <returns></returns>
        internal IDataTableRow[] GetAllRowsSafe()
        {
            return m_rows.Select(x => x.RowData.NewObject()).ToArray();
        }

        /// <summary>
        /// Get all data rows as map with RowId as key without modify default object
        /// </summary>
        /// <returns></returns>
        internal Dictionary<string, IDataTableRow> GetRowMapSafe()
        {
            return m_rows.ToDictionary(x => x.RowId, x => x.RowData.NewObject());
        }
        #endregion
    }
}