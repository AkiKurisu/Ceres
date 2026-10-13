using System;
using System.Collections.Generic;
using Ceres.Serialization;
using UnityEngine;

namespace Ceres.DataDriven
{
    /// <summary>
    /// Implement this interface for custom DataTable Row struct
    /// </summary>
    public interface IDataTableRow { }

    /// <summary>
    /// Implement this interface to validate DataTable Row on import
    /// </summary>
    public interface IValidateRow
    {
        bool ValidateRow(string rowId, out string reason);
    }

    /// <summary>
    /// Read-only table imported from a .datatable source; rows decode on first access
    /// </summary>
    public sealed class DataTable : ScriptableObject
    {
        [SerializeField]
        private SerializedType<IDataTableRow> m_rowType;

        [SerializeField]
        private string[] m_rowIds = Array.Empty<string>();

        [SerializeField]
        private int[] m_rowOffsets = Array.Empty<int>();

        [SerializeField]
        private byte[] m_rowData = Array.Empty<byte>();

        [SerializeField]
        private DataTableSchemaType[] m_schema = Array.Empty<DataTableSchemaType>();

        [SerializeField]
        private Hash128 m_layoutHash;

        private IDataTableRow[] _rows;

        private Dictionary<string, int> _rowIndices;

        private DataTableCodec _codec;

        private DataTableTranscoder _transcoder;

        public int RowCount => m_rowIds.Length;

        public IReadOnlyList<string> RowIds => m_rowIds;

        /// <summary>
        /// Get default row struct
        /// </summary>
        /// <returns></returns>
        public IDataTableRow GetRowStruct()
        {
            return m_rowType?.GetObject();
        }

        /// <summary>
        /// Get row struct type
        /// </summary>
        /// <returns></returns>
        public Type GetRowStructType()
        {
            return m_rowType?.GetObjectType();
        }

        public bool ContainsRow(string rowId)
        {
            return GetRowIndex(rowId) >= 0;
        }

        /// <summary>
        /// Get the index of a row, or -1 when the table has no such row
        /// </summary>
        public int GetRowIndex(string rowId)
        {
            if (rowId == null) return -1;
            if (_rowIndices == null)
            {
                _rowIndices = new Dictionary<string, int>(m_rowIds.Length);
                for (int i = 0; i < m_rowIds.Length; i++)
                {
                    _rowIndices[m_rowIds[i]] = i;
                }
            }
            return _rowIndices.GetValueOrDefault(rowId, -1);
        }

        /// <summary>
        /// Get data rows from table
        /// </summary>
        public T[] GetAllRows<T>() where T : class, IDataTableRow
        {
            var rows = new T[m_rowIds.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = GetRow(i) as T;
            }
            return rows;
        }

        /// <summary>
        /// Get data rows from table
        /// </summary>
        /// <returns></returns>
        public IDataTableRow[] GetAllRows()
        {
            var rows = new IDataTableRow[m_rowIds.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = GetRow(i);
            }
            return rows;
        }

        /// <summary>
        /// Get data rows from table by predicate
        /// </summary>
        public T[] GetRows<T>(Predicate<T> predicate) where T : class, IDataTableRow
        {
            var rows = new List<T>();
            for (int i = 0; i < m_rowIds.Length; i++)
            {
                if (GetRow(i) is T row && predicate(row)) rows.Add(row);
            }
            return rows.ToArray();
        }

        /// <summary>
        /// Get data row from table by index
        /// </summary>
        public T GetRow<T>(int index) where T : class, IDataTableRow
        {
            return GetRow(index) as T;
        }

        /// <summary>
        /// Get data row from table by index
        /// </summary>
        public IDataTableRow GetRow(int index)
        {
            if ((uint)index >= (uint)m_rowIds.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"DataTable '{name}' has {m_rowIds.Length} rows.");
            }
            _rows ??= new IDataTableRow[m_rowIds.Length];
            return _rows[index] ??= DecodeRow(index);
        }

        /// <summary>
        /// Get data row from table by RowId
        /// </summary>
        public T GetRow<T>(string rowId) where T : class, IDataTableRow
        {
            return GetRow(rowId) as T;
        }

        /// <summary>
        /// Get data row from table by RowId
        /// </summary>
        public IDataTableRow GetRow(string rowId)
        {
            int index = GetRowIndex(rowId);
            return index >= 0 ? GetRow(index) : null;
        }

        /// <summary>
        /// Get the first data row matching the predicate
        /// </summary>
        public T GetRow<T>(Predicate<T> predicate) where T : class, IDataTableRow
        {
            for (int i = 0; i < m_rowIds.Length; i++)
            {
                if (GetRow(i) is T row && predicate(row)) return row;
            }
            return null;
        }

        /// <summary>
        /// Get all data rows as map with RowId as key
        /// </summary>
        public Dictionary<string, IDataTableRow> GetRowMap()
        {
            var map = new Dictionary<string, IDataTableRow>(m_rowIds.Length);
            for (int i = 0; i < m_rowIds.Length; i++)
            {
                map[m_rowIds[i]] = GetRow(i);
            }
            return map;
        }

        private IDataTableRow DecodeRow(int index)
        {
            _codec ??= GetCodec();
            int start = m_rowOffsets[index];
            var bytes = new ReadOnlySpan<byte>(m_rowData, start, m_rowOffsets[index + 1] - start);
            return (IDataTableRow)DataTableCodec.Decode(_codec, _transcoder != null ? _transcoder.Transcode(bytes) : bytes);
        }

        private DataTableCodec GetCodec()
        {
            var rowType = GetRowStructType() ?? throw new InvalidOperationException($"DataTable '{name}' has no valid row type.");
            var layout = DataTableLayout.Get(rowType);
            if (layout.Hash != m_layoutHash)
            {
                _transcoder = new DataTableTranscoder(m_schema, layout.Codec);
            }
            return layout.Codec;
        }

        internal void Initialize(Type rowType, DataTableLayout layout, string[] rowIds, int[] rowOffsets, byte[] rowData)
        {
            m_rowType = rowType != null ? SerializedType<IDataTableRow>.FromType(rowType) : null;
            m_schema = layout?.Types ?? Array.Empty<DataTableSchemaType>();
            m_layoutHash = layout?.Hash ?? default;
            m_rowIds = rowIds;
            m_rowOffsets = rowOffsets;
            m_rowData = rowData;
            _rows = null;
            _rowIndices = null;
            _codec = null;
            _transcoder = null;
        }
    }
}
