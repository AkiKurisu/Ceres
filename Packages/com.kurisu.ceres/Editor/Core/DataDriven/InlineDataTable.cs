using System;
using System.Linq;
using UnityEngine;

namespace Ceres.DataDriven.Editor
{
    /// <summary>
    /// Rows an asset stores for Editor tools, edited like a .datatable source and never imported as a runtime table
    /// </summary>
    [Serializable]
    public sealed class InlineDataTable
    {
        [Serializable]
        internal struct Row
        {
            public string id;

            public string data;
        }

        [SerializeField]
        internal string rowType = string.Empty;

        [SerializeField]
        internal Row[] rows = Array.Empty<Row>();

        public InlineDataTable()
        {
        }

        public InlineDataTable(Type rowType)
        {
            this.rowType = DataTableSource.GetRowTypeName(rowType);
        }

        public Type RowType => DataTableSource.ResolveRowType(rowType);

        public int RowCount => rows.Length;

        internal DataTableSource ToSource()
        {
            return new DataTableSource(rowType, rows.Select(row => new DataTableSource.Row(row.id, row.data)).ToArray());
        }

        /// <summary>
        /// Replace the rows, returning false when they already match the source
        /// </summary>
        internal bool Write(DataTableSource source)
        {
            if (rowType == source.RowTypeName && rows.Length == source.Rows.Count
                && rows.Select((row, index) => row.id == source.Rows[index].Id && row.data == source.Rows[index].Json).All(same => same))
            {
                return false;
            }
            rowType = source.RowTypeName;
            rows = source.Rows.Select(row => new Row { id = row.Id, data = row.Json }).ToArray();
            return true;
        }
    }
}
