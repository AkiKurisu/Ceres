using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Chris.DataDriven;
using Chris.Serialization;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Provides Chris.DataDriven table access helpers for Flow graphs.
    /// </summary>
    [CeresGroup("DataDriven")]
    public partial class DataDrivenExecutableLibrary: ExecutableFunctionLibrary
    {
        /// <summary>
        /// Gets or creates a data table manager of the selected manager type.
        /// </summary>
        /// <param name="managerType">The concrete data table manager type to resolve.</param>
        /// <returns>The resolved data table manager instance.</returns>
        [ExecutableFunction]
        public static DataTableManager Flow_GetDataTableManager(
            [ResolveReturn] SerializedType<DataTableManager> managerType)
        {
            return DataTableManager.GetOrCreateDataTableManager(managerType);
        }
        
        /// <summary>
        /// Gets a named data table from the target manager.
        /// </summary>
        /// <param name="dataTableManager">The manager that owns the data table.</param>
        /// <param name="name">The data table name.</param>
        /// <returns>The matching data table, or null when it is not found.</returns>
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Get DataTable")]
        public static DataTable Flow_DataTableManagerGetDataTable(DataTableManager dataTableManager, string name)
        {
            return dataTableManager.GetDataTable(name);
        }
        
        /// <summary>
        /// Gets a data row from the table by row id.
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
        /// Gets a data row from the table by index.
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
        /// Gets all rows from the data table.
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
