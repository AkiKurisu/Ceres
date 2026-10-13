using System;

namespace Ceres.DataDriven
{
    /// <summary>
    /// Keeps a row member in the .datatable source and omits it from the runtime table
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EditorOnlyAttribute : Attribute
    {
    }
}
