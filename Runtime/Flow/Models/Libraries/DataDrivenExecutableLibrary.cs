using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Chris.DataDriven;
using Chris.Serialization;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Executable function library for Chris.DataDriven
    /// </summary>
    [CeresGroup("DataDriven")]
    public partial class DataDrivenExecutableLibrary: ExecutableFunctionLibrary
    {
        [ExecutableFunction]
        public static DataTableManager Flow_GetDataTableManager(
            [ResolveReturn] SerializedType<DataTableManager> managerType)
        {
            return DataTableManager.GetOrCreateDataTableManager(managerType);
        }
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Get DataTable")]
        public static DataTable Flow_DataTableManagerGetDataTable(DataTableManager dataTableManager, string name)
        {
            return dataTableManager.GetDataTable(name);
        }
        
        /// <summary>
        /// Get data row from table by RowId
        /// </summary>
        /// <param name="dataTable"></param>
        /// <param name="rowId"></param>
        /// <param name="rowType"></param>
        /// <returns></returns>
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Get Row")]
        public static IDataTableRow Flow_DataTableGetRow(DataTable dataTable, string rowId,
            [ResolveReturn] SerializedType<IDataTableRow> rowType)
        {
            return dataTable.GetRow(rowId);
        }
        
        /// <summary>
        /// Get data row from table by index
        /// </summary>
        /// <param name="dataTable"></param>
        /// <param name="index"></param>
        /// <param name="rowType"></param>
        /// <returns></returns>
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Get Row by Index")]
        public static IDataTableRow Flow_DataTableGetRowByIndex(DataTable dataTable, int index,
            [ResolveReturn] SerializedType<IDataTableRow> rowType)
        {
            return dataTable.GetRow(index);
        }
        
        /// <summary>
        /// Get all data rows from table.
        /// </summary>
        /// <param name="dataTable"></param>
        /// <returns></returns>
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Get All Rows")]
        public static IDataTableRow[] Flow_DataTableGetAllRows(DataTable dataTable)
        {
            return dataTable.GetAllRows();
        }
    }
}