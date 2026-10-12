using System;
using System.IO;
using UnityEditor.AssetImporters;

namespace Ceres.DataDriven.Editor
{
    /// <summary>
    /// Imports a .datatable source as a binary <see cref="DataTable"/>
    /// </summary>
    [ScriptedImporter(1, DataTableSource.Extension)]
    public sealed class DataTableImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            DataTableSource source;
            try
            {
                source = DataTableSource.Parse(File.ReadAllText(ctx.assetPath));
            }
            catch (FormatException e)
            {
                ctx.LogImportError($"[DataTable] {ctx.assetPath} is not a valid source: {e.Message}");
                return;
            }

            if (!string.IsNullOrEmpty(source.RowTypeName))
            {
                ctx.DependsOnCustomDependency(DataTableSchema.GetDependencyName(source.RowTypeName));
            }

            DataTable table;
            try
            {
                table = DataTableBuilder.Build(source, warning => ctx.LogImportWarning($"[DataTable] {ctx.assetPath}: {warning}"));
            }
            catch (FormatException e)
            {
                ctx.LogImportError($"[DataTable] {ctx.assetPath}: {e.Message}");
                return;
            }

            table.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            ctx.AddObjectToAsset("DataTable", table);
            ctx.SetMainObject(table);
        }
    }
}
